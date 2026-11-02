# External OSM datasets

The lab uses the **October 28, 2026** Niedersachsen snapshot from [Geofabrik](https://download.geofabrik.de/europe/germany/niedersachsen.html). The instructor's archive contains links to the files, not the datasets themselves. Download and unpack the two inputs into these paths relative to `lab/`:

```text
data/
├── niedersachsen-261028-free/
│   └── niedersachsen.gpkg
└── niedersachsen-261028.osm.pbf
```

The GeoPackage provides features such as places and roads; the PBF provides address data. Check the file paths before starting the imports.

Run the corresponding `just` recipes from inside `/workspace/lab` in the devcontainer:

```bash
just s03-import-gpkg
just s04-import-addresses
```

The recipes use the Hannover bounding box by default. To select files stored elsewhere, override the recipe variables, for example `just pbf=/path/to/niedersachsen.osm.pbf s04-import-addresses`. To import the entire Niedersachsen dataset instead, use `just bbox='' s03-import-gpkg` (this takes substantially more space and time).

These large datasets are excluded from version control. Respect the source's OpenStreetMap/ODbL licensing and provide OpenStreetMap attribution in any published results.
