ALTER TABLE user_tenant ADD COLUMN nova_user_id text;
ALTER TABLE user_tenant ADD CONSTRAINT ck_nova_launch_mapping CHECK
 (nova_user_id IS NULL OR (nova_database_id IS NOT NULL AND is_default AND length(nova_user_id) BETWEEN 1 AND 128 AND nova_user_id=upper(trim(nova_user_id))));
CREATE UNIQUE INDEX ix_nova_launch_user ON user_tenant(tenant_id,nova_database_id,nova_user_id) WHERE nova_user_id IS NOT NULL;

-- Narrow pre-authentication lookups. These functions deliberately run as the migration
-- owner (BYPASSRLS), never expose arbitrary SQL, and are not available to PUBLIC.
-- No mapping means the database cannot be used for a Nova launch.
CREATE FUNCTION nova_launch_database(database_id uuid)
RETURNS TABLE("TenantId" uuid,"Json" text)
LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT d.tenant_id,d.connection::text FROM public.nova_database d
 WHERE d.id=database_id AND EXISTS(SELECT 1 FROM public.user_tenant m
 WHERE m.tenant_id=d.tenant_id AND m.nova_database_id=d.id AND m.nova_user_id IS NOT NULL AND m.is_default)
$$;
CREATE FUNCTION nova_launch_user(tenant uuid,database_id uuid,nova_user text)
RETURNS uuid LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT m.user_id FROM public.user_tenant m
 WHERE m.tenant_id=tenant AND m.nova_database_id=database_id
 AND m.nova_user_id=upper(trim(nova_user)) AND m.is_default
 AND EXISTS(SELECT 1 FROM public."AspNetUserRoles" ur JOIN public."AspNetRoles" r ON r."Id"=ur."RoleId"
 WHERE ur."UserId"=m.user_id AND ur."TenantId"=m.tenant_id AND r."Name" LIKE 'aurora:%')
 AND EXISTS(SELECT 1 FROM public.tenant_product tp JOIN public.product p ON p.code=tp.product_code
 WHERE tp.tenant_id=m.tenant_id AND tp.is_active AND p.role_prefix='aurora')
$$;
REVOKE ALL ON FUNCTION nova_launch_database(uuid),nova_launch_user(uuid,uuid,text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION nova_launch_database(uuid),nova_launch_user(uuid,uuid,text) TO aurora_app;
