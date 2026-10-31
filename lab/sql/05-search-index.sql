-- Lab 2, step 5: create search.search_index as a MATERIALIZED VIEW over the imported OSM snapshot.
-- Inputs: osm.gis_osm_{places,pois,pois_a,roads,adminareas_a}_free (GeoPackage)
--         osm.address_points, osm.address_multipolygons (PBF)
-- Variable: :snapshot (set by the justfile, e.g. 2026-10-28)
-- Later updates: REFRESH MATERIALIZED VIEW CONCURRENTLY search.search_index; (just s09-refresh-index)
\set ON_ERROR_STOP on
\timing on

-- Normalisation used for both the index and the query. Fully qualified, because PostgreSQL
-- runs CREATE/REFRESH MATERIALIZED VIEW with a restricted search_path.
CREATE OR REPLACE FUNCTION search.normalize(t text) RETURNS text
LANGUAGE sql STABLE AS $$
  SELECT regexp_replace(lower(public.unaccent('public.unaccent'::regdictionary, coalesce(t, ''))),
                        'str\.?(\s|$)', 'strasse\1', 'g')
$$;

-- Which snapshot the osm schema currently holds (read by the view on every refresh)
CREATE TABLE IF NOT EXISTS osm.snapshot (snapshot_id text NOT NULL, imported_at timestamptz NOT NULL DEFAULT now());
TRUNCATE osm.snapshot;
INSERT INTO osm.snapshot (snapshot_id) VALUES (:'snapshot');

-- Category mapping: which fclass values become searchable, with type and German label
DROP TABLE IF EXISTS search.category_map CASCADE;
CREATE TABLE search.category_map (layer text, fclass text, type text, label_de text, importance real,
                                  PRIMARY KEY (layer, fclass));
INSERT INTO search.category_map VALUES
  ('places', 'city', 'place', 'Stadt', 1.0),
  ('places', 'town', 'place', 'Stadt', 0.9),
  ('places', 'suburb', 'place', 'Stadtteil', 0.8),
  ('places', 'village', 'place', 'Dorf', 0.7),
  ('places', 'hamlet', 'place', 'Weiler', 0.5),
  ('places', 'locality', 'place', 'Ort', 0.4),
  ('places', 'island', 'place', 'Insel', 0.6),
  ('places', 'farm', 'place', 'Hof', 0.3),
  ('places', 'national_capital', 'place', 'Hauptstadt', 1.0),
  ('pois', 'town_hall', 'poi', 'Rathaus', 0.5),
  ('pois', 'pharmacy', 'poi', 'Apotheke', 0.4),
  ('pois', 'hospital', 'poi', 'Krankenhaus', 0.5),
  ('pois', 'doctors', 'poi', 'Arztpraxis', 0.3),
  ('pois', 'dentist', 'poi', 'Zahnarzt', 0.3),
  ('pois', 'school', 'poi', 'Schule', 0.4),
  ('pois', 'kindergarten', 'poi', 'Kindergarten', 0.3),
  ('pois', 'university', 'poi', 'Hochschule', 0.5),
  ('pois', 'college', 'poi', 'Hochschule', 0.4),
  ('pois', 'library', 'poi', 'Bibliothek', 0.4),
  ('pois', 'police', 'poi', 'Polizei', 0.4),
  ('pois', 'fire_station', 'poi', 'Feuerwehr', 0.4),
  ('pois', 'post_office', 'poi', 'Post', 0.3),
  ('pois', 'supermarket', 'poi', 'Supermarkt', 0.3),
  ('pois', 'bakery', 'poi', 'Bäckerei', 0.3),
  ('pois', 'restaurant', 'poi', 'Restaurant', 0.3),
  ('pois', 'cafe', 'poi', 'Café', 0.3),
  ('pois', 'pub', 'poi', 'Kneipe', 0.3),
  ('pois', 'hotel', 'poi', 'Hotel', 0.4),
  ('pois', 'museum', 'poi', 'Museum', 0.5),
  ('pois', 'theatre', 'poi', 'Theater', 0.5),
  ('pois', 'cinema', 'poi', 'Kino', 0.4),
  ('pois', 'park', 'poi', 'Park', 0.4),
  ('pois', 'zoo', 'poi', 'Zoo', 0.5),
  ('pois', 'attraction', 'poi', 'Sehenswürdigkeit', 0.5),
  ('pois', 'castle', 'poi', 'Schloss', 0.5),
  ('pois', 'swimming_pool', 'poi', 'Schwimmbad', 0.4),
  ('pois', 'sports_centre', 'poi', 'Sportzentrum', 0.3),
  ('pois', 'stadium', 'poi', 'Stadion', 0.5),
  ('pois', 'community_centre', 'poi', 'Gemeindezentrum', 0.3),
  ('pois', 'bank', 'poi', 'Bank', 0.3),
  ('pois', 'kiosk', 'poi', 'Kiosk', 0.2),
  ('roads', 'motorway', 'street', 'Autobahn', 0.7),
  ('roads', 'trunk', 'street', 'Bundesstraße', 0.6),
  ('roads', 'primary', 'street', 'Straße', 0.6),
  ('roads', 'secondary', 'street', 'Straße', 0.55),
  ('roads', 'tertiary', 'street', 'Straße', 0.5),
  ('roads', 'unclassified', 'street', 'Straße', 0.45),
  ('roads', 'residential', 'street', 'Straße', 0.45),
  ('roads', 'living_street', 'street', 'Straße', 0.45),
  ('roads', 'pedestrian', 'street', 'Fußgängerzone', 0.45),
  ('adminareas', 'admin_level6', 'admin', 'Landkreis', 0.9),
  ('adminareas', 'admin_level8', 'admin', 'Gemeinde', 0.95),
  ('adminareas', 'admin_level9', 'admin', 'Ortsteil', 0.8),
  ('adminareas', 'admin_level10', 'admin', 'Ortsteil', 0.75);

