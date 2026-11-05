---
marp: true
theme: hsh-lecture
size: 16:9
paginate: true
lang: en
title: Geospatial Systems — Lab 2
footer: "M.Sc. R. Bischof | Hochschule Hannover | MIN-SWA"
---

<!-- _class: cover -->
<!-- _footer: "" -->
# Lab 2: A search and nearby web app on OSM data

Lab 2 · GIS · Software Architectures 2026

**Task:** implement an HTTP search API over `search.search_index` and a web application that shows a map, searches addresses and places, and finds what is nearby the user's device.

<!--
Presenter notes
This lab turns the Lecture 2 pipeline into an application. Students build on the database prepared by the justfile recipes s01–s05; they implement the HTTP layer and the browser client. The backend language is free.
-->

---

# You build two parts on top of the prepared database

| Part | Responsibility |
| --- | --- |
| **Search API** (your backend) | `/api/search`, `/api/reverse`, `/api/nearby`, `/api/categories` |
| **Web app** (served by the same backend at `/`) | map, search field, device position, nearby list |
| **PostGIS** (given) | `search.search_index`, `search.search()`, `search.reverse()` |

The browser talks **only** to your API — never to the database or to a third-party geocoder.

<!--
Presenter notes
This is the "thin API over a SQL function" option from Lecture 2. The architecture rule matters for grading: no Nominatim, Photon, Google or Mapbox geocoding in the client, and no database credentials in the frontend.
-->

---

<!-- _class: code -->
# Start from the prepared database

```bash
just s01-stack-up
just s02-db-setup
just s03-import-gpkg                                     # Region Hannover (default)
just s04-import-addresses
just s05-search-index                                    # ≈ 130,000 documents
just s06-search "Limmerstr 12 Hannover"                  # smoke test
just s07-reverse 9.7320 52.3759
```

Connection: `localhost:5432`, database and user `lab1` (see `compose.yaml`).

<!--
Presenter notes
Run the recipes in this folder: it holds the justfile, compose.yaml, sql/ and osmconf-addresses.ini; `just` lists all recipes. Put the two Geofabrik downloads into data/: niedersachsen-261101-free/niedersachsen.gpkg and niedersachsen-261101.osm.pbf. The imports default to the Hannover extent and finish in under a minute; the full Lower Saxony import is optional (just bbox="" s03-import-gpkg). The index contains document types address, poi, street, place and admin. Data: Geofabrik GeoPackage and PBF of 2026-10-28, © OpenStreetMap contributors, ODbL.
-->

---

<!-- _class: code -->
# Endpoint 1: `GET /api/search` takes free text only

```http
GET /api/search?q=Limmerstr%2012%20Hannover&limit=5
```

| Parameter | Rule |
| --- | --- |
| `q` | required, 1–200 characters, free text |
| `limit` | optional, 1–20, default 10 |

Use `search.search(q, limit)`. No structured fields: street, house number and town are one string.

<!--
Presenter notes
The SQL function already combines full-text and fuzzy matching and ranks by score × importance. Students may improve ranking, but the endpoint contract stays the same. Missing or empty q returns 400.
-->

---

<!-- _class: code -->
# Endpoints 2–4: reverse, nearby and categories

```http
GET /api/reverse?lat=52.3759&lon=9.7320&radius=250
GET /api/nearby?lat=52.3759&lon=9.7320&radius=500&category=Apotheke&limit=20
GET /api/categories
```

| Endpoint | Returns |
| --- | --- |
| `reverse` | nearest address, POI or street (`search.reverse`), radius ≤ 1000 m |
| `nearby` | POIs within `radius` (≤ 2000 m), optional `category`, sorted by distance |
| `categories` | the POI categories available for `nearby` |

<!--
Presenter notes
reverse uses the given function. nearby and categories are the students' own SQL. Validate lat in [-90, 90], lon in [-180, 180] and the radius limits; reject invalid input with 400.
-->

---

<!-- _class: code -->
# All endpoints answer with the same GeoJSON shape

