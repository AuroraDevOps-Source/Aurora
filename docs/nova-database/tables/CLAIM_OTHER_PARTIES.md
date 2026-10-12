# CLAIM_OTHER_PARTIES

Claims and receipts. Claim Other Parties records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CLOP_YR_KEY, CLOP_NUM_KEY, CLOP_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CLOP_YR_KEY | VARCHAR(2) | Yes | — | RDB$6461 | NONE/NONE |
| 2 | CLOP_NUM_KEY | INTEGER | Yes | — | RDB$6462 | — |
| 3 | CLOP_SEQ | INTEGER | Yes | — | RDB$6463 | — |
| 4 | CLOP_ID | VARCHAR(12) | No | — | RDB$6464 | NONE/NONE |
| 5 | CLOP_PRO | VARCHAR(32) | No | — | RDB$6465 | NONE/NONE |
| 6 | CLOP_IL_DATE | DATE | No | — | RDB$6466 | — |
| 7 | CLOP_STAT | VARCHAR(2) | No | — | RDB$6467 | NONE/NONE |
| 8 | CLOP_SPLIT | NUMERIC(10,2) | No | — | RDB$6468 | — |
| 9 | CLOP_CLAIM_ID | VARCHAR(32) | No | — | RDB$6469 | NONE/NONE |
| 10 | CLOP_SETL_DT | DATE | No | — | RDB$6470 | — |
| 11 | CLOP_NOTES | BLOB SUB_TYPE 0 | No | — | RDB$6471 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CLAIM_OTH_PARTY_KEY | CLOP_YR_KEY, CLOP_NUM_KEY, CLOP_SEQ | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R039 | CLAIM_OTHER_PARTIES.(CLOP_YR_KEY, CLOP_NUM_KEY) | CLAIM_HEADER.(CLH_YR_KEY, CLH_NUM_KEY) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
