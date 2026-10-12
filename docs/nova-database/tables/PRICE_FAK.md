# PRICE_FAK

Pricing and tariffs. Price Fak records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PFAK_REC_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PFAK_ACCOUNT | VARCHAR(12) | Yes | — | RDB$1952 | NONE/NONE |
| 2 | PFAK_EXP_DATE | DATE | No | — | RDB$1953 | — |
| 3 | PFAK_EFF_DATE | DATE | No | — | RDB$1954 | — |
| 4 | PFAK_REC_NUM | NUMERIC(18,0) | Yes | — | RDB$1955 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PFAK_CUST | PFAK_ACCOUNT, PFAK_EFF_DATE, PFAK_EXP_DATE | No | Yes | ASC | — |
| PFAK_KEY | PFAK_REC_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R243 | PRICE_FAK.(PFAK_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R244 | PRICE_FAK_DATA.(PFAKD_REC_NUM) | PRICE_FAK.(PFAK_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
