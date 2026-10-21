CREATE TABLE applog (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 occurred_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
 tenant_id uuid,
 user_id uuid,
 user_name text NOT NULL,
 level text NOT NULL CHECK(level IN ('Information','Warning','Error')),
 action text NOT NULL,
 resource text NOT NULL,
 status_code integer NOT NULL,
 detail text NOT NULL,
 trace_id text NOT NULL,
 duration_ms bigint NOT NULL
);
CREATE INDEX ix_applog_tenant_time ON applog(tenant_id,occurred_utc DESC,id DESC);
CREATE INDEX ix_applog_level_time ON applog(tenant_id,level,occurred_utc DESC);
ALTER TABLE applog ENABLE ROW LEVEL SECURITY;
ALTER TABLE applog FORCE ROW LEVEL SECURITY;
CREATE POLICY applog_insert ON applog FOR INSERT WITH CHECK (
 (tenant_id IS NOT DISTINCT FROM nullif(current_setting('app.tenant_id',true),'')::uuid)
 AND (user_id IS NOT DISTINCT FROM nullif(current_setting('app.user_id',true),'')::uuid));
CREATE POLICY applog_read ON applog FOR SELECT USING (
 (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid OR (tenant_id IS NULL AND user_id IS NULL))
 AND EXISTS (SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id"=ur."RoleId"
 WHERE ur."TenantId"=nullif(current_setting('app.tenant_id',true),'')::uuid
 AND ur."UserId"=nullif(current_setting('app.user_id',true),'')::uuid
 AND r."Name" IN ('aurora:Admin','aurora:PowerUser')));
-- Audit history is append-only for the application; deleting a user keeps their history.
GRANT SELECT,INSERT ON applog TO aurora_app;
