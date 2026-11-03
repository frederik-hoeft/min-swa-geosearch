-- A full s05 rebuild drops and recreates search.search_index, resetting its grants.
-- Preserve read access for an already provisioned reader without changing its password.
DO $$
BEGIN
    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'geosearch_reader') THEN
        GRANT SELECT ON search.search_index TO geosearch_reader;
    END IF;
END
$$;
