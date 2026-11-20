BEGIN;
ALTER TABLE document_packet_mapping ADD COLUMN handling_terminal text;
-- The Orlando packet is handled by Lykes MCO, including the BNA-origin carrier waybills.
UPDATE document_packet_mapping SET handling_terminal='MCO'
WHERE tenant_id='11111111-1111-1111-1111-111111111111'
  AND file_sha256='2BDE98C6EDAFD0EE74C65E2C08DAE66ED5AC84B9F806C669CB476EE0CFFD3C95';
COMMIT;
