# CASHRECPTHDR

Claims and receipts. Cash receipt batches.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CH_DATE, CH_BATCH). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CH_DATE | DATE | Yes | — | RDB$3581 | — |
| 2 | CH_BATCH | VARCHAR(24) | Yes | — | RDB$3582 | NONE/NONE |
| 3 | CH_SEQ | SMALLINT | No | — | RDB$3583 | — |
| 4 | CH_COUNT | INTEGER | No | — | RDB$3584 | — |
| 5 | CH_AMOUNT | NUMERIC(13,2) | No | — | RDB$3585 | — |
| 6 | CH_INIT | VARCHAR(12) | No | — | RDB$3586 | NONE/NONE |
| 7 | CH_STATUS | VARCHAR(2) | No | — | RDB$3587 | NONE/NONE |
| 8 | CH_ADJ | NUMERIC(13,2) | No | — | RDB$3588 | — |
| 9 | CH_CUR_AMT | NUMERIC(13,2) | No | — | RDB$3589 | — |
| 10 | CH_COMPANY | VARCHAR(2) | No | — | RDB$3590 | NONE/NONE |
| 11 | CH_ACTUAL_DATE | DATE | No | — | RDB$3591 | — |
| 12 | CH_RETURN_DT | DATE | No | — | RDB$3592 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CH_KEY | CH_DATE, CH_BATCH | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R027 | CASHRECPTBDY.(CP_DATE, CP_BATCH) | CASHRECPTHDR.(CH_DATE, CH_BATCH) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
