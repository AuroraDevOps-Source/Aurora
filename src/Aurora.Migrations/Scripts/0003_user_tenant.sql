CREATE TABLE user_tenant (
    tenant_id   uuid        NOT NULL,
    user_id     uuid        NOT NULL,
    is_default  boolean     NOT NULL DEFAULT false,
    joined_utc  timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_user_tenant PRIMARY KEY (tenant_id, user_id),
    CONSTRAINT fk_user_tenant_user   FOREIGN KEY (user_id)   REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    CONSTRAINT fk_user_tenant_tenant FOREIGN KEY (tenant_id) REFERENCES tenant (id)          ON DELETE CASCADE
);

CREATE UNIQUE INDEX ix_user_tenant_one_default_per_user ON user_tenant (user_id) WHERE is_default = true;

ALTER TABLE user_tenant ENABLE ROW LEVEL SECURITY;
ALTER TABLE user_tenant FORCE ROW LEVEL SECURITY;

-- Scoped by user_id, NOT tenant_id: this table answers "which tenants can I see", which must
-- be queryable before any tenant is known (see AuthorizationController.Authorize). A user can
-- see their own membership rows regardless of which tenant (if any) is active in the session;
-- they can never see another user's rows. A future "list everyone in my tenant" admin screen
-- will need a second, additional policy for that case — out of scope for this slice.
CREATE POLICY user_tenant_self_isolation ON user_tenant
    USING      (user_id = nullif(current_setting('app.user_id', true), '')::uuid)
    WITH CHECK (user_id = nullif(current_setting('app.user_id', true), '')::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON user_tenant TO aurora_app;
