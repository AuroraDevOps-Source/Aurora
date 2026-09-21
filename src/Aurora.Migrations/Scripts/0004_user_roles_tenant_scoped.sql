CREATE TABLE "AspNetUserRoles" (
    "UserId"   uuid NOT NULL,
    "RoleId"   uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("TenantId", "UserId", "RoleId"),
    CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AspNetUserRoles_user_tenant_TenantId_UserId" FOREIGN KEY ("TenantId", "UserId") REFERENCES user_tenant (tenant_id, user_id) ON DELETE CASCADE
);

CREATE INDEX "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" ("RoleId");
CREATE INDEX "IX_AspNetUserRoles_UserId" ON "AspNetUserRoles" ("UserId");

ALTER TABLE "AspNetUserRoles" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "AspNetUserRoles" FORCE ROW LEVEL SECURITY;

-- Role assignment is per-tenant (token contract: "roles within that tenant") — the same person
-- can be Admin in one tenant and Dispatcher in another.
CREATE POLICY user_role_tenant_isolation ON "AspNetUserRoles"
    USING      ("TenantId" = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK ("TenantId" = nullif(current_setting('app.tenant_id', true), '')::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON "AspNetUserRoles" TO aurora_app;
