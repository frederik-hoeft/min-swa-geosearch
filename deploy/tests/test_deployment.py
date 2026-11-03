"""Deployment preflight regression checks; no Docker daemon or network required."""
from __future__ import annotations

import os
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
DEPLOY = ROOT / "deploy"


class DeploymentTests(unittest.TestCase):
    def test_dockerfiles_separate_build_and_runtime_stages(self) -> None:
        api = (DEPLOY / "Dockerfile.api").read_text()
        web = (DEPLOY / "Dockerfile.web").read_text()
        self.assertIn("FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish", api)
        self.assertIn("FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime", api)
        self.assertIn('USER $APP_UID', api)
        self.assertIn("FROM nginx:stable-alpine AS runtime", web)
        self.assertIn("/published/wwwroot/ /usr/share/nginx/html/", web)
        ignore = (ROOT / ".dockerignore").read_text()
        for sensitive in ("**/.env", "src/GeoSearch.Web.Server/appsettings.json", "**/*.pem"):
            self.assertIn(sensitive, ignore)

    def test_nginx_has_explicit_proxy_and_static_asset_routes(self) -> None:
        routes = (DEPLOY / "nginx/routes.conf").read_text()
        for route in ("location ^~ /api/", "location = /swagger", "location ^~ /swagger/",
                      "location ^~ /_framework/", "location ^~ /_content/"):
            self.assertIn(route, routes)
        self.assertIn("access_log off;", routes)
        self.assertIn("try_files $uri $uri/ /index.html;", routes)
        self.assertIn("resolver 127.0.0.11", routes)

    def test_scripts_reject_missing_or_example_credentials(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "scripts").mkdir()
            for name in ("stack.sh", "provision.sh", "validate-env.sh"):
                shutil.copy2(DEPLOY / "scripts" / name, root / "scripts" / name)
            environment = os.environ.copy()
            environment["PATH"] = str(root) + os.pathsep + environment["PATH"]
            for name, args in (("stack.sh", ["up"]), ("provision.sh", [])):
                result = subprocess.run(["bash", str(root / "scripts" / name), *args],
                                        env=environment, text=True, capture_output=True, check=False)
                self.assertNotEqual(0, result.returncode)
                self.assertIn("Missing", result.stderr)
            (root / ".env").write_text((DEPLOY / ".env.example").read_text())
            for name, args in (("stack.sh", ["up"]), ("provision.sh", [])):
                result = subprocess.run(["bash", str(root / "scripts" / name), *args],
                                        env=environment, text=True, capture_output=True, check=False)
                self.assertNotEqual(0, result.returncode)
                self.assertIn("Replace", result.stderr)

    def test_provisioning_is_ordered_and_fail_fast(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "scripts").mkdir()
            shutil.copy2(DEPLOY / "scripts/provision.sh", root / "scripts/provision.sh")
            shutil.copy2(DEPLOY / "scripts/validate-env.sh", root / "scripts/validate-env.sh")
            (root / ".env").write_text("POSTGIS_PASSWORD=longDatabasePassword1234567890\nGEOSEARCH_READER_PASSWORD=123456789012345678901234567890\n")
            log = root / "docker.log"
            docker = root / "docker"
            docker.write_text('''#!/bin/sh
printf '%s\\n' "$*" >> "$DOCKER_TEST_LOG"
case "$*" in
  *s04-import-addresses*) if [ "${FAIL_IMPORT:-0}" = 1 ]; then exit 42; fi ;;
esac
''')
            docker.chmod(0o755)
            environment = os.environ.copy()
            environment["PATH"] = str(root) + os.pathsep + environment["PATH"]
            environment["DOCKER_TEST_LOG"] = str(log)
            args = ["bash", str(root / "scripts/provision.sh")]
            result = subprocess.run(args, env=environment, text=True, capture_output=True, check=False)
            self.assertEqual(0, result.returncode, result.stderr)
            command_lines = log.read_text().splitlines()
            order = ["s00-download-data", "s02-db-setup", "s03-import-gpkg", "s04-import-addresses",
                     "s05-search-index", "s10-reader-setup", "s06-search", "s07-reverse"]
            self.assertEqual(order, [recipe for line in command_lines for recipe in order if line.endswith(" " + recipe) or " " + recipe + " " in line])
            self.assertLess(command_lines.index(next(line for line in command_lines if 's03-import-gpkg' in line)),
                            command_lines.index(next(line for line in command_lines if 's04-import-addresses' in line)))
            log.unlink()
            environment["FAIL_IMPORT"] = "1"
            result = subprocess.run(args, env=environment, text=True, capture_output=True, check=False)
            self.assertNotEqual(0, result.returncode)
            commands = log.read_text()
            self.assertIn("s04-import-addresses", commands)
            self.assertNotIn("s05-search-index", commands)
            self.assertNotIn("s10-reader-setup", commands)

    def test_published_asset_aliases_are_materialized_safely(self) -> None:
        import sys
        sys.path.insert(0, str(DEPLOY / "scripts"))
        from importlib import import_module
        module = import_module("materialize-static-assets")
        with tempfile.TemporaryDirectory() as temp:
            publish = Path(temp)
            (publish / "wwwroot/_framework").mkdir(parents=True)
            (publish / "wwwroot/_framework/blazor.webassembly.abc123.js").write_text("original-loader")
            (publish / "wwwroot/_framework/blazor.webassembly.abc123.js.gz").write_text("compressed-loader")
            data = {"Endpoints": [
                {"Route": "_framework/blazor.webassembly.js", "AssetFile": "_framework/blazor.webassembly.abc123.js.gz", "Selectors": [{"Name": "Content-Encoding", "Value": "gzip"}]},
                {"Route": "_framework/blazor.webassembly.js", "AssetFile": "_framework/blazor.webassembly.abc123.js", "Selectors": []},
                {"Route": "_framework/blazor.webassembly.js.gz", "AssetFile": "_framework/blazor.webassembly.abc123.js.gz", "Selectors": []},
            ]}
            (publish / "Sample.staticwebassets.endpoints.json").write_text(json.dumps(data))
            self.assertEqual(2, module.materialize(publish))
            loader = publish / "wwwroot/_framework/blazor.webassembly.js"
            self.assertTrue(loader.is_symlink())
            self.assertEqual("original-loader", loader.read_text())
            self.assertEqual("compressed-loader", (publish / "wwwroot/_framework/blazor.webassembly.js.gz").read_text())
            self.assertEqual(0, module.materialize(publish))
            data["Endpoints"].append({"Route": "../secret", "AssetFile": "_framework/blazor.webassembly.abc123.js", "Selectors": []})
            (publish / "Sample.staticwebassets.endpoints.json").write_text(json.dumps(data))
            with self.assertRaises(ValueError):
                module.materialize(publish)

    def test_shell_syntax(self) -> None:
        paths = sorted((DEPLOY / "scripts").glob("*.sh"))
        result = subprocess.run(["bash", "-n", *(str(p) for p in paths)], capture_output=True, text=True, check=False)
        self.assertEqual(0, result.returncode, result.stderr)


if __name__ == "__main__":
    unittest.main()
