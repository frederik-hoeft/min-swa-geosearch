# Instructor-provided GIS lab

This directory contains the Lab 2 specification, Docker Compose services, PostgreSQL SQL scripts, GDAL import configuration, and the instructor's `justfile`. The instructor's search/index logic is retained, with transactional, repeatable setup and refresh recipes. The GeoServer URL can also be provided through `GEOSERVER_URL` when the recipes run inside the devcontainer.

The **devcontainer is the preferred execution environment**. It installs `just`, `psql`, and `ogr2ogr` and starts PostGIS. From `/workspace/lab`, run `just` to list the available recipes; use `just s00-download-data` to retrieve the pinned Geofabrik inputs, followed by `just s02-db-setup`. `s01-stack-up` refuses to run inside the devcontainer, where its Compose project already provides PostGIS.

For the datasets and expected paths, see [data/README.md](data/README.md). For the complete setup and database lifecycle, see [the development guide](../docs/development.md). The instructor's [lab specification](lab-2.md) is preserved as reference material; its standalone `s01-stack-up` instruction applies when running the lab without the devcontainer.

## Rebuild the disposable GIS database

To discard **only** the `lab1` database and replay the numbered lab setup and smoke-test recipes, run from `lab/`:

```bash
just reset-lab RESET-LAB1
```

This command is destructive: it disconnects users of `lab1`, drops and recreates that database, and reruns `s02`–`s10` in order (plus `s01-stack-up` outside the devcontainer). Compose volumes and downloaded source datasets are not deleted. The input GeoPackage/PBF files (download with `just s00-download-data`) and `GEOSEARCH_READER_PASSWORD` must be present **before** starting. The command checks those prerequisites, database ownership/privileges, and optional GeoServer availability before dropping anything. A failure *after* the drop may leave the database incomplete; rerun the recipe after correcting the problem.

`reset-lab` detects GeoServer and runs `s08-geoserver-publish` if it is reachable. Use `just reset-lab RESET-LAB1 yes` to **require** publication, or `just reset-lab RESET-LAB1 no` to skip it regardless of availability. The `s09-refresh-index` step runs even after rebuilding in `s05`, intentionally exercising the refresh operation. Recipe variable overrides (such as `bbox`, `gpkg`, `pbf`, and `snapshot`) are forwarded to each step.

This is a **development-only, destructive rebuild**, not an idempotent migration or a production reset procedure.
