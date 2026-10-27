CREATE TABLE aurora_driver (
 tenant_id uuid NOT NULL REFERENCES tenant(id),
 id bigint GENERATED ALWAYS AS IDENTITY,
 number text NOT NULL,
 terminal text NOT NULL,
 data jsonb NOT NULL,
 revision bigint NOT NULL DEFAULT 0,
 deleted_at timestamptz,
 PRIMARY KEY(tenant_id,id),
 UNIQUE(tenant_id,number)
);
ALTER TABLE aurora_driver ENABLE ROW LEVEL SECURITY;
ALTER TABLE aurora_driver FORCE ROW LEVEL SECURITY;
CREATE POLICY driver_tenant ON aurora_driver USING(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid)
 WITH CHECK(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid);
GRANT SELECT,INSERT,UPDATE,DELETE ON aurora_driver TO aurora_app;
GRANT USAGE,SELECT ON SEQUENCE aurora_driver_id_seq TO aurora_app;
