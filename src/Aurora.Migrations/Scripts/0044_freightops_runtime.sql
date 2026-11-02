-- Discover both providers without exposing credentials. Execution remains guarded
-- by the existing connection enable flag and the host's FreightOps runtime opt-in.
CREATE OR REPLACE FUNCTION public.aurora_enabled_sync_tenants() RETURNS TABLE(tenant_id uuid)
LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public
AS $$ SELECT DISTINCT c.tenant_id FROM public.aurora_sync_connection c WHERE c.enabled $$;

-- Retire only never-prepared unsupported work from the initial broad fanout.
-- Preserve in-flight/prepared requests for explicit reconciliation.
UPDATE aurora_sync_outbox o SET state='superseded',lease_id=NULL,lease_until=NULL,
 last_error='FreightOps supports order synchronization only.'
FROM aurora_sync_connection c
WHERE c.tenant_id=o.tenant_id AND c.id=o.connection_id AND c.provider='freightops'
 AND o.entity_type<>'order' AND o.state IN ('pending','conflict')
 AND NOT EXISTS(SELECT 1 FROM aurora_freightops_request r WHERE r.tenant_id=o.tenant_id AND r.connection_id=o.connection_id AND r.change_id=o.id);
