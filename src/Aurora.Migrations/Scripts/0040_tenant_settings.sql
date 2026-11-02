BEGIN;
-- Tenant-owned integration settings. Preserve encrypted legacy connections and launch IDs.
CREATE TABLE tenant_settings (
 tenant_id uuid PRIMARY KEY REFERENCES tenant(id),
 nova_id uuid NOT NULL DEFAULT gen_random_uuid(),
 nova jsonb,
 freightops jsonb,
 revision integer NOT NULL DEFAULT 0,
 UNIQUE(tenant_id,nova_id)
);
DO $$ BEGIN
 IF EXISTS(SELECT 1 FROM nova_database d WHERE NOT EXISTS(SELECT 1 FROM aurora_sync_connection c WHERE c.tenant_id=d.tenant_id)
 GROUP BY d.tenant_id HAVING count(*)>1) THEN
  RAISE EXCEPTION 'A tenant has multiple legacy databases and no tenant connection. Select its source before migrating settings.';
 END IF;
END $$;
INSERT INTO tenant_settings(tenant_id,nova_id,nova)
SELECT t.id,COALESCE(d.id,gen_random_uuid()),d.connection FROM tenant t
LEFT JOIN LATERAL (
 SELECT n.id,n.connection FROM nova_database n WHERE n.tenant_id=t.id
 AND (NOT EXISTS(SELECT 1 FROM aurora_sync_connection c WHERE c.tenant_id=t.id)
      OR n.id=(SELECT credential_reference FROM aurora_sync_connection c WHERE c.tenant_id=t.id))
 LIMIT 1
) d ON true;
ALTER TABLE tenant_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenant_settings FORCE ROW LEVEL SECURITY;
CREATE POLICY settings_admin ON tenant_settings FOR ALL
 USING(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND EXISTS(
 SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id"=ur."RoleId"
 WHERE ur."TenantId"=tenant_id AND ur."UserId"=nullif(current_setting('app.user_id',true),'')::uuid AND r."Name"='aurora:Admin'))
 WITH CHECK(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND EXISTS(
 SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id"=ur."RoleId"
 WHERE ur."TenantId"=tenant_id AND ur."UserId"=nullif(current_setting('app.user_id',true),'')::uuid AND r."Name"='aurora:Admin'));
GRANT SELECT,INSERT,UPDATE ON tenant_settings TO aurora_app;
CREATE FUNCTION public.aurora_tenant_nova_connection() RETURNS text
LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT s.nova::text FROM public.tenant_settings s
 WHERE s.tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND EXISTS(
 SELECT 1 FROM public.user_tenant m WHERE m.tenant_id=s.tenant_id AND m.user_id=nullif(current_setting('app.user_id',true),'')::uuid)
$$;
REVOKE ALL ON FUNCTION public.aurora_tenant_nova_connection() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.aurora_tenant_nova_connection() TO aurora_app;
CREATE OR REPLACE FUNCTION public.aurora_sync_credentials()
RETURNS TABLE(id uuid,checkpoint text,credentials text,source_identity text,source_name text)
LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT c.id,c.checkpoint,s.nova::text,c.source_identity,COALESCE(s.nova->>'Database','Nova')
 FROM public.aurora_sync_connection c JOIN public.tenant_settings s ON s.tenant_id=c.tenant_id AND s.nova_id=c.credential_reference
 WHERE c.tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND c.enabled AND c.provider='nova' AND s.nova->>'SourceKey'=c.source_identity
$$;
CREATE OR REPLACE FUNCTION nova_launch_database(database_id uuid)
RETURNS TABLE("TenantId" uuid,"Json" text)
LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT s.tenant_id,s.nova::text FROM public.tenant_settings s WHERE s.nova_id=database_id AND s.nova IS NOT NULL
 AND EXISTS(SELECT 1 FROM public.user_tenant m WHERE m.tenant_id=s.tenant_id AND m.nova_user_id IS NOT NULL AND m.is_default)
$$;
CREATE OR REPLACE FUNCTION nova_launch_user(tenant uuid,database_id uuid,nova_user text)
RETURNS uuid LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT m.user_id FROM public.user_tenant m
 WHERE m.tenant_id=tenant AND EXISTS(SELECT 1 FROM public.tenant_settings s WHERE s.tenant_id=m.tenant_id AND s.nova_id=database_id AND s.nova IS NOT NULL)
 AND m.nova_user_id=upper(trim(nova_user)) AND m.is_default
 AND EXISTS(SELECT 1 FROM public."AspNetUserRoles" ur JOIN public."AspNetRoles" r ON r."Id"=ur."RoleId"
 WHERE ur."UserId"=m.user_id AND ur."TenantId"=m.tenant_id AND r."Name" LIKE 'aurora:%')
 AND EXISTS(SELECT 1 FROM public.tenant_product tp JOIN public.product p ON p.code=tp.product_code
 WHERE tp.tenant_id=m.tenant_id AND tp.is_active AND p.role_prefix='aurora')
$$;
CREATE OR REPLACE FUNCTION nova_api_account(uid uuid)
RETURNS TABLE("TenantId" uuid,"DatabaseId" uuid)
LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT m.tenant_id,s.nova_id FROM public.user_tenant m JOIN public.tenant_settings s ON s.tenant_id=m.tenant_id
 WHERE m.user_id=uid AND m.is_default AND m.nova_api_login AND s.nova IS NOT NULL
 AND EXISTS(SELECT 1 FROM public."AspNetUserRoles" ur JOIN public."AspNetRoles" r ON r."Id"=ur."RoleId"
 WHERE ur."UserId"=m.user_id AND ur."TenantId"=m.tenant_id AND r."Name"='aurora:User')
 AND NOT EXISTS(SELECT 1 FROM public."AspNetUserRoles" ur JOIN public."AspNetRoles" r ON r."Id"=ur."RoleId"
 WHERE ur."UserId"=m.user_id AND r."Name"<>'aurora:User')
 AND EXISTS(SELECT 1 FROM public.tenant_product tp JOIN public.product p ON p.code=tp.product_code
 WHERE tp.tenant_id=m.tenant_id AND tp.is_active AND p.role_prefix='aurora')
$$;

ALTER TABLE user_tenant DROP CONSTRAINT ck_nova_launch_mapping;
ALTER TABLE user_tenant DROP CONSTRAINT ck_nova_api_login;
DROP INDEX ix_nova_launch_user;
DROP POLICY assigned_database ON nova_database;
ALTER TABLE user_tenant DROP COLUMN nova_database_id;
ALTER TABLE user_tenant ADD CONSTRAINT ck_nova_launch_mapping CHECK(nova_user_id IS NULL OR (is_default AND length(nova_user_id) BETWEEN 1 AND 128 AND nova_user_id=upper(trim(nova_user_id))));
ALTER TABLE user_tenant ADD CONSTRAINT ck_nova_api_login CHECK(NOT nova_api_login OR is_default);
CREATE UNIQUE INDEX ix_nova_launch_user ON user_tenant(tenant_id,nova_user_id) WHERE nova_user_id IS NOT NULL;
-- Retain a recovery archive, with no application access, rather than discard unused encrypted connections.
ALTER TABLE nova_database RENAME TO nova_database_legacy;
REVOKE ALL ON nova_database_legacy FROM aurora_app;

COMMIT;