-- Replace an earlier table or view of the same name
DO $$
BEGIN
  IF to_regclass('search.search_index') IS NOT NULL THEN
    EXECUTE CASE (SELECT relkind FROM pg_class WHERE oid = 'search.search_index'::regclass)
              WHEN 'm' THEN 'DROP MATERIALIZED VIEW search.search_index'
              ELSE 'DROP TABLE search.search_index' END;
  END IF;
END $$;

-- The search document: one stored query over the raw layers
CREATE MATERIALIZED VIEW search.search_index AS
WITH
-- Places, POIs (points and areas) and administrative areas.
-- osm_id is not unique per layer (one OSM object can yield several features, Geofabrik §2.5),
-- so the id also carries fclass.
features AS (
  SELECT 'places:' || p.osm_id || ':' || p.fclass AS id, 'places' AS source_layer, p.osm_id,
         m.type, p.fclass AS category, m.label_de, p.name, NULL::text AS ref,
         NULL::text AS municipality, NULL::text AS district, m.importance,
         p.geom AS display_pt, p.geom::geometry(Geometry, 4326) AS geom
  FROM osm.gis_osm_places_free p
  JOIN search.category_map m ON m.layer = 'places' AND m.fclass = p.fclass
  WHERE p.name <> ''
  UNION ALL
  SELECT 'pois:' || p.osm_id || ':' || p.fclass, 'pois', p.osm_id, m.type, p.fclass, m.label_de,
         p.name, NULL, NULL, NULL, m.importance, p.geom, p.geom
  FROM osm.gis_osm_pois_free p
  JOIN search.category_map m ON m.layer = 'pois' AND m.fclass = p.fclass
  WHERE p.name <> ''
  UNION ALL
  SELECT 'pois_a:' || a.osm_id || ':' || a.fclass, 'pois_a', a.osm_id, m.type, a.fclass, m.label_de,
         a.name, NULL, NULL, NULL, m.importance, ST_PointOnSurface(a.geom), a.geom
  FROM osm.gis_osm_pois_a_free a
  JOIN search.category_map m ON m.layer = 'pois' AND m.fclass = a.fclass
  WHERE a.name <> ''
  UNION ALL
  SELECT 'admin:' || g.osm_id || ':' || g.fclass, 'adminareas_a', g.osm_id, m.type, g.fclass, m.label_de,
         g.name, NULL, NULL, NULL, m.importance, ST_PointOnSurface(g.geom), g.geom
  FROM osm.gis_osm_adminareas_a_free g
  JOIN search.category_map m ON m.layer = 'adminareas' AND m.fclass = g.fclass
  WHERE g.name <> ''
),
-- Streets: merge named segments per name and municipality
streets AS (
  SELECT 'street:' || md5(s.name || ':' || s.municipality_id) AS id, 'roads' AS source_layer,
         NULL::varchar AS osm_id, 'street' AS type, s.fclass AS category, s.label_de, s.name,
         NULL::text AS ref, s.municipality, NULL::text AS district, s.importance,
         ST_ClosestPoint(s.geom, ST_Centroid(s.geom)) AS display_pt, s.geom
  FROM (
    SELECT r.name, g.osm_id AS municipality_id, g.name AS municipality,
           (array_agg(r.fclass ORDER BY m.importance DESC))[1] AS fclass,
           (array_agg(m.label_de ORDER BY m.importance DESC))[1] AS label_de,
           max(m.importance) AS importance,
           ST_LineMerge(ST_Collect(r.geom)) AS geom
    FROM osm.gis_osm_roads_free r
    JOIN search.category_map m ON m.layer = 'roads' AND m.fclass = r.fclass
    JOIN osm.gis_osm_adminareas_a_free g
      ON g.fclass = 'admin_level8' AND ST_Intersects(g.geom, r.geom)
    WHERE r.name <> ''
    GROUP BY r.name, g.osm_id, g.name
  ) s
),
-- Addresses from the PBF: buildings (ways w, relations r) and nodes (n)
addresses_raw AS (
  SELECT 'addr:' || COALESCE('w' || a.osm_way_id, 'r' || a.osm_id) AS id,
         'address_multipolygons' AS source_layer, COALESCE(a.osm_way_id, a.osm_id) AS osm_id,
         a.addr_street || ' ' || a.addr_housenumber AS name, a.addr_postcode AS ref,
         a.addr_city AS municipality, a.addr_suburb AS district, true AS is_building,
         ST_PointOnSurface(a.geom) AS display_pt, a.geom::geometry(Geometry, 4326) AS geom
  FROM osm.address_multipolygons a
  WHERE a.addr_street IS NOT NULL
  UNION ALL
  SELECT 'addr:n' || p.osm_id, 'address_points', p.osm_id,
         p.addr_street || ' ' || p.addr_housenumber, p.addr_postcode,
         p.addr_city, p.addr_suburb, false, p.geom, p.geom
  FROM osm.address_points p
  WHERE p.addr_street IS NOT NULL
),
-- One address, one document: same street, number and postcode within about 50 m
-- (eps 0.0006 degrees: 67 m north-south, 41 m east-west at 52° N) form one cluster;
-- the building wins over nodes.
addresses AS (
  SELECT DISTINCT ON (c.name, c.ref, c.cluster)
         c.id, c.source_layer, c.osm_id, 'address' AS type, 'address' AS category,
         'Adresse' AS label_de, c.name, c.ref, c.municipality, c.district, 0.35::real AS importance,
         c.display_pt, c.geom
  FROM (SELECT r.*, ST_ClusterDBSCAN(r.display_pt, eps := 0.0006, minpoints := 1)
                      OVER (PARTITION BY r.name, r.ref) AS cluster
        FROM addresses_raw r) c
  ORDER BY c.name, c.ref, c.cluster, c.is_building DESC, c.id
),
docs AS (
  SELECT DISTINCT ON (id) * FROM (
    SELECT * FROM features UNION ALL SELECT * FROM streets UNION ALL SELECT * FROM addresses
  ) u
  ORDER BY id
)
-- Place context (Gemeinde, Ortsteil) by point-in-polygon, free-text representation, version
SELECT d.id, d.source_layer, d.osm_id, d.type, d.category, d.label_de, d.name, d.ref,
       COALESCE(d.municipality, gem.name) AS municipality,
       COALESCE(d.district, ot.name) AS district,
       search.normalize(concat_ws(' ', d.name, d.ref,
                                  CASE WHEN d.type IN ('poi', 'place', 'admin') THEN d.label_de END,
                                  COALESCE(d.district, ot.name), COALESCE(d.municipality, gem.name)))
         AS search_text,
       d.importance::real AS importance,
       snap.snapshot_id,
       now() AS refreshed_at,
       d.display_pt::geometry(Point, 4326) AS display_pt,
       d.geom
