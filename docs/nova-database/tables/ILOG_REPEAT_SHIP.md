# ILOG_REPEAT_SHIP

Integration and reference. Ilog Repeat Ship records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (IRS_TERM, IRS_ROUTE, IRS_SEQ, IRS_CONS_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | IRS_TERM | VARCHAR(12) | Yes | — | RDB$7215 | NONE/NONE |
| 2 | IRS_ROUTE | VARCHAR(24) | Yes | — | RDB$7216 | NONE/NONE |
| 3 | IRS_SEQ | INTEGER | Yes | — | RDB$7217 | — |
| 4 | IRS_CONS_ID | VARCHAR(12) | Yes | — | RDB$7218 | NONE/NONE |
| 5 | IRS_DRIVER | VARCHAR(12) | No | — | RDB$7219 | NONE/NONE |
| 6 | IRS_TRUCK | VARCHAR(12) | No | — | RDB$7220 | NONE/NONE |
| 7 | IRS_DLV_TIME | VARCHAR(4) | No | — | RDB$7221 | NONE/NONE |
| 8 | IRS_MILES | INTEGER | No | — | RDB$7222 | — |
| 9 | IRS_CUBE | VARCHAR(20) | No | — | RDB$7223 | NONE/NONE |
| 10 | IRS_COMMENT | VARCHAR(80) | No | — | RDB$7224 | NONE/NONE |
| 11 | IRS_LAST_ORDER_NUM | INTEGER | No | — | RDB$7225 | — |
| 12 | IRS_LAST_DATE | DATE | No | — | RDB$7226 | — |
| 13 | IRS_LAST_MANIFEST | INTEGER | No | — | RDB$7249 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| ILOG_REPEAT_SHIP_KEY | IRS_TERM, IRS_ROUTE, IRS_SEQ, IRS_CONS_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R141 | ILOG_REPEAT_SHIP.(IRS_CONS_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R142 | ILOG_REPEAT_SHIP.(IRS_TERM) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
