#!/usr/bin/env bash
# Idempotent local-only provisioning of the restricted GIS search login.
set -euo pipefail
umask 077

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ -z "${GEOSEARCH_READER_PASSWORD:-}" ]]; then
    echo 'GEOSEARCH_READER_PASSWORD is required (set it in the devcontainer environment or export it before running just s10-reader-setup).' >&2
    exit 1
fi
if (( ${#GEOSEARCH_READER_PASSWORD} < 24 )); then
    echo 'GEOSEARCH_READER_PASSWORD must contain at least 24 characters.' >&2
    exit 1
fi
export GEOSEARCH_READER_PASSWORD
export PGHOST="${PGHOST:-localhost}"
export PGPORT="${PGPORT:-${POSTGIS_PORT:-5432}}"
export PGUSER="${PGUSER:-lab1}"
export PGPASSWORD="${PGPASSWORD:-${POSTGIS_PASSWORD:-lab1-local-only}}"
export PGDATABASE=lab1

psql -X -v ON_ERROR_STOP=1 --single-transaction -f "$repo_dir/lab/sql/08-search-reader.sql"
echo 'geosearch_reader is provisioned; configure ConnectionStrings:Search to use this login.'