FROM docs d
CROSS JOIN osm.snapshot snap
LEFT JOIN LATERAL (
  SELECT g.name FROM osm.gis_osm_adminareas_a_free g
  WHERE g.fclass = 'admin_level8' AND ST_Within(d.display_pt, g.geom) LIMIT 1
) gem ON true
LEFT JOIN LATERAL (
  SELECT g.name FROM osm.gis_osm_adminareas_a_free g
  WHERE d.type <> 'admin' AND g.fclass IN ('admin_level9', 'admin_level10')
    AND ST_Within(d.display_pt, g.geom)
  ORDER BY ST_Area(g.geom) LIMIT 1   -- finest unit: Stadtteil before Stadtbezirk
) ot ON true;

-- Indexes on the materialized view. The unique index on id makes REFRESH ... CONCURRENTLY possible.
CREATE UNIQUE INDEX search_index_id ON search.search_index (id);
CREATE INDEX search_index_pt ON search.search_index USING gist (display_pt);
CREATE INDEX search_index_fts ON search.search_index USING gin (to_tsvector('simple', search_text));
CREATE INDEX search_index_trgm ON search.search_index USING gin (search_text gin_trgm_ops);
ANALYZE search.search_index;

SELECT type, count(*) FROM search.search_index GROUP BY type ORDER BY count(*) DESC;
