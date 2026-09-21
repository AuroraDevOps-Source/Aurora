-- Product-scoped role vocabulary.
--
-- Roles are namespaced by product ('fo:Manager', 'hub:Admin') rather than flattened into a
-- shared set. Each application strips its own prefix at token-validation time, so every
-- existing [Authorize(Roles = "Manager,Admin")] in FreightOps and RequireRole("Admin") in the
-- Hub keeps working unchanged — and an Admin in one product is never an Admin in another.
--
-- FreightOps keeps its own vocabulary (Admin/Manager/Dock) and the Hub keeps its own
-- (Admin/User); nothing had to be renamed in either application.

-- The role seeded before products existed becomes Aurora's own admin role. Renaming rather
-- than replacing keeps the existing AspNetUserRoles assignment intact (same role id).
UPDATE "AspNetRoles"
SET    "Name" = 'aurora:Admin', "NormalizedName" = 'AURORA:ADMIN'
WHERE  "NormalizedName" = 'ADMIN';

INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid(), v.name, upper(v.name), gen_random_uuid()::text
FROM (VALUES
    ('aurora:Admin'),
    ('fo:Admin'),
    ('fo:Manager'),
    ('fo:Dock'),
    ('hub:Admin'),
    ('hub:User')
) AS v(name)
WHERE NOT EXISTS (
    SELECT 1 FROM "AspNetRoles" r WHERE r."NormalizedName" = upper(v.name)
);
