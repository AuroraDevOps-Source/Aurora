CREATE TABLE aurora_routing_status_configuration (
 tenant_id uuid NOT NULL REFERENCES tenant(id),
 source_key text NOT NULL,
 ready_statuses integer[] NOT NULL CHECK (cardinality(ready_statuses) > 0),
 PRIMARY KEY (tenant_id, source_key)
);
ALTER TABLE aurora_routing_status_configuration ENABLE ROW LEVEL SECURITY;
ALTER TABLE aurora_routing_status_configuration FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON aurora_routing_status_configuration
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE ON aurora_routing_status_configuration TO aurora_app;
