ALTER TABLE aurora_routing_status_configuration ADD COLUMN pickup_ready_statuses integer[] NOT NULL DEFAULT ARRAY[]::integer[];
