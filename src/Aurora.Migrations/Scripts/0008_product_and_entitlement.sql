-- Product catalogue + per-tenant entitlements.
--
-- This is what makes one login serve several applications: a tenant sees a product on the
-- launcher only if it has an active row here, and Aurora only mints that product's roles into
-- a token when the entitlement exists. Access control therefore falls out of role emission —
-- no separate licence check is needed inside FreightOps or the Hub.

-- Global reference data (the same three products for everyone) — no tenant_id, no RLS.
CREATE TABLE product (
    code         text NOT NULL,
    name         text NOT NULL,
    description  text,
    -- Prefix Aurora stamps onto this product's role names, e.g. 'fo' -> 'fo:Manager'.
    -- Each application strips its own prefix and ignores every other product's roles,
    -- so "Admin" in the Hub never implies "Admin" in FreightOps.
    role_prefix  text NOT NULL,
    -- Audience Aurora stamps into tokens for this product, so each application can validate
    -- that a token was actually meant for it rather than accepting anything Aurora signed.
    api_audience text NOT NULL,
    sort_order   integer NOT NULL DEFAULT 0,
    CONSTRAINT pk_product PRIMARY KEY (code),
    CONSTRAINT uq_product_role_prefix UNIQUE (role_prefix)
);

INSERT INTO product (code, name, description, role_prefix, api_audience, sort_order) VALUES
    ('aurora',     'Aurora',          'Transportation management',       'aurora', 'aurora-api',     1),
    ('freightops', 'FreightOps',      'Dock, manifests and operations',  'fo',     'freightops-api', 2),
    ('hub',        'Integration Hub', 'Connectors and data integration', 'hub',    'hub-api',        3);

-- Which tenant bought what.
--
-- instance_url matters because FreightOps is deployed once per customer rather than as one
-- shared multi-tenant install: the launcher needs to know which deployment to send this
-- tenant to. Products that run as a single shared instance leave it null and fall back to
-- the configured default URL.
CREATE TABLE tenant_product (
    tenant_id     uuid        NOT NULL,
    product_code  text        NOT NULL,
    is_active     boolean     NOT NULL DEFAULT true,
    instance_url  text,
    purchased_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_tenant_product PRIMARY KEY (tenant_id, product_code),
    CONSTRAINT fk_tenant_product_tenant  FOREIGN KEY (tenant_id)    REFERENCES tenant (id)  ON DELETE CASCADE,
    CONSTRAINT fk_tenant_product_product FOREIGN KEY (product_code) REFERENCES product (code)
);

ALTER TABLE tenant_product ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenant_product FORCE  ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON tenant_product
    USING      (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);

GRANT SELECT ON product TO aurora_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON tenant_product TO aurora_app;
