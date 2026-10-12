# PRICE_SPECIAL

Pricing and tariffs. Price Special records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PS_REC_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PS_ACCOUNT | VARCHAR(12) | Yes | — | RDB$6395 | NONE/NONE |
| 2 | PS_EFF_DATE | DATE | No | — | RDB$6396 | — |
| 3 | PS_EXP_DATE | DATE | No | — | RDB$6397 | — |
| 4 | PS_REC_NUM | NUMERIC(18,0) | Yes | — | RDB$6398 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PS_CUST | PS_ACCOUNT, PS_EFF_DATE, PS_EXP_DATE | No | Yes | ASC | — |
| PS_KEY | PS_REC_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R250 | PRICE_SPECIAL.(PS_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R252 | PRICE_SPECIAL_DATA.(PSD_REC_NUM) | PRICE_SPECIAL.(PS_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
