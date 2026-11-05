ALTER TABLE tenant_settings ADD COLUMN driver_cost_per_hour numeric(9,4) NOT NULL DEFAULT 0
    CHECK (driver_cost_per_hour >= 0 AND driver_cost_per_hour <= 99999.9999);

-- Planners can read this one non-secret setting without access to connection credentials.
CREATE FUNCTION public.aurora_driver_cost_per_hour() RETURNS numeric
LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT s.driver_cost_per_hour FROM public.tenant_settings s
 WHERE s.tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND EXISTS(
 SELECT 1 FROM public.user_tenant m WHERE m.tenant_id=s.tenant_id AND m.user_id=nullif(current_setting('app.user_id',true),'')::uuid)
$$;
REVOKE ALL ON FUNCTION public.aurora_driver_cost_per_hour() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.aurora_driver_cost_per_hour() TO aurora_app;
