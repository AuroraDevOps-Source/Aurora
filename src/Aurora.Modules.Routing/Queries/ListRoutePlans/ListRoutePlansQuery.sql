SELECT id AS "Id", name AS "Name", created_utc AS "CreatedUtc"
FROM route_plan
WHERE tenant_id = @TenantId
ORDER BY created_utc DESC;
