#!/usr/bin/env python3
"""Fetch the fixed Geofabrik snapshot used by the lab without committing GIS data."""

import argparse
import hashlib
import os
import re
import shutil
import sqlite3
import sys
import time
import urllib.error
import urllib.request
import zipfile
from pathlib import Path

SNAPSHOT = "261028"
BASE_URL = "https://download.geofabrik.de/europe/germany"
PBF_NAME = f"niedersachsen-{SNAPSHOT}.osm.pbf"
GPKG_ARCHIVE_NAME = f"niedersachsen-{SNAPSHOT}-free.gpkg.zip"
GPKG_RELATIVE_PATH = Path(f"niedersachsen-{SNAPSHOT}-free/niedersachsen.gpkg")
CHUNK_SIZE = 1024 * 1024


class DownloadError(Exception):
    """A dataset could not be retrieved or verified."""


def request_bytes(url: str) -> bytes:
    with urllib.request.urlopen(
        urllib.request.Request(url, headers={"User-Agent": "GeoSearch-Lab2/1.0"}), timeout=60
    ) as response:
        return response.read(4096)


def download(url: str, destination: Path) -> None:
    """Download to a staging file; resume only when the server honors Range."""
    destination.parent.mkdir(parents=True, exist_ok=True)
    part = destination.with_name(f"{destination.name}.part")
    for attempt in range(3):
        offset = part.stat().st_size if part.exists() else 0
        headers = {"User-Agent": "GeoSearch-Lab2/1.0"}
        if offset:
            headers["Range"] = f"bytes={offset}-"
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=60) as response:
                status = response.status
                if status not in (200, 206):
                    raise DownloadError(f"Unexpected HTTP {status} for {url}")
                if status == 206:
                    content_range = response.headers.get("Content-Range", "")
                    if not re.match(rf"^bytes {offset}-\d+/\d+$", content_range):
                        raise DownloadError(f"Unexpected Content-Range for {url}: {content_range}")
                mode = "ab" if status == 206 and offset else "wb"
                expected = response.headers.get("Content-Length")
                received = 0
                with part.open(mode) as output:
                    while chunk := response.read(CHUNK_SIZE):
                        output.write(chunk)
                        received += len(chunk)
                if expected is not None and received != int(expected):
                    raise DownloadError(f"Incomplete response for {url} ({received} of {expected} bytes)")
                if received == 0 and offset == 0:
                    raise DownloadError(f"Empty download: {url}")
            part.replace(destination)
            return
        except urllib.error.HTTPError as exc:
            if exc.code == 416 and offset:
                part.unlink()
            elif exc.code in (400, 401, 403, 404):
                raise DownloadError(f"HTTP {exc.code} fetching {url}") from exc
            elif attempt == 2:
                raise DownloadError(f"Unable to download {url}: {exc}") from exc
        except (OSError, urllib.error.URLError, DownloadError) as exc:
            if isinstance(exc, DownloadError) and "Content-Range" in str(exc):
                # Reusing a partial response from a different remote object is unsafe.
                part.unlink(missing_ok=True)
            if attempt == 2:
                raise DownloadError(f"Unable to download {url}: {exc}") from exc
        if attempt < 2:
            time.sleep(attempt + 1)


def md5_of(path: Path) -> str:
    digest = hashlib.md5()  # noqa: S324 - Geofabrik publishes MD5 sidecars for this snapshot.
    with path.open("rb") as source:
        while chunk := source.read(CHUNK_SIZE):
            digest.update(chunk)
    return digest.hexdigest()


def get_pbf_checksum(base_url: str, cache: Path) -> str:
    checksum_file = cache / f"{PBF_NAME}.md5"
    if checksum_file.exists():
        data = checksum_file.read_text(encoding="ascii")
    else:
        data = request_bytes(f"{base_url}/{PBF_NAME}.md5").decode("ascii")
    match = re.search(r"\b[0-9a-fA-F]{32}\b", data)
    if not match:
        raise DownloadError(f"Invalid Geofabrik checksum file for {PBF_NAME}")
    if not checksum_file.exists():
        checksum_file.write_text(data, encoding="ascii")
    return match.group(0).lower()


