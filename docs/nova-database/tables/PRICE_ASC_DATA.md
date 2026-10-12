# PRICE_ASC_DATA

Pricing and tariffs. Price Asc Data records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PASCD_REC_NUM, PASCD_SEQ_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PASCD_REC_NUM | NUMERIC(18,0) | Yes | — | RDB$2048 | — |
| 2 | PASCD_SEQ_NUM | SMALLINT | Yes | — | RDB$2049 | — |
| 3 | PASCD_KW | VARCHAR(8) | No | — | RDB$2050 | NONE/NONE |
| 4 | PASCD_CMPT_TYPE | VARCHAR(1) | No | — | RDB$2051 | NONE/NONE |
| 5 | PASCD_AMT | NUMERIC(11,4) | No | — | RDB$2052 | — |
| 6 | PASCD_RULE | VARCHAR(1) | No | — | RDB$2053 | NONE/NONE |
| 7 | PASCD_WHEN | VARCHAR(1) | No | — | RDB$2054 | NONE/NONE |
| 8 | PASCD_MIN | NUMERIC(9,2) | No | — | RDB$2055 | — |
| 9 | PASCD_MAX | NUMERIC(9,2) | No | — | RDB$2056 | — |
| 10 | PASCD_IL_RULE | VARCHAR(1) | No | — | RDB$2057 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PASCD_KEY | PASCD_REC_NUM, PASCD_SEQ_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R241 | PRICE_ASC_DATA.(PASCD_KW) | KEYWORDB.(KW_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R242 | PRICE_ASC_DATA.(PASCD_REC_NUM) | PRICE_ASC.(PASC_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
