CREATE TABLE aurora_customer_planning (
 tenant_id uuid NOT NULL REFERENCES tenant(id),
 source_key text NOT NULL,
 customer_code text NOT NULL,
 settings jsonb NOT NULL,
 revision integer NOT NULL DEFAULT 1,
 PRIMARY KEY(tenant_id,source_key,customer_code)
);
ALTER TABLE aurora_customer_planning ENABLE ROW LEVEL SECURITY;
ALTER TABLE aurora_customer_planning FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON aurora_customer_planning
 USING(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid)
 WITH CHECK(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid);
GRANT SELECT,INSERT,UPDATE,DELETE ON aurora_customer_planning TO aurora_app;