```json
{ "type": "FeatureCollection",
  "snapshot": "2026-10-28",
  "attribution": "© OpenStreetMap contributors, ODbL",
  "features": [{ "type": "Feature",
    "geometry": { "type": "Point", "coordinates": [9.71497, 52.36677] },
    "properties": { "id": "pois:…", "name": "Markt-Apotheke Linden",
      "label": "Markt-Apotheke Linden, Linden-Mitte, Hannover",
      "type": "poi", "category": "Apotheke",
      "score": 0.18, "distance_m": null } }] }
```

Errors: `400` with `{"error": "…"}`; `distance_m` is set for reverse and nearby.

<!--
Presenter notes
Coordinates are longitude, latitude (RFC 7946). The example values come from a real query on the Hannover extent. categories may return a plain JSON array instead of GeoJSON.
Sources: https://www.rfc-editor.org/rfc/rfc7946 ; https://www.openstreetmap.org/copyright .
-->

---

<!-- _class: code -->
# Hint: nearby is a radius filter plus a distance sort

```sql
SELECT s.id, s.name, s.label_de AS category,
       ST_Distance(s.display_pt::geography, p.pt::geography) AS distance_m
FROM search.search_index s,
     (SELECT ST_SetSRID(ST_MakePoint(:lon, :lat), 4326) AS pt) p
WHERE s.type = 'poi'
  AND (:category IS NULL OR s.label_de = :category)
  AND ST_DWithin(s.display_pt::geography, p.pt::geography, :radius)
ORDER BY distance_m
LIMIT :limit;
```

Bind the parameters — never concatenate user input into SQL.

<!--
Presenter notes
Test result at Hannover Kröpcke (9.7320, 52.3759), category Apotheke, 1000 m: Georg Apotheke 192 m, Europa Apotheke 330 m, Altstadt-Apotheke 361 m. Sorting by the geography distance avoids the small order errors of <-> in degrees. For large radii, add an index on the geography expression.
Sources: https://postgis.net/docs/ST_DWithin.html ; https://postgis.net/docs/geometry_distance_knn.html .
-->

---

# The web app: map and search field

1. **Map** with an OSM-based basemap and visible attribution (e.g. MapLibre GL JS, Leaflet or OpenLayers)
2. **Search field:** request `/api/search` after ≥ 3 characters, debounced (≥ 300 ms)
3. **Result list** with label and type; selecting a result shows a marker and moves the map there
4. Clear feedback for “no results” and API errors

<!--
Presenter notes
Debouncing keeps the API load low and makes the free-text search feel like autocomplete. Any basemap is fine as long as its terms allow use and the attribution is visible.
Sources: https://maplibre.org/maplibre-gl-js/docs/ ; https://leafletjs.com/ ; https://openlayers.org/ ; https://www.openstreetmap.org/copyright .
-->

---

<!-- _class: code -->
# The Geolocation API asks the user for the device position

```js
navigator.geolocation.getCurrentPosition(
  (pos) => showPosition(pos.coords.longitude, pos.coords.latitude, pos.coords.accuracy),
  (err) => showError(err.code),   // 1 denied, 2 unavailable, 3 timeout
  { enableHighAccuracy: true, timeout: 10000, maximumAge: 60000 }
);
```

- Works only in a **secure context**: `https://` or `http://localhost`
- Ask only after the user clicks **“Locate me”**
- Show the position with its accuracy circle, then call `/api/reverse`

<!--
Presenter notes
Browsers show a permission prompt; a denial is a normal case, not an error the app may ignore. Without permission, nearby falls back to the map centre. Testing on a phone requires HTTPS, for example through a reverse proxy with a local certificate.
Sources: https://developer.mozilla.org/en-US/docs/Web/API/Geolocation_API ; https://developer.mozilla.org/en-US/docs/Web/API/Geolocation/getCurrentPosition ; https://www.w3.org/TR/geolocation/ .
-->

---

# The web app: “nearby”

1. Category select filled from `/api/categories`, radius select (250 / 500 / 1000 / 2000 m)
2. Request `/api/nearby` with the **device position** — or the map centre if permission is denied
3. Show results as markers and as a list sorted by distance (“Georg Apotheke · 192 m”)
4. Selecting a list entry highlights its marker

