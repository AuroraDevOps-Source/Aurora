-- Manual additive migration. Do not install during application startup.
-- A sync-created PRO stays reserved after cancellation; receipts handle retries.
-- This serializes sync creators, not legacy Nova's separate PRO-number allocator.
CREATE TABLE AURORA_SYNC_ORDER_KEY (
 PRO_NUMBER NUMERIC(9,0) NOT NULL PRIMARY KEY,
 ORDER_ID BIGINT NOT NULL UNIQUE,
 CREATED_AT TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL
);
