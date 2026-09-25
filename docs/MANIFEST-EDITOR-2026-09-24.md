# Manifest editor

Release 20260924-10 adds Edit Manifest and Route tabs below the selected manifest. Edit is the initial tab; Route contains the existing bird’s-eye map and ordered stops.

Reviewed Nova 2.0 Domain/Entities.cs Manifest and dispatch/ManifestDetailPage.tsx. Nova carries date/direction/terminals, driver and equipment assignments, and notes at manifest level; bill-to is a shipment/account field. Aurora includes bill-to code/name and carrier as requested, plus manifest date, direction, origin/destination terminal, driver/trailer reference text, and notes. These are local manifest details, not Nova synchronization or customer/driver directory links. Optimized truck assignment and route sequence are read-only.

PUT /api/v1/aurora/manifests/{id} validates text limits/direction, scopes by tenant and relies on RLS, and uses an optimistic revision to reject concurrent overwrites. Metadata is stored under ManifestDetails and ManifestRevision in the existing JSON data column; no schema migration. Existing route deserialization ignores those additional properties. Unsaved edits disable switching manifests/Route or manual refresh until Save/Cancel; focus refresh skips dirty edits.

Build and release publish passed. 39 workspace checks passed in an isolated PostgreSQL database, including metadata persistence/reload, unchanged route snapshots, stale edit rejection, tenant isolation, and field limits. Test database removed after success. Standard test reset remains active.

Archive SHA256: 54a2c8b4f1ab24d87dd8f01f7c2cccfa4967f4dd946613489d8cf8d48224607b
