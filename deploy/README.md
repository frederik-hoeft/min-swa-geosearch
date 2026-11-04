# Local Docker deployment

This deployment follows the same **separate frontend, ASP, and database** pattern as UFWeb, with ordinary TCP over Docker networks instead of Unix-domain sockets. Docker Compose builds both application images locally; no registry, CI deployment, host .NET SDK, or host GDAL is required.

| Service | Responsibility | Network exposure |
| --- | --- | --- |
| `frontend` | Nginx serves the published Blazor WASM app and terminates HTTPS | Only published port (`127.0.0.1:8443` by default) |
| `api` | ASP.NET Core search endpoints (and Swagger in Development) | Private `api:8080`; no host port |
| `postgis` | Persistent `lab1` search database | Private `postgis:5432`; no host port |
| `gis-tools` | One-off dataset download, import and reader-role setup | `setup` profile only; not part of the running app |

Nginx and ASP share the `frontend` Docker network. ASP and PostGIS share the separate `database` network; Nginx cannot connect directly to PostGIS. All browser requests share the same HTTPS origin: `/api/*` and `/swagger` are proxied to ASP; everything else is served statically, with a fallback to `index.html` for client-side routes. No browser CORS setting or API base-URL override is needed.

## Recommended acceptance-test setup: Docker-outside-of-Docker (DooD)

**Supported and recommended for local acceptance testing:** open the repository in the VS Code devcontainer, then run the deployment commands **inside that devcontainer** using its Docker CLI and the **host's Docker daemon** (DooD). This is not Docker-in-Docker: the devcontainer does not run a second daemon. The host only needs Docker Engine/Compose, a working browser, and enough storage for the GIS data; the .NET SDK, GDAL, and build tooling are provided by the devcontainer and/or image build stages. Running the same commands directly on the Docker host is supported as well.

1. On the host, start Docker and the devcontainer. In the devcontainer terminal at `/workspace`, verify that the CLI can reach the host daemon:

   ```sh
   docker info --format 'Docker server: {{.ServerVersion}}'
   docker compose version
   docker ps
   ```

