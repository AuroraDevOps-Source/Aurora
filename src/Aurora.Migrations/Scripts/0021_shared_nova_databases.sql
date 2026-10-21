CREATE TABLE nova_database (
 tenant_id uuid NOT NULL REFERENCES tenant(id),
 id uuid NOT NULL DEFAULT gen_random_uuid(),
 name text NOT NULL CHECK(length(trim(name)) BETWEEN 1 AND 200),
 connection jsonb NOT NULL,
 revision integer NOT NULL DEFAULT 0,
 PRIMARY KEY(tenant_id,id)
);
CREATE UNIQUE INDEX ix_nova_database_name ON nova_database(tenant_id,lower(name));
ALTER TABLE user_tenant ADD COLUMN nova_database_id uuid;
ALTER TABLE user_tenant ADD CONSTRAINT fk_user_nova_database
 FOREIGN KEY(tenant_id,nova_database_id) REFERENCES nova_database(tenant_id,id);

-- Preserve each existing encrypted credential and source identity without decrypting it.
INSERT INTO nova_database(tenant_id,id,name,connection)
SELECT m.tenant_id,m.user_id,left(u."UserName",100)||' Nova '||m.user_id::text,m.nova_connection
FROM user_tenant m JOIN "AspNetUsers" u ON u."Id"=m.user_id
WHERE m.nova_connection IS NOT NULL;
UPDATE user_tenant SET nova_database_id=user_id WHERE nova_connection IS NOT NULL;

ALTER TABLE nova_database ENABLE ROW LEVEL SECURITY;
ALTER TABLE nova_database FORCE ROW LEVEL SECURITY;
CREATE POLICY database_admin ON nova_database FOR ALL
 USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND EXISTS (
 SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id"=ur."RoleId"
 WHERE ur."TenantId"=tenant_id AND ur."UserId"=nullif(current_setting('app.user_id',true),'')::uuid AND r."Name"='aurora:Admin'))
 WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND EXISTS (
 SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id"=ur."RoleId"
 WHERE ur."TenantId"=tenant_id AND ur."UserId"=nullif(current_setting('app.user_id',true),'')::uuid AND r."Name"='aurora:Admin'));
CREATE POLICY assigned_database ON nova_database FOR SELECT
 USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND EXISTS (
 SELECT 1 FROM user_tenant m WHERE m.tenant_id=nova_database.tenant_id AND m.nova_database_id=nova_database.id
 AND m.user_id=nullif(current_setting('app.user_id',true),'')::uuid));
GRANT SELECT,INSERT,UPDATE,DELETE ON nova_database TO aurora_app;

ALTER TABLE user_tenant DROP COLUMN nova_connection;
