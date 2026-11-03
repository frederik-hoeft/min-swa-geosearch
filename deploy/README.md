# Local container deployment

This is a **separate deployment stack**, not an extension of `lab/compose.yaml` or the VS Code devcontainer. It builds the ASP.NET Core API and the Blazor WebAssembly client locally, serves the client with stock Nginx, and keeps a persistent PostGIS database on a private Compose network. It does not require a container registry or a CI deployment job.

The browser uses one origin: Nginx serves `/` and all WASM assets, and forwards `/api/*` and `/swagger*` to the API service. The API is never published directly on a host port. `wwwroot/appsettings.json` intentionally has no `Api:BaseUrl` override, so browser requests use relative URLs. No CORS configuration is needed for this deployment.

## Prerequisites

- Docker Engine and Docker Compose v2 with support for `env_file.required` (Compose 2.24 or newer); Docker Desktop also works.
- Internet access for the first build (`mcr.microsoft.com/dotnet/sdk:10.0`, ASP.NET runtime, stock Nginx, Debian 13 GIS tools, and `postgis/postgis:17-3.5`) and for the one-time Geofabrik download (~1.3 GB).
- Enough disk for the GIS dataset, imported PostgreSQL database, image layers, and build cache. The PostGIS image is currently explicitly pinned to `linux/amd64`, like the supplied lab stack.
- Bash for the convenience scripts. Alternatively invoke `docker compose --env-file deploy/.env -f deploy/compose.yaml ...` directly from the repository root.

Run commands from the **repository root**, although the scripts themselves also work from another directory:

```sh
cp deploy/.env.example deploy/.env
# Edit deploy/.env: replace BOTH example passwords with strong local secrets.

# First run: download data, import, create/refresh materialized view, grant reader role.
./deploy/scripts/provision.sh

# Build both .NET images locally and start API + static Nginx frontend.
./deploy/scripts/stack.sh up
```

Visit **http://localhost:8080/**. The API is exposed through the same origin, for example:

```sh
curl -fsS http://localhost:8080/api/health
curl -fsS 'http://localhost:8080/api/search?q=Limmerstr%2012%20Hannover&limit=5'
```

The default host bind is `127.0.0.1`, **not** a LAN-wide or public deployment. `http://localhost` is considered a secure context for browser geolocation on the same machine. Other devices and hostnames require **trusted HTTPS**; see below. The web app's interactive geolocation UI is still separate frontend work.

The one-off GIS tools container writes downloads using `GEOSEARCH_DATA_UID` and `GEOSEARCH_DATA_GID` from `deploy/.env` (default `1000:1000`); on Linux, set these to `id -u` and `id -g` if your checkout uses a different UID/GID.

`provision.sh` reuses existing verified downloads in `lab/data/` and is safe to run again with unchanged data. The imports intentionally replace their tables and recreate the index, so it is **not** an online/no-downtime migration. Stop the API if reimporting substantial data in an existing deployment. Provisioning skips optional GeoServer, which the search API does not require. Do **not** run `docker compose down -v` unless you explicitly want to destroy the imported PostGIS volume. `stack.sh down` retains it.

## Service and image boundaries

| Service | Image | Exposure | Responsibility |
| --- | --- | --- | --- |
| `web` | `nginx:stable-alpine` with published WASM assets | Host loopback `8080` by default | SPA static files, same-origin API and Swagger reverse proxy |
| `api` | ASP.NET Core 10 runtime, unprivileged UID | Internal `api:8080` only | Read-only GeoJSON endpoints |
| `postgis` | `postgis/postgis:17-3.5` | Internal `postgis:5432` only | Persistent instructor-style `lab1` dataset and search functions |
| `gis-tools` | Debian + GDAL, Just, Python and `psql` | One-off, behind the `setup` profile | Download/import data and provision `geosearch_reader` |

Only the reader credentials are passed to the API. Database-admin credentials are reserved for the database and the one-off provisioning container. Both passwords live in Git-ignored `deploy/.env`; Compose substitutes them when starting services. Compose `config` and container inspection can expose the values, so treat machine access as privileged and do not paste their output into bug reports. The `api` service also accepts an optional Git-ignored `deploy/.env.local` for other ASP.NET Core settings. **Do not put frontend settings or secrets in that file**; it is loaded only into the API container.

The supplied source snapshot predates the separate auth-removal work and still requires its legacy application settings and database at API startup. Those interim settings can be supplied in `deploy/.env.local`; **this deployment does not provision or configure the legacy Identity database**. After the parallel auth-removal change is merged, those settings should no longer be needed. This deployment layer intentionally does not edit or depend on auth source files, existing auth instructions, or Identity migration scripts.

