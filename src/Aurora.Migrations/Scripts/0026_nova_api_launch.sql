ALTER TABLE user_tenant ADD COLUMN nova_api_login boolean NOT NULL DEFAULT false;
ALTER TABLE user_tenant ADD CONSTRAINT ck_nova_api_login CHECK
 (NOT nova_api_login OR (is_default AND nova_database_id IS NOT NULL));

CREATE TABLE nova_api_code (
 code_hash text PRIMARY KEY CHECK(code_hash ~ '^[0-9a-f]{64}$'),
 tenant_id uuid NOT NULL,
 user_id uuid NOT NULL,
 database_id uuid NOT NULL,
 security_stamp text NOT NULL,
 expires_at timestamptz NOT NULL DEFAULT clock_timestamp()+interval '60 seconds',
 FOREIGN KEY(tenant_id,user_id) REFERENCES user_tenant(tenant_id,user_id) ON DELETE CASCADE
);
CREATE INDEX ix_nova_api_code_expiry ON nova_api_code(expires_at);
ALTER TABLE nova_api_code ENABLE ROW LEVEL SECURITY;
ALTER TABLE nova_api_code FORCE ROW LEVEL SECURITY;
REVOKE ALL ON nova_api_code FROM PUBLIC,aurora_app;

-- Deliberately limited pre-auth lookups and code operations, owned by the migration
-- owner (BYPASSRLS). No direct table access or arbitrary account selection by clients.
CREATE FUNCTION nova_api_account(uid uuid)
RETURNS TABLE("TenantId" uuid,"DatabaseId" uuid)
LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT m.tenant_id,m.nova_database_id FROM public.user_tenant m
 WHERE m.user_id=uid AND m.is_default AND m.nova_api_login AND m.nova_database_id IS NOT NULL
 AND EXISTS(SELECT 1 FROM public."AspNetUserRoles" ur JOIN public."AspNetRoles" r ON r."Id"=ur."RoleId"
 WHERE ur."UserId"=m.user_id AND ur."TenantId"=m.tenant_id AND r."Name"='aurora:User')
 AND NOT EXISTS(SELECT 1 FROM public."AspNetUserRoles" ur JOIN public."AspNetRoles" r ON r."Id"=ur."RoleId"
 WHERE ur."UserId"=m.user_id AND r."Name"<>'aurora:User')
 AND EXISTS(SELECT 1 FROM public.tenant_product tp JOIN public.product p ON p.code=tp.product_code
 WHERE tp.tenant_id=m.tenant_id AND tp.is_active AND p.role_prefix='aurora')
$$;
CREATE FUNCTION nova_api_issue(uid uuid,hash text,stamp text)
RETURNS TABLE("TenantId" uuid,"DatabaseId" uuid)
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
BEGIN
 DELETE FROM public.nova_api_code WHERE expires_at<=clock_timestamp();
 RETURN QUERY
 INSERT INTO public.nova_api_code(code_hash,tenant_id,user_id,database_id,security_stamp)
 SELECT hash,a."TenantId",uid,a."DatabaseId",stamp FROM public.nova_api_account(uid) a
 RETURNING tenant_id,database_id;
END $$;
CREATE FUNCTION nova_api_consume(hash text)
RETURNS TABLE("UserId" uuid,"TenantId" uuid,"DatabaseId" uuid,"SecurityStamp" text)
LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 DELETE FROM public.nova_api_code WHERE code_hash=hash AND expires_at>clock_timestamp()
 RETURNING user_id,tenant_id,database_id,security_stamp
$$;
REVOKE ALL ON FUNCTION nova_api_account(uuid),nova_api_issue(uuid,text,text),nova_api_consume(text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION nova_api_account(uuid),nova_api_issue(uuid,text,text),nova_api_consume(text) TO aurora_app;
