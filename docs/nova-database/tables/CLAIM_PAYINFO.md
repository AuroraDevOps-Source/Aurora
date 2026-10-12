# CLAIM_PAYINFO

Claims and receipts. Claim Payinfo records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CLPI_YR_KEY, CLPI_NUM_KEY, CLPI_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CLPI_YR_KEY | VARCHAR(2) | Yes | — | RDB$5779 | NONE/NONE |
| 2 | CLPI_NUM_KEY | INTEGER | Yes | — | RDB$5780 | — |
| 3 | CLPI_SEQ | INTEGER | Yes | — | RDB$5781 | — |
| 4 | CLPI_PAY_CO | VARCHAR(2) | No | — | RDB$5782 | NONE/NONE |
| 5 | CLPI_PAY_VENDID | VARCHAR(8) | No | — | RDB$5783 | NONE/NONE |
| 6 | CLPI_PAY_INVID | VARCHAR(32) | No | — | RDB$5784 | NONE/NONE |
| 7 | CLPI_PAY_INVDT | DATE | No | — | RDB$5785 | — |
| 8 | CLPI_PAY_AMT | NUMERIC(10,2) | No | — | RDB$5786 | — |
| 9 | CLPI_CHKNUM | VARCHAR(12) | No | — | RDB$5787 | NONE/NONE |
| 10 | CLPI_CHKDT | DATE | No | — | RDB$5788 | — |
| 11 | CLPI_CHKAMT | NUMERIC(10,2) | No | — | RDB$5789 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CLAIM_PAY_KEY | CLPI_YR_KEY, CLPI_NUM_KEY, CLPI_SEQ | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R040 | CLAIM_PAYINFO.(CLPI_YR_KEY, CLPI_NUM_KEY) | CLAIM_HEADER.(CLH_YR_KEY, CLH_NUM_KEY) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
