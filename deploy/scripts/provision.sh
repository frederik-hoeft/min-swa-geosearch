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
# Docker may initialize a fresh named volume as root-owned. Prepare only the
# Docker-managed GIS volume as root, then run all recipes as the unprivileged
# image user. This also repairs volumes left by an interrupted earlier setup.
echo 'Preparing GIS data volume ownership'
"${compose[@]}" run --rm --no-deps --user 0:0 --entrypoint sh gis-tools -ec '
    mkdir -p /workspace/lab/data/.downloads
    chown -R 10001:10001 /workspace/lab/data
    chmod u+rwx /workspace/lab/data /workspace/lab/data/.downloads
'
# Fail early if the volume is not writable by the normal non-root user.
echo 'Checking GIS data volume permissions'
if ! "${compose[@]}" run --rm --no-deps --entrypoint sh gis-tools -ec '
    mkdir -p /workspace/lab/data/.downloads
    : > /workspace/lab/data/.downloads/.write-check
    rm /workspace/lab/data/.downloads/.write-check
'; then
    echo 'GIS data volume is not writable by the gis-tools user (10001:10001).' >&2
    echo 'See deploy/README.md for volume permission recovery; no database import was attempted.' >&2
    exit 1
fi
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
