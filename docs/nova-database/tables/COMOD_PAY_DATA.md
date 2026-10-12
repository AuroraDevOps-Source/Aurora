# COMOD_PAY_DATA

Pricing and tariffs. Comod Pay Data records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTP_REC_NUM, CTP_SEQ_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTP_REC_NUM | BIGINT | Yes | — | RDB$3517 | — |
| 2 | CTP_SEQ_NUM | SMALLINT | Yes | — | RDB$3518 | — |
| 3 | CTP_PAY_CODE | VARCHAR(4) | No | — | RDB$3519 | NONE/NONE |
| 4 | CTP_RATE | NUMERIC(9,3) | No | — | RDB$3520 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CTP_KEY | CTP_REC_NUM, CTP_SEQ_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R043 | COMOD_PAY_DATA.(CTP_REC_NUM) | COMOD_TARIFF_RATE.(CTR_PAY_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  Referenced fields have no exact active unique index; multiple matches are possible. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
