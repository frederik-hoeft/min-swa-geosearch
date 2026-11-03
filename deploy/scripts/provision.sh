#!/usr/bin/env bash
# Populate only the disposable GIS database; app updates never rerun imports.
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
if [[ ! -f .env ]]; then
    echo 'Missing deploy/.env: copy deploy/.env.example and set passwords.' >&2
    exit 1
fi
compose=(docker compose --profile setup)
"${compose[@]}" up -d --wait postgis
"${compose[@]}" build gis-tools
for recipe in s00-download-data s02-db-setup s03-import-gpkg s04-import-addresses s05-search-index s10-reader-setup s06-search s07-reverse; do
    echo "Provisioning: $recipe"
    if [[ "$recipe" == s06-search ]]; then
        "${compose[@]}" run --rm --no-deps gis-tools "$recipe" 'Limmerstr 12 Hannover'
    elif [[ "$recipe" == s07-reverse ]]; then
        "${compose[@]}" run --rm --no-deps gis-tools "$recipe" 9.7320 52.3759
    else
        "${compose[@]}" run --rm --no-deps gis-tools "$recipe"
    fi
done
