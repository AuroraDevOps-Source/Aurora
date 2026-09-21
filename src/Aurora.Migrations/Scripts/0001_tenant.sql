CREATE TABLE tenant (
    id           uuid        NOT NULL DEFAULT gen_random_uuid(),
    name         text        NOT NULL,
    slug         text        NOT NULL,
    is_active    boolean     NOT NULL DEFAULT true,
    created_utc  timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_tenant PRIMARY KEY (id)
);

CREATE UNIQUE INDEX ix_tenant_slug ON tenant (slug);

ALTER TABLE tenant ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenant FORCE ROW LEVEL SECURITY;

-- Self-referential: a tenant can see its own row, not enumerate others. Not strictly mandated
-- by the RLS convention (which is phrased around a tenant_id column this table doesn't have),
-- but cheap and closes an obvious hole.
CREATE POLICY tenant_self_isolation ON tenant
    USING      (id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (id = nullif(current_setting('app.tenant_id', true), '')::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON tenant TO aurora_app;
