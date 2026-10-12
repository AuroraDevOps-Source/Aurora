# FUEL_SURCHARGE_DATA

Pricing and tariffs. Fuel Surcharge Data records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (FSCD_REC_NUM, FSCD_SEQ_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | FSCD_REC_NUM | BIGINT | Yes | — | RDB$2793 | — |
| 2 | FSCD_SEQ_NUM | SMALLINT | Yes | — | RDB$2794 | — |
| 3 | FSCD_LOW | NUMERIC(14,4) | No | — | RDB$2795 | — |
| 4 | FSCD_HIGH | NUMERIC(14,4) | No | — | RDB$2796 | — |
| 5 | FSCD_PERC | NUMERIC(9,4) | No | — | D_PERCENTAGE | — |
| 6 | FSCD_AMOUNT | NUMERIC(9,2) | No | — | RDB$2798 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| FSCD_KEY | FSCD_REC_NUM, FSCD_SEQ_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R137 | FUEL_SURCHARGE_DATA.(FSCD_REC_NUM) | FUEL_SURCHARGE.(FSC_SCALE_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
