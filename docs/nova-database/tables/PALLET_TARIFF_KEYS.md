# PALLET_TARIFF_KEYS

Pricing and tariffs. Pallet Tariff Keys records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PAL_KEYS_REC_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PAL_KEYS_REC_NUM | BIGINT | Yes | — | RDB$3699 | — |
| 2 | PAL_TARIFF_ID | VARCHAR(6) | Yes | — | RDB$3700 | NONE/NONE |
| 3 | PAL_CUSTOMER_ID | VARCHAR(12) | Yes | — | RDB$3701 | NONE/NONE |
| 4 | PAL_TYPE | VARCHAR(1) | Yes | — | RDB$3702 | NONE/NONE |
| 5 | PAL_ORIGIN_TYPE | VARCHAR(2) | Yes | — | RDB$3703 | NONE/NONE |
| 6 | PAL_ORIGIN | VARCHAR(12) | Yes | — | RDB$3704 | NONE/NONE |
| 7 | PAL_DEST_TYPE | VARCHAR(2) | Yes | — | RDB$3705 | NONE/NONE |
| 8 | PAL_DEST | VARCHAR(12) | Yes | — | RDB$3706 | NONE/NONE |
| 9 | PAL_TABLE_ID | VARCHAR(8) | Yes | — | RDB$3707 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PALLET_TARIFF_KEYS_ALTKEY | PAL_TARIFF_ID, PAL_CUSTOMER_ID, PAL_TYPE, PAL_ORIGIN_TYPE, PAL_ORIGIN, PAL_DEST_TYPE, PAL_DEST | Yes | Yes | ASC | — |
| PALLET_TARIFF_KEYS_PRIME | PAL_KEYS_REC_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R238 | PALLET_TARIFF_KEYS.(PAL_CUSTOMER_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`PALLET_TARIFF_KEYS_BEF_INSERT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
