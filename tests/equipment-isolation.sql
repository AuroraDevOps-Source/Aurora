\set ON_ERROR_STOP on
BEGIN;
SELECT set_config('app.tenant_id','11111111-0000-4000-8000-111111111111',true);
INSERT INTO tenant(id,name,slug) VALUES ('11111111-0000-4000-8000-111111111111','Equipment test A','equipment-test-a');
INSERT INTO routing_equipment_type(tenant_id,code,data) VALUES ('11111111-0000-4000-8000-111111111111','TEST','{}');
INSERT INTO routing_equipment_unit(tenant_id,id,type_code,data) VALUES ('11111111-0000-4000-8000-111111111111','UNIT','TEST','{}');
SELECT set_config('app.tenant_id','22222222-0000-4000-8000-222222222222',true);
INSERT INTO tenant(id,name,slug) VALUES ('22222222-0000-4000-8000-222222222222','Equipment test B','equipment-test-b');
INSERT INTO routing_equipment_type(tenant_id,code,data) VALUES ('22222222-0000-4000-8000-222222222222','TEST','{}');
INSERT INTO routing_equipment_unit(tenant_id,id,type_code,data) VALUES ('22222222-0000-4000-8000-222222222222','UNIT','TEST','{}');
SELECT set_config('app.tenant_id','11111111-0000-4000-8000-111111111111',true);
DO $$ BEGIN
    IF (SELECT count(*) FROM routing_equipment_type) <> 1 OR (SELECT count(*) FROM routing_equipment_unit) <> 1 THEN RAISE EXCEPTION 'Tenant isolation failed'; END IF;
    UPDATE routing_equipment_unit SET data='{"changed":true}' WHERE tenant_id='22222222-0000-4000-8000-222222222222';
    IF FOUND THEN RAISE EXCEPTION 'Cross-tenant update was allowed'; END IF;
    BEGIN
        INSERT INTO routing_equipment_unit(tenant_id,id,type_code,data) VALUES ('22222222-0000-4000-8000-222222222222','BAD','TEST','{}');
        RAISE EXCEPTION 'Cross-tenant insert was allowed';
    EXCEPTION WHEN insufficient_privilege THEN NULL;
    END;
END $$;
SELECT set_config('app.tenant_id','',true);
DO $$ BEGIN
    IF EXISTS(SELECT FROM routing_equipment_type) OR EXISTS(SELECT FROM routing_equipment_unit) THEN RAISE EXCEPTION 'Missing tenant context exposed equipment'; END IF;
END $$;
ROLLBACK;
SELECT 'Equipment tenant isolation, cross-tenant write rejection and missing-context checks passed' AS result;
