# PRICE_WGT_BRKS

Pricing and tariffs. Price Wgt Brks records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PRWB_REC_NUM, PRWB_SEQ_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PRWB_REC_NUM | NUMERIC(18,0) | Yes | — | RDB$1942 | — |
| 2 | PRWB_SEQ_NUM | SMALLINT | Yes | — | RDB$1943 | — |
| 3 | PRWB_LOW | NUMERIC(9,3) | No | — | RDB$1944 | — |
| 4 | PRWB_HIGH | NUMERIC(9,3) | No | — | RDB$1945 | — |
| 5 | PRWB_PERC | NUMERIC(6,4) | No | — | RDB$1946 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PRWB_KEY | PRWB_REC_NUM, PRWB_SEQ_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R254 | PRICE_WGT_BRKS.(PRWB_REC_NUM) | PRICING.(PRMA_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
