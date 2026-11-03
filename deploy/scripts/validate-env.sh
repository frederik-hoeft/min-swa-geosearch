#!/usr/bin/env bash
# Preflight Compose credentials without evaluating the dotenv file or printing secrets.
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ ! -f "$root/.env" ]]; then
    echo 'Missing deploy/.env. Copy deploy/.env.example and provide strong passwords.' >&2
    exit 1
fi
for key in POSTGIS_PASSWORD GEOSEARCH_READER_PASSWORD; do
    line="$(grep -E "^${key}=" "$root/.env" | tail -n 1 || true)"
    if [[ -z "$line" ]]; then
        printf 'Missing %s in deploy/.env.\n' "$key" >&2
        exit 1
    fi
    value="${line#*=}"
    if [[ "$value" == \"*\" && "$value" == *\" ]]; then
        value="${value:1:${#value}-2}"
    elif [[ "$value" == \'*\' && "$value" == *\' ]]; then
        value="${value:1:${#value}-2}"
    fi
    if [[ "$value" == REPLACE_* || ${#value} -lt 24 ]]; then
        printf 'Replace %s with a strong password (at least 24 characters).\n' "$key" >&2
        exit 1
    fi
done
