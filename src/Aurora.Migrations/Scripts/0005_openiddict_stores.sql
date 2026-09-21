-- Captured via `dotnet ef migrations script` (UseOpenIddict<Guid>() in AuroraDbContext), then
-- hand-augmented with the grants below. Platform auth infrastructure shared by every tenant:
-- no tenant_id, no RLS. OpenIddict's own store issues plain queries with no app.tenant_id
-- awareness — RLS here would break OpenIddict, not add isolation (a user's own tokens are
-- already scoped by who's asking, at the application layer, not by a DB-row tenant filter).

CREATE TABLE "OpenIddictApplications" (
    "Id" uuid NOT NULL,
    "ApplicationType" character varying(50),
    "ClientId" character varying(100),
    "ClientSecret" text,
    "ClientType" character varying(50),
    "ConcurrencyToken" character varying(50),
    "ConsentType" character varying(50),
    "DisplayName" text,
    "DisplayNames" text,
    "JsonWebKeySet" text,
    "Permissions" text,
    "PostLogoutRedirectUris" text,
    "Properties" text,
    "RedirectUris" text,
    "Requirements" text,
    "Settings" text,
    CONSTRAINT "PK_OpenIddictApplications" PRIMARY KEY ("Id")
);

CREATE TABLE "OpenIddictScopes" (
    "Id" uuid NOT NULL,
    "ConcurrencyToken" character varying(50),
    "Description" text,
    "Descriptions" text,
    "DisplayName" text,
    "DisplayNames" text,
    "Name" character varying(200),
    "Properties" text,
    "Resources" text,
    CONSTRAINT "PK_OpenIddictScopes" PRIMARY KEY ("Id")
);

CREATE TABLE "OpenIddictAuthorizations" (
    "Id" uuid NOT NULL,
    "ApplicationId" uuid,
    "ConcurrencyToken" character varying(50),
    "CreationDate" timestamp with time zone,
    "Properties" text,
    "Scopes" text,
    "Status" character varying(50),
    "Subject" character varying(400),
    "Type" character varying(50),
    CONSTRAINT "PK_OpenIddictAuthorizations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_OpenIddictAuthorizations_OpenIddictApplications_Application~" FOREIGN KEY ("ApplicationId") REFERENCES "OpenIddictApplications" ("Id")
);

CREATE TABLE "OpenIddictTokens" (
    "Id" uuid NOT NULL,
    "ApplicationId" uuid,
    "AuthorizationId" uuid,
    "ConcurrencyToken" character varying(50),
    "CreationDate" timestamp with time zone,
    "ExpirationDate" timestamp with time zone,
    "Payload" text,
    "Properties" text,
    "RedemptionDate" timestamp with time zone,
    "ReferenceId" character varying(100),
    "Status" character varying(50),
    "Subject" character varying(400),
    "Type" character varying(150),
    CONSTRAINT "PK_OpenIddictTokens" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_OpenIddictTokens_OpenIddictApplications_ApplicationId" FOREIGN KEY ("ApplicationId") REFERENCES "OpenIddictApplications" ("Id"),
    CONSTRAINT "FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId" FOREIGN KEY ("AuthorizationId") REFERENCES "OpenIddictAuthorizations" ("Id")
);

CREATE UNIQUE INDEX "IX_OpenIddictApplications_ClientId" ON "OpenIddictApplications" ("ClientId");
CREATE INDEX "IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type" ON "OpenIddictAuthorizations" ("ApplicationId", "Status", "Subject", "Type");
CREATE UNIQUE INDEX "IX_OpenIddictScopes_Name" ON "OpenIddictScopes" ("Name");
CREATE INDEX "IX_OpenIddictTokens_ApplicationId_Status_Subject_Type" ON "OpenIddictTokens" ("ApplicationId", "Status", "Subject", "Type");
CREATE INDEX "IX_OpenIddictTokens_AuthorizationId" ON "OpenIddictTokens" ("AuthorizationId");
CREATE UNIQUE INDEX "IX_OpenIddictTokens_ReferenceId" ON "OpenIddictTokens" ("ReferenceId");

GRANT SELECT, INSERT, UPDATE, DELETE ON "OpenIddictApplications", "OpenIddictScopes", "OpenIddictAuthorizations", "OpenIddictTokens" TO aurora_app;
