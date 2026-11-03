-- Idempotent least-privilege search account. Run with administrative credentials
-- after s05-search-index. The password enters psql from its environment, never argv.
\set ON_ERROR_STOP on
\getenv reader_password GEOSEARCH_READER_PASSWORD

DO $$
BEGIN
    IF to_regprocedure('search.api_search(text,integer)') IS NULL
       OR to_regprocedure('search.api_reverse(double precision,double precision,double precision)') IS NULL
       OR to_regprocedure('search.api_nearby(double precision,double precision,double precision,text,integer)') IS NULL
    THEN
        RAISE EXCEPTION 'Search API functions are missing. Run just s05-search-index first.';
    END IF;
END
$$;

SELECT format('CREATE ROLE %I LOGIN', 'geosearch_reader')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'geosearch_reader') \gexec
SELECT format('ALTER ROLE %I LOGIN PASSWORD %L', 'geosearch_reader', :'reader_password') \gexec
ALTER ROLE geosearch_reader SET default_transaction_read_only = on;
GRANT CONNECT ON DATABASE lab1 TO geosearch_reader;
GRANT USAGE ON SCHEMA search, osm TO geosearch_reader;
GRANT SELECT ON search.search_index, osm.snapshot TO geosearch_reader;
GRANT EXECUTE ON FUNCTION search.search(text, integer) TO geosearch_reader;
GRANT EXECUTE ON FUNCTION search.reverse(double precision, double precision, double precision, integer) TO geosearch_reader;
GRANT EXECUTE ON FUNCTION search.label(text, text, text, text) TO geosearch_reader;
GRANT EXECUTE ON FUNCTION search.normalize(text) TO geosearch_reader;
GRANT EXECUTE ON FUNCTION search.api_search(text, integer) TO geosearch_reader;
GRANT EXECUTE ON FUNCTION search.api_reverse(double precision, double precision, double precision) TO geosearch_reader;
GRANT EXECUTE ON FUNCTION search.api_nearby(double precision, double precision, double precision, text, integer) TO geosearch_reader;
