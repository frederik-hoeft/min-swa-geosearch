"""Static deployment contract checks and no-Docker provisioning orchestration tests."""
from __future__ import annotations

import os
from pathlib import Path
import shutil
import stat
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
DEPLOY = ROOT / "deploy"


class DeploymentTests(unittest.TestCase):
    def test_builds_remain_separate_and_client_uses_native_fingerprinting(self) -> None:
        api = (DEPLOY / "Dockerfile.api").read_text()
        web = (DEPLOY / "Dockerfile.web").read_text()
        html = (ROOT / "src/GeoSearch.Web.Client/wwwroot/index.html").read_text()
        self.assertIn("mcr.microsoft.com/dotnet/aspnet:10.0", api)
        self.assertIn("USER $APP_UID", api)
        self.assertIn("mcr.microsoft.com/dotnet/sdk:10.0", web)
        self.assertIn("FROM nginx:stable-alpine", web)
        self.assertIn("USER nginx", web)
        self.assertIn("chown 101:101", (DEPLOY / "scripts/load-cert.sh").read_text())
        self.assertIn("/out/client/wwwroot/ /usr/share/nginx/html/", web)
        self.assertIn("_framework/blazor.webassembly#[.{fingerprint}].js", html)
        self.assertFalse((DEPLOY / "scripts/materialize-static-assets.py").exists())
        ignore = (ROOT / ".dockerignore").read_text()
        for sensitive in ("**/.env", "src/GeoSearch.Web.Server/appsettings.json", "**/*.pem"):
            self.assertIn(sensitive, ignore)

    def test_nginx_proxies_only_api_and_swagger_and_avoids_uri_logs(self) -> None:
        conf = (DEPLOY / "nginx/nginx.conf").read_text()
        for route in ("location ^~ /api/", "location = /swagger", "location ^~ /swagger/",
                      "location ^~ /_framework/", "location ^~ /_content/"):
            self.assertIn(route, conf)
        self.assertIn("listen 8443 ssl;", conf)
        self.assertIn("access_log off;", conf)
        self.assertIn("try_files $uri $uri/ /index.html;", conf)
        self.assertIn("resolver 127.0.0.11", conf)

    def test_compose_separates_frontend_and_database_networks(self) -> None:
        text = (DEPLOY / "compose.yaml").read_text()
        self.assertIn("internal: true", text)
        self.assertIn("profiles: [setup]", text)
        self.assertIn("- gis-data:/workspace/lab/data", text)
        self.assertNotIn("../lab/data:/workspace/lab/data", text)
        self.assertNotIn("GEOSEARCH_DATA_UID", text)
        tools = (DEPLOY / "Dockerfile.tools").read_text()
        self.assertIn("install -d -o 10001 -g 10001 -m 0755 /workspace/lab/data", tools)
        self.assertIn("USER 10001:10001", tools)
        env_example = (DEPLOY / ".env.example").read_text()
        self.assertNotIn("GEOSEARCH_DATA_UID", env_example)
        self.assertNotIn("GEOSEARCH_DATA_GID", env_example)
        self.assertIn("- frontend # One-off downloader needs Internet access", text)
        self.assertIn("GEOSEARCH_READER_PASSWORD", text)
        self.assertIn("POSTGIS_PASSWORD", text)
        self.assertNotIn("ConnectionStrings__Identity", text)
        self.assertIn("tls-certs:/run/tls:ro", text)
        self.assertIn("cap_add:\n      - CHOWN", text)
        self.assertIn("/tmp:rw,nosuid,nodev,size=64m,mode=1777", text)
        self.assertIn("network_mode: none", text)
        self.assertIn("tls-certs:/run/tls", text)
        self.assertNotIn("./certs:/run/tls", text)
        self.assertFalse((DEPLOY / "compose.https.yaml").exists())

    def test_provisioning_rejects_missing_env_and_is_ordered_and_fail_fast(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "scripts").mkdir()
            shutil.copy2(DEPLOY / "scripts/provision.sh", root / "scripts/provision.sh")
            env = os.environ.copy()
            result = subprocess.run(["bash", str(root / "scripts/provision.sh")], env=env,
                                    text=True, capture_output=True, check=False)
            self.assertNotEqual(0, result.returncode)
            self.assertIn("Missing deploy/.env", result.stderr)

            (root / ".env").write_text("POSTGIS_PASSWORD=test\nGEOSEARCH_READER_PASSWORD=test\n")
            docker = root / "docker"
            log = root / "docker.log"
            docker.write_text('''#!/bin/sh
printf '%s\\n' "$*" >> "$DOCKER_TEST_LOG"
case "$*" in
  *--user\\ 0:0*) if [ "${FAIL_VOLUME_PREP:-0}" = 1 ]; then exit 41; fi ;;
  *--entrypoint\\ sh\\ gis-tools\\ -ec*) if [ "${FAIL_PERMISSION:-0}" = 1 ] && [ "${FAIL_VOLUME_PREP:-0}" != 1 ]; then exit 43; fi ;;
  *s04-import-addresses*) if [ "${FAIL_IMPORT:-0}" = 1 ]; then exit 42; fi ;;
esac
''')
            docker.chmod(0o755)
            env.update(PATH=str(root) + os.pathsep + env["PATH"], DOCKER_TEST_LOG=str(log))
            cmd = ["bash", str(root / "scripts/provision.sh")]
            result = subprocess.run(cmd, env=env, text=True, capture_output=True, check=False)
            self.assertEqual(0, result.returncode, result.stderr)
            lines = log.read_text().splitlines()
            expected = ["s00-download-data", "s02-db-setup", "s03-import-gpkg", "s04-import-addresses",
                        "s05-search-index", "s10-reader-setup", "s06-search", "s07-reverse"]
            actual = [word for line in lines for word in expected if (" " + word) in line]
            self.assertEqual(expected, actual)
            volume_init = [line for line in lines if "--user 0:0 --entrypoint sh gis-tools -ec" in line]
            self.assertEqual(1, len(volume_init), lines)
            permission_test = [line for line in lines if "--entrypoint sh gis-tools -ec" in line and "--user 0:0" not in line]
            self.assertEqual(1, len(permission_test), lines)
            self.assertLess(lines.index(volume_init[0]), lines.index(permission_test[0]))
            self.assertLess(lines.index(permission_test[0]), next(i for i, line in enumerate(lines) if " s00-download-data" in line))
            log.unlink()
            env["FAIL_VOLUME_PREP"] = "1"
            result = subprocess.run(cmd, env=env, text=True, capture_output=True, check=False)
            self.assertNotEqual(0, result.returncode)
            self.assertNotIn(" s00-download-data", log.read_text())
            log.unlink()
            env.pop("FAIL_VOLUME_PREP")
            env["FAIL_PERMISSION"] = "1"
            result = subprocess.run(cmd, env=env, text=True, capture_output=True, check=False)
            self.assertNotEqual(0, result.returncode)
            self.assertIn("GIS data volume is not writable", result.stderr)
            self.assertNotIn(" s00-download-data", log.read_text())
            log.unlink()
            env.pop("FAIL_PERMISSION")
            env["FAIL_IMPORT"] = "1"
            result = subprocess.run(cmd, env=env, text=True, capture_output=True, check=False)
            self.assertNotEqual(0, result.returncode)
            self.assertIn("s04-import-addresses", log.read_text())
            self.assertNotIn("s05-search-index", log.read_text())

    def test_tls_loader_streams_key_without_host_bind_mount(self) -> None:
        # Emulate `docker compose run` by executing its BusyBox-compatible
        # import command against a disposable volume. No Docker daemon needed.
        if not shutil.which("openssl") or not shutil.which("busybox"):
            self.skipTest("OpenSSL and BusyBox are required for the TLS fixture")

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "scripts").mkdir()
            certs = root / "certs"
            certs.mkdir()
            shutil.copy2(DEPLOY / "scripts/load-cert.sh", root / "scripts/load-cert.sh")
            volume = root / "volume"
            volume.mkdir()
            docker_log = root / "docker.log"
            docker = root / "docker"
            docker.write_text('''#!/usr/bin/env python3
import os
from pathlib import Path
import subprocess
import sys

args = sys.argv[1:]
assert args[:4] == ["compose", "--profile", "setup", "run"], args
assert "tls-loader" in args, args
assert "--user" in args and "0:0" in args, args
assert "--no-deps" in args, args
script = args[-1].replace("/run/tls", os.environ["FAKE_TLS_VOLUME"])
# Non-root CI hosts cannot chown to arbitrary users. The real one-off loader
# has CAP_CHOWN; simulate the ownership step while still testing contents/mode.
if os.geteuid() != 0:
    script = "chown() { :; }; " + script
Path(os.environ["DOCKER_TEST_LOG"]).write_text("called")
result = subprocess.run(["busybox", "sh", "-ec", script], stdin=sys.stdin.buffer)
sys.exit(result.returncode)
''')
            docker.chmod(0o755)
            env = os.environ.copy()
            env.update(PATH=str(root) + os.pathsep + env["PATH"],
                       FAKE_TLS_VOLUME=str(volume), DOCKER_TEST_LOG=str(docker_log))

            def generate_certificate(common_name: str) -> None:
                subprocess.run([
                    "openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes",
                    "-days", "1", "-subj", f"/CN={common_name}",
                    "-keyout", str(certs / "privkey.pem"),
                    "-out", str(certs / "fullchain.pem")
                ], check=True, capture_output=True)

            command = ["bash", str(root / "scripts/load-cert.sh")]
            generate_certificate("localhost")
            result = subprocess.run(command, env=env, capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertEqual("called", docker_log.read_text())
            for name in ("fullchain.pem", "privkey.pem"):
                installed = volume / name
                self.assertEqual((certs / name).read_bytes(), installed.read_bytes())
                self.assertEqual(0o600, stat.S_IMODE(installed.stat().st_mode))
                if os.geteuid() == 0:
                    self.assertEqual(101, installed.stat().st_uid)
                    self.assertEqual(101, installed.stat().st_gid)
            self.assertEqual([], list(volume.glob(".import.*")))

            # A repeat import replaces the cert without permission changes.
            previous = (volume / "fullchain.pem").read_bytes()
            generate_certificate("new-localhost")
            result = subprocess.run(command, env=env, capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stderr)
            updated = (volume / "fullchain.pem").read_bytes()
            self.assertNotEqual(previous, updated)

            # Invalid input must fail *before* calling Docker, preserving existing
            # volume contents and avoiding partially installed key material.
            docker_log.unlink()
            (certs / "fullchain.pem").write_bytes(b"invalid certificate")
            result = subprocess.run(command, env=env, capture_output=True, text=True)
            self.assertNotEqual(0, result.returncode)
            self.assertFalse(docker_log.exists())
            self.assertEqual(updated, (volume / "fullchain.pem").read_bytes())

    def test_shell_syntax(self) -> None:
        scripts = sorted((DEPLOY / "scripts").glob("*.sh"))
        result = subprocess.run(["bash", "-n", *(str(p) for p in scripts)],
                                text=True, capture_output=True, check=False)
        self.assertEqual(0, result.returncode, result.stderr)


if __name__ == "__main__":
    unittest.main()
