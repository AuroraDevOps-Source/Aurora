# Local copy of the Aurora test database

Copied September 27, 2026 from the Aurora test installation (`aurora-auth-db`, database `aurora`). The source database was read only; no test-server application or data changes were made.

- Local PostgreSQL 18 instance: `127.0.0.1:55432`, database `aurora_test`.
- The existing PostgreSQL service and `aurora` database on port 5432 were preserved.
- Data, the custom-format backup, restore metadata, and logs are under the Git-ignored, access-restricted `deploy/private/local-test` directory.
- All 24 table row counts matched immediately after restoration: 100 orders, 6 manifests, 20 equipment types, 285 equipment units, and the existing account.
- Migration `0014_tms_crud.sql` was then applied locally to match the working tree. Development seeding remains disabled.
- API and migration connections are configured in .NET user secrets with separate app/owner roles. The app role is neither superuser nor BYPASSRLS.
- Only the local copy's Aurora SPA callbacks and Aurora product URL were changed to localhost. The API uses local development signing certificates. Existing account credentials were retained.
- PTV settings were copied into API user secrets. Optimization uses the real PTV service when requested; verification did not submit an optimization.

Run `./scripts/start-local.ps1` in PowerShell, then open [local Aurora Orders](https://localhost:7259/aurora/orders). The database must be started again after a reboot; this script starts it and the API/client. It does not reset or re-copy data.

Sign in with the same account used on the test server. Local password login, PKCE authorization/token exchange, and authenticated orders, manifests, equipment, and product API reads were verified. Other product deployments (FreightOps, Hub, and Nova) were not copied locally or reconfigured.

The old `http://localhost:5186` address is a synthetic UI harness; use port 7259 for real local testing.
