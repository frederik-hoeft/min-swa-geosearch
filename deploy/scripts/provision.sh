#!/usr/bin/env bash
# Download/import the fixed Hannover extract and provision the restricted reader.
# Run before starting the API on a fresh deployment database; safe to rerun.
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
"$root/scripts/validate-env.sh"

compose=(docker compose --env-file "$root/.env" -f "$root/compose.yaml" --profile setup)

"${compose[@]}" up -d --wait postgis
"${compose[@]}" build gis-tools
for recipe in s00-download-data s02-db-setup s03-import-gpkg s04-import-addresses s05-search-index s10-reader-setup; do
    echo "Provisioning: $recipe"
    "${compose[@]}" run --rm --no-deps gis-tools "$recipe"
done
# --no-deps avoids repeatedly starting/inspecting the already healthy database.
"${compose[@]}" run --rm --no-deps gis-tools s06-search 'Limmerstr 12 Hannover'
"${compose[@]}" run --rm --no-deps gis-tools s07-reverse 9.7320 52.3759
printf '%s\n' 'GIS data and read-only role provisioned. Run deploy/scripts/stack.sh up.'
