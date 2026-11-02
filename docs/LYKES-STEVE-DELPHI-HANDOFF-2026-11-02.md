# Lykes expanded sync: Delphi handoff for Steve

## Status and scope

The Lykes test database now has nullable `OPS_STOP_REC.OPS_ETA_TIME TIMESTAMP`. No backfill or population rule has been enabled. Appointment/ready/close times retain their existing fields. ETA should initially represent planned final-mile delivery and respect the appointment window; network forecasts and SLA/holiday logic remain separate future work.

Nova full reverse freight writes are blocked pending stable line identity rollout. The deployed MVP identifies operational pieces in Aurora without claiming those identifiers are stable native Nova rows. Do not simply flip NativeReadOnly off: existing baselines and queued payloads must be reconciled first.

## Requested Delphi work (Steve owns implementation)

1. Add optional OPL_SYNC_ID to the order-line object and persistence mapping. Use a UUID for a newly created logical line. Preserve it when editing, resequencing, saving, or deleting/reinserting the same logical line. A copied/new shipment must receive new line IDs. Older databases without the column must remain usable.
2. Carry this identity through the rating request/response DTOs and rating transformations. Preserve the original identity only for the same logical line; reject missing/duplicate/ambiguous identity before overwriting identified native lines. Do not infer identity from row position, sequence, or SKU. Do not alter rating calculations or financial rules.
3. A true delete stays deleted; a newly added line gets a new identity. Ensure tombstones distinguish replacement/reordering from actual deletion in the supported workflow.
4. If straightforward, display OPS_ETA_TIME read-only in order entry. It is date and time, nullable. No required value, default, or rating dependency. Population is not yet enabled.

`src/Aurora.Integrations.Nova/Sync/NovaStableLines.patch` is an existing draft for review, not a verified build or a request to apply blindly. `StableLineIds.sql`, `LineTombstones.sql`, and `OrderKeys.sql` are additive database preparation, but do not install/activate full reverse mode before the writer rollout and reconciliation below.

## Integration rollout after Steve's build

- Verify the build on the Lykes TEST database only, including save, rerate, reorder, add, delete, copy, and cancellation paths. Existing financial amounts and rating behavior must remain correct.
- Pause the Lykes worker, reconcile native logical lines with the existing operational identities and FreightOps barcodes explicitly. Abort on ambiguity. Record mapping evidence; do not regenerate existing piece identities.
- Adopt native UUIDs for charge lines separately. Migrate baselines and any unsent snapshots coherently; retain audit records and idempotent external receipts.
- Exercise stale-revision conflicts, retries, duplicate deliveries, and transactional rollback before enabling reverse writes. Preserve rated/billed/assigned/order-lifecycle guards.
- Enable narrowly for the prepared test PROs, test both directions, then expand. Creating a new ETA column does not authorize disabling rating or native-state safeguards.

## Email draft (not sent)

Hey Steve,

We’ve added OPS_STOP_REC.ops_eta_time to the Lykes test DB as a nullable timestamp, so it holds both date and time. We’ll leave it blank until we agree on how to populate it. Mike’s final-mile planned delivery approach makes sense for the first pass.

To finish full two-way freight sync, we also need a persistent UUID on each Nova line (OPL_SYNC_ID). It needs to survive edits, saves, resequencing, and rating round trips. Truly new/copied lines get new IDs; the same logical line keeps its ID. We don’t want any rating calculations changed—just the identity carried through safely.

There’s a draft patch and rollout checklist ready for your review. Could you handle the Delphi side and let us know when the test build is ready? We’ll do the database adoption, mapping reconciliation, and end-to-end sync tests after that. If it’s easy, showing ETA read-only in order entry would be useful too.

Thanks,
Bryan
