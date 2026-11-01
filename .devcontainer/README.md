# Development container

The development environment uses a Debian Trixie .NET 10 SDK image with GDAL (`ogr2ogr`),
PostgreSQL CLI utilities, the Docker CLI, Git and the developer's customary Oh My Zsh configuration.
It uses the lab Compose definition to start PostGIS on the same internal Compose network as the
workspace container. `PGHOST=postgis` resolves the database service without exposing credentials
in source code. GeoServer remains an optional service; start it with `./scripts/lab.sh geoserver-up`.

**Preferred workflow:** open this repository in VS Code and choose **Dev Containers: Reopen in Container**. Run the lab scripts, GIS imports, PostgreSQL commands, .NET CLI, and tests from the container terminal. Visual Studio on Windows can optionally run and debug the web applications against the same Docker-hosted database.
The first creation downloads base images, extensions and shell customizations, so it requires
network access. Subsequent `dotnet restore`, `dotnet build` and `dotnet test` use the ordinary SDK.

The container uses a persistent NuGet volume and binds the repository at `/workspace`.
Docker-outside-of-Docker shares the host's Docker daemon and should only be enabled on trusted
machines. Do not mount or expose the database or Docker sockets to production containers.

The lab database and GeoServer run as **separate services on the Compose network**, not in the
.NET container. Inside the devcontainer, use `postgis` (not `localhost`) for the database host,
and `http://geoserver:8080/geoserver` for optional GIS publishing. The server's local configuration
can use `ConnectionStrings__Identity` and `ConnectionStrings__Search` with `Host=postgis`.
The Compose lab file also publishes database and GeoServer ports on host loopback for host-side
clients, but these are not ports of the devcontainer itself. The lab's provided credentials
are development-only and should not be reused in a production deployment.

The devcontainer shares the Docker daemon through a host socket. Do not use it to work with
untrusted repositories. The Git ownership exception is limited to the `/workspace` project mount; other repositories are not globally trusted. The initial build requires the container service and the PostGIS image;
rebuilding the devcontainer without network access requires pre-cached images/features.

## Application ports and host debugging

The devcontainer's Compose service uses Docker to publish application ports on distinct
host-loopback ports. Visual Studio continues to use the ordinary project launch-profile ports.

| Application | Inside devcontainer | Docker-published on host | Visual Studio on host |
|---|---|---|---|
| ASP.NET Core API | `5100` | `15100` | `5100` |
| Blazor WebAssembly dev server | `5200` | `15200` | `5200` |

VS Code's automatic port forwarding is disabled for the devcontainer, so it does not
compete for the host application's `5100` and `5200` ports. PostgreSQL is still published
by `lab/compose.yaml` on host port `5432`; no additional database tunnel is required.

The Docker host ports can be overridden before creating/recreating the Compose services:
`GEOSEARCH_DEV_API_PORT` and `GEOSEARCH_DEV_CLIENT_PORT`. For example, set
`GEOSEARCH_DEV_API_PORT=25100` if `15100` is reserved on Windows. This changes only
host publication, not the ports that ASP.NET Core listens on inside the container.

The ports above are Docker TCP mappings, not TLS termination; they can also carry HTTPS.
Running the web apps **inside the container** requires a separate HTTPS development
certificate available to the container process **and trusted by the Windows browser**.
Do not use the earlier HTTP `--urls` examples for authentication: `SecurePolicy=Always`
will reject the antiforgery exchange. If container-hosted web development is needed,
provision a trusted development certificate for the container and bind the apps to
`https://0.0.0.0:5100` and `https://0.0.0.0:5200`. The browser-visible URLs will then
be `https://localhost:15100` and `https://localhost:15200` (or the configured host ports).
Set the container-hosted client's `Api:BaseUrl` to the former and the API's
`Cors:ClientOrigin` to the latter. The Compose environment already sets the HTTPS
client origin. Do not commit the certificate or its password.

For the optional **Visual Studio on Windows + devcontainer for GIS tooling** workflow,
no container certificate is needed: Visual Studio uses the trusted Windows developer
certificate for `https://localhost:5100` and `https://localhost:5200`, and PostgreSQL
remains published by Docker at `localhost:5432`.

.NET projects can also build and run inside the devcontainer once a trusted development certificate is configured. When the devcontainer is used only for GIS tooling and scripts, **do not start either
.NET process inside it**. The host's Visual Studio projects can run on 5100/5200
without changing their launch profiles, while the running PostGIS service remains
accessible through `localhost:5432`.

The client uses its hosting origin by default (`wwwroot/appsettings.json`); `wwwroot/appsettings.Development.json` sets the Visual Studio API origin to `https://localhost:5100/`. For a container-hosted browser client, override that Development value to the Docker-published API port (`https://localhost:15100/`) before launching it. The API and client must use the same URL scheme for secure, SameSite cookies.
