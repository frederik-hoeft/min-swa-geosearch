-- Lab 2, step 6: free-text search and reverse geocoding over search.search_index.
\set ON_ERROR_STOP on

-- Readable label: name (+ postcode), Ortsteil, Gemeinde — without repeating equal parts
CREATE OR REPLACE FUNCTION search.label(name text, ref text, district text, municipality text)
RETURNS text LANGUAGE sql IMMUTABLE AS $$
  SELECT concat_ws(', ', name || coalesce(' ' || ref, ''),
                   nullif(nullif(district, name), municipality),
                   nullif(municipality, name))
$$;

-- Free text only. Step 1: full-text match (all words present), ranked by trigram similarity
-- and importance. Step 2, only if step 1 finds nothing: fuzzy trigram match for typos.
CREATE OR REPLACE FUNCTION search.search(q text, lim int DEFAULT 10)
RETURNS TABLE (id text, name text, label text, type text, category text, score real,
               lon double precision, lat double precision, snapshot_id text)
LANGUAGE sql STABLE AS $$
  WITH n AS (
    SELECT search.normalize(q) AS t, plainto_tsquery('simple', search.normalize(q)) AS tsq
  ), fulltext AS (
    SELECT s.*, ((1 + similarity(s.search_text, n.t)) * s.importance)::real AS score
    FROM search.search_index s, n
    WHERE to_tsvector('simple', s.search_text) @@ n.tsq
    ORDER BY score DESC
    LIMIT lim
  ), fuzzy AS (
    SELECT s.*, (similarity(s.search_text, n.t) * s.importance)::real AS score
    FROM search.search_index s, n
    WHERE NOT EXISTS (SELECT 1 FROM fulltext) AND s.search_text % n.t
    ORDER BY score DESC
    LIMIT lim
  )
  SELECT r.id, r.name, search.label(r.name, r.ref, r.district, r.municipality),
         r.type, r.label_de, r.score, ST_X(r.display_pt), ST_Y(r.display_pt), r.snapshot_id
  FROM (SELECT * FROM fulltext UNION ALL SELECT * FROM fuzzy) r
  ORDER BY r.score DESC
$$;

-- Nearest addresses, POIs and streets within a radius in metres, plus the place context.
-- Step 1: the GiST index returns the nearest candidates by <-> (in degrees).
-- Step 2: exact distances in metres on the geography type filter and order them.
CREATE OR REPLACE FUNCTION search.reverse(lon double precision, lat double precision,
                                          radius double precision DEFAULT 250, lim int DEFAULT 5)
RETURNS TABLE (id text, name text, label text, type text, distance_m double precision)
LANGUAGE sql STABLE AS $$
  WITH p AS (SELECT ST_SetSRID(ST_MakePoint(lon, lat), 4326) AS pt),
  candidates AS (
    SELECT s.*, ST_Distance(s.display_pt::geography, p.pt::geography) AS distance_m
    FROM search.search_index s, p
    WHERE s.type IN ('address', 'poi', 'street')
    ORDER BY s.display_pt <-> p.pt
    LIMIT 50
  )
  SELECT c.id, c.name, search.label(c.name, c.ref, c.district, c.municipality), c.type, c.distance_m
  FROM candidates c
  WHERE c.distance_m <= radius
  ORDER BY c.distance_m
  LIMIT lim
$$;
