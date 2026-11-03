"""Make .NET 10 static-web-asset routes usable by a filesystem-only web server.

ASP.NET Core understands the published *.staticwebassets.endpoints.json aliases,
but stock Nginx does not. Create relative symlinks for each uncompressed route
that is not already present on disk, preserving its fingerprinted source file.
"""

from __future__ import annotations

import json
import os
from pathlib import Path, PurePosixPath
import sys


def _relative_path(value: str) -> Path:
    path = PurePosixPath(value)
    if path.is_absolute() or not path.parts or ".." in path.parts:
        raise ValueError(f"Unsafe static asset path: {value!r}")
    return Path(*path.parts)


def materialize(publish_dir: Path) -> int:
    manifest_files = list(publish_dir.glob("*.staticwebassets.endpoints.json"))
    if len(manifest_files) != 1:
        raise ValueError(f"Expected exactly one static assets endpoint manifest in {publish_dir}")

    web_root = publish_dir / "wwwroot"
    if not web_root.is_dir():
        raise ValueError(f"Missing published wwwroot directory in {publish_dir}")

    data = json.loads(manifest_files[0].read_text(encoding="utf-8-sig"))
    created = 0
    for endpoint in data["Endpoints"]:
        # Routes may have multiple Accept-Encoding representations. Only the
        # default one should own the public route; gzip_static uses .gz aliases.
        if endpoint.get("Selectors"):
            continue
        route = web_root / _relative_path(endpoint["Route"])
        source = web_root / _relative_path(endpoint["AssetFile"])
        if not source.is_file():
            raise FileNotFoundError(f"Static asset {endpoint['Route']!r} targets missing {source}")
        if route.exists() or route.is_symlink():
            continue
        route.parent.mkdir(parents=True, exist_ok=True)
        route.symlink_to(os.path.relpath(source, route.parent))
        created += 1

    return created


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: materialize-static-assets.py <dotnet-publish-output-directory>")
    print(f"Materialized {materialize(Path(sys.argv[1]))} static asset route aliases")
