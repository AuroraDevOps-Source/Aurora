-- A background worker has no user assignment. Resolve only the enabled connection
-- for its explicitly scoped tenant, without granting access to the catalog.
CREATE FUNCTION public.aurora_sync_credentials()
RETURNS TABLE(id uuid,checkpoint text,credentials text,source_identity text,source_name text)
LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT c.id,c.checkpoint,d.connection::text,c.source_identity,d.name
 FROM public.aurora_sync_connection c
 JOIN public.nova_database d ON d.tenant_id=c.tenant_id AND d.id=c.credential_reference
 WHERE c.tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
   AND c.enabled AND c.provider='nova' AND d.connection->>'SourceKey'=c.source_identity
$$;
REVOKE ALL ON FUNCTION public.aurora_sync_credentials() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.aurora_sync_credentials() TO aurora_app;
