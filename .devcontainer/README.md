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
