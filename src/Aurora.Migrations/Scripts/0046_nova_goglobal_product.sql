-- GO-Global hosts the existing Nova desktop. Its Windows sign-in remains independent.
-- Tenant-specific URLs are provisioned explicitly, never copied from Nova 2.0.
INSERT INTO product(code,name,description,role_prefix,api_audience,sort_order)
VALUES('nova','Nova','Nova desktop through GO-Global','nova','nova-api',4)
ON CONFLICT(code) DO NOTHING;
