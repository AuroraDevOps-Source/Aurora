CREATE TABLE aurora_terminal (
 tenant_id uuid NOT NULL REFERENCES tenant(id), id uuid NOT NULL DEFAULT gen_random_uuid(),
 code text NOT NULL CHECK(length(code) BETWEEN 1 AND 80),
 name text NOT NULL CHECK(length(name) BETWEEN 1 AND 200),
 address text NOT NULL DEFAULT '', city text NOT NULL DEFAULT '', state text NOT NULL DEFAULT '',
 postal_code text NOT NULL DEFAULT '', country text NOT NULL DEFAULT '',
 revision integer NOT NULL DEFAULT 0, deleted_at timestamptz,
 PRIMARY KEY(tenant_id,id)
);
CREATE UNIQUE INDEX ix_aurora_terminal_code ON aurora_terminal(tenant_id,lower(code));
ALTER TABLE aurora_terminal ENABLE ROW LEVEL SECURITY;
ALTER TABLE aurora_terminal FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON aurora_terminal
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE, DELETE ON aurora_terminal TO aurora_app;

INSERT INTO aurora_terminal(tenant_id,code,name)
SELECT tenant_id,min(code),min(code) FROM (
 SELECT tenant_id,trim(request#>>'{reporting,terminal}') AS code FROM aurora_order_source
 UNION ALL SELECT tenant_id,trim(data->>'Terminal') FROM routing_equipment_unit
) codes WHERE length(code) BETWEEN 1 AND 80 GROUP BY tenant_id,lower(code);

ALTER TABLE aurora_order ADD COLUMN terminal_id uuid;
ALTER TABLE aurora_order ADD CONSTRAINT fk_order_terminal FOREIGN KEY(tenant_id,terminal_id) REFERENCES aurora_terminal(tenant_id,id);
CREATE INDEX ix_aurora_order_terminal ON aurora_order(tenant_id,terminal_id) WHERE deleted_at IS NULL;
UPDATE aurora_order o SET terminal_id=t.id FROM aurora_order_source s,aurora_terminal t
WHERE (s.tenant_id,s.id)=(o.tenant_id,o.source_id) AND t.tenant_id=o.tenant_id
 AND lower(t.code)=lower(trim(s.request#>>'{reporting,terminal}'));
