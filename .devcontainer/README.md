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
