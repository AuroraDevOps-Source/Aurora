-- Existing configured connections are the source of the FreightOps launcher address.
-- Keep entitlements and unrelated product URLs unchanged.
UPDATE tenant_product p
SET instance_url = s.freightops->>'ApiUrl'
FROM tenant_settings s
WHERE p.tenant_id = s.tenant_id
  AND p.product_code = 'freightops'
  AND NULLIF(s.freightops->>'ApiUrl', '') IS NOT NULL;
