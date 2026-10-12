# PRICE_ASC

Pricing and tariffs. Price Asc records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PASC_REC_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PASC_ACCOUNT | VARCHAR(12) | Yes | — | RDB$2043 | NONE/NONE |
| 2 | PASC_EFF_DATE | DATE | No | — | RDB$2044 | — |
| 3 | PASC_EXP_DATE | DATE | No | — | RDB$2045 | — |
| 4 | PASC_LOC_FLAG | VARCHAR(1) | No | — | RDB$2046 | NONE/NONE |
| 5 | PASC_REC_NUM | NUMERIC(18,0) | Yes | — | RDB$2047 | — |
| 6 | PASC_FSC_EFF_DT_TYPE | VARCHAR(1) | No | — | RDB$7234 | NONE/NONE |
| 7 | PASC_FSC_SHIP | VARCHAR(1) | No | — | RDB$7235 | NONE/NONE |
| 8 | PASC_FSC_CONS | VARCHAR(1) | No | — | RDB$7236 | NONE/NONE |
| 9 | PASC_FSC_BILLTO | VARCHAR(1) | No | — | RDB$7237 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PASC_CUST | PASC_ACCOUNT, PASC_EFF_DATE, PASC_EXP_DATE | No | Yes | ASC | — |
| PASC_KEY | PASC_REC_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R240 | PRICE_ASC.(PASC_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R242 | PRICE_ASC_DATA.(PASCD_REC_NUM) | PRICE_ASC.(PASC_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
