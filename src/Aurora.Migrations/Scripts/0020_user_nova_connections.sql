ALTER TABLE user_tenant ADD COLUMN nova_connection jsonb;
-- Global identities may be shared. Only identities owned by this company are editable here.
ALTER TABLE "AspNetUsers" ADD COLUMN "ManagementTenantId" uuid REFERENCES tenant(id);
UPDATE "AspNetUsers" u SET "ManagementTenantId"=m.tenant_id
FROM user_tenant m WHERE m.user_id=u."Id"
 AND (SELECT count(*) FROM user_tenant x WHERE x.user_id=u."Id")=1;

CREATE POLICY user_tenant_administration ON user_tenant
 USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND EXISTS (
  SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id"=ur."RoleId"
  WHERE ur."TenantId"=tenant_id AND ur."UserId"=nullif(current_setting('app.user_id',true),'')::uuid AND r."Name"='aurora:Admin'))
 WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND EXISTS (
  SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id"=ur."RoleId"
  WHERE ur."TenantId"=tenant_id AND ur."UserId"=nullif(current_setting('app.user_id',true),'')::uuid AND r."Name"='aurora:Admin'));

INSERT INTO "AspNetRoles" ("Id","Name","NormalizedName","ConcurrencyStamp")
SELECT gen_random_uuid(),v.name,upper(v.name),gen_random_uuid()::text
FROM (VALUES ('aurora:User'),('aurora:PowerUser')) v(name)
WHERE NOT EXISTS(SELECT 1 FROM "AspNetRoles" r WHERE r."Name"=v.name);
INSERT INTO "AspNetUserRoles" ("TenantId","UserId","RoleId")
SELECT m.tenant_id,m.user_id,r."Id" FROM user_tenant m CROSS JOIN "AspNetRoles" r
WHERE r."Name"='aurora:User' AND NOT EXISTS (
 SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" ar ON ar."Id"=ur."RoleId"
 WHERE ur."TenantId"=m.tenant_id AND ur."UserId"=m.user_id AND ar."Name" LIKE 'aurora:%');
