CREATE TABLE routing_equipment_type (
    tenant_id uuid NOT NULL REFERENCES tenant(id) ON DELETE CASCADE,
    code text NOT NULL,
    data jsonb NOT NULL,
    updated_utc timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (tenant_id, code)
);
CREATE TABLE routing_equipment_unit (
    tenant_id uuid NOT NULL REFERENCES tenant(id) ON DELETE CASCADE,
    id text NOT NULL,
    type_code text NOT NULL,
    data jsonb NOT NULL,
    updated_utc timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (tenant_id, id),
    FOREIGN KEY (tenant_id, type_code) REFERENCES routing_equipment_type(tenant_id, code)
);
CREATE INDEX ix_routing_equipment_unit_type ON routing_equipment_unit(tenant_id, type_code);
ALTER TABLE routing_equipment_type ENABLE ROW LEVEL SECURITY;
ALTER TABLE routing_equipment_type FORCE ROW LEVEL SECURITY;
ALTER TABLE routing_equipment_unit ENABLE ROW LEVEL SECURITY;
ALTER TABLE routing_equipment_unit FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON routing_equipment_type
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_isolation ON routing_equipment_unit
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE, DELETE ON routing_equipment_type, routing_equipment_unit TO aurora_app;
