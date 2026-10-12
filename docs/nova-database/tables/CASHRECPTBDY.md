# CASHRECPTBDY

Claims and receipts. Cash receipt batch detail.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CP_DATE, CP_BATCH, CP_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CP_DATE | DATE | Yes | — | RDB$3593 | — |
| 2 | CP_BATCH | VARCHAR(24) | Yes | — | RDB$3594 | NONE/NONE |
| 3 | CP_SEQ | INTEGER | Yes | — | RDB$3595 | — |
| 4 | CP_PRO_LOC | VARCHAR(12) | No | — | RDB$3596 | NONE/NONE |
| 5 | CP_PRO_NUM | INTEGER | No | — | RDB$3597 | — |
| 6 | CP_PRO_SUF | VARCHAR(2) | No | — | RDB$3598 | NONE/NONE |
| 7 | CP_CHECK_DATE | DATE | No | — | RDB$3599 | — |
| 8 | CP_CHECK_NUM | VARCHAR(12) | No | — | RDB$3600 | NONE/NONE |
| 9 | CP_AMOUNT | NUMERIC(13,2) | No | — | RDB$3601 | — |
| 10 | CP_COMMENT | VARCHAR(256) | No | — | RDB$3602 | NONE/NONE |
| 11 | CP_TYPE | VARCHAR(8) | No | — | RDB$3603 | NONE/NONE |
| 12 | CP_FLAG | VARCHAR(1) | No | — | RDB$3604 | NONE/NONE |
| 13 | CP_O_U_I | VARCHAR(1) | No | — | RDB$3605 | NONE/NONE |
| 14 | CP_COMPANY | VARCHAR(2) | No | — | RDB$3606 | NONE/NONE |
| 15 | CP_O_U_A | NUMERIC(13,2) | No | — | RDB$3607 | — |
| 16 | CP_CUST_ID | VARCHAR(12) | No | — | RDB$3633 | NONE/NONE |
| 17 | CP_CUST_NAME | VARCHAR(40) | No | — | RDB$3634 | NONE/NONE |
| 18 | CP_SHIP_NUM | BIGINT | No | — | RDB$6968 | — |
| 19 | CP_PRO_ENT_DT | TIMESTAMP | No | — | RDB$6969 | — |
| 20 | CP_BOL | VARCHAR(80) | No | — | RDB$7214 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CP_CUST_ID_KEY | CP_CUST_ID, CP_DATE | No | Yes | ASC | — |
| CP_KEY | CP_DATE, CP_BATCH, CP_SEQ | Yes | Yes | ASC | — |
| CP_PRO_KEY | CP_PRO_LOC, CP_PRO_NUM, CP_PRO_SUF | No | Yes | ASC | — |
| CP_SHIP_NUM_KEY | CP_SHIP_NUM | No | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R026 | CASHRECPTBDY.(CP_CUST_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R027 | CASHRECPTBDY.(CP_DATE, CP_BATCH) | CASHRECPTHDR.(CH_DATE, CH_BATCH) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
