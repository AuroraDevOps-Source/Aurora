BEGIN;
CREATE TABLE document_packet_mapping (
    tenant_id uuid NOT NULL REFERENCES tenant(id) ON DELETE CASCADE,
    file_sha256 text NOT NULL CHECK(length(file_sha256)=64),
    first_page integer NOT NULL CHECK(first_page>0),
    last_page integer NOT NULL CHECK(last_page>=first_page),
    bol text NOT NULL CHECK(length(bol) BETWEEN 1 AND 200),
    payer text NOT NULL CHECK(length(payer) BETWEEN 1 AND 200),
    PRIMARY KEY(tenant_id,file_sha256,first_page)
);
ALTER TABLE document_packet_mapping ENABLE ROW LEVEL SECURITY;
ALTER TABLE document_packet_mapping FORCE ROW LEVEL SECURITY;
CREATE POLICY read_document_packet_mapping ON document_packet_mapping FOR SELECT USING (
    tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND EXISTS (
        SELECT FROM user_tenant m WHERE m.tenant_id=document_packet_mapping.tenant_id
        AND m.user_id=nullif(current_setting('app.user_id',true),'')::uuid
    )
);
GRANT SELECT ON document_packet_mapping TO aurora_app;

-- Provisional selections for the reviewed copy of 20261109115115.pdf.
INSERT INTO document_packet_mapping(tenant_id,file_sha256,first_page,last_page,bol,payer)
SELECT t.id,'2BDE98C6EDAFD0EE74C65E2C08DAE66ED5AC84B9F806C669CB476EE0CFFD3C95',p.*
FROM tenant t CROSS JOIN (VALUES
 (1,1,'XP22405814','Xpeditious Unlimited'),
 (2,2,'MAV145130160','Maverick Acquisition Ventures INC'),
 (3,3,'BNA 30014450','MCKESSON SDF'),
 (4,4,'BNA 30014446','MCKESSON SDF'),
 (5,5,'05760575001','Capital Logistics International LLC'),
 (6,6,'MCO3735721','UPS SUPPLY CHAIN / MCO - MCO'),
 (7,7,'BNA 30014436','MCKESSON SDF'),
 (8,8,'2274999','PARAMOUNT / R+L TRUCKLOAD'),
 (9,9,'C010849','Beyond Transportation Inc.'),
 (10,10,'600241520A','PGL'),
 (11,11,'BNA 30014440','MCKESSON SDF'),
 (12,13,'J24083','Alliance Store Fixtures Inc.'),
 (14,14,'RO26003315','Laufer Group International')
) AS p(first_page,last_page,bol,payer)
WHERE t.id='11111111-1111-1111-1111-111111111111' AND lower(trim(t.name))='lykes cartage';
COMMIT;
