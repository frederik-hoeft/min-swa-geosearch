#!/usr/bin/env bash
# Import locally held TLS material into a Docker-managed volume. Works when the
# Docker daemon is remote or accessed from inside a devcontainer.
set -euo pipefail

root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cert="$root/certs/fullchain.pem"
key="$root/certs/privkey.pem"

for tool in docker openssl tar; do
    command -v "$tool" >/dev/null || { echo "Missing required tool: $tool" >&2; exit 1; }
done
for file in "$cert" "$key"; do
    if [[ ! -s "$file" || ! -f "$file" ]]; then
        echo "Missing or empty TLS file: $file" >&2
        exit 1
    fi
done
if ! openssl x509 -in "$cert" -noout >/dev/null 2>&1 || ! openssl pkey -in "$key" -noout -passin pass: >/dev/null 2>&1; then
    echo 'Invalid TLS certificate or unencrypted private key.' >&2
    exit 1
fi
if [[ "$(openssl x509 -in "$cert" -pubkey -noout)" != "$(openssl pkey -in "$key" -pubout -passin pass:)" ]]; then
    echo 'TLS certificate and private key do not match.' >&2
    exit 1
fi

# Docker Compose resolves the named volume on the daemon host, while the tar
# archive is streamed from this shell (the Docker client). No host path sharing
# and no readable key file in the container image are required.
(
    cd -- "$root/certs"
    tar -chf - fullchain.pem privkey.pem
) | (
    cd -- "$root"
    docker compose --profile setup run --rm --no-deps -T --user 0:0 --entrypoint sh tls-loader -ec '
        set -eu
        umask 077
        stage="$(mktemp -d /run/tls/.import.XXXXXX)"
        cleanup() { rm -rf -- "$stage"; }
        trap cleanup EXIT
        # BusyBox tar -o skips archived ownership; CAP_CHOWN is not needed.
        tar -xof - -C "$stage"
        test -s "$stage/fullchain.pem" && test -s "$stage/privkey.pem"
        chmod 0600 "$stage/fullchain.pem" "$stage/privkey.pem"
        # nginx:stable-alpine uses UID/GID 101; nginx runs non-root.
        chown 101:101 "$stage/fullchain.pem" "$stage/privkey.pem"
        mv -f "$stage/fullchain.pem" /run/tls/fullchain.pem
        mv -f "$stage/privkey.pem" /run/tls/privkey.pem
    '
)
echo 'TLS certificate and key imported into the Docker-managed tls-certs volume.'
echo 'Recreate or restart the frontend container to load the new certificate.'
