# PRICE_FROM_TO

Pricing and tariffs. Price From To records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PFT_REC_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PFT_ACCOUNT | VARCHAR(12) | Yes | — | RDB$2120 | NONE/NONE |
| 2 | PFT_EFF_DATE | DATE | No | — | RDB$2121 | — |
| 3 | PFT_EXP_DATE | DATE | No | — | RDB$2122 | — |
| 4 | PFT_REC_NUM | NUMERIC(18,0) | Yes | — | RDB$2123 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PFT_CUST | PFT_ACCOUNT, PFT_EFF_DATE, PFT_EXP_DATE | No | Yes | ASC | — |
| PFT_KEY | PFT_REC_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R245 | PRICE_FROM_TO.(PFT_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R246 | PRICE_FROM_TO_DATA.(PFTD_REC_NUM) | PRICE_FROM_TO.(PFT_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
