#!/usr/bin/env bash
# Creates an independent Identity database. The API applies EF migrations at startup.
set -euo pipefail
export PGHOST="${PGHOST:-localhost}"
export PGPORT="${PGPORT:-${POSTGIS_PORT:-5432}}"
export PGUSER="${PGUSER:-lab1}"
export PGPASSWORD="${PGPASSWORD:-${POSTGIS_PASSWORD:-lab1-local-only}}"
export PGDATABASE=postgres
DATABASE_NAME=geosearch_auth
if ! psql -v ON_ERROR_STOP=1 -tAc 'SELECT 1 FROM pg_database WHERE datname = '\''geosearch_auth'\''' | grep -qx 1; then
    createdb "$DATABASE_NAME"
fi
