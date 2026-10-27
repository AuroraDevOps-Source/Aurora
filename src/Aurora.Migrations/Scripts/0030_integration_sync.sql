-- Provider-neutral integration state. No Nova business columns or credentials belong here.
-- One optional connection per tenant; disconnecting never deletes local business data.
CREATE TABLE aurora_sync_connection (
 tenant_id uuid PRIMARY KEY REFERENCES tenant(id),
 id uuid NOT NULL DEFAULT gen_random_uuid(),
 provider text NOT NULL CHECK(provider='nova'),
 source_identity text NOT NULL,
 credential_reference uuid NOT NULL,
 enabled boolean NOT NULL DEFAULT false,
 revision integer NOT NULL DEFAULT 0,
 checkpoint text,
 last_success_at timestamptz,
 UNIQUE(tenant_id,id)
);
CREATE TABLE aurora_sync_mapping (
 tenant_id uuid NOT NULL,
 connection_id uuid NOT NULL,
 entity_type text NOT NULL,
 local_id text NOT NULL,
 external_id text NOT NULL,
 external_revision text,
 baseline jsonb NOT NULL DEFAULT '{}',
 PRIMARY KEY(tenant_id,connection_id,entity_type,local_id),
 UNIQUE(tenant_id,connection_id,entity_type,external_id),
 FOREIGN KEY(tenant_id,connection_id) REFERENCES aurora_sync_connection(tenant_id,id)
);
CREATE TABLE aurora_sync_outbox (
 tenant_id uuid NOT NULL,
 id uuid NOT NULL DEFAULT gen_random_uuid(),
 sequence bigint GENERATED ALWAYS AS IDENTITY,
 connection_id uuid NOT NULL,
 entity_type text NOT NULL,
 local_id text NOT NULL,
 local_revision bigint NOT NULL,
 operation text NOT NULL CHECK(operation IN ('upsert','delete')),
 payload jsonb NOT NULL,
 state text NOT NULL DEFAULT 'pending' CHECK(state IN ('pending','working','synced','conflict')),
 attempts integer NOT NULL DEFAULT 0,
 available_at timestamptz NOT NULL DEFAULT now(),
 lease_id uuid,
 lease_until timestamptz,
 last_error text,
 created_at timestamptz NOT NULL DEFAULT now(),
 synced_at timestamptz,
 PRIMARY KEY(tenant_id,id),
 UNIQUE(tenant_id,connection_id,entity_type,local_id,local_revision),
 FOREIGN KEY(tenant_id,connection_id) REFERENCES aurora_sync_connection(tenant_id,id)
);
CREATE INDEX ix_sync_outbox_pending ON aurora_sync_outbox(tenant_id,connection_id,sequence) WHERE state<>'synced';
CREATE TABLE aurora_sync_inbox (
 tenant_id uuid NOT NULL,
 connection_id uuid NOT NULL,
 event_id text NOT NULL,
 received_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY(tenant_id,connection_id,event_id),
 FOREIGN KEY(tenant_id,connection_id) REFERENCES aurora_sync_connection(tenant_id,id)
);
CREATE TABLE aurora_sync_conflict (
 tenant_id uuid NOT NULL,
 id uuid NOT NULL DEFAULT gen_random_uuid(),
 connection_id uuid NOT NULL,
 entity_type text NOT NULL,
 local_id text NOT NULL,
 external_id text NOT NULL,
 local_value jsonb NOT NULL,
 external_value jsonb NOT NULL,
 fields jsonb NOT NULL,
 created_at timestamptz NOT NULL DEFAULT now(),
 resolved_at timestamptz,
 PRIMARY KEY(tenant_id,id),
 FOREIGN KEY(tenant_id,connection_id) REFERENCES aurora_sync_connection(tenant_id,id)
);
ALTER TABLE aurora_sync_connection ENABLE ROW LEVEL SECURITY;
ALTER TABLE aurora_sync_connection FORCE ROW LEVEL SECURITY;
ALTER TABLE aurora_sync_mapping ENABLE ROW LEVEL SECURITY;
ALTER TABLE aurora_sync_mapping FORCE ROW LEVEL SECURITY;
ALTER TABLE aurora_sync_outbox ENABLE ROW LEVEL SECURITY;
ALTER TABLE aurora_sync_outbox FORCE ROW LEVEL SECURITY;
ALTER TABLE aurora_sync_inbox ENABLE ROW LEVEL SECURITY;
ALTER TABLE aurora_sync_inbox FORCE ROW LEVEL SECURITY;
ALTER TABLE aurora_sync_conflict ENABLE ROW LEVEL SECURITY;
ALTER TABLE aurora_sync_conflict FORCE ROW LEVEL SECURITY;
CREATE POLICY sync_tenant ON aurora_sync_connection USING(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid) WITH CHECK(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid);
CREATE POLICY sync_tenant ON aurora_sync_mapping USING(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid) WITH CHECK(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid);
CREATE POLICY sync_tenant ON aurora_sync_outbox USING(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid) WITH CHECK(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid);
CREATE POLICY sync_tenant ON aurora_sync_inbox USING(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid) WITH CHECK(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid);
CREATE POLICY sync_tenant ON aurora_sync_conflict USING(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid) WITH CHECK(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid);
GRANT SELECT,INSERT,UPDATE,DELETE ON aurora_sync_connection,aurora_sync_mapping,aurora_sync_outbox,aurora_sync_inbox,aurora_sync_conflict TO aurora_app;
GRANT USAGE,SELECT ON SEQUENCE aurora_sync_outbox_sequence_seq TO aurora_app;
