ALTER TABLE aurora_order ADD COLUMN details jsonb NOT NULL DEFAULT '{}', ADD COLUMN revision integer NOT NULL DEFAULT 0, ADD COLUMN deleted_at timestamptz;
ALTER TABLE aurora_manifest ALTER COLUMN draft_id DROP NOT NULL;
ALTER TABLE aurora_manifest ADD COLUMN details jsonb NOT NULL DEFAULT '{}', ADD COLUMN revision integer NOT NULL DEFAULT 0, ADD COLUMN deleted_at timestamptz, ADD COLUMN number text, ADD COLUMN manifest_date date NOT NULL DEFAULT CURRENT_DATE, ADD COLUMN status text NOT NULL DEFAULT 'Planning';
UPDATE aurora_manifest SET details=COALESCE(data->'ManifestDetails','{}'), number='MAN-'||upper(left(id::text,8)), manifest_date=COALESCE((data->'ManifestDetails'->>'ManifestDate')::date,created_at::date);
ALTER TABLE aurora_manifest ALTER COLUMN number SET NOT NULL;
CREATE UNIQUE INDEX ix_aurora_manifest_number ON aurora_manifest(tenant_id,number);
ALTER TABLE aurora_manifest ADD CONSTRAINT tms_manifest_status CHECK(status IN ('Planning','Loading','Loaded','Ready','Dispatched','En Route','Arrived','Complete'));
CREATE INDEX ix_aurora_manifest_filter ON aurora_manifest(tenant_id,manifest_date,status) WHERE deleted_at IS NULL;

ALTER TABLE aurora_manifest ALTER COLUMN number SET DEFAULT ('MAN-' || upper(substr(gen_random_uuid()::text,1,8)));
