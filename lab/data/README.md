# External OSM datasets

Lab 2 uses the **October 28, 2026** Niedersachsen snapshot from [Geofabrik](https://download.geofabrik.de/europe/germany/niedersachsen.html), matching the search index's default `snapshot` value. From `lab/`, run:

```bash
just s00-download-data
```

This retrieves the dated `niedersachsen-261101.osm.pbf` and `niedersachsen-261101-free.gpkg.zip` files, extracts the single GeoPackage member, and installs them at the locations expected by the import recipes:

```text
data/
├── niedersachsen-261101-free/
│   └── niedersachsen.gpkg
└── niedersachsen-261101.osm.pbf
```

The PBF supplies address tags; the GeoPackage supplies places, POIs, roads, and administrative boundaries. The downloads total approximately **1.3 GB**; the GeoPackage additionally requires space for extraction. The input files and temporary downloads are excluded from Git. CI does not download either dataset.

The downloader is repeatable: it verifies the existing PBF against the published Geofabrik `.md5` file and recognizes an existing GeoPackage, so a successful rerun does not download the large files again. Interrupted transfers can resume, downloaded PBFs are checked before replacement, and GeoPackage extraction checks the ZIP CRC and SQLite GeoPackage metadata. The ZIP archive is removed after successful extraction to save disk space. An invalid ZIP is discarded so the next run can retry. The cached checksum in `data/.downloads/` allows subsequent runs to work without network access.

Next run `just s02-db-setup`, `just s03-import-gpkg`, and `just s04-import-addresses` (or use [the full reset recipe](../README.md#rebuild-the-disposable-gis-database) after provisioning the reader password). The import recipes default to the Hannover bounding box. To supply pre-existing datasets instead, override their paths, e.g. `just pbf=/path/to/input.osm.pbf s04-import-addresses`. To import all of Niedersachsen rather than the default regional subset, use `just bbox='' s03-import-gpkg`.

The Geofabrik archives are date-pinned for reproducibility. If that snapshot is no longer hosted, provide a matching local dataset and update the recipe paths/snapshot metadata deliberately, rather than silently substituting `latest`. Respect OpenStreetMap's ODbL licensing and include attribution in published results.
