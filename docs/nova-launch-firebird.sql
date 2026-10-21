-- Installed and verified on 172.23.0.3:D:\DB\TEST\OPERATIONS.FDB, 2026-10-15.
-- Reference for other installations; do not rerun against TEST.
-- This is not an Aurora/PostgreSQL migration. Do not apply automatically to Nova.
CREATE TABLE AURORA_LAUNCH_TICKET (
    TICKET_HASH CHAR(64) CHARACTER SET ASCII NOT NULL PRIMARY KEY,
    NOVA_USER_ID VARCHAR(128) NOT NULL,
    CREATED_AT TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
    EXPIRES_AT TIMESTAMP NOT NULL,
    CONSTRAINT CK_AURORA_LAUNCH_EXPIRY CHECK (
        EXPIRES_AT > CREATED_AT AND EXPIRES_AT <= DATEADD(60 SECOND TO CREATED_AT))
);
CREATE INDEX IX_AURORA_LAUNCH_EXPIRY ON AURORA_LAUNCH_TICKET(EXPIRES_AT);

-- TEST connection login, provisioned on 172.23.0.3:
GRANT SELECT, DELETE ON AURORA_LAUNCH_TICKET TO USER AURORA_TEST_APP;
-- For other installations, substitute Aurora's configured Firebird login.
-- Grant ticket creation only through a trusted issuance path. A shared Nova DB
-- login with unrestricted INSERT access can forge another Nova user's identity.
-- Implement a procedure validating the current Nova user's credentials against
-- USERS before inserting, and prevent that client login from inserting/updating
-- tickets directly. Do not use a procedure that simply trusts a caller-supplied ID.
-- If Nova uses an owner/SYSDBA connection, provision a restricted login/issuer
-- before enabling Aurora mappings. DB administrators remain trusted.
