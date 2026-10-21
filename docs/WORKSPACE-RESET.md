# Testing reset

Company administrators can open Admin → Reset. Review the manifest and draft counts, type `RESET`, and select **Reset testing data**. Publishing a release does not run this action.

The reset deletes this company's Aurora-created manifests, planning drafts and saved route plans; clears test workflows; returns manifest orders to Ready to Route; and removes encrypted sessions associated with deleted drafts. Nova manifest routing stops, membership and driver links are removed by native delete triggers. Source delivery records remain, with manifest assignment and execution times cleared. Company setup, customers, trucks, terminals, users, source orders and activity history remain.

Native manifests are identified by the company's Aurora export reference or the creation reference now added to manually created manifests. Pre-existing Nova manifests, another company's manifests, and older manually created manifests without a reference are preserved. An order shared with a preserved manifest blocks reset rather than changing that manifest's orders.

The API requires the Aurora Admin role and a matching preview revision. Native cleanup uses one Firebird transaction; local cleanup uses one tenant-scoped PostgreSQL transaction. Native commits first. After a connection or commit interruption, reload the preview and retry to finish remaining cleanup. No distributed transaction is claimed.

The retired Import page, sample download, import contracts/store and replacement endpoints have been removed.
