# PALLET_MILEAGE_DATA

Pricing and tariffs. Pallet Mileage Data records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PALMD_REC_NUM, PALMD_SEQ_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PALMD_REC_NUM | NUMERIC(18,0) | Yes | — | RDB$3653 | — |
| 2 | PALMD_SEQ_NUM | SMALLINT | Yes | — | RDB$3654 | — |
| 3 | PALMD_MILES | BIGINT | Yes | — | RDB$3655 | — |
| 4 | PALMD_TABLE_ID | VARCHAR(8) | Yes | — | RDB$3656 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PALMD_KEY | PALMD_REC_NUM, PALMD_SEQ_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R236 | PALLET_MILEAGE_DATA.(PALMD_REC_NUM) | PALLET_TARIFF_TABLE.(PALT_TABLE_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  Referenced fields have no exact active unique index; multiple matches are possible. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
