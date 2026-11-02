#!/usr/bin/env bash
set -euo pipefail

# Collect every NuGet package needed to restore the whole solution from a clean cache.
# Optional package-archive helper; not required for normal development or CI.
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
CACHE="$SCRIPT_DIR/.nuget-cache"
OUT="$SCRIPT_DIR/nuget-packages"
ARCHIVE="$SCRIPT_DIR/nuget-packages.zip"
SOURCE='https://api.nuget.org/v3/index.json'

command -v dotnet >/dev/null 2>&1 || { echo 'A .NET SDK is required.' >&2; exit 1; }
command -v python3 >/dev/null 2>&1 || { echo 'Python 3 is required to create the ZIP.' >&2; exit 1; }

STAGING="$(mktemp -d "$SCRIPT_DIR/.restore-bundle.XXXXXXXX")"
trap 'rm -rf -- "$STAGING"' EXIT
mkdir -p "$STAGING/packages"

# A fresh, isolated package cache prevents accidental reliance on packages
# installed by unrelated projects or earlier restores.
rm -rf -- "$CACHE"
mkdir -p "$CACHE"

# Generic environment variables can be interpreted as MSBuild properties.
unset Platform PLATFORM || true

dotnet restore "$SCRIPT_DIR/GeoSearch.slnx" \
    --packages "$CACHE" \
    --source "$SOURCE" \
    --no-http-cache \
    --force

mapfile -d '' -t PACKAGES < <(find "$CACHE" -type f -name '*.nupkg' -print0 | sort -z)
if (( ${#PACKAGES[@]} == 0 )); then
    echo 'Restore produced no NuGet package archives.' >&2
    exit 1
fi

for PACKAGE in "${PACKAGES[@]}"; do
    TARGET="$STAGING/packages/$(basename -- "$PACKAGE")"
    if [[ -e "$TARGET" ]]; then
        cmp -s -- "$PACKAGE" "$TARGET" || {
            echo "Conflicting package archives: $(basename -- "$PACKAGE")" >&2
            exit 1
        }
    else
        cp -- "$PACKAGE" "$TARGET"
    fi
done

# Python's standard library avoids a dependency on the external `zip` utility.
python3 - "$STAGING/packages" "$STAGING/nuget-packages.zip" <<'PY'
from pathlib import Path
from sys import argv
from zipfile import ZIP_DEFLATED, ZipFile

source = Path(argv[1])
archive = Path(argv[2])
packages = sorted(source.glob('*.nupkg'))

with ZipFile(archive, 'w', compression=ZIP_DEFLATED, compresslevel=6) as output:
    for package in packages:
        output.write(package, arcname=package.name)

with ZipFile(archive) as verification:
    if verification.testzip() is not None or len(verification.namelist()) != len(packages):
        raise RuntimeError('NuGet package ZIP verification failed')

print(f'Bundled {len(packages)} NuGet packages.')
PY

# Preserve the previous successful bundle if restore or packaging fails.
rm -rf -- "$OUT"
mv -- "$STAGING/packages" "$OUT"
mv -f -- "$STAGING/nuget-packages.zip" "$ARCHIVE"
printf 'Package directory: %s\nArchive for handoff: %s\n' "$OUT" "$ARCHIVE"
