CREATE TABLE aurora_order_routing (
 tenant_id uuid NOT NULL REFERENCES tenant(id),
 source_key text NOT NULL,
 order_id bigint NOT NULL,
 receiving_terminal text,
 revision integer NOT NULL DEFAULT 1,
 PRIMARY KEY (tenant_id, source_key, order_id)
);
ALTER TABLE aurora_order_routing ENABLE ROW LEVEL SECURITY;
ALTER TABLE aurora_order_routing FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON aurora_order_routing
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE, DELETE ON aurora_order_routing TO aurora_app;
