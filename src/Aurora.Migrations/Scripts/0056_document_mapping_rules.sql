BEGIN;
CREATE TABLE document_mapping_rules (
 tenant_id uuid NOT NULL REFERENCES tenant(id) ON DELETE CASCADE,
 sender text NOT NULL CHECK(char_length(sender) BETWEEN 1 AND 160),
 form text NOT NULL CHECK(char_length(form) BETWEEN 1 AND 160),
 field text NOT NULL CHECK(field IN ('PRO','BillTo')),
 label text NOT NULL CHECK(char_length(label) BETWEEN 1 AND 160),
 PRIMARY KEY(tenant_id,sender,form,field)
);
ALTER TABLE document_mapping_rules ENABLE ROW LEVEL SECURITY;
ALTER TABLE document_mapping_rules FORCE ROW LEVEL SECURITY;
CREATE POLICY member_rules ON document_mapping_rules USING
 (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND EXISTS
 (SELECT 1 FROM user_tenant m WHERE m.tenant_id=document_mapping_rules.tenant_id AND m.user_id=nullif(current_setting('app.user_id',true),'')::uuid));
GRANT SELECT,INSERT,UPDATE ON document_mapping_rules TO aurora_app;
COMMIT;
