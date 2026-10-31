-- Lab 2, step 2: prepare the database (slide "Live: Using psql to prepare the database").
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS pg_trgm;   -- fuzzy matching (search step)
CREATE EXTENSION IF NOT EXISTS unaccent;  -- normalisation (search step)
CREATE SCHEMA IF NOT EXISTS osm;          -- imported OSM snapshot
CREATE SCHEMA IF NOT EXISTS search;       -- derived search products
SELECT postgis_full_version();
