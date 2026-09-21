CREATE TABLE route_plan (
    tenant_id    uuid        NOT NULL,
    id           uuid        NOT NULL DEFAULT gen_random_uuid(),
    name         text        NOT NULL,
    created_utc  timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_route_plan PRIMARY KEY (tenant_id, id),
    CONSTRAINT fk_route_plan_tenant FOREIGN KEY (tenant_id) REFERENCES tenant (id) ON DELETE CASCADE
);

CREATE INDEX ix_route_plan_created ON route_plan (tenant_id, created_utc DESC);

ALTER TABLE route_plan ENABLE ROW LEVEL SECURITY;
ALTER TABLE route_plan FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON route_plan
    USING      (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON route_plan TO aurora_app;
