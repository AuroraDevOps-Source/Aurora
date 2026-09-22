-- Aurora TMS joins the product catalogue.
--
-- Nothing else is needed to make it a first-class member of the single sign-on family: the
-- authorization endpoint already mints roles and audiences by joining product to tenant_product,
-- so this row is what makes 'atms:Dispatcher' issuable and 'auroratms-api' an addressable audience.
--
-- The role prefix is 'atms' rather than 'aurora' because 'aurora' already belongs to the Route
-- Optimization product in this table, and uq_product_role_prefix rightly refuses a second claim on
-- it. Two products sharing a prefix would be worse than a rejected insert: each application strips
-- its own prefix and ignores the rest, so a collision would silently make every Route Optimization
-- role a TMS role of the same name.
--
-- No tenant is entitled here. Granting the product is a per-customer decision (an INSERT into
-- tenant_product with that customer's instance_url) — see docs/single-sign-on.md.

INSERT INTO product (code, name, description, role_prefix, api_audience, sort_order) VALUES
    ('auroratms', 'Aurora TMS', 'Dispatch, shipments and billing', 'atms', 'auroratms-api', 4)
ON CONFLICT (code) DO NOTHING;

-- The TMS role vocabulary, namespaced like every other product's (script 0009).
--
-- Only the four office roles are issuable through Aurora. Agent, Jockey and PortalUser are
-- deliberately absent: those sessions are minted by the TMS itself for a delivery agent, a shared
-- yard device and an external customer contact, each with its own narrow token and its own login
-- path. Handing them out through the launcher would put a scoped, device-bound session behind an
-- ordinary office sign-in, which is the opposite of what makes them safe.
INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid(), v.name, upper(v.name), gen_random_uuid()::text
FROM (VALUES
    ('atms:Admin'),
    ('atms:Dispatcher'),
    ('atms:Billing'),
    ('atms:User')
) AS v(name)
WHERE NOT EXISTS (
    SELECT 1 FROM "AspNetRoles" r WHERE r."NormalizedName" = upper(v.name)
);
