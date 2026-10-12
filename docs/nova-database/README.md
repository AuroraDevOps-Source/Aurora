# Nova operations database map

Start with **[the interactive HTML report](nova-database-map.html)**. Open it in a browser; it is a self-contained file with no external dependencies or database connection. It includes a searchable table/column explorer, relationship diagrams, confidence filters, index details, and CSV export.

Captured on **2026-10-09**, from `172.23.0.5`, database `D:\db\wayl_test\operations.fdb`. The connected engine reports **Firebird 5.0.3** and the expected database path. Scope follows the revised request: map the database's tables and their relationships.

| Inventory | Count |
| --- | ---: |
| User tables | 222 |
| User views | 0 |
| Columns | 4,224 |
| Tables with declared primary keys | 215 |
| Indexes, including constraint indexes | 331 |
| Declared foreign keys | **0** |
| Declared UNIQUE constraints | 0 |
| Logical relationship mappings | 298 |
| High / Medium / Low confidence mappings | 37 / 242 / 19 |

**There are no declared foreign keys.** Primary keys and unique indexes constrain identities, while selected triggers perform cleanup and copy related values. This is not equivalent to foreign-key enforcement. All relationship arrows in these documents are explicitly inferred logical relationships.

## Files

- [HTML report](nova-database-map.html): readable summary and offline explorer covering every table.
- [Table inventory and coverage checklist](TABLES.md): all 222 tables, with links to individual column/key/index catalogs in `tables/`.
- [Relationship catalog](RELATIONSHIPS.md) and [CSV](relationships.csv): exact child and parent columns, confidence, evidence, uniqueness and caveats.
- [Diagram index](DIAGRAMS.md): core map and links to 12 business-area Mermaid diagrams. The HTML also draws a relationship neighborhood for any selected table.
- [Uncertainties](UNCERTAINTIES.md): unresolved relationships and important interpretation limits.
- [Machine-readable map](map-data.json): the same inventory and relationships used by the HTML.
- [Raw metadata](metadata/): metadata evidence, including all inspected trigger definitions. No business records or secret values were extracted.

## Core structure

`OPS_HEADER.PB_ID` identifies a shared header. `OPS_HEADER_AFTER_INSERT` distinguishes orders (`PB_TYPE='O'`), manifests (`'M'`), templates (`'T'`) and rate quotes (`'Q'`).

`OPS_STOP_REC.OPS_ORDER_ID` refers to that header. The same stop table holds order pickup/delivery stops and manifest routing stops. `OPS_MANIFEST_STOPS` bridges those roles through `OSM_STOP_ID` (routing stop) and `OSM_PKUP_DELV_ID` (order stop). `OPS_STOP_REC.OPS_MANIFEST_NUMBER` is an additional reference to the manifest header; Aurora writes it along with the bridge. Do not equate a stop ID with a header ID or join manifests directly to orders by matching PB_ID values.

`OPS_LINE_ITEMS`, `OPS_AGENTS`, `OPS_NUMBERS`, `OPS_OTHER_PARTIES`, and `OPS_HISTORY` carry related order detail. `CT_STAT` supplies status definitions. `ACCOUNT` is the party master candidate for account references, and `LOCMAST` is the terminal/location master. Equipment assignments reference `EQUIP_HEADER`; the current-driver join is explicit in Aurora, while the wider driver identity model remains partly inferred.

## Evidence and limits

High confidence means a direct equality or value mapping was observed in trigger source or Aurora source. Medium confidence means matching field roles and key structure provide a plausible join. Low confidence flags conflicting storage types or ambiguous ownership. These are confidence levels in intended linkage, **not proof that current records satisfy the relationship**. Exact index matching establishes whether the target columns are unique, not whether each child has a matching parent. Optionality, sentinel values, subtype predicates and temporal validity can affect any join.

Source code inspected: `src/Aurora.Modules.Routing/FirebirdRoutingSource.cs`, `FirebirdRoutingSource.Manifests.cs`, and `FirebirdRoutingSource.Export.cs`. These files were read, not executed. In particular, no export or generator allocation was run. Trigger source is retained in [triggers.json](metadata/triggers.json); it was inspected as text, not invoked.

Only system metadata and database identity were queried using `deploy/private/local-test/firebird-read.ps1`. This existing helper reads the local user-secret connection without printing it, opens a READ / READ COMMITTED transaction, sets a 30-second command timeout, and rolls back. The column query was divided into batches after the original large query timed out. No application configuration or database records/schema were changed. The metadata files were captured through separate short transactions, so they are not a single transactionally consistent schema snapshot if concurrent DDL occurred.

No user-row sampling, counts, orphan scans, stored-procedure execution, generator calls or complete workflow investigation were performed under the narrowed scope. All tables are inventoried; not every application-defined relationship can be proven from this evidence. Tables with no established links are explicitly retained in the inventory and HTML.

Firebird type and trigger metadata were interpreted using the official [RDB$FIELDS reference](https://www.firebirdsql.org/file/documentation/chunk/en/refdocs/fblangref50/fblangref-appx04-fields.html) and [RDB$TRIGGERS reference](https://www.firebirdsql.org/file/documentation/chunk/en/refdocs/fblangref50/fblangref-appx04-triggers.html).

## Rebuilding

From the repository root, `python docs/nova-database/scripts/build-map.py` rebuilds Markdown, CSV, diagrams and map JSON from the captured metadata. `python docs/nova-database/scripts/build-html.py` rebuilds the standalone HTML. Neither Python script connects to Firebird.

The optional PowerShell script `docs/nova-database/scripts/extract-metadata.ps1` refreshes metadata through the existing helper and the local secret store. It overwrites only this documentation's metadata files; it does not change the helper or database configuration. Run the build scripts afterward. Historical counts in the prose and verification expectations must be reviewed when the schema changes.
