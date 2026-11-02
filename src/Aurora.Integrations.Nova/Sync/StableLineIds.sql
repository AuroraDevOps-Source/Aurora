-- Manual, additive Nova migration. NOT installed by Aurora startup.
-- Deploy updated OrderEntry AND rating binaries before enabling line sync.
-- Nullable for older writers: missing identity is held for review, never guessed.
ALTER TABLE OPS_LINE_ITEMS ADD OPL_SYNC_ID VARCHAR(36);
CREATE UNIQUE INDEX UX_OPL_SYNC_ID ON OPS_LINE_ITEMS(OPL_SYNC_ID);
-- No automatic backfill or insert trigger: legacy delete/reinsert would mint new IDs.
-- Adopt existing rows separately in a maintenance transaction after writer rollout.
