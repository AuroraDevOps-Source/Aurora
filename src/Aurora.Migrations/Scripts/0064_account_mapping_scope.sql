BEGIN;
ALTER TABLE document_mapping_rules
    ADD COLUMN account text NOT NULL DEFAULT '' CHECK(char_length(account)<=160),
    DROP CONSTRAINT document_mapping_rules_pkey;
ALTER TABLE document_mapping_rules ADD PRIMARY KEY(tenant_id,sender,form,field,account);
COMMIT;
