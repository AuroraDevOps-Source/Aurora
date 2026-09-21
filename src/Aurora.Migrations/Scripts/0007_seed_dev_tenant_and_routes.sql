-- Dev-only seed data: plain column values, safe as raw SQL. The OpenIddict client application
-- and the dev admin user are NOT seeded here — both have internal formats (permission/redirect
-- URI serialization, password hashing) that should go through their own manager APIs rather
-- than being hand-rolled in SQL. See Aurora.Migrations/Seeding/DevDataSeeder.cs.

INSERT INTO tenant (id, name, slug, is_active, created_utc)
VALUES ('11111111-1111-1111-1111-111111111111', 'Aurora Demo Co', 'aurora-demo', true, now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO route_plan (tenant_id, id, name, created_utc)
VALUES
    ('11111111-1111-1111-1111-111111111111', gen_random_uuid(), 'I-80 Chicago to Omaha', now()),
    ('11111111-1111-1111-1111-111111111111', gen_random_uuid(), 'I-40 Memphis to Amarillo', now())
ON CONFLICT DO NOTHING;
