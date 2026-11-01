# Development container

The development environment uses a Debian Trixie .NET 10 SDK image with GDAL (`ogr2ogr`),
PostgreSQL CLI utilities, the Docker CLI, Git and the developer's customary Oh My Zsh configuration.
It uses the lab Compose definition to start PostGIS on the same internal Compose network as the
workspace container. `PGHOST=postgis` resolves the database service without exposing credentials
in source code. GeoServer remains an optional service; start it with `./scripts/lab.sh geoserver-up`.

Open this repository in a Dev Containers-compatible editor and choose **Reopen in Container**.
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
untrusted repositories. The initial build requires the container service and the PostGIS image;
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

When starting an ASP.NET Core process **inside** the container, bind to all container
interfaces rather than the loopback-only URLs in the existing launch profiles:

```bash
dotnet run --project src/GeoSearch.Web.Server --no-launch-profile --urls http://0.0.0.0:5100
# In a second container terminal:
dotnet run --project src/GeoSearch.Web.Client --no-launch-profile --urls http://0.0.0.0:5200
```

The container sets `ASPNETCORE_ENVIRONMENT=Development` and its browser CORS origin
(`http://localhost:15200`, or the configured client host port). The backend still
needs container-local connection strings (`Host=postgis`). In particular,
when testing the container-hosted browser client, the static client configuration
(`src/GeoSearch.Web.Client/wwwroot/appsettings.json`) must point at the Docker-published
API URL (`http://localhost:15100/`) rather than the host Visual Studio API URL
(`http://localhost:5100/`). Its browser origin is `http://localhost:15200`.
The checked-in settings remain aimed at Visual Studio on the host; adjust the client
configuration only when deliberately running a separate container-hosted frontend.

When the devcontainer is used only for GIS tooling and scripts, **do not start either
.NET process inside it**. The host's Visual Studio projects can run on 5100/5200
without changing their launch profiles, while the running PostGIS service remains
accessible through `localhost:5432`.
