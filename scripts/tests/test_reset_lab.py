"""Offline safety/ordering tests for the destructive lab reset orchestrator.

Run with: python3 -m unittest discover -s scripts/tests
All external programs are stubbed; no database or containers are modified.
"""

import os
import subprocess
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts/reset-lab-db.sh"


class ResetLabTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        binaries = self.root / "bin"
        binaries.mkdir()
        self.log = self.root / "calls.log"
        self.gpkg = self.root / "source.gpkg"
        self.pbf = self.root / "source.osm.pbf"
        self.gpkg.touch()
        self.pbf.touch()

        stubs = {
            "psql": '''#!/bin/bash
printf 'psql %s\\n' "$*" >> "$MOCK_LOG"
case "$*" in
  *rolsuper*) echo "${MOCK_PRIVILEGE:-1}" ;;
  *pg_get_userbyid*) echo "${MOCK_OWNER:-lab1}" ;;
esac
''',
            "just": '''#!/bin/bash
printf 'just %s\\n' "$*" >> "$MOCK_LOG"
if [[ "$*" == *" ${MOCK_FAIL_STEP:-__none__}" ]]; then exit 43; fi
''',
            "curl": '''#!/bin/bash
printf 'curl %s\\n' "$*" >> "$MOCK_LOG"
printf '%s' "${MOCK_HTTP_CODE:-000}"
''',
            "ogr2ogr": "#!/bin/sh\nexit 0\n",
        }
        for name, contents in stubs.items():
            target = binaries / name
            target.write_text(contents, encoding="utf-8")
            target.chmod(0o755)

        self.environment = {
            **os.environ,
            "PATH": f"{binaries}:{os.environ['PATH']}",
            "MOCK_LOG": str(self.log),
            "PGHOST": "postgis",
            "PGUSER": "lab1",
            "PGDATABASE": "lab1",
            "PGPASSWORD": "not-a-real-password",
            "GEOSEARCH_DEVCONTAINER": "1",
            "GEOSEARCH_READER_PASSWORD": "this-is-only-a-fake-test-password",
            "MOCK_HTTP_CODE": "000",
        }

    def run_reset(self, *, confirmation="RESET-LAB1", publish="auto", gpkg=None, pbf=None, **overrides):
        env = {**self.environment, **overrides}
        return subprocess.run(
            ["bash", str(SCRIPT), confirmation, str(gpkg or self.gpkg),
             str(pbf or self.pbf), "9.60 52.30 9.92 52.45", "2026-10-28", publish],
            cwd=ROOT / "lab", env=env, text=True, capture_output=True, check=False,
        )

    def calls(self):
        return self.log.read_text().splitlines() if self.log.exists() else []

    def test_rebuild_in_numeric_order_without_geoserver(self):
        outcome = self.run_reset()
        self.assertEqual(outcome.returncode, 0, outcome.stderr)
        calls = self.calls()
        self.assertLess(next(i for i, c in enumerate(calls) if "DROP DATABASE" in c),
                        next(i for i, c in enumerate(calls) if "CREATE DATABASE" in c))
        recipes = [c.split(" ")[-1] for c in calls if c.startswith("just ")]
        # The two smoke-test recipes also receive positional arguments.
        self.assertEqual([r for r in recipes if r.startswith(("s02", "s03", "s04", "s05", "s08", "s09", "s10"))],
                         ["s02-db-setup", "s03-import-gpkg", "s04-import-addresses",
                          "s05-search-index", "s09-refresh-index", "s10-reader-setup"])
        self.assertTrue(any("s06-search Limmerstr 12 Hannover" in c for c in calls))
        self.assertTrue(any("s07-reverse 9.7320 52.3759" in c for c in calls))
        self.assertTrue(any("bbox=9.60 52.30 9.92 52.45" in c for c in calls))
        self.assertIn("s08 publication will be skipped", outcome.stdout)

    def test_geoserver_is_published_when_available(self):
        outcome = self.run_reset(MOCK_HTTP_CODE="404")
        self.assertEqual(outcome.returncode, 0, outcome.stderr)
        self.assertTrue(any(c.endswith("s08-geoserver-publish") for c in self.calls()))

    def test_rejected_preconditions_never_drop_database(self):
        for description, options in (
            ("confirmation", {"confirmation": "wrong"}),
            ("missing input", {"gpkg": self.root / "missing.gpkg"}),
            ("credential", {"GEOSEARCH_READER_PASSWORD": "short"}),
            ("remote host", {"PGHOST": "other-database.example"}),
            ("unexpected owner", {"MOCK_OWNER": "someone-else"}),
            ("no create privilege", {"MOCK_PRIVILEGE": "0"}),
            ("required GeoServer", {"publish": "yes", "MOCK_HTTP_CODE": "000"}),
            ("GeoServer HTTP failure", {"MOCK_HTTP_CODE": "403"}),
        ):
            with self.subTest(description):
                self.log.unlink(missing_ok=True)
                outcome = self.run_reset(**options)
                self.assertNotEqual(outcome.returncode, 0)
                self.assertFalse(any("DROP DATABASE" in c for c in self.calls()))

    def test_recipe_failure_stops_the_pipeline(self):
        outcome = self.run_reset(publish="no", MOCK_FAIL_STEP="s03-import-gpkg")
        self.assertNotEqual(outcome.returncode, 0)
        calls = self.calls()
        self.assertTrue(any("DROP DATABASE" in c for c in calls))
        self.assertFalse(any("s04-import-addresses" in c for c in calls))


if __name__ == "__main__":
    unittest.main()