def is_geopackage(path: Path) -> bool:
    if not path.is_file():
        return False
    try:
        # The presence of GeoPackage metadata is stronger than checking just the SQLite magic.
        with sqlite3.connect(path.as_uri() + "?mode=ro", uri=True) as db:
            return db.execute("SELECT EXISTS(SELECT 1 FROM gpkg_contents)").fetchone()[0] == 1
    except (sqlite3.DatabaseError, OSError):
        return False


def ensure_pbf(base_url: str, data: Path, cache: Path) -> None:
    expected = get_pbf_checksum(base_url, cache)
    target = data / PBF_NAME
    if target.is_file() and md5_of(target) == expected:
        print(f"Verified existing {target}")
        return
    staging = cache / PBF_NAME
    print(f"Downloading {PBF_NAME} (about 485 MiB)", flush=True)
    download(f"{base_url}/{PBF_NAME}", staging)
    if md5_of(staging) != expected:
        staging.unlink(missing_ok=True)
        raise DownloadError(f"Checksum mismatch for {PBF_NAME}; the existing file was not replaced")
    staging.replace(target)
    print(f"Verified {target} against Geofabrik MD5")


def ensure_gpkg(base_url: str, data: Path, cache: Path) -> None:
    target = data / GPKG_RELATIVE_PATH
    if is_geopackage(target):
        print(f"Verified existing {target}")
        return
    staging_zip = cache / GPKG_ARCHIVE_NAME
    if not staging_zip.exists():
        print(f"Downloading {GPKG_ARCHIVE_NAME} (about 835 MiB)", flush=True)
        download(f"{base_url}/{GPKG_ARCHIVE_NAME}", staging_zip)
    target.parent.mkdir(parents=True, exist_ok=True)
    staging_gpkg = target.with_name(f"{target.name}.part")
    try:
        with zipfile.ZipFile(staging_zip) as archive:
            members = [i for i in archive.infolist() if not i.is_dir() and i.filename.lower().endswith(".gpkg")]
            if len(members) != 1:
                raise DownloadError(f"Expected one .gpkg entry in {GPKG_ARCHIVE_NAME}, got {len(members)}")
            # Extract only the expected data member, never untrusted archive paths.
            with archive.open(members[0]) as source, staging_gpkg.open("wb") as output:
                shutil.copyfileobj(source, output, CHUNK_SIZE)  # ZipFile checks CRC while reading.
        if not is_geopackage(staging_gpkg):
            raise DownloadError(f"Extracted file is not a valid GeoPackage: {GPKG_ARCHIVE_NAME}")
        staging_gpkg.replace(target)
        staging_zip.unlink()  # The expanded file is the persistent cache; save ~835 MiB.
        print(f"Extracted and verified {target}")
    except (OSError, zipfile.BadZipFile, DownloadError) as exc:
        # A complete but invalid archive must not poison subsequent retries.
        staging_zip.unlink(missing_ok=True)
        raise DownloadError(f"Failed to extract {GPKG_ARCHIVE_NAME}: {exc}") from exc
    finally:
        staging_gpkg.unlink(missing_ok=True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--data-dir", type=Path, default=Path(__file__).resolve().parent.parent / "lab/data")
    parser.add_argument("--base-url", default=BASE_URL, help="Geofabrik download base URL (test/mirror override)")
    args = parser.parse_args()
    data = args.data_dir.resolve()
    cache = data / ".downloads"
    cache.mkdir(parents=True, exist_ok=True)
    try:
        ensure_pbf(args.base_url.rstrip("/"), data, cache)
        ensure_gpkg(args.base_url.rstrip("/"), data, cache)
    except (DownloadError, OSError, UnicodeError) as exc:
        print(f"Dataset setup failed: {exc}", file=sys.stderr)
        return 1
    print("Lab 2 data ready (Geofabrik snapshot 2026-10-28, ODbL).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
