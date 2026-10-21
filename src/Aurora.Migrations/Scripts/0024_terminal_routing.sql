CREATE TABLE aurora_terminal_routing (
 tenant_id uuid NOT NULL REFERENCES tenant(id),
 source_key text NOT NULL,
 terminal_code text NOT NULL,
 data jsonb NOT NULL DEFAULT '{}',
 revision integer NOT NULL DEFAULT 1,
 PRIMARY KEY (tenant_id, source_key, terminal_code)
);
ALTER TABLE aurora_terminal_routing ENABLE ROW LEVEL SECURITY;
ALTER TABLE aurora_terminal_routing FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON aurora_terminal_routing
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE, DELETE ON aurora_terminal_routing TO aurora_app;
