# PRICE_MAX_CHGS

Pricing and tariffs. Price Max Chgs records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PRMC_REC_NUM, PRMC_SEQ_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PRMC_REC_NUM | NUMERIC(18,0) | Yes | — | RDB$1947 | — |
| 2 | PRMC_SEQ_NUM | SMALLINT | Yes | — | RDB$1948 | — |
| 3 | PRMC_MAX_LOW | NUMERIC(9,3) | No | — | RDB$1949 | — |
| 4 | PRMC_MAX_HIGH | NUMERIC(9,3) | No | — | RDB$1950 | — |
| 5 | PRMC_MAX_AMT | NUMERIC(9,2) | No | — | RDB$1951 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PRMC_KEY | PRMC_REC_NUM, PRMC_SEQ_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R249 | PRICE_MAX_CHGS.(PRMC_REC_NUM) | PRICING.(PRMA_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
