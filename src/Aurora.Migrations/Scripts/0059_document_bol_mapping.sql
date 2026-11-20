ALTER TABLE document_mapping_rules DROP CONSTRAINT document_mapping_rules_field_check;
ALTER TABLE document_mapping_rules ADD CONSTRAINT document_mapping_rules_field_check CHECK(field IN ('PRO','BOL','BillTo'));
