# PRICE_IL_DATA

Pricing and tariffs. Price Il Data records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PILD_REC_NUM, PILD_SEQ_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PILD_REC_NUM | NUMERIC(18,0) | Yes | — | RDB$5113 | — |
| 2 | PILD_SEQ_NUM | SMALLINT | Yes | — | RDB$5114 | — |
| 3 | PILD_CARRIER | VARCHAR(12) | No | — | RDB$5115 | NONE/NONE |
| 4 | PILD_DIRECTION | VARCHAR(1) | No | — | RDB$5116 | NONE/NONE |
| 5 | PILD_PERC | NUMERIC(6,4) | No | — | RDB$5117 | — |
| 6 | PILD_INTER_MIN | NUMERIC(9,2) | No | — | RDB$5118 | — |
| 7 | PILD_INTRA_MIN | NUMERIC(9,2) | No | — | RDB$5119 | — |
| 8 | PILD_RULE | VARCHAR(1) | No | — | RDB$5120 | NONE/NONE |
| 9 | PILD_STATE1 | VARCHAR(2) | No | — | RDB$5121 | NONE/NONE |
| 10 | PILD_STATE2 | VARCHAR(2) | No | — | RDB$5122 | NONE/NONE |
| 11 | PILD_STATE3 | VARCHAR(2) | No | — | RDB$5123 | NONE/NONE |
| 12 | PILD_STATE4 | VARCHAR(2) | No | — | RDB$5124 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PILD_KEY | PILD_REC_NUM, PILD_SEQ_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R248 | PRICE_IL_DATA.(PILD_REC_NUM) | PRICE_IL.(PIL_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
