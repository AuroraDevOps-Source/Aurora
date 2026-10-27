-- Durable inbound deduplication and conflict review. Never discard a queued local
-- edit merely because the external system delivered a newer snapshot.
ALTER TABLE aurora_sync_inbox ADD COLUMN fingerprint text;
ALTER TABLE aurora_sync_mapping ADD COLUMN external_deleted boolean NOT NULL DEFAULT false;
ALTER TABLE aurora_sync_outbox DROP CONSTRAINT aurora_sync_outbox_state_check;
ALTER TABLE aurora_sync_outbox ADD CONSTRAINT aurora_sync_outbox_state_check
 CHECK(state IN ('pending','working','synced','conflict','superseded'));
ALTER TABLE aurora_sync_conflict ADD COLUMN external_revision text;
ALTER TABLE aurora_sync_conflict ADD COLUMN local_revision bigint;
ALTER TABLE aurora_sync_conflict ADD COLUMN local_deleted boolean NOT NULL DEFAULT false;
ALTER TABLE aurora_sync_conflict ADD COLUMN external_deleted boolean NOT NULL DEFAULT false;
ALTER TABLE aurora_sync_conflict ADD COLUMN event_id text;
ALTER TABLE aurora_sync_conflict ADD COLUMN sequence bigint GENERATED ALWAYS AS IDENTITY;
GRANT USAGE,SELECT ON SEQUENCE aurora_sync_conflict_sequence_seq TO aurora_app;
CREATE UNIQUE INDEX ix_sync_conflict_event ON aurora_sync_conflict(tenant_id,connection_id,event_id) WHERE event_id IS NOT NULL;
DROP INDEX ix_sync_outbox_pending;
CREATE INDEX ix_sync_outbox_pending ON aurora_sync_outbox(tenant_id,connection_id,sequence) WHERE state NOT IN ('synced','superseded');
