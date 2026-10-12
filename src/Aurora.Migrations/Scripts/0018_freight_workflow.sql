-- Separate from order details: ordinary order edits/imports cannot overwrite execution history.
CREATE TABLE aurora_freight_workflow (
 tenant_id uuid NOT NULL,
 source_id uuid NOT NULL,
 order_id text NOT NULL,
 revision integer NOT NULL DEFAULT 0 CHECK (revision >= 0),
 data jsonb NOT NULL,
 updated_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY (tenant_id, source_id, order_id),
 FOREIGN KEY (tenant_id, source_id, order_id) REFERENCES aurora_order(tenant_id, source_id, id)
);
ALTER TABLE aurora_freight_workflow ENABLE ROW LEVEL SECURITY;
ALTER TABLE aurora_freight_workflow FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON aurora_freight_workflow
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE ON aurora_freight_workflow TO aurora_app;
