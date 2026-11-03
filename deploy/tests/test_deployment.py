"""Static deployment contract checks and no-Docker provisioning orchestration tests."""
from __future__ import annotations

import os
from pathlib import Path
import shutil
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
        self.assertIn("- frontend # One-off downloader needs Internet access", text)
        self.assertIn("GEOSEARCH_READER_PASSWORD", text)
        self.assertIn("POSTGIS_PASSWORD", text)
        self.assertNotIn("ConnectionStrings__Identity", text)
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
            log.unlink()
            env["FAIL_IMPORT"] = "1"
            result = subprocess.run(cmd, env=env, text=True, capture_output=True, check=False)
            self.assertNotEqual(0, result.returncode)
            self.assertIn("s04-import-addresses", log.read_text())
            self.assertNotIn("s05-search-index", log.read_text())

    def test_shell_syntax(self) -> None:
        scripts = sorted((DEPLOY / "scripts").glob("*.sh"))
        result = subprocess.run(["bash", "-n", *(str(p) for p in scripts)],
                                text=True, capture_output=True, check=False)
        self.assertEqual(0, result.returncode, result.stderr)


if __name__ == "__main__":
    unittest.main()
