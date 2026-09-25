# Test publishing data reset

Bryan requested on September 24, 2026: every test-site publication resets orders/plans, keeping trucks, logins and tenants.

The standard `scripts/deploy-aurora-release.py` now requires and runs `scripts/reset-staging-orders.py` before replacing the test containers. Always include both files in release packages; use current deployment scripts rather than copying them unchanged from an older release.

Scope: only Aurora Demo Co (11111111-1111-1111-1111-111111111111). Restore the original 100 appointment-test orders to ReadyToRoute (displayed as Ready to Ship). Clear its manifests, manifest assignments, planning drafts and stored optimization recovery sessions. Saved fleet, tenant records, users and all other tenants/products are retained.

The reset captures the original source/order baseline once in /opt/aurora-auth/private/demo-order-baseline.json, validates all 100 PRO numbers and tenant IDs, and keeps a protected PostgreSQL dump plus archived recovery files for every reset. The API stops during the reset; SQL changes use a transaction. User and truck data are compared before/after. Shared data-protection keys are never removed. Original test dates are retained; Clear filters shows them when the grid defaults to today.

Run `python3 /opt/aurora-auth/reset-staging-orders.py --check` for non-mutating preflight checks. This helper only targets the named test deployment and requires the expected demo tenant. It is not a production reset tool.

Already-open browser tabs must refresh to load a new client. The screenshot with Import and Available showed a pre-status-migration client; the old Available filter is rejected by the updated API. Nginx already serves the application with Cache-Control: no-cache.

Initial release: 20260924-6, also adds searchable inline order appointment expanders to the setup screen. Archive SHA256: 88e31cdb0e22fe9a56bba5acb19d2737db540ab4da7cd29b1c8f8ed49147ccef.

First reset succeeded during publish. Protected backup: /opt/aurora-auth/private/orders-pre-reset-20260924-063422. Verified 100 Ready to Ship orders and unchanged fleet/users.
