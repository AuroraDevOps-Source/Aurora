ALTER TABLE tenant_settings ADD COLUMN navigation jsonb NOT NULL DEFAULT '{}'::jsonb
    CHECK (jsonb_typeof(navigation) = 'object');

-- Expose only menu preferences to members, never integration credentials.
CREATE FUNCTION public.aurora_tenant_navigation() RETURNS text
LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT COALESCE(s.navigation,'{}'::jsonb)::text
 FROM public.user_tenant m
 LEFT JOIN public.tenant_settings s ON s.tenant_id=m.tenant_id
 WHERE m.tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
 AND m.user_id=nullif(current_setting('app.user_id',true),'')::uuid
$$;
REVOKE ALL ON FUNCTION public.aurora_tenant_navigation() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.aurora_tenant_navigation() TO aurora_app;