2. Follow [First-time setup](#first-time-setup) **from the devcontainer terminal** to create `deploy/.env`, generate or supply a TLS certificate, run `load-cert.sh`, and provision PostGIS using `provision.sh`. The two environment files have different purposes: `.devcontainer/.env` configures the *development* stack; `deploy/.env` configures this *acceptance/deployment* stack. Keep their secrets separate.

3. Build and start the independent deployment project (Compose project name `geosearch`, distinct from the `geospatial-search` devcontainer stack):

   ```sh
   cd /workspace/deploy
   docker compose config --quiet
   docker compose up -d --build
   docker compose ps
   docker compose logs --tail=50 frontend api postgis
   ```

4. **Open the application from a browser on the Docker daemon host**, normally at `https://localhost:8443/` (or the port specified by `GEOSEARCH_HTTPS_PORT`). Test that the Blazor app boots without missing `/_framework/` modules, and that `/api/health` and a real `/api/search` request work **through Nginx**, not via the API container directly. From a shell **on that host**, a quick smoke test is:

   ```sh
   curl -kfsS https://localhost:8443/api/health
   curl -kfsS 'https://localhost:8443/api/search?q=Limmerstr%2012%20Hannover&limit=5'
   ```

   `-k` bypasses certificate verification **for this curl smoke test only**. It does not establish browser trust. For actual browser/geolocation acceptance tests, trust the local certificate in the browser/OS or supply a trusted certificate. Check the search UI, result markers, nearby results, OSM attribution, and both granted and denied geolocation paths **once those frontend features are implemented**. Swagger is available at `/swagger` only when `ASPNETCORE_ENVIRONMENT=Development`; a Debug image build alone does not enable it.

### DooD boundaries and common pitfalls

- **The Docker daemon owns volumes and resolves bind-mount paths.** `/workspace` inside the devcontainer is not necessarily a path on the host daemon's filesystem. Do not mount `deploy/certs/` or `lab/data/` directly into deployment containers; the existing `load-cert.sh` and `provision.sh` transfer TLS material and download GIS inputs into Docker-managed named volumes instead. Image build contexts are sent by the Docker client and do not need a corresponding host checkout path.
- **`localhost` is relative to where the command runs.** The published HTTPS port binds to `127.0.0.1` on the **Docker daemon host** by default. `curl https://localhost:8443` *inside* the devcontainer may contact the wrong network namespace even while `docker compose ps` works. Use the host browser/terminal, or an explicitly configured secure host tunnel if using a remote daemon; do not expose the test port publicly merely to work around this.
- **State is independent of the devcontainer stack.** The `geosearch_postgis-data`, `geosearch_gis-data`, and `geosearch_tls-certs` volumes belong to this deployment project. `docker compose down` stops this stack without deleting them. `docker compose down -v` **destroys** them, including the imported data and TLS material. Do not rerun provisioning for routine application rebuilds because it replaces imported GIS data and rebuilds the search index.
- **DooD grants powerful host access.** Access to the Docker daemon socket effectively grants administrative control over the daemon host. Use this workflow only from a trusted, local devcontainer with trusted repository content, and do not expose the Docker API/socket over a public network. This recommendation is for **local acceptance tests**, not a hardened Internet-facing production deployment.

## First-time setup

1. Configure the one deployment environment file:

   ```sh
   cp deploy/.env.example deploy/.env
   chmod 600 deploy/.env
   # Set POSTGIS_PASSWORD and GEOSEARCH_READER_PASSWORD to strong local secrets.
   ```

2. Supply Nginx's TLS certificate and private key locally as `deploy/certs/fullchain.pem` and `deploy/certs/privkey.pem`. For a **localhost-only smoke test**, you can create a temporary self-signed certificate:

   ```sh
   ./deploy/scripts/local-cert.sh
   ```

   Import the certificate and private key into Docker-managed TLS storage:

   ```sh
   ./deploy/scripts/load-cert.sh
   ```

   This step is required **before starting Nginx**, and again whenever the certificate changes (restart the frontend afterward). **After updating from DEP2.3, run it once more:** it updates certificate ownership in the existing TLS volume to match the non-root frontend. The import uses a temporary, network-isolated container to receive the files over Docker's standard input, sets owner-only permissions (mode `0600`, owned by the non-root Nginx user), and stores them in a named volume. It works when Docker is accessed from a devcontainer whose `/workspace` path is different from the Docker daemon host's filesystem. Neither the certificate nor the key is copied into the Nginx image. The one-off importer has only the `CHOWN` capability and no network access; the frontend runs entirely as the unprivileged `nginx` user. The importer requires `docker`, `tar`, and `openssl` in the calling environment.

   Browsers will **not trust this certificate automatically**; configure local trust before testing geolocation or use a trusted certificate for a real hostname. Avoid exposing the default setup over a public network.

3. Import the instructor's Geofabrik data, build the search index, and provision the read-only database role:

   ```sh
   ./deploy/scripts/provision.sh
   ```

   This uses the one-off `gis-tools` image and a separate Docker-managed `gis-data` volume for downloaded inputs. It does **not** write into the checkout's `lab/data/` directory and therefore does not depend on the development container's or host's file ownership. The image runs as non-root UID/GID `10001:10001`. Provisioning first assigns ownership on **only the Docker-managed GIS volume**, then verifies that the non-root process can write to its download cache before the first recipe. The `gis-data` and PostGIS volumes persist across reruns. Downloads and GeoPackage extraction log byte counts, percentages, and transfer rates approximately every five seconds directly in the provisioning terminal. Partial downloads resume on the next run. It is safe to retry after failure but **rebuilds the index and replaces imported data**; it is not an online database migration. Run it only when initializing or intentionally refreshing the local dataset.

4. Build and start the runtime services:

   ```sh
   cd deploy
   docker compose up -d --build
   ```

   Open **https://localhost:8443/** once your browser trusts the certificate. Check `https://localhost:8443/api/health` or `/api/search?q=Limmerstr%2012%20Hannover&limit=5`.

## Where configuration belongs

- **`deploy/.env`** (Git-ignored): deployment-specific PostgreSQL admin/reader passwords, published HTTPS port/bind address, and ASP environment/build mode. Values are interpolated into `compose.yaml`; only the services that need each secret receive it.
- **`deploy/compose.yaml`**: image builds, private service DNS names, networks, volumes and non-secret application settings. The database/admin secret is passed to PostGIS and `gis-tools`; the API receives **only the read-only login**.
- **`deploy/nginx/nginx.conf`**: TLS termination, static file routing, `/api/*` and `/swagger*` reverse proxy. Do not put PostgreSQL or application secrets here.
- **`deploy/certs/` and the `tls-certs` volume**: keep the source certificate and private key on the Docker client host; run `./deploy/scripts/load-cert.sh` to stream them securely into the Docker-managed volume. The frontend mounts it read-only. Do not use a source-code bind mount for TLS files, because the Docker daemon may not see the devcontainer's `/workspace` path.
- **ASP runtime**: ASP.NET Core consumes `ConnectionStrings__Search` from its environment. The frontend uses its existing same-origin fallback (`Api:BaseUrl` unset). No runtime frontend configuration rewrite is needed.
- **`lab/` and `scripts/`**: Geofabrik download and PostGIS provisioning logic. The production containers do not run or modify these scripts on normal startup.

The one-off provisioning setup is deliberately separate from ordinary application updates. Docker does not touch database data when you rebuild the API or Nginx images.

## Operations

From `deploy/`:

```sh
docker compose ps
docker compose logs -f frontend api postgis
docker compose up -d --build       # Rebuild app images without reimporting data
docker compose down                # Stop containers; keep data
docker compose config --quiet      # Validate the merged deployment configuration
```

**Do not use `docker compose down -v`** unless you explicitly want to delete the PostGIS database, cached GIS datasets **and the imported TLS certificate/key**. These are stored in the named volumes `geosearch_postgis-data`, `geosearch_gis-data`, and `geosearch_tls-certs`, respectively. Back up the original files in Git-ignored `deploy/certs/` separately; the deployment `.env` file also remains outside Docker volumes.

The development container's `lab/data/` and the deployment's `gis-data` are intentionally independent, so a first deployment may download the snapshot again even if development data already exists. This avoids coupling production provisioning to a Docker-daemon-host path that might not match `/workspace` inside the development container. Docker owns the dataset volume; **do not `chown` the repository to match container UID 10001**.

If the non-root writeability check still reports an error, inspect `geosearch_gis-data` with `docker volume inspect geosearch_gis-data` and verify that the Docker daemon has sufficient disk space. The provisioning script already attempts to correct volume ownership; **do not change ownership of the repository checkout**. The preflight does not touch the database or import tables.

ASP is built in `Release` by default. To enable Swagger locally, set `ASPNETCORE_ENVIRONMENT=Development` in `deploy/.env` and recreate the API container with `docker compose up -d --force-recreate api`; Swagger UI is then at `https://localhost:8443/swagger` and JSON at `/swagger/v1/swagger.json`. `DOTNET_BUILD_CONFIGURATION=Debug` changes the ASP build configuration but **does not** enable Swagger by itself. Do not publish Development behavior to untrusted users.

`frontend` is the only published service. Nginx access logging is disabled because nearby queries can contain device coordinates. Missing `/_framework/` and `/_content/` assets return 404 rather than the SPA HTML. The standalone .NET 10 client relies on a publish-generated `<script type="importmap">` in `index.html` to resolve fingerprinted framework modules such as `dotnet.js`. The frontend Docker build verifies that the import map is present; do not remove the placeholder from the client `index.html`. GeoSearch requires map tiles and browser geolocation, so UFWeb's more restrictive CSP and permissions policy cannot be reused unchanged. HTTPS is required for geolocation on non-localhost devices.

### Scope

The Compose stack is meant for a **local, single-host deployment**, not an Internet-facing managed service. It does not include certificate renewal, database backups, monitoring, resource limits, or deployment automation through CI. The current application baseline still has authentication startup requirements; this deployment is designed for the parallel auth-removal branch and intentionally does not configure the old Identity database.
