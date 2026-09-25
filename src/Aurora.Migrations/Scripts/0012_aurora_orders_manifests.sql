CREATE TABLE aurora_order_source (
 tenant_id uuid NOT NULL REFERENCES tenant(id), id uuid NOT NULL, name text NOT NULL,
 request jsonb NOT NULL, PRIMARY KEY(tenant_id,id), UNIQUE(tenant_id,name)
);
CREATE TABLE aurora_order (
 tenant_id uuid NOT NULL, source_id uuid NOT NULL, id text NOT NULL,
 scheduled_at timestamptz NOT NULL, customer text NOT NULL, city text NOT NULL,
 status text NOT NULL DEFAULT 'Available' CHECK(status IN ('Available','Manifested')),
 PRIMARY KEY(tenant_id,source_id,id),
 FOREIGN KEY(tenant_id,source_id) REFERENCES aurora_order_source(tenant_id,id)
);
CREATE INDEX ix_aurora_order_filter ON aurora_order(tenant_id,source_id,status,scheduled_at);
CREATE TABLE aurora_planning_draft (
 tenant_id uuid NOT NULL, id uuid NOT NULL, owner_id text NOT NULL, source_id uuid NOT NULL,
 order_ids text[] NOT NULL, request jsonb NOT NULL, created_at timestamptz NOT NULL DEFAULT now(),
 finished_session uuid, PRIMARY KEY(tenant_id,id),
 FOREIGN KEY(tenant_id,source_id) REFERENCES aurora_order_source(tenant_id,id)
);
CREATE TABLE aurora_manifest (
 tenant_id uuid NOT NULL, id uuid NOT NULL, draft_id uuid NOT NULL, vehicle text NOT NULL,
 created_at timestamptz NOT NULL DEFAULT now(), data jsonb NOT NULL,
 PRIMARY KEY(tenant_id,id), UNIQUE(tenant_id,draft_id,vehicle),
 FOREIGN KEY(tenant_id,draft_id) REFERENCES aurora_planning_draft(tenant_id,id)
);
CREATE TABLE aurora_manifest_order (
 tenant_id uuid NOT NULL, manifest_id uuid NOT NULL, source_id uuid NOT NULL, order_id text NOT NULL,
 stop_number integer NOT NULL,
 PRIMARY KEY(tenant_id,source_id,order_id),
 FOREIGN KEY(tenant_id,manifest_id) REFERENCES aurora_manifest(tenant_id,id),
 FOREIGN KEY(tenant_id,source_id,order_id) REFERENCES aurora_order(tenant_id,source_id,id)
);
ALTER TABLE aurora_order_source ENABLE ROW LEVEL SECURITY;
ALTER TABLE aurora_order_source FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON aurora_order_source
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE, DELETE ON aurora_order_source TO aurora_app;

ALTER TABLE aurora_order ENABLE ROW LEVEL SECURITY;
ALTER TABLE aurora_order FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON aurora_order
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE, DELETE ON aurora_order TO aurora_app;

ALTER TABLE aurora_planning_draft ENABLE ROW LEVEL SECURITY;
ALTER TABLE aurora_planning_draft FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON aurora_planning_draft
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE, DELETE ON aurora_planning_draft TO aurora_app;

ALTER TABLE aurora_manifest ENABLE ROW LEVEL SECURITY;
ALTER TABLE aurora_manifest FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON aurora_manifest
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE, DELETE ON aurora_manifest TO aurora_app;

ALTER TABLE aurora_manifest_order ENABLE ROW LEVEL SECURITY;
ALTER TABLE aurora_manifest_order FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON aurora_manifest_order
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE, DELETE ON aurora_manifest_order TO aurora_app;
