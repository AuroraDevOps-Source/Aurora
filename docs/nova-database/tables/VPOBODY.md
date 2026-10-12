# VPOBODY

Maintenance and purchasing. Vpobody records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (VPOB_LOC, VPOB_PO_NUM, VPOB_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | VPOB_LOC | VARCHAR(6) | Yes | — | RDB$4643 | NONE/NONE |
| 2 | VPOB_PO_NUM | INTEGER | Yes | — | RDB$4644 | — |
| 3 | VPOB_SEQ | SMALLINT | Yes | — | RDB$4645 | — |
| 4 | VPOB_DESC | VARCHAR(30) | No | — | RDB$4646 | NONE/NONE |
| 5 | VPOB_RN_PREF | VARCHAR(1) | No | — | RDB$4647 | NONE/NONE |
| 6 | VPOB_RN_NUM | INTEGER | No | — | RDB$4648 | — |
| 7 | VPOB_RN_SUF | VARCHAR(2) | No | — | RDB$4649 | NONE/NONE |
| 8 | VPOB_QTY_ORD | INTEGER | No | — | RDB$4650 | — |
| 9 | VPOB_UCOST | NUMERIC(11,2) | No | — | RDB$4651 | — |
| 10 | VPOB_RECV_UCOST | NUMERIC(11,2) | No | — | RDB$4652 | — |
| 14 | VPOB_BO_QTY | INTEGER | No | — | RDB$4656 | — |
| 15 | VPOB_VEND_PART_NUM | VARCHAR(20) | No | — | RDB$4657 | NONE/NONE |
| 16 | VPOB_RECV_QTY | INTEGER | No | — | RDB$4980 | — |
| 17 | VPOB_NON_STOCK_IND | VARCHAR(1) | No | — | RDB$6366 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| VPOB_KEY | VPOB_LOC, VPOB_PO_NUM, VPOB_SEQ | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R281 | VPOBODY.(VPOB_LOC, VPOB_PO_NUM) | VPOHEAD.(VPOH_LOC, VPOH_PO_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
