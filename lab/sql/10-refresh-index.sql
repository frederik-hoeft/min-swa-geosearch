-- Refresh the derived read model and its source-snapshot identity as one transaction.
-- Supply -v snapshot=... from the justfile; use the date of the *imported* source files.
\set ON_ERROR_STOP on
\timing on

UPDATE osm.snapshot SET snapshot_id = :'snapshot';
REFRESH MATERIALIZED VIEW CONCURRENTLY search.search_index;
SELECT snapshot_id, refreshed_at, count(*) AS documents
FROM search.search_index
GROUP BY snapshot_id, refreshed_at;
