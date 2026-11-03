#!/usr/bin/env bash
set -euo pipefail

root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
"$root/scripts/validate-env.sh"

if (( $# < 1 || $# > 2 )); then
    echo 'Usage: deploy/scripts/stack.sh {up|down|ps|logs|config} [--https]' >&2
    exit 2
fi
command="$1"
mode="${2:-}"
if [[ -n "$mode" && "$mode" != '--https' ]]; then
    echo 'Only --https is supported as the optional second argument.' >&2
    exit 2
fi

compose=(docker compose --env-file "$root/.env" -f "$root/compose.yaml")
if [[ "$mode" == '--https' ]]; then
    if [[ ! -s "$root/certs/fullchain.pem" || ! -s "$root/certs/privkey.pem" ]]; then
        echo 'HTTPS requires deploy/certs/fullchain.pem and privkey.pem.' >&2
        exit 1
    fi
    compose+=(-f "$root/compose.https.yaml")
fi

case "$command" in
    up)      "${compose[@]}" up --build -d ;;
    down)    "${compose[@]}" down ;; # Keep persistent PostGIS data.
    ps)      "${compose[@]}" ps ;;
    logs)    "${compose[@]}" logs --follow --tail=100 api web postgis ;;
    config)  "${compose[@]}" config --quiet ;;
    *) echo 'Usage: deploy/scripts/stack.sh {up|down|ps|logs|config} [--https]' >&2; exit 2 ;;
esac
