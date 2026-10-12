# PRICE_IL

Pricing and tariffs. Price Il records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PIL_REC_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PIL_ACCOUNT | VARCHAR(12) | Yes | — | RDB$5109 | NONE/NONE |
| 2 | PIL_EFF_DATE | DATE | No | — | RDB$5110 | — |
| 3 | PIL_EXP_DATE | DATE | No | — | RDB$5111 | — |
| 4 | PIL_REC_NUM | NUMERIC(18,0) | Yes | — | RDB$5112 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PIL_CUST | PIL_ACCOUNT, PIL_EFF_DATE, PIL_EXP_DATE | No | Yes | ASC | — |
| PIL_KEY | PIL_REC_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R247 | PRICE_IL.(PIL_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R248 | PRICE_IL_DATA.(PILD_REC_NUM) | PRICE_IL.(PIL_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
