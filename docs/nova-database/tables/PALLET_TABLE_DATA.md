# PALLET_TABLE_DATA

Pricing and tariffs. Pallet Table Data records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PALTD_REC_NUM, PALTD_SEQ_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PALTD_REC_NUM | NUMERIC(18,0) | Yes | — | RDB$3669 | — |
| 2 | PALTD_SEQ_NUM | SMALLINT | Yes | — | RDB$3670 | — |
| 3 | PALTD_PALLET_COUNT | SMALLINT | Yes | — | RDB$3671 | — |
| 4 | PALTD_WGT_BREAK | INTEGER | Yes | — | RDB$3672 | — |
| 5 | PALTD_RATE | NUMERIC(9,4) | Yes | — | RDB$3673 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PALTD_KEY | PALTD_REC_NUM, PALTD_SEQ_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R237 | PALLET_TABLE_DATA.(PALTD_REC_NUM) | PALLET_TARIFF_TABLE.(PALT_TABLE_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  Referenced fields have no exact active unique index; multiple matches are possible. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
