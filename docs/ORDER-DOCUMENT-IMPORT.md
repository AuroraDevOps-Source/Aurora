# Order document import

Data Entry has two input panels: **Upload document** (browse or drop a file) and **Paste text**. The active source is highlighted; Remove/Clear switches back to the other source. **Convert to orders** shows progress, then opens the grouped editable review dialog. Pasted content is sent as UTF-8 `pasted-orders.txt` through the same conversion endpoint. Both sources are limited to 10 MiB; blank text and empty files cannot be submitted. Extraction does not create orders: users review the results and explicitly select Create. Duplicate alerts disable Create while a flagged order remains.

Local order creation uses `POST /api/v1/tms/orders/import`; native workspaces use `POST /api/v1/aurora/firebird/orders/import`. Import does not use optimization planning-status mappings. Native orders default to the same status as manual Nova entry (110), with an editable Nova status in review. Work type only chooses which stop receives the service appointment. Optional shipper/consignee and terminal fields can be completed later; a selected terminal overrides the Tag prefix. Import bypasses routing-readiness and general order business validation, including the combined 4,000-character notes check. Duplicate protection, tenant ownership, numeric PRO parsing, native field types/column lengths and database reference checks remain. Normal manual-entry validation is unchanged.

## Recent imports

Migration `0051_order_import_history.sql` stores tenant/user ownership, a sanitized filename, creation time, extraction status/count, and distinct successfully created PROs. The authenticated history endpoint returns the latest 20 entries for the company without returning the PRO list. Source files, pasted text, and extracted address details are not retained by this history feature. History begins with this release; earlier imports are not reconstructed.

Statuses are Converting, Converted, Failed, and Orders created. Conversions still marked Converting after seven minutes display Interrupted. Orders created means at least one order was saved; the count shows how many were created out of those extracted. History is a record, not a resumable draft. Only the initiating user can attach an order to a batch; company members can read the company's recent history. Extracted batch IDs are discarded and replaced by server-generated IDs.

Successful local and native creates update history on the server; repeated updates for the same PRO do not double-count. A history-update failure is logged without turning an already successful business write into a failed response. In that exceptional case the displayed count can lag the orders list.

Supported uploads: PDF, CSV, TSV, TXT, XLSX, DOCX, JPEG, PNG, WebP, GIF, BMP and multi-page TIFF; maximum 10 MiB and 100 extracted orders. Image conversion requires the accompanying Integration-Hub update in `DocumentConvertService` (Magick.NET 15.0.0). Images are limited to 20 pages / 80 megapixels and bounded decoded PNG sizes.

## Configuration

For each authenticated Aurora tenant GUID, configure server-side secrets:

```
DocumentConversion__Tenants__<aurora-tenant-guid>__BaseUrl=https://your-hub-host
DocumentConversion__Tenants__<aurora-tenant-guid>__ServiceCredential=<credential-id>.<secret>
```

Provision that credential against the corresponding Hub client using Integration-Hub's `tools/New-DocumentServiceCredential.ps1` and `docs/Document-Conversion-Service-Authentication.md`. There is deliberately no shared fallback credential. Do not put secrets in the browser configuration or commit them. Deploy the Hub image support and Aurora API/client together.

Tenant administrators can edit **Settings → Document conversion → Extraction instructions**. The same tab is available in **Edit Tenant**. Choose a sample document and **Preview extraction** to inspect the fields returned by the Hub using the unsaved text. Preview runs are excluded from Recent imports. **Save** applies the instructions to the company's next upload or pasted-text conversion.

Conversion calls `POST /api/document-convert/service` with multipart `file` and `prompt` using the company's `DocumentService` credential. Aurora combines its required response format, the global extraction instructions, and the authenticated tenant's form-specific instructions. Aurora staff administrators can edit the global instructions under Settings → Document conversion. Tenant administrators can read the global instructions and edit their company instructions. Sample previews use unsaved edits. Migration `0055_global_document_settings.sql` seeds the Prepaid, Collect and Third Party BOL billing rules. Migration `0053_document_extraction_instructions.sql` copies the current carrier instructions to existing tenants. New tenants start with the shared format and rules, which are also used when the instructions are cleared. Recognized Crown XLSX reports use the built-in reader.

