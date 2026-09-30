CREATE TABLE aurora_customer (
 tenant_id uuid NOT NULL REFERENCES tenant(id), id uuid NOT NULL DEFAULT gen_random_uuid(),
 code text NOT NULL CHECK(length(code) BETWEEN 1 AND 80), name text NOT NULL CHECK(length(name) BETWEEN 1 AND 200),
 data jsonb NOT NULL DEFAULT '{}', revision integer NOT NULL DEFAULT 0, deleted_at timestamptz,
 PRIMARY KEY(tenant_id,id)
);
CREATE UNIQUE INDEX ix_aurora_customer_code ON aurora_customer(tenant_id,lower(code));
ALTER TABLE aurora_customer ENABLE ROW LEVEL SECURITY;
ALTER TABLE aurora_customer FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON aurora_customer
 USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid)
 WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid);
GRANT SELECT, INSERT, UPDATE, DELETE ON aurora_customer TO aurora_app;

INSERT INTO aurora_customer(tenant_id,code,name,data)
SELECT DISTINCT ON(o.tenant_id,o.customer) o.tenant_id,'C-'||upper(substr(md5(o.customer),1,20)),o.customer,
 jsonb_build_object('Address',COALESCE(o.details->>'Address',s.request->'reporting'->'orders'->o.id->>'address1',''),
 'Address2',COALESCE(o.details->>'Address2',''),'City',o.city,
 'State',COALESCE(o.details->>'State',s.request->'reporting'->'orders'->o.id->>'state',''),
 'PostalCode',COALESCE(o.details->>'PostalCode',s.request->'reporting'->'orders'->o.id->>'postalCode',''),
 'Country',COALESCE(o.details->>'Country',''))
FROM aurora_order o JOIN aurora_order_source s ON (s.tenant_id,s.id)=(o.tenant_id,o.source_id)
WHERE length(trim(o.customer))>0 ORDER BY o.tenant_id,o.customer,o.scheduled_at DESC,o.id;
ALTER TABLE aurora_order ADD COLUMN customer_id uuid;
ALTER TABLE aurora_order ADD CONSTRAINT fk_order_customer FOREIGN KEY(tenant_id,customer_id) REFERENCES aurora_customer(tenant_id,id);
CREATE INDEX ix_aurora_order_customer ON aurora_order(tenant_id,customer_id) WHERE deleted_at IS NULL;
UPDATE aurora_order o SET customer_id=c.id FROM aurora_customer c WHERE c.tenant_id=o.tenant_id AND c.name=o.customer;
