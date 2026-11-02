-- Preserve existing Nova IDs and all inbox/outbox/mapping foreign keys.
-- One connection per provider for this first integration; no FreightOps worker is enabled here.
ALTER TABLE aurora_sync_connection DROP CONSTRAINT aurora_sync_connection_pkey;
ALTER TABLE aurora_sync_connection ADD CONSTRAINT aurora_sync_connection_pkey PRIMARY KEY(tenant_id,provider);
ALTER TABLE aurora_sync_connection DROP CONSTRAINT aurora_sync_connection_provider_check;
ALTER TABLE aurora_sync_connection ADD CONSTRAINT aurora_sync_connection_provider_check CHECK(provider IN ('nova','freightops'));

-- Only the implemented Nova runtime is scheduled. FreightOps activation requires its adapter.
CREATE OR REPLACE FUNCTION public.aurora_enabled_sync_tenants() RETURNS TABLE(tenant_id uuid)
LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public
AS $$ SELECT c.tenant_id FROM public.aurora_sync_connection c WHERE c.enabled AND c.provider='nova' $$;

-- A second provider must not hide the tenant's existing Firebird planning source.
CREATE OR REPLACE FUNCTION aurora_source_in_workspace(p_tenant uuid,p_source uuid) RETURNS boolean
LANGUAGE sql STABLE AS $$
 SELECT NOT EXISTS(
   SELECT 1 FROM aurora_order_source s JOIN aurora_sync_connection c
     ON c.tenant_id=s.tenant_id AND c.provider='nova'
   WHERE s.tenant_id=p_tenant AND s.id=p_source
     AND s.request#>>'{reporting,firebirdSource}' IS NOT NULL
     AND s.request#>>'{reporting,firebirdSource}'<>c.source_identity
 )
$$;
REVOKE ALL ON FUNCTION aurora_source_in_workspace(uuid,uuid) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION aurora_source_in_workspace(uuid,uuid) TO aurora_app;
