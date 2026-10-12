# OPS_LINE_ITEMS

Orders and manifests. Order freight and charge detail, optionally attached to a stop.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (OPL_ORDER_ID, OPL_SEQUENCE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | OPL_ORDER_ID | NUMERIC(9,0) | Yes | — | RDB$552 | — |
| 2 | OPL_SEQUENCE | INTEGER | Yes | — | RDB$553 | — |
| 3 | OPL_STOP_ID | BIGINT | Yes | — | D_SYSID | — |
| 4 | OPL_PIECES | NUMERIC(15,3) | No | — | D_PIECES | — |
| 5 | OPL_WEIGHT | NUMERIC(15,3) | No | — | D_WEIGHT | — |
| 6 | OPL_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 7 | OPL_METRIC | VARCHAR(1) | No | — | RDB$554 | NONE/NONE |
| 8 | OPL_CUBE | VARCHAR(20) | No | — | RDB$555 | NONE/NONE |
| 9 | OPL_PALLETS | INTEGER | No | — | RDB$556 | — |
| 10 | OPL_PALLET_POSITIONS | INTEGER | No | — | RDB$557 | — |
| 11 | OPL_STACKABLE | VARCHAR(1) | No | — | RDB$558 | NONE/NONE |
| 12 | OPL_RATE | NUMERIC(15,4) | No | — | D_RATE | — |
| 13 | OPL_AMOUNT | NUMERIC(16,2) | No | — | D_BILL_AMOUNT | — |
| 14 | OPL_KEYWORD | VARCHAR(8) | No | — | D_KEYWORD | NONE/NONE |
| 15 | OPL_ORIG_CLASS | VARCHAR(8) | No | — | RDB$2683 | NONE/NONE |
| 16 | OPL_RATE_TYPE | VARCHAR(2) | No | — | RDB$5356 | NONE/NONE |
| 17 | OPL_KILOS | NUMERIC(15,3) | No | — | RDB$6382 | — |
| 18 | OPL_DIM_WGT | NUMERIC(15,3) | No | — | RDB$6411 | — |
| 19 | OPL_HAZMAT | VARCHAR(6) | No | — | RDB$6581 | NONE/NONE |
| 20 | OPL_TRACE1 | VARCHAR(80) | No | — | RDB$6856 | NONE/NONE |
| 21 | OPL_TRACE2 | VARCHAR(80) | No | — | RDB$6857 | NONE/NONE |
| 22 | OPL_TONS | NUMERIC(15,3) | No | — | RDB$6976 | — |
| 23 | OPL_ITEM_NUM | VARCHAR(36) | No | — | RDB$6977 | NONE/NONE |
| 24 | OPL_MET_TONS | NUMERIC(15,3) | No | — | RDB$7155 | — |
| 25 | OPL_FEEDTAG_FILE_NAME | VARCHAR(250) | No | — | RDB$7179 | NONE/NONE |
| 26 | OPL_UOM | VARCHAR(20) | No | — | RDB$7233 | NONE/NONE |
| 27 | OPL_SKU | VARCHAR(36) | No | — | RDB$7255 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| OPL_STOP_KEY | OPL_STOP_ID, OPL_SEQUENCE | No | Yes | ASC | — |
| PK_OPS_LINE_ITEMS | OPL_ORDER_ID, OPL_SEQUENCE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R202 | OPS_LINE_ITEMS.(OPL_ITEM_NUM) | ITEM_WEIGHT.(IW_ITEM) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R203 | OPS_LINE_ITEMS.(OPL_KEYWORD) | KEYWORDB.(KW_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R204 | OPS_LINE_ITEMS.(OPL_ORDER_ID) | OPS_HEADER.(PB_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping; Trigger OPS_HEADER_AFTER_DELETE explicitly deletes matching child rows  |
| R205 | OPS_LINE_ITEMS.(OPL_STOP_ID) | OPS_STOP_REC.(OPS_STOP_ID) | High | Trigger OPS_STOP_REC_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |

## Trigger evidence

`OPS_LINE_ITEMS_AFTER_MOD`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
