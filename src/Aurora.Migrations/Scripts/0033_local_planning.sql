ALTER TABLE aurora_manifest ADD COLUMN planning_id bigint GENERATED ALWAYS AS IDENTITY;
CREATE UNIQUE INDEX ux_manifest_planning_id ON aurora_manifest(tenant_id,planning_id);
GRANT USAGE,SELECT ON SEQUENCE aurora_manifest_planning_id_seq TO aurora_app;
