ALTER TABLE routing_equipment_type ADD COLUMN revision bigint NOT NULL DEFAULT 1;
ALTER TABLE routing_equipment_unit ADD COLUMN revision bigint NOT NULL DEFAULT 1;
