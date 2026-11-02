-- Manual additive migration for the verified Lykes TEST database only.
-- Store Nova-local date AND time, consistent with OPS_APPT_LOW/HIGH.
-- Nullable, no default, no backfill, no rating or appointment changes.
ALTER TABLE OPS_STOP_REC ADD OPS_ETA_TIME TIMESTAMP;
