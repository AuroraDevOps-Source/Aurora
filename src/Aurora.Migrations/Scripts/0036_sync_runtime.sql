ALTER TABLE aurora_sync_connection ADD COLUMN last_attempt_at timestamptz;
ALTER TABLE aurora_sync_connection ADD COLUMN last_error text;
ALTER TABLE aurora_sync_connection ADD COLUMN initialized_at timestamptz;

-- Background scheduling needs connection discovery without a user's login. Expose
-- only enabled tenant IDs; credential resolution and every business query retain RLS.
CREATE FUNCTION public.aurora_enabled_sync_tenants() RETURNS TABLE(tenant_id uuid)
LANGUAGE sql SECURITY DEFINER SET search_path = pg_catalog, public
AS $$ SELECT c.tenant_id FROM public.aurora_sync_connection c WHERE c.enabled $$;
REVOKE ALL ON FUNCTION public.aurora_enabled_sync_tenants() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.aurora_enabled_sync_tenants() TO aurora_app;
