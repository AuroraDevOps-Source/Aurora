# PRICE_SPECIAL_DATA

Pricing and tariffs. Price Special Data records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PSD_REC_NUM, PSD_SEQ_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PSD_REC_NUM | NUMERIC(18,0) | Yes | — | RDB$6399 | — |
| 2 | PSD_SEQ_NUM | SMALLINT | Yes | — | RDB$6400 | — |
| 3 | PSD_LOCATION | VARCHAR(12) | No | — | RDB$6401 | NONE/NONE |
| 4 | PSD_DOW | VARCHAR(3) | No | — | RDB$6402 | NONE/NONE |
| 5 | PSD_START_TIME | TIMESTAMP | No | — | RDB$6403 | — |
| 6 | PSD_END_TIME | TIMESTAMP | No | — | RDB$6404 | — |
| 7 | PSD_COMPUTE_TYPE | VARCHAR(1) | No | — | RDB$6405 | NONE/NONE |
| 8 | PSD_RATE | NUMERIC(9,2) | No | — | RDB$6406 | — |
| 9 | PSD_MIN | NUMERIC(9,2) | No | — | RDB$6407 | — |
| 10 | PSD_MAX | NUMERIC(9,2) | No | — | RDB$6408 | — |
| 11 | PSD_KEYWORD | VARCHAR(8) | No | — | RDB$6409 | NONE/NONE |
| 12 | PSD_SERVICE | VARCHAR(1) | No | — | RDB$6410 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PSD_KEY | PSD_REC_NUM, PSD_SEQ_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R251 | PRICE_SPECIAL_DATA.(PSD_KEYWORD) | KEYWORDB.(KW_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R252 | PRICE_SPECIAL_DATA.(PSD_REC_NUM) | PRICE_SPECIAL.(PS_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
