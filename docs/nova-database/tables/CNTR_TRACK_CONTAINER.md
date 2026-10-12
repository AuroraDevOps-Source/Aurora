# CNTR_TRACK_CONTAINER

Containers and warehouse. Cntr Track Container records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTC_BOL, CTC_CNTR_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTC_BOL | VARCHAR(80) | Yes | — | D_CONTROL_NUMBER | NONE/NONE |
| 2 | CTC_CNTR_ID | VARCHAR(14) | Yes | — | D_UNIT_ID | NONE/NONE |
| 3 | CTC_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 4 | CTC_ORIG_CITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 5 | CTC_ORIG_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 6 | CTC_ORIG_COUNTRY | VARCHAR(4) | No | — | D_COUNTRY_CODE | NONE/NONE |
| 7 | CTC_ORIG_LOC_CODE | VARCHAR(10) | No | — | RDB$6181 | NONE/NONE |
| 8 | CTC_DEST_CITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 9 | CTC_DEST_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 10 | CTC_DEST_COUNTRY | VARCHAR(4) | No | — | D_COUNTRY_CODE | NONE/NONE |
| 11 | CTC_DEST_LOC_CODE | VARCHAR(10) | No | — | RDB$6182 | NONE/NONE |
| 12 | CTC_POD_CITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 13 | CTC_POD_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 14 | CTC_POD_COUNTRY | VARCHAR(4) | No | — | D_COUNTRY_CODE | NONE/NONE |
| 15 | CTC_POD_LOC_CODE | VARCHAR(10) | No | — | RDB$6183 | NONE/NONE |
| 16 | CTC_POL_CITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 17 | CTC_POL_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 18 | CTC_POL_COUNTRY | VARCHAR(4) | No | — | D_COUNTRY_CODE | NONE/NONE |
| 19 | CTC_POL_LOC_CODE | VARCHAR(10) | No | — | RDB$6184 | NONE/NONE |
| 20 | CTC_ETA | TIMESTAMP | No | — | RDB$6371 | — |
| 21 | CTC_TERM | VARCHAR(50) | No | — | RDB$6372 | NONE/NONE |
| 22 | CTC_TERM_CODE | VARCHAR(4) | No | — | RDB$6373 | NONE/NONE |
| 23 | CTC_LAST_FREE_DATE | DATE | No | — | RDB$6374 | — |
| 24 | CTC_LAST_FREE_DATE_DETENTION | DATE | No | — | RDB$6375 | — |
| 25 | CTC_AVAILABLE_DATE | TIMESTAMP | No | — | RDB$6376 | — |
| 26 | CTC_CUSTOMS_STATUS | VARCHAR(20) | No | — | RDB$6377 | NONE/NONE |
| 27 | CTC_FREIGHT_STATUS | VARCHAR(20) | No | — | RDB$6378 | NONE/NONE |
| 28 | CTC_NOTES | VARCHAR(255) | No | — | RDB$6379 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_CNTR_TRACK_CONTAINER | CTC_BOL, CTC_CNTR_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R041 | CNTR_TRACK_CONTAINER.(CTC_BOL) | CNTR_TRACK_BOLS.(CTB_CARRIER_BOL) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R042 | CNTR_TRACK_EVENTS.(CTE_BOL, CTE_CNTR_ID) | CNTR_TRACK_CONTAINER.(CTC_BOL, CTC_CNTR_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
