CREATE TABLE order_import_history (
 id uuid PRIMARY KEY,
 tenant_id uuid NOT NULL REFERENCES tenant(id),
 user_id uuid NOT NULL,
 file_name text NOT NULL CHECK(length(file_name) BETWEEN 1 AND 255),
 created_at timestamptz NOT NULL DEFAULT now(),
 status text NOT NULL CHECK(status IN ('Converting','Converted','Failed','Orders created')),
 extracted_count integer NOT NULL DEFAULT 0 CHECK(extracted_count BETWEEN 0 AND 100),
 created_pros text[] NOT NULL DEFAULT '{}'
);
CREATE INDEX order_import_history_recent ON order_import_history(tenant_id,created_at DESC);
ALTER TABLE order_import_history ENABLE ROW LEVEL SECURITY;
ALTER TABLE order_import_history FORCE ROW LEVEL SECURITY;
CREATE POLICY import_member_read ON order_import_history FOR SELECT USING(
 tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND EXISTS(
 SELECT 1 FROM user_tenant m WHERE m.tenant_id=order_import_history.tenant_id
 AND m.user_id=nullif(current_setting('app.user_id',true),'')::uuid));
CREATE POLICY import_owner_insert ON order_import_history FOR INSERT WITH CHECK(
 tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
 AND user_id=nullif(current_setting('app.user_id',true),'')::uuid AND EXISTS(
 SELECT 1 FROM user_tenant m WHERE m.tenant_id=order_import_history.tenant_id AND m.user_id=order_import_history.user_id));
CREATE POLICY import_owner_update ON order_import_history FOR UPDATE USING(
 tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
 AND user_id=nullif(current_setting('app.user_id',true),'')::uuid)
 WITH CHECK(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
 AND user_id=nullif(current_setting('app.user_id',true),'')::uuid);
GRANT SELECT,INSERT,UPDATE ON order_import_history TO aurora_app;
