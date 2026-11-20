BEGIN;
-- Recorded issuer/form and optional attachment cutoff for visually reviewed packets.
ALTER TABLE document_packet_mapping
    ADD COLUMN issuer text,
    ADD COLUMN form text,
    ADD COLUMN extraction_last_page integer CHECK(extraction_last_page BETWEEN first_page AND last_page),
    ADD COLUMN nova_assigns_pro boolean NOT NULL DEFAULT false;

-- 20261109114802.pdf
INSERT INTO document_packet_mapping(tenant_id,file_sha256,first_page,last_page,bol,payer,issuer,form,extraction_last_page,nova_assigns_pro)
SELECT t.id,'6275ACC044D21632B40111591A2714617ED00F77EBD1989903DA9C62E5287441',p.*,true
FROM tenant t CROSS JOIN (VALUES
 (1, 2, '51124280', 'Hub Group', 'Hub Group', 'Short Form Delivery Receipt', NULL::integer),
 (3, 4, '51127398', 'Hub Group', 'Hub Group', 'Short Form Delivery Receipt', NULL::integer),
 (5, 6, '51135724', 'Hub Group', 'Hub Group', 'Short Form Delivery Receipt', NULL::integer),
 (7, 8, '51154026', 'Hub Group', 'Hub Group', 'Short Form Delivery Receipt', NULL::integer),
 (9, 10, '51145269', 'Hub Group', 'Hub Group', 'Short Form Delivery Receipt', NULL::integer),
 (11, 13, 'R1161760', 'Hub Group', 'Hub Group', 'Short Form Bill of Lading', NULL::integer),
 (14, 16, 'R1158182', 'Hub Group', 'Hub Group', 'Short Form Bill of Lading', NULL::integer),
 (17, 18, '51154024', 'Hub Group', 'Hub Group', 'Short Form Delivery Receipt', NULL::integer),
 (19, 21, 'R1156110', 'Hub Group', 'Hub Group', 'Short Form Bill of Lading', NULL::integer),
 (22, 24, 'R1157418', 'Hub Group', 'Hub Group', 'Short Form Bill of Lading', NULL::integer),
 (25, 27, 'R1166271', 'Hub Group', 'Hub Group', 'Short Form Bill of Lading', NULL::integer),
 (28, 30, 'R1163285', 'Hub Group', 'Hub Group', 'Short Form Bill of Lading', NULL::integer),
 (31, 33, 'R1169802', 'Hub Group', 'Hub Group', 'Short Form Bill of Lading', NULL::integer),
 (34, 36, 'R1166293', 'Hub Group', 'Hub Group', 'Short Form Bill of Lading', NULL::integer),
 (37, 39, 'R1166150', 'Hub Group', 'Hub Group', 'Short Form Bill of Lading', NULL::integer),
 (40, 42, 'R1162549', 'Hub Group', 'Hub Group', 'Short Form Bill of Lading', NULL::integer),
 (43, 45, 'R1139412', 'Hub Group', 'Hub Group', 'Short Form Bill of Lading', NULL::integer),
 (46, 48, 'R1169738', 'Hub Group', 'Hub Group', 'Short Form Bill of Lading', NULL::integer),
 (49, 51, 'R1169195', 'Hub Group', 'Hub Group', 'Short Form Bill of Lading', NULL::integer),
 (52, 53, '51152662', 'Hub Group', 'Hub Group', 'Short Form Delivery Receipt', NULL::integer),
 (54, 55, '51154029', 'Hub Group', 'Hub Group', 'Short Form Delivery Receipt', NULL::integer),
 (56, 58, 'R1166801', 'Hub Group', 'Hub Group', 'Short Form Bill of Lading', NULL::integer),
 (59, 61, 'R1169069', 'Hub Group', 'Hub Group', 'Short Form Bill of Lading', NULL::integer),
 (62, 64, 'R1162792', 'Hub Group', 'Hub Group', 'Short Form Bill of Lading', NULL::integer),
 (65, 66, '51123789', 'Hub Group', 'Hub Group', 'Short Form Delivery Receipt', NULL::integer)
) AS p(first_page,last_page,bol,payer,issuer,form,extraction_last_page)
WHERE t.id='11111111-1111-1111-1111-111111111111' AND lower(trim(t.name))='lykes cartage';

