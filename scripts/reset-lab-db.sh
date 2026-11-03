#!/usr/bin/env bash
# Destructively rebuild only the disposable lab1 GIS database from the lab recipes.
set -euo pipefail

if (( $# != 6 )); then
    echo 'Usage: reset-lab-db.sh RESET-LAB1 <gpkg> <pbf> <bbox> <snapshot> <geoserver: auto|yes|no>' >&2
    exit 2
fi

confirmation=$1
gpkg=$2
pbf=$3
bbox=$4
snapshot=$5
publish_geoserver=$6

if [[ "$confirmation" != 'RESET-LAB1' ]]; then
    echo 'Refusing destructive reset: pass RESET-LAB1 as the explicit confirmation.' >&2
    exit 2
fi

# This workflow is deliberately restricted to the local lab stack. In particular,
# never allow inherited PostgreSQL environment variables to target another database.
case "${PGHOST:-}" in
    localhost|127.0.0.1|postgis) ;;
    *) echo 'Refusing reset: PGHOST must be localhost, 127.0.0.1, or postgis.' >&2; exit 2 ;;
esac
if [[ "${PGDATABASE:-}" != 'lab1' || "${PGUSER:-}" != 'lab1' ]]; then
    echo 'Refusing reset: PGDATABASE and PGUSER must both be lab1.' >&2
    exit 2
fi
case "$publish_geoserver" in
    auto|yes|no) ;;
    *) echo 'GeoServer option must be auto, yes, or no.' >&2; exit 2 ;;
esac

lab_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/../lab" && pwd)"
cd "$lab_dir"

# Fail before dropping anything if the requested rebuild cannot be completed.
for tool in just psql ogr2ogr; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "Missing required tool: $tool" >&2
        exit 1
    fi
done
for dataset in "$gpkg" "$pbf"; do
    if [[ ! -f "$dataset" ]]; then
        echo "Missing input dataset: $dataset" >&2
        exit 1
    fi
done
if [[ -z "${GEOSEARCH_READER_PASSWORD:-}" || ${#GEOSEARCH_READER_PASSWORD} -lt 24 ]]; then
    echo 'GEOSEARCH_READER_PASSWORD must be set to at least 24 characters before resetting the database.' >&2
    exit 1
fi

# Propagate just variable overrides to each nested recipe, rather than silently
# falling back to their defaults when this composite recipe is customized.
run() {
    just "gpkg=$gpkg" "pbf=$pbf" "bbox=$bbox" "snapshot=$snapshot" "$@"
}

# Standalone labs need their Compose stack started; the devcontainer already
# runs the same PostGIS service, and must not start a second Compose project.
if [[ "${GEOSEARCH_DEVCONTAINER:-}" != '1' && ! -d /workspace/.devcontainer ]]; then
    if ! command -v docker >/dev/null 2>&1; then
        echo 'Docker is required outside the devcontainer for s01-stack-up.' >&2
        exit 1
    fi
    run s01-stack-up
fi

# Check connectivity and privileges on the maintenance database before removing
# the current GIS database. The PostgreSQL administrative role is local-only.
if ! psql -X -v ON_ERROR_STOP=1 -d postgres -Atqc \
    'SELECT (rolsuper OR rolcreatedb)::integer FROM pg_roles WHERE rolname = current_user' | grep -qx '1'; then
    echo 'Cannot connect to the PostgreSQL maintenance database with database-creation privileges.' >&2
    exit 1
fi
owner=$(psql -X -v ON_ERROR_STOP=1 -d postgres -Atqc \
    "SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname = 'lab1'")
if [[ -n "$owner" && "$owner" != 'lab1' ]]; then
    echo "Refusing reset: lab1 is owned by $owner, not the expected local lab1 role." >&2
    exit 1
fi

# GeoServer is optional for the HTTP search API. Resolve publication before
# deleting lab1 so broken credentials or an unhealthy existing service cannot
# turn an otherwise predictable rebuild into a partial failure.
if [[ "$publish_geoserver" != 'no' ]]; then
    if ! command -v curl >/dev/null 2>&1; then
        echo 'Missing required tool: curl (for GeoServer publication detection).' >&2
        exit 1
    fi
    geoserver_url="${GEOSERVER_URL:-http://localhost:${GEOSERVER_PORT:-8081}/geoserver}"
    http_code=$(curl --silent --output /dev/null --write-out '%{http_code}' \
        --connect-timeout 2 --max-time 5 -u "admin:${GEOSERVER_ADMIN_PASSWORD:-lab1-local-only}" \
        "$geoserver_url/rest/workspaces/osm.json" || true)
    case "$http_code" in
        200|404) publish_geoserver=yes ;;
        000)
            if [[ "$publish_geoserver" == 'yes' ]]; then
                echo 'GeoServer publication was required but GeoServer is unreachable.' >&2
                exit 1
            fi
            publish_geoserver=no
            echo 'GeoServer is not reachable; optional s08 publication will be skipped.'
            ;;
        *)
            echo "GeoServer preflight failed: HTTP $http_code. Refusing to reset lab1." >&2
            exit 1
            ;;
    esac
fi

printf '%s\n' 'WARNING: This permanently deletes all data and schema objects in database lab1.' \
    'The geosearch_auth database, Compose volumes, and downloaded source datasets are preserved.'

# DROP DATABASE cannot run inside a transaction. WITH (FORCE) disconnects API and
# GeoServer sessions still using lab1. Both commands target literal names only.
psql -X -v ON_ERROR_STOP=1 -d postgres -c 'DROP DATABASE IF EXISTS lab1 WITH (FORCE);'
psql -X -v ON_ERROR_STOP=1 -d postgres -c 'CREATE DATABASE lab1 OWNER lab1;'

# Run the lab steps in numeric order. The refresh (s09) is intentionally run even
# after the full rebuild (s05) to exercise the same refresh path used subsequently.
run s02-db-setup
run s03-import-gpkg
run s04-import-addresses
run s05-search-index
run s06-search 'Limmerstr 12 Hannover'
run s07-reverse 9.7320 52.3759
if [[ "$publish_geoserver" == 'yes' ]]; then
    run s08-geoserver-publish
fi
run s09-refresh-index
run s10-reader-setup

echo 'lab1 reset and lab recipes completed successfully.'
