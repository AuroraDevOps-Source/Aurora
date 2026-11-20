ALTER TABLE order_import_history
    ADD COLUMN skipped_count integer CHECK (skipped_count >= 0),
    ADD COLUMN failed_count integer CHECK (failed_count >= 0);

UPDATE order_import_history SET skipped_count=0, failed_count=0
WHERE status='Orders created' AND cardinality(created_pros)=extracted_count;
