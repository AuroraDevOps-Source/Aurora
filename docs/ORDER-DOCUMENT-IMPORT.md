# Order document import

Orders > Import orders (beside Optimize routes) uploads one document, displays editable extracted orders, then creates each through `POST /api/v1/tms/orders`. This uses `TmsStore.SaveOrder`, including its existing transactional operational outbox. No direct Nova/FreightOps push or sync implementation changes are introduced.

Supported uploads: PDF, CSV, TSV, TXT, XLSX, DOCX, JPEG, PNG, WebP, GIF, BMP and multi-page TIFF; maximum 10 MiB and 100 extracted orders. Image conversion requires the accompanying Integration-Hub update in `DocumentConvertService` (Magick.NET 15.0.0). Images are limited to 20 pages / 80 megapixels and bounded decoded PNG sizes.

## Configuration

For each authenticated Aurora tenant GUID, configure server-side secrets:

```
DocumentConversion__Tenants__<aurora-tenant-guid>__BaseUrl=https://your-hub-host
DocumentConversion__Tenants__<aurora-tenant-guid>__ServiceCredential=<credential-id>.<secret>
```

Provision that credential against the corresponding Hub client using Integration-Hub's `tools/New-DocumentServiceCredential.ps1` and `docs/Document-Conversion-Service-Authentication.md`. There is deliberately no shared fallback credential. Do not put secrets in the browser configuration or commit them. Deploy the Hub image support and Aurora API/client together.

Conversion calls `POST /api/document-convert/service` with multipart `file` and a fixed Aurora extraction `prompt`. Hub authentication remains `DocumentService`; no end-user token is forwarded. Conversion only returns a preview. Users review missing identifiers/addresses and dates (a missing date defaults to today), select a terminal, and create orders Ready to Route. Extracted ownership and lifecycle identifiers are discarded.

Create validates all preview rows before saving. Successfully saved rows are removed from the pending list; if a later save fails, its remaining rows stay available and the created count is shown. Existing order numbers are rejected by normal create validation, never overwritten. After an ambiguous network failure, check the orders list before retrying. Existing date/status/search filters still apply after import.

Checks: `dotnet run --project tests/OrderDocumentImportChecks`; Hub `DocumentImageTests` and existing `DocumentConvert` tests. Live conversion requires tenant service credentials and Hub AI configuration; sync delivery requires the existing enabled tenant sync connections.
