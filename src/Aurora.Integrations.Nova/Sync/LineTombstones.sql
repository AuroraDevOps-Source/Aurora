-- Manual additive migration, after StableLineIds.sql. Never run at application startup.
-- Reserve added IDs and retain deleted IDs so snapshots cannot reassign a freight line.
-- DELETED_AT NULL is a reservation for a live sync-created row.
CREATE TABLE AURORA_SYNC_LINE_TOMBSTONE (
 LINE_ID VARCHAR(36) NOT NULL PRIMARY KEY,
 ORDER_ID BIGINT NOT NULL,
 DELETED_AT TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