<!--
Presenter notes
The nearby function is the reason the app needs the device position. The fallback to the map centre must be visible to the user, so that they know which position the list refers to.
-->

---

# Quality requirements are part of the task

| Quality | Requirement |
| --- | --- |
| **Privacy** | position is sent only on user action; the backend does not log or store coordinates |
| **Security** | parameterised SQL; input validation; database credentials only in the backend |
| **Correctness** | GeoJSON in longitude/latitude; distances in metres |
| **Licence** | OSM attribution in the map and in every API response |
| **Performance** | search answers in < 300 ms on the Hannover extent |

<!--
Presenter notes
A device position is personal data. Logging query strings that contain lat and lon would store it, so request logging must leave them out. Measure the latency of the ten fixture queries and report the median.
Sources: https://www.openstreetmap.org/copyright ; https://osmfoundation.org/wiki/Licence/Community_Guidelines/Geocoding_-_Guideline .
-->

---

# Test your search with ten fixture queries

| Query | Expected top result |
| --- | --- |
| `Limmerstraße 12` · `Limmerstr 12 Hannover` | address Limmerstraße 12, Linden-Nord |
| `Rathuas Linden` (typo) | Lindener Rathaus |
| `Linden-Mitte` · `Garbsen` | place / municipality |
| `Herrenhäuser Gärten` · `Lister Meile` | POI / street |
| `Hauptstrasse Laatzen` · `Zoo Hannover` · `Markthalle Hannover` | **currently wrong** — explain why |

<!--
Presenter notes
Results measured on 2026-10-29 with the given functions. "Hauptstrasse Laatzen" returns the admin area Oesselse, "Zoo Hannover" the district Zoo and "Markthalle Hannover" a street. Students document the observed result for all ten and may improve the ranking (for example a bonus for exact name matches); improvements are optional.
-->

---

# Deliverables

1. **Repository** with backend and web app; `README.md` with start commands (a `just` recipe or a Compose service)
2. **OpenAPI description** of the four endpoints
3. **Test evidence:** the ten fixture queries with observed top results and latency; one screenshot of nearby with device position
4. **Architecture note** (½ page): where the search logic lives, why, and one quality attribute you measured
5. **Demo** of 5 minutes in the lab session

<!--
Presenter notes
Submission date and format follow the Moodle announcement. The architecture note feeds into the ADR in Lecture 3.
-->

---

# Acceptance checklist

- ☐ All four endpoints follow the contract, including `400` errors
- ☐ The browser uses only your API; no external geocoder, no database credentials in the client
- ☐ Search field, result list and map marker work
- ☐ “Locate me” handles granted, denied, unavailable and timeout
- ☐ Nearby works with device position and with the map-centre fallback
- ☐ Attribution visible; coordinates not logged
- ☐ README lets a fellow student start the app in under 10 minutes

<!--
Presenter notes
Use this checklist in the demo. Optional extensions: better ranking, pagination, keyboard navigation in the result list, a radius circle on the map.
-->

---

<!-- _class: references -->
# References

- Lecture 2 deck: `presentations/02-geospatial-search/lecture-2.md`; `justfile` in this folder, recipes `s01`–`s09`.
- IETF. [RFC 7946 — The GeoJSON Format](https://www.rfc-editor.org/rfc/rfc7946).
- MDN. [Geolocation API](https://developer.mozilla.org/en-US/docs/Web/API/Geolocation_API); W3C. [Geolocation](https://www.w3.org/TR/geolocation/).
- PostGIS. [ST_DWithin](https://postgis.net/docs/ST_DWithin.html), [<-> (KNN)](https://postgis.net/docs/geometry_distance_knn.html).
- [MapLibre GL JS](https://maplibre.org/maplibre-gl-js/docs/), [Leaflet](https://leafletjs.com/), [OpenLayers](https://openlayers.org/).
- OpenAPI Initiative. [OpenAPI Specification](https://spec.openapis.org/oas/latest.html).
- OpenStreetMap. [Copyright and licence](https://www.openstreetmap.org/copyright); OSMF. [Geocoding — Guideline](https://osmfoundation.org/wiki/Licence/Community_Guidelines/Geocoding_-_Guideline).
