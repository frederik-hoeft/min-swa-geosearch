#!/usr/bin/env bash
# Local loopback development only. Use a certificate trusted by clients for real HTTPS.
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
command -v openssl >/dev/null || { echo 'openssl is required.' >&2; exit 1; }
mkdir -p "$root/certs"
if [[ -e "$root/certs/fullchain.pem" || -e "$root/certs/privkey.pem" ]]; then
    echo 'Refusing to overwrite an existing TLS certificate/key.' >&2
    exit 1
fi
umask 077
openssl req -x509 -newkey rsa:3072 -sha256 -nodes -days 30 \
    -subj '/CN=localhost' \
    -addext 'subjectAltName=DNS:localhost,IP:127.0.0.1' \
    -keyout "$root/certs/privkey.pem" -out "$root/certs/fullchain.pem"
printf '%s\n' 'Created a local-only self-signed certificate. Browsers will not trust it automatically.'
