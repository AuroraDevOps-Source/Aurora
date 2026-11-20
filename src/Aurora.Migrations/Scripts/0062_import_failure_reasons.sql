ALTER TABLE order_import_history ADD COLUMN failure_reason text;
CREATE TABLE order_import_failure (
    import_id uuid NOT NULL REFERENCES order_import_history(id) ON DELETE CASCADE,
    row_id uuid NOT NULL,
    order_label text NOT NULL,
    reason text NOT NULL,
    occurred_at timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX order_import_failure_reason ON order_import_failure(import_id,row_id,md5(reason));
ALTER TABLE order_import_failure ENABLE ROW LEVEL SECURITY;
ALTER TABLE order_import_failure FORCE ROW LEVEL SECURITY;
CREATE POLICY read_import_failure ON order_import_failure FOR SELECT USING (
    EXISTS (SELECT FROM order_import_history h WHERE h.id=import_id AND h.tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid)
);
CREATE POLICY write_import_failure ON order_import_failure FOR INSERT WITH CHECK (
    EXISTS (SELECT FROM order_import_history h WHERE h.id=import_id AND h.tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
        AND h.user_id=nullif(current_setting('app.user_id',true),'')::uuid)
);
GRANT SELECT,INSERT ON order_import_failure TO aurora_app;