To minimize potential leakage of machine-local configuration, `.dockerignore` excludes local `appsettings` overrides, dotenv files, certificate keys, build artifacts, and downloaded GIS data. Configuration reaches the final API image at runtime, not during `dotnet publish`. The WebAssembly output contains only the environment-neutral tracked `appsettings.json` and uses relative API calls.

## Operational commands

```sh
./deploy/scripts/stack.sh ps
./deploy/scripts/stack.sh logs
./deploy/scripts/stack.sh config      # Parses Compose without printing rendered secrets
./deploy/scripts/stack.sh up          # Rebuild locally and restart changed services
./deploy/scripts/stack.sh down        # No -v: preserves imported PostGIS data
```

To provision again after a source dataset update, change the pinned snapshot/import definitions in `lab/` deliberately, then rerun `provision.sh` against the same Compose database. Ordinary `up` does **not** download or import data. Updates to application images do not reset the PostGIS volume.

The web build also materializes the .NET 10 static-web-assets manifest aliases into its published `wwwroot`, so Nginx can resolve original loader URLs such as `/_framework/blazor.webassembly.js` to their fingerprinted files. This happens in a temporary Python build stage; the running frontend image remains stock Nginx with static files only.

The build configuration defaults to `Release`; set `DOTNET_BUILD_CONFIGURATION=Debug` in `deploy/.env` for a local debugging build. ASP.NET Core's Swagger registration is controlled separately by `ASPNETCORE_ENVIRONMENT`. Set it to `Development` **only for local debugging**; then Swagger UI is reachable at `http://localhost:8080/swagger` and the document at `/swagger/v1/swagger.json`. A Debug build running with `ASPNETCORE_ENVIRONMENT=Production` will not enable Swagger. Never expose Development behavior to untrusted users.

## Optional local HTTPS

For a same-origin HTTPS smoke test, create a **self-signed localhost certificate** and enable the Compose overlay:

```sh
./deploy/scripts/local-cert.sh
./deploy/scripts/stack.sh up --https
# https://localhost:8443/
```

The helper refuses to overwrite existing certificate files; they remain under Git-ignored `deploy/certs/`. Browsers will **not automatically trust** a self-signed certificate. You must establish trust separately for browser testing. For a real hostname or for access from mobile devices, provide a certificate chain and private key trusted by those devices at `deploy/certs/fullchain.pem` and `deploy/certs/privkey.pem`, with appropriate file permissions. The HTTPS overlay listens on container port 8443; only loopback host port 8443 is published by default. To expose it outside the host, configure `GEOSEARCH_HTTPS_BIND_ADDRESS` and appropriate firewalls/hostnames, or terminate TLS at a trusted external ingress. Do not expose the default HTTP listener on an untrusted network.

The HTTPS overlay keeps the HTTP listener for loopback smoke tests, rather than redirecting to a potentially incorrect host/port. Both endpoints use identical static and proxy routes. Docker's embedded DNS resolver is configured in Nginx so replacement API containers do not require a manual Nginx restart.

## Security and troubleshooting

- The proxy strips no query parameters: `/api/nearby?lat=...&lon=...` reaches PostGIS unchanged. **Nginx access logging is disabled**, since request query strings can contain device coordinates. Don't enable request-URI logging in any upstream proxy or APM agent without redaction.
- The API is bound internally to HTTP port 8080. Only Nginx publishes a browser-facing port. No PostGIS administrative port is published on the host.
- Neither the frontend container nor the browser receives any database credentials. The restricted reader role must be provisioned by `provision.sh` before starting the application.
- Nginx responds with its own `404` for missing `_framework` and `_content` assets rather than returning the SPA's HTML, which would produce confusing Blazor JSON/MIME errors. Ordinary client-side navigation paths fall back to `index.html`.
- The release does not introduce application-level rate limiting, resource quotas, certificate automation, managed backups, or an off-host availability guarantee. Treat the default loopback configuration as local deployment, not an Internet-ready production deployment.
- If `/` loads but `/api/health` returns `502`, inspect `./deploy/scripts/stack.sh logs`. A fresh snapshot with auth not yet removed needs its legacy application settings; a fresh PostGIS volume needs `provision.sh`.
- The frontend static resources and server API are served on the **same origin**, so do not add an absolute `Api:BaseUrl` client override for this deployment.
