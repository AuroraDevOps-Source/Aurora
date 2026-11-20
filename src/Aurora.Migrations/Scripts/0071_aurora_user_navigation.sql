CREATE OR REPLACE FUNCTION public.aurora_tenant_navigation() RETURNS text
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT CASE WHEN u."Aurora_User" THEN '{}'::jsonb
             ELSE COALESCE(s.navigation, '{}'::jsonb) END::text
 FROM public.user_tenant m
 JOIN public."AspNetUsers" u ON u."Id"=m.user_id
 LEFT JOIN public.tenant_settings s ON s.tenant_id=m.tenant_id
 WHERE m.tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
 AND m.user_id=nullif(current_setting('app.user_id',true),'')::uuid
$$;
