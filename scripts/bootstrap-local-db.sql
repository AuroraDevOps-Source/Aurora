-- One-time manual bootstrap, run ONCE as the Postgres superuser before the migrator's first run.
-- Not tracked by DbUp: DbUp connects as aurora_owner, which doesn't exist yet the very first
-- time anything runs, so role/database creation has to happen outside its numbered script chain
-- (the same posture production will want too — see the plan's "Database & roles" section).
--
-- Run with (adjust host/port if needed):
--   & "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -f "D:\Development\Aurora\scripts\bootstrap-local-db.sql"

CREATE ROLE aurora_owner LOGIN BYPASSRLS PASSWORD 'VegA23q6phxksGCtWPNFaSjn';
CREATE ROLE aurora_app   LOGIN          PASSWORD 'JZHNVzE3XiOeTsrBY0vKoD2R';
CREATE ROLE aurora_admin LOGIN BYPASSRLS PASSWORD 'jBYOrCMJbhVwt0D267mef4aS';

CREATE DATABASE aurora OWNER aurora_owner;
