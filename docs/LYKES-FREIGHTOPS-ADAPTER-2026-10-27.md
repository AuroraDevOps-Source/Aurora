# FreightOps adapter: Aurora-side HTTP transport

Follow-on implementation: [FreightOps endpoints and initial Lykes mapping](LYKES-ENDPOINTS-AND-MAPPING-2026-10-27.md). The target endpoints described below are now implemented locally for that bounded profile; this document records the earlier transport increment.

Implemented locally on October 27, 2026. Nothing deployed, no live FreightOps connection enabled, and no changes to FreightOps's existing HTTP endpoints or import behavior in this increment.

## Implemented

`Aurora.Integrations.FreightOps` implements `ISyncProvider` and `ISyncAcknowledgement` for the existing `SyncRunner`. It sends versioned JSON over authenticated HTTP, validates confirmations, reads bounded pending-event batches, and acknowledges exact event IDs only after the runner's import transaction succeeds.

Migration `0043_freightops_sync_requests.sql` adds an immutable, tenant-isolated prepared-request table. `FreightOpsRequestStore` verifies an enabled FreightOps connection, source identity, outbox content, and current worker lease. It freezes the native ID, expected revision, and payload before the first HTTP request. A new worker retries that same request after a lost response even if an intervening import changed the mapping. Concurrent preparations serialize on the connection and return the same stored request. Application credentials cannot update or delete that ledger.

The ledger intentionally stores JSON as text: PostgreSQL JSONB may reorder properties or rewrite number spelling, which would change a hash over the wire representation. Do not rewrite prepared requests or purge them before their corresponding delivery is reconciled. Administrative retention must delete ledger entries before deleting referenced outbox rows.

The adapter uses a bearer-token callback per request, allowing a host to supply refreshed credentials. It requires HTTPS except on loopback, disables redirects and cookies, bounds request/response size to 4 MiB, and imposes a 60-second deadline covering headers and body. It persists no remote response body in error diagnostics.

The live scheduler still runs Nova only. The new adapter is explicitly constructed by a host/test; no API registration, credential provisioning, background enablement, or production feature switch was added.

## Target v1 contract to implement in FreightOps

The HTTP checks use a loopback test server. These target endpoints are a defined contract, **not yet implemented FreightOps production endpoints**:

| POST route under the installation URL | Request / success response |
|---|---|
| `api/aurora-sync/v1/changes` | `FreightOpsWrite` / `FreightOpsReceipt` |
| `api/aurora-sync/v1/pull` | `FreightOpsPull` / `FreightOpsBatch` |
| `api/aurora-sync/v1/acknowledge` | `FreightOpsAck` / `FreightOpsAckReceipt` |

All envelopes carry protocol version 1, the configured source identity, and the connection ID. The source identity must name a database/feed generation, not merely a mutable hostname. Restoring/replacing a source requires explicit reconciliation and a new generation identity. Mismatched envelopes fail before completion or acknowledgement.

For writes, the adapter sends an uppercase SHA-256 digest of the exact UTF-8 JSON request bytes in `X-Aurora-Request-Hash`. The target must verify that digest before processing and store it with the receipt. Receipts repeat the connection, source, change ID, hash, native ID, and committed target revision. Only HTTP 200 with a matching JSON receipt confirms delivery; 202 does not mean committed.

The target must authenticate the bearer credential and authorize its connection against the installation before receipt lookup or mutation. A connection ID in a request is not authorization. In one target transaction: check for an existing immutable receipt; reject reuse with different bytes; check expected version; apply permitted domain commands; commit required side effects/outgoing capture and the receipt together. Identical retries return the original receipt before checking the now-outdated target version. Null native ID/revision means create; existing mappings require both. Unsupported operations must fail explicitly. Never use force-update commands to bypass execution guards.

HTTP 409/412 become reviewable sync conflicts. HTTP 400/422 become invalid-operation/mapping failures. Authentication failures, throttling, outages, redirects, and unconfirmed success statuses remain retryable through the existing queue. Remote error bodies are deliberately omitted from persisted diagnostics.

Pull returns at most 200 immutable events with globally unique IDs within the source generation, native IDs, revisions, agreed projection data, and deletion flags. The target must durably capture all required writers; raw database sequence watermarks are insufficient because transactions can commit out of order. Pull always rereads pending events. Checkpoints describe exact acknowledged IDs, not a highest sequence number. Acknowledgement is idempotent and affects only the authorized connection's listed events. Late lower-numbered events remain eligible.

`Data` must be the complete agreed projection for the entity/mapping version. This transport preserves explicit nulls, stable IDs, and unknown/source-only values supplied in that projection, but it does not invent field mappings or turn a partial partner DTO into a safe full snapshot. Existing Aurora merges still treat arrays atomically. Line-level merging and business ownership rules remain part of the next mapping increment.

## Local validation

Final local run: **48 HTTP checks and 45 database/runner checks passed**. Both projects build without warnings. Logs are in `artifacts/lykes-foundation/adapter-http-results.log` and `adapter-database-results.log`.

- `dotnet run --project tests/FreightOpsAdapterChecks`: actual loopback HTTP requests; lost-response retry, request/receipt identity, authentication header, redirects, status handling, source replacement, malformed/oversized responses, clear values, line IDs, exact acknowledgements, tombstones, cancellation, and URL validation.
- `dotnet run --project tests/SyncFoundationChecks`: fresh local PostgreSQL database, production migrations, application-role RLS, immutable preparation, lease/tenant checks, concurrent preparation, restart after newer baseline, and the actual queue/importer/runner over loopback HTTP. Tests confirm onward Nova queuing, acknowledgement after commit, replay deduplication, and no acknowledgement after failed import.

The database harness requires `SYNC_FOUNDATION_TEST_CONNECTION` targeting an empty local `aurora_sync_check_*` database and the existing `aurora_app` role. It refuses nonlocal or nonempty databases. No production/staging data was used. The test target's in-memory receipt is intentionally a transport fixture; it does not prove Marten business-write atomicity.

## Next integration step

Implement the three FreightOps target endpoints with installation-bound machine authentication, durable capture, and transactional receipts using the existing identity-adoption work as a starting point. Add an agreed Lykes order/line mapping and target domain tests before connecting the adapter to the scheduler. Keep the legacy TMS import endpoint unchanged. PostgreSQL 12 staging compatibility, Nova ID persistence, repeated SKUs, split lineage, and full operational round trips remain activation gates from the full plan.
