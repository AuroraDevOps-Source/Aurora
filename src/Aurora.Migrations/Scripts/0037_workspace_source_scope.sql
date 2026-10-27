-- Retain old snapshots for audit, while excluding a different native database
-- from the workspace selected for tenant-wide synchronization.
CREATE FUNCTION aurora_source_in_workspace(p_tenant uuid,p_source uuid) RETURNS boolean
LANGUAGE sql STABLE AS $$
 SELECT NOT EXISTS(
   SELECT 1 FROM aurora_order_source s JOIN aurora_sync_connection c ON c.tenant_id=s.tenant_id
   WHERE s.tenant_id=p_tenant AND s.id=p_source
     AND s.request#>>'{reporting,firebirdSource}' IS NOT NULL
     AND s.request#>>'{reporting,firebirdSource}'<>c.source_identity
 )
$$;
REVOKE ALL ON FUNCTION aurora_source_in_workspace(uuid,uuid) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION aurora_source_in_workspace(uuid,uuid) TO aurora_app;
