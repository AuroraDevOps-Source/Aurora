# PALLET_TARIFF_TABLE

Pricing and tariffs. Pallet Tariff Table records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PALT_TARIFF_ID, PALT_TABLE_ID, PALT_EFF_DATE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PALT_TARIFF_ID | VARCHAR(6) | Yes | — | RDB$3691 | NONE/NONE |
| 2 | PALT_TABLE_ID | VARCHAR(8) | Yes | — | RDB$3692 | NONE/NONE |
| 3 | PALT_EFF_DATE | DATE | Yes | — | RDB$3693 | — |
| 4 | PALT_EXP_DATE | DATE | Yes | — | RDB$3694 | — |
| 5 | PALT_TABLE_TYPE | VARCHAR(1) | Yes | — | RDB$3695 | NONE/NONE |
| 6 | PALT_TABLE_REC_NUM | BIGINT | No | — | RDB$3696 | — |
| 7 | PALT_TABLE_DESC | VARCHAR(128) | No | — | RDB$3697 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_PALLET_TARIFF_TABLE | PALT_TARIFF_ID, PALT_TABLE_ID, PALT_EFF_DATE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R236 | PALLET_MILEAGE_DATA.(PALMD_REC_NUM) | PALLET_TARIFF_TABLE.(PALT_TABLE_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  Referenced fields have no exact active unique index; multiple matches are possible. |
| R237 | PALLET_TABLE_DATA.(PALTD_REC_NUM) | PALLET_TARIFF_TABLE.(PALT_TABLE_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  Referenced fields have no exact active unique index; multiple matches are possible. |

## Trigger evidence

`PALLET_TARIFF_TABLE_BEF_INSERT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
