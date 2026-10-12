# Verification

Verified on 2026-10-09.

- Offline coverage check passes: all 222 metadata relations have individual catalogs; all 4,224 table/column pairs match the independent column-name capture without duplicates.
- All 331 indexes and 215 primary-key-bearing tables are represented. There are zero foreign-key or UNIQUE constraints in the user-table constraint capture.
- All 298 relationship mappings resolve to existing table/column endpoints. CSV and HTML contain the same map data. All 12 area diagrams include every locally assigned table, including isolated nodes.
- All relative Markdown links resolve. The HTML has its metadata embedded and references no external scripts, stylesheets or images.
- Browser visual review passed at approximately 1265 px width. Table search, manifest-table selection, neighborhood diagram and High-confidence filtering worked. The filter displayed 37 of 298 links. No browser error logs were reported during those checks.
- [Preview](preview.jpg) records the HTML appearance. A temporary loopback-only HTTP server was used for browser checks and stopped afterward. The browser automation surface does not permit file-URL navigation, so direct file opening was not browser-tested; the delivered HTML is self-contained and can be opened manually in a browser.
- No customer records were queried. No DML, DDL, stored-procedure invocation or generator allocation was executed against Firebird. The existing read-only helper and secret configuration were unchanged.

Re-run the offline consistency check with `python docs/nova-database/scripts/verify-map.py`. These checks verify completeness and internal consistency of the documentation; they do not validate the truth of inferred joins against business data.
