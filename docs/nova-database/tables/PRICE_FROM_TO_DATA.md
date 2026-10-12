# PRICE_FROM_TO_DATA

Pricing and tariffs. Price From To Data records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PFTD_REC_NUM, PFTD_SEQ_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PFTD_REC_NUM | NUMERIC(18,0) | Yes | — | RDB$2108 | — |
| 2 | PFTD_SEQ_NUM | SMALLINT | Yes | — | RDB$2109 | — |
| 3 | PFTD_FROM_TYPE | VARCHAR(1) | No | — | RDB$2110 | NONE/NONE |
| 4 | PFTD_FROM | VARCHAR(12) | No | — | RDB$2111 | NONE/NONE |
| 5 | PFTD_TO_TYPE | VARCHAR(1) | No | — | RDB$2112 | NONE/NONE |
| 6 | PFTD_TO | VARCHAR(12) | No | — | RDB$2113 | NONE/NONE |
| 7 | PFTD_PERC | NUMERIC(6,4) | No | — | RDB$2114 | — |
| 8 | PFTD_MIN_PERC | NUMERIC(6,4) | No | — | RDB$2115 | — |
| 9 | PFTD_TL_PERC | NUMERIC(6,4) | No | — | RDB$2116 | — |
| 10 | PFTD_RULE | VARCHAR(1) | No | — | RDB$2117 | NONE/NONE |
| 11 | PFTD_MIN | NUMERIC(9,2) | No | — | RDB$2118 | — |
| 12 | PFTD_BI_DIR | VARCHAR(1) | No | — | RDB$2119 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PFTD_KEY | PFTD_REC_NUM, PFTD_SEQ_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R246 | PRICE_FROM_TO_DATA.(PFTD_REC_NUM) | PRICE_FROM_TO.(PFT_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
