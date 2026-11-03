"""Exercise real downloader I/O against an in-process HTTP server, never Geofabrik."""

import hashlib
import io
import sqlite3
import subprocess
import sys
import tempfile
import threading
import unittest
import zipfile
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

REPOSITORY = Path(__file__).resolve().parents[2]
SCRIPT = REPOSITORY / "scripts/download-lab-data.py"
PBF = "niedersachsen-261028.osm.pbf"
ZIP = "niedersachsen-261028-free.gpkg.zip"


class DatasetServer(ThreadingHTTPServer):
    def __init__(self, address, files):
        super().__init__(address, DatasetHandler)
        self.files = files
        self.requests = []


class DatasetHandler(BaseHTTPRequestHandler):
    def do_GET(self):
        name = self.path.rsplit("/", 1)[-1]
        self.server.requests.append((name, self.headers.get("Range")))
        if name not in self.server.files:
            self.send_error(404)
            return
        data = self.server.files[name]
        header = self.headers.get("Range")
        offset = 0
        if header:
            if not header.startswith("bytes=") or not header.endswith("-"):
                self.send_error(400)
                return
            offset = int(header[6:-1])
            if offset >= len(data):
                self.send_response(416)
                self.end_headers()
                return
        self.send_response(206 if header else 200)
        self.send_header("Content-Length", str(len(data) - offset))
        if header:
            self.send_header("Content-Range", f"bytes {offset}-{len(data) - 1}/{len(data)}")
        self.end_headers()
        self.wfile.write(data[offset:])

    def log_message(self, *_args):
        pass


def package_gpkg(path: Path) -> bytes:
    """Construct a real tiny SQLite GeoPackage and archive with a nested member path."""
    with sqlite3.connect(path) as db:
        db.execute("CREATE TABLE gpkg_contents (table_name TEXT)")
        db.execute("INSERT INTO gpkg_contents VALUES ('roads')")
    output = io.BytesIO()
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(path, arcname="niedersachsen-261028-free/niedersachsen.gpkg")
    return output.getvalue()


class LabDatasetDownloadTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.destination = self.root / "downloaded"
        self.pbf_bytes = b"PBF fixture bytes\n" * 200
        archive = package_gpkg(self.root / "fixture.gpkg")
        self.files = {
            PBF: self.pbf_bytes,
            f"{PBF}.md5": f"{hashlib.md5(self.pbf_bytes).hexdigest()}  {PBF}\n".encode(),
            ZIP: archive,
        }
        self.server = DatasetServer(("127.0.0.1", 0), self.files)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.addCleanup(self.server.server_close)
        self.addCleanup(self.server.shutdown)
        self.url = f"http://127.0.0.1:{self.server.server_port}"

    def run_downloader(self, *, base_url=None, success=True):
        result = subprocess.run(
            [sys.executable, str(SCRIPT), "--data-dir", str(self.destination), "--base-url", base_url or self.url],
            capture_output=True, text=True, timeout=20, check=False,
        )
        if success:
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        else:
            self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        return result

    def test_download_extract_and_rerun_offline_without_network_requests(self):
        self.run_downloader()
        self.assertEqual(self.pbf_bytes, (self.destination / PBF).read_bytes())
        gpkg = self.destination / "niedersachsen-261028-free/niedersachsen.gpkg"
        with sqlite3.connect(gpkg) as db:
            self.assertEqual(("roads",), db.execute("SELECT table_name FROM gpkg_contents").fetchone())
        self.assertFalse((self.destination / ".downloads" / ZIP).exists())
        original_mtimes = ((self.destination / PBF).stat().st_mtime_ns, gpkg.stat().st_mtime_ns)
        count = len(self.server.requests)
        self.run_downloader(base_url="http://127.0.0.1:1")
        self.assertEqual(count, len(self.server.requests))
        self.assertEqual(original_mtimes, ((self.destination / PBF).stat().st_mtime_ns, gpkg.stat().st_mtime_ns))

    def test_detect_corrupt_pbf_and_restore_it_from_pinned_checksum(self):
        self.run_downloader()
        (self.destination / PBF).write_bytes(b"bad data")
        self.server.requests.clear()
        self.run_downloader()
        self.assertEqual(self.pbf_bytes, (self.destination / PBF).read_bytes())
        self.assertIn((PBF, None), self.server.requests)
        self.assertNotIn((ZIP, None), self.server.requests)

    def test_resume_a_partial_pbf_download_with_http_range(self):
        part = self.destination / ".downloads" / f"{PBF}.part"
        part.parent.mkdir(parents=True)
        part.write_bytes(self.pbf_bytes[:123])
        self.run_downloader()
        self.assertIn((PBF, "bytes=123-"), self.server.requests)
        self.assertEqual(self.pbf_bytes, (self.destination / PBF).read_bytes())

    def test_invalid_zip_is_rejected_and_does_not_poison_next_rerun(self):
        self.files[ZIP] = b"Not a zip file"
        self.run_downloader(success=False)
        self.assertFalse((self.destination / "niedersachsen-261028-free/niedersachsen.gpkg").exists())
        self.assertFalse((self.destination / ".downloads" / ZIP).exists())
        self.files[ZIP] = package_gpkg(self.root / "retry.gpkg")
        self.run_downloader()

    def test_bad_pbf_checksum_does_not_install_corrupt_data(self):
        self.files[f"{PBF}.md5"] = f"{'0' * 32}  {PBF}".encode()
        self.run_downloader(success=False)
        self.assertFalse((self.destination / PBF).exists())
        self.assertFalse((self.destination / ".downloads" / PBF).exists())


if __name__ == "__main__":
    unittest.main()
