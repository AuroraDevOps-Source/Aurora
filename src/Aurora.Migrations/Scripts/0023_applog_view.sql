CREATE TABLE applog_view (
 tenant_id uuid NOT NULL,
 user_id uuid NOT NULL,
 cleared_before timestamptz NOT NULL,
 PRIMARY KEY (tenant_id,user_id)
);
ALTER TABLE applog_view ENABLE ROW LEVEL SECURITY;
ALTER TABLE applog_view FORCE ROW LEVEL SECURITY;
CREATE POLICY applog_view_owner ON applog_view USING (
 tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
 AND user_id=nullif(current_setting('app.user_id',true),'')::uuid
) WITH CHECK (
 tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
 AND user_id=nullif(current_setting('app.user_id',true),'')::uuid
);
GRANT SELECT,INSERT,UPDATE ON applog_view TO aurora_app;
