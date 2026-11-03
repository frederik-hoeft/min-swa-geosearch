#!/usr/bin/env bash
# Idempotent local-only provisioning of the restricted GIS search login.
set -euo pipefail
umask 077

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
password_file="$repo_dir/lab/.secrets/search-reader-password"

if [[ -z "${GEOSEARCH_READER_PASSWORD:-}" ]]; then
    if [[ -s "$password_file" ]]; then
        GEOSEARCH_READER_PASSWORD="$(<"$password_file")"
    else
        command -v python3 >/dev/null || { echo 'python3 is required to generate a reader password' >&2; exit 1; }
        mkdir -p "$(dirname "$password_file")"
        GEOSEARCH_READER_PASSWORD="$(python3 -c 'import secrets; print(secrets.token_hex(32))')"
        printf '%s\n' "$GEOSEARCH_READER_PASSWORD" > "$password_file"
        chmod 600 "$password_file"
        echo "Generated reader password in $password_file (ignored by Git). Keep it private." >&2
    fi
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
