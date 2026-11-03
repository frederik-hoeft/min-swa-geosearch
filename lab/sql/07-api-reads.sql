-- API-specific read functions. The instructor-owned search.search and search.reverse
-- remain unchanged; these adapters expose one common, keyless GeoJSON projection.
-- Called after 06-search-functions.sql, including after a full index rebuild.
\set ON_ERROR_STOP on

CREATE OR REPLACE FUNCTION search.api_search(query text, result_limit integer)
RETURNS TABLE (id text, name text, label text, type text, category text,
               score real, lon double precision, lat double precision,
               snapshot_id text, distance_m double precision)
LANGUAGE sql STABLE AS $$
    SELECT r.id, r.name, r.label, r.type, r.category, r.score,
           r.lon, r.lat, r.snapshot_id, NULL::double precision
    FROM search.search(query, result_limit) AS r
$$;

CREATE OR REPLACE FUNCTION search.api_reverse(longitude double precision, latitude double precision,
                                               search_radius double precision)
RETURNS TABLE (id text, name text, label text, type text, category text,
               score real, lon double precision, lat double precision,
               snapshot_id text, distance_m double precision)
LANGUAGE sql STABLE AS $$
    SELECT r.id, r.name, r.label, r.type, s.label_de,
           NULL::real, ST_X(s.display_pt), ST_Y(s.display_pt),
           s.snapshot_id, r.distance_m
    FROM search.reverse(longitude, latitude, search_radius, 1) AS r
    JOIN search.search_index AS s ON s.id = r.id
    ORDER BY r.distance_m, r.id
$$;

-- A DB function keeps geography-based filtering and sorting in PostGIS, rather
-- than approximating distances with degrees or client-side LINQ evaluation.
CREATE OR REPLACE FUNCTION search.api_nearby(longitude double precision, latitude double precision,
                                              search_radius double precision, selected_category text,
                                              result_limit integer)
RETURNS TABLE (id text, name text, label text, type text, category text,
               score real, lon double precision, lat double precision,
               snapshot_id text, distance_m double precision)
LANGUAGE sql STABLE AS $$
    SELECT s.id, s.name,
           search.label(s.name, s.ref, s.district, s.municipality),
           s.type, s.label_de, NULL::real,
           ST_X(s.display_pt), ST_Y(s.display_pt), s.snapshot_id,
           ST_Distance(s.display_pt::geography, p.pt::geography) AS distance_m
    FROM search.search_index AS s
    CROSS JOIN (SELECT ST_SetSRID(ST_MakePoint(longitude, latitude), 4326) AS pt) AS p
    WHERE s.type = 'poi'
      AND (selected_category IS NULL OR s.label_de = selected_category)
      AND ST_DWithin(s.display_pt::geography, p.pt::geography, search_radius)
    ORDER BY distance_m, s.id
    LIMIT result_limit
$$;