Conversion returns drafts for review. Users review missing identifiers/addresses and dates (a missing date defaults to today), select a terminal, and create orders with the selected Nova status (or the local workspace's initial status). Extracted ownership and lifecycle identifiers are discarded.

Create normalizes PRO numbers and checks duplicates before saving. Successfully saved rows are removed from the pending list; if a later save fails, its remaining rows stay available and the created count is shown. Existing order numbers are rejected by normal create validation, never overwritten. After an ambiguous network failure, check the orders list before retrying. Existing date/status/search filters still apply after import.

Checks: `dotnet run --project tests/OrderDocumentImportChecks`; Hub `DocumentImageTests` and existing `DocumentConvert` tests. Live conversion requires tenant service credentials and Hub AI configuration; sync delivery requires the existing enabled tenant sync connections.


## Tag mapping and duplicate protection (2026-11-02)

Extraction returns an array of `OrderDocumentDraft` objects. `TagNumber` retains the printed Tag, and `OrderDocumentDraft.SplitTag` derives a canonical positive numeric PRO (up to nine digits) and terminal prefix. For example `MCO3706592` becomes PRO `3706592` / terminal `MCO`. Leading zeroes are removed; embedded letters, negative numbers and malformed Tags are rejected rather than silently changing their identity. Each row selects its own terminal; missing/mismatching terminals must be corrected before create.

`BillOfLading` alone maps to `TmsOrder.Reference` / Nova `PBBOL`. `ShipmentReference`, `HouseAirWaybill` and `MasterAirWaybill` are reviewed separately and retained as labeled notes, with the original Tag. There is no identifier fallback. Extraction explicitly leaves blank labels blank, takes actual Weight rather than Dim Weight, and retains printed freight descriptions/dimensions in notes without fabricating lines or units. The existing Nova lifecycle writer's delivery-only and feature-switch restrictions remain in effect; extracting a pickup does not enable unsupported native writes.

`POST /api/v1/tms/orders/import/check` checks exact canonical PROs across all Aurora tenant sources (including deleted orders and outside screen filters), then queries the saved tenant Nova database's `OPS_HEADER` directly, including orders outside the sync window/pilot allowlist. A configured but unavailable Nova connection blocks import. Preview marks duplicates; Create rechecks all rows and refuses duplicates. The server rechecks each imported row and normal order creation repeats the Aurora duplicate query under the existing transaction/advisory lock. Nothing overwrites an existing order. Nova's existing writer also retains its final duplicate/reservation checks for a concurrent native create after preflight.

Seven PDF identities were manually reviewed: MCO3706592, DFW3696808, TPA3701517, AUS3698582, MCO3662120, IAH3704352, DFW3720309. Regression checks cover these mappings and blank BOL behavior. This is not a claim that live AI conversion of the samples has been tested: the deployed tenant still needs conversion service configuration.


## Native document creation — 2026-11-03 (historical)

The planning-status, required-name, prefix-match, and address-lookup behavior below is superseded by Import independence from optimization.

Supersedes the preview-only limitation. Add > Import now posts reviewed drafts to the native-only /api/v1/aurora/firebird/orders/import endpoint. It retains the existing all-source Aurora/Nova preflight and rechecks the PRO inside the native create transaction, serialized with the tenant routing advisory lock shared by Aurora native creates. It never updates an existing PRO. Native header, pickup and delivery stops, and document notes commit atomically. Configured work-type ready statuses are used; missing pickup configuration fails explicitly. Native field limits and address lookup are reused from Add > New.

The selected Nova terminal code is validated against the Tag prefix and LOCMAST; the dropdown GUID is UI-only. Numeric PRO, addresses, bill-to, totals and local service/appointment time map to native fields. Only explicitly extracted BOL maps to PBBOL/OPS_BOL. Original Tag, shipment reference, HAWB, MAWB and extracted freight descriptions remain labeled in PB_NOTES. This does not invent native freight-detail rows from header totals or expand the FreightOps pilot allowlist. Imported address-lookup warnings are now displayed after creation.

Configured a dedicated documents:convert service credential for Hub client 3 (Lykes), expiry 2027-02-01; secret exists only in backend configuration. Hub hash configuration preserves existing credentials. No Hub code republish was necessary. Prior configuration backup: C:/inetpub/publisher/hub-document-before-20261103.json on the Hub server; protect as secret material.

Validation: 44 mapping/import checks passed. Native tests created/read header and both stops then rolled back, rejected an existing PRO, and enforced field length limits. Live btx1.pdf was uploaded from Bryan's Desktop through Aurora's authenticated conversion endpoint and converted by Integration Hub. Reviewed PRO MCO3706592, 1 piece, 125 lb, shipper FORWARD AIR, consignee BRIAN ROBINSON. Set the reviewed service time to the printed delivery appointment (2026-10-15 11:00 America/New_York); raw extraction supplied midnight UTC. Two concurrent create submissions produced one HTTP 200 and one duplicate HTTP 400; subsequent retry returned HTTP 409. Nova PB_ID 1887 / PBNUM 3706592 has exactly two stops; blank BOL and both air waybills were verified directly in the database. The order is visible on native Aurora Orders. Address lookup reported consignee match score 86 below required 90; review the address before relying on its routing coordinates.

Browser automation could open the dialog but file upload was blocked by the extension's file-URL permission. The actual conversion/create test used the same authenticated HTTP endpoints; browser verification confirmed the resulting order. No claim of a fully automated browser upload is made.

Published API 20261103-2 and web 20261103-3. Backups: /opt/aurora-auth/backups/20261103-2 and /opt/aurora-auth/backups/20261103-3. API readiness, native reads, planning refresh, stale local-write rejection and sync status checks passed. No database schema changes. Sample order 1887 remains as the authorized test result.

## Import status preflight — 2026-11-03 (superseded)

Before the native import dialog creates any orders, it posts all reviewed work types to `/api/v1/aurora/firebird/orders/import/preflight`. This authenticated, read-only endpoint checks the connected source's delivery and pickup mappings against its available Nova statuses. A missing or stale mapping blocks the entire pending batch and names the required Ready for delivery / Ready for pickup setting under Ship Status → Planning statuses. Users are directed to a company administrator and can retain and correct the reviewed orders before retrying. Individual native imports repeat the validation as a safeguard. Local Aurora imports do not require native status configuration.

Preflight failures explicitly say no orders were created in that attempt; connection-loss/retry guidance is reserved for attempts that started creation. This is a configuration preflight, not an atomic batch transaction: later write failures or configuration changes during creation can still leave partial results.

Mike's attached test report distinguishes an existing-PRO rejection for MCO3706592 from the missing pickup mapping for AUS3698582. DFW3696808 is labeled successful. Validation: 56 order document import checks passed, including mixed work types, missing mappings, stale status IDs, and invalid work types; the client build passed. Not deployed as part of this change.

### Import independence from optimization

The legacy `/firebird/orders/import/preflight` endpoint now returns success without consulting planning configuration, for older clients. New clients do not call it. Native imports do not run external geocoding or require shipper/consignee names, appointment-window pairing, or matching Tag/terminal prefixes. Actual database constraints still apply at save time; imported orders can be incomplete and are not guaranteed to qualify for optimization.

### Nova address reuse during import

Conversion attempts a lookup of shipper, consignee and bill-to names in the current tenant's Nova ACCOUNT master. A case-insensitive, trimmed name match must be unique after applying any supplied city, state and ZIP. A matching master must contain a street address. Its stored address replaces the extracted address before review; each party displays the lookup outcome. Missing, ambiguous or unavailable matches retain the extracted fields and do not block conversion.

Native creation repeats the lookup using the reviewed names and locality. Matched stops retain the Nova account link, third address line and valid master coordinates; bill-to retains its account link. No external geocoding is introduced.

### Crown spreadsheet reports

Recognized Crown XLSX reports are read directly by column labels, without the AI conversion/output limit. Repeated full CID/Tag values are combined into one shipment, retaining pieces and actual weight once. Service Date is read as an Excel date, not Invoice Date. Missing street addresses and BOL values remain blank until a Nova address match is found. Dot placeholders are blank. Full customer names are preserved in notes when Nova's 40-character bill-to field requires shortening.

The normal numeric PRO comes from the full Crown Tag. Tags sharing a numeric PRO remain visible as conflicts for review. Conflicting shipment details within one full Tag are reported rather than silently discarded. Driver/charge rows do not multiply freight totals.

History supports up to 5,000 extracted shipments (migration 0052). Large review batches show one order at a time with navigation; duplicate lookups run in groups of 100. Creation still uses normal authenticated, tenant-scoped order import endpoints and preserves existing PROs. Standard ZIP+4 values retain all nine digits without the hyphen to fit Nova's postal field.

### Scanned documents: identifiers, local dates and cube

Lykes Test uses the printed Crown Tag as the PRO. When it is missing, review requires manual entry before creation. Filenames and carrier identifiers never supply the Lykes PRO.

Separately printed terminal codes are extracted independently of numeric waybills. Pegasus's header terminal is used, not its Org/Dest airport codes. Printed service dates and appointment windows are extracted as local wall-clock values and converted using the tenant's Nova timezone, preserving the calendar date in review. The delivery window takes precedence over shipment or pickup dates. Editing the local scheduled time moves its extracted appointment window with it; no weekend adjustment is applied.

Printed freight totals take precedence. Otherwise Aurora sums extracted freight rows, rather than asking the model to calculate totals. Cube is calculated only from complete dimensions and quantities with known units. Bryan approved inches as the default specifically for unlabeled Pegasus dimensions; this assumption is recorded in notes. Other forms require units. Incomplete scans, such as page 1 of 2 with no second page, are noted and missing freight is not invented.

### Reopen and edit imported orders

Recent imports displays successfully created PROs as links. Each link opens the same grouped review form with current saved order values and a Save changes action. Saving updates the existing order, without creating another order or changing the import count. Converted documents with no created orders show no order number. Links also support opening `/data-entry?import={batchId}&order={pro}` directly.

The authenticated editor requires the PRO to belong to the current tenant's import history. Server configuration chooses native Nova or local storage. The PRO is read-only; revision checks prevent overwriting intervening edits. Native saves preserve fields outside the compact form, inactive appointment windows, and unedited stop-specific BOLs. Orders expanded to multiple pickup/delivery stops must use the full Orders editor. Work type and document references are retained in labeled order notes. Editing remains independent of optimization planning configuration.

## Assisted review

Global extraction instructions apply across companies alongside each company's own instructions. When the PRO or Bill To is unclear, the review dialog shows the available printed labels, values, and page numbers. Operators can select a value or enter a correction. Applying a printed label to the batch uses each ticket's own value. Remembering a label scopes that choice to the current company, sender, form, and field.

Review opens as soon as extraction completes. Address and duplicate checks run concurrently, and Create waits for both checks and any mapping answers. For the reviewed Waylanders Atlas profile, Aurora reads recognized PDF tickets directly, including freight rows carried over to another page. Unrecognized layouts, handling instructions, or edited extraction settings use the normal AI path.
