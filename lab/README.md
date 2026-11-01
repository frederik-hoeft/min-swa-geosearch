# Instructor-provided GIS lab

This directory contains the Lab 2 specification, Docker Compose services, PostgreSQL SQL scripts, GDAL import configuration, and the instructor's `justfile`. The SQL and recipe semantics are retained; the GeoServer URL can also be provided through `GEOSERVER_URL` when the recipes run inside the devcontainer.

The **devcontainer is the preferred execution environment**. It installs `just`, `psql`, and `ogr2ogr` and starts PostGIS. From `/workspace/lab`, run `just` to list the available recipes and begin with `just s02-db-setup`. Do not run `s01-stack-up` inside the devcontainer, since its Compose stack already provides PostGIS.

For the datasets and expected paths, see [data/README.md](data/README.md). For the complete setup and database lifecycle, see [the development guide](../docs/development.md). The instructor's [lab specification](lab-2.md) is preserved as reference material; its standalone `s01-stack-up` instruction applies when running the lab without the devcontainer.