-- 20261109114930.pdf
INSERT INTO document_packet_mapping(tenant_id,file_sha256,first_page,last_page,bol,payer,issuer,form,extraction_last_page,nova_assigns_pro)
SELECT t.id,'A33D3799A2D80DC936A4DBD1695657314CAA7A433DC9230E77D5004E6195860B',p.*,true
FROM tenant t CROSS JOIN (VALUES
 (1, 1, 'DFW 3048520', 'ESSN HPE S4 ESN500', 'Pegasus Logistics Group', 'Waybill', NULL::integer),
 (2, 2, 'DFW 23700379', 'GE HEALTHCARE - EQUIPMENT', 'Pegasus Logistics Group', 'Waybill', NULL::integer),
 (3, 3, 'DFW 97138825', 'COMPUCOM C/O AFS', 'Pegasus Logistics Group', 'Waybill', NULL::integer),
 (4, 4, 'DFW 139111086', 'ECOLAB INC', 'Pegasus Logistics Group', 'Waybill', NULL::integer),
 (5, 5, 'DFW 23699020', 'TIDEL ENGINEERING LP', 'Pegasus Logistics Group', 'Waybill', NULL::integer),
 (6, 6, 'DFW 23699442', 'ENCORE GROUP (USA) LLC - DPC', 'Pegasus Logistics Group', 'Waybill', NULL::integer),
 (7, 7, 'DFW 23698088', 'PIAGGIO GROUP AMERICAS, INC.', 'Pegasus Logistics Group', 'Waybill', NULL::integer),
 (8, 8, 'DFW 97144337', 'ESSN HPE S4 ESN500', 'Pegasus Logistics Group', 'Waybill', NULL::integer),
 (9, 9, 'DFW 23698385', 'ENOVIS CORP (FORMERLY DJO GLOBAL)', 'Pegasus Logistics Group', 'Waybill', NULL::integer),
 (10, 10, 'DFW 23698398', 'ENOVIS CORP (FORMERLY DJO GLOBAL)', 'Pegasus Logistics Group', 'Waybill', NULL::integer),
 (11, 11, 'DFW 23698399', 'ENOVIS CORP (FORMERLY DJO GLOBAL)', 'Pegasus Logistics Group', 'Waybill', NULL::integer),
 (12, 12, 'DFW 23698400', 'ENOVIS CORP (FORMERLY DJO GLOBAL)', 'Pegasus Logistics Group', 'Waybill', NULL::integer),
 (13, 13, 'DFW 23698401', 'ENOVIS CORP (FORMERLY DJO GLOBAL)', 'Pegasus Logistics Group', 'Waybill', NULL::integer),
 (14, 16, '801087764', 'ECOLAB - RETURNS', 'Pegasus Logistics Group', 'Pick-up Alert', NULL::integer),
 (17, 26, 'PHX 3927997', 'Allstates WorldCargo', 'Allstates WorldCargo', 'Pick-up Alert', 18)
) AS p(first_page,last_page,bol,payer,issuer,form,extraction_last_page)
WHERE t.id='11111111-1111-1111-1111-111111111111' AND lower(trim(t.name))='lykes cartage';

-- 20261109115028.pdf
INSERT INTO document_packet_mapping(tenant_id,file_sha256,first_page,last_page,bol,payer,issuer,form,extraction_last_page,nova_assigns_pro)
SELECT t.id,'AF3394BDDC66976E664EA8C514F1BD2FCFED9CAFC1E77F626F99B1A9B9721EC0',p.*,true
FROM tenant t CROSS JOIN (VALUES
 (1, 1, '0018130350', 'Averitt Express', 'Averitt Express', 'Delivery Receipt', NULL::integer),
 (2, 2, '0760664502', 'Averitt Express', 'Averitt Express', 'Delivery Receipt', NULL::integer),
 (3, 3, '0012934630', 'Averitt Express', 'Averitt Express', 'Delivery Receipt', NULL::integer),
 (4, 4, '0760387920', 'Averitt Express', 'Averitt Express', 'Delivery Receipt', NULL::integer),
 (5, 5, '0697958036', 'Averitt Express', 'Averitt Express', 'Delivery Receipt', NULL::integer),
 (6, 6, '0368013203', 'Averitt Express', 'Averitt Express', 'Delivery Receipt', NULL::integer),
 (7, 7, '0090645025', 'Averitt Express', 'Averitt Express', 'Delivery Receipt', NULL::integer),
 (8, 8, '1009084003', 'Averitt Express', 'Averitt Express', 'Delivery Receipt', NULL::integer),
 (9, 9, '9509095152', 'Averitt Express', 'Averitt Express', 'Delivery Receipt', NULL::integer),
 (10, 10, '0879123194', 'Averitt Express', 'Averitt Express', 'Delivery Receipt', NULL::integer),
 (11, 11, '0697957983', 'Averitt Express', 'Averitt Express', 'Delivery Receipt', NULL::integer),
 (12, 12, '9506204413', 'Averitt Express', 'Averitt Express', 'Delivery Receipt', NULL::integer),
 (13, 13, '0415394486', 'Averitt Express', 'Averitt Express', 'Delivery Receipt', NULL::integer),
 (14, 14, '9508114556', 'Averitt Express', 'Averitt Express', 'Delivery Receipt', NULL::integer),
 (15, 15, '0658170118', 'Averitt Express', 'Averitt Express', 'Delivery Receipt', NULL::integer)
) AS p(first_page,last_page,bol,payer,issuer,form,extraction_last_page)
WHERE t.id='11111111-1111-1111-1111-111111111111' AND lower(trim(t.name))='lykes cartage';
COMMIT;
