# FUEL_SURCHARGE

Pricing and tariffs. Fuel Surcharge records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (FSC_TYPE, FSC_ACCOUNT, FSC_LOCATION, FSC_EFF_DATE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | FSC_TYPE | VARCHAR(1) | Yes | — | RDB$2846 | NONE/NONE |
| 2 | FSC_ACCOUNT | VARCHAR(12) | Yes | — | RDB$2847 | NONE/NONE |
| 3 | FSC_LOCATION | VARCHAR(12) | Yes | — | RDB$2848 | NONE/NONE |
| 4 | FSC_EFF_DATE | DATE | Yes | — | RDB$2849 | — |
| 5 | FSC_EXP_DATE | DATE | No | — | RDB$2850 | — |
| 6 | FSC_WEIGHT_MILE | VARCHAR(1) | No | — | RDB$2851 | NONE/NONE |
| 7 | FSC_PERCENT_AMOUNT | VARCHAR(1) | No | — | RDB$2852 | NONE/NONE |
| 8 | FSC_AMOUNT_TYPE | VARCHAR(1) | No | — | RDB$2853 | NONE/NONE |
| 9 | FSC_MINIMUM | NUMERIC(9,2) | No | — | RDB$2854 | — |
| 10 | FSC_MAXIMUM | NUMERIC(9,2) | No | — | RDB$2855 | — |
| 11 | FSC_NOT_APPICABLE | VARCHAR(1) | No | — | RDB$2856 | NONE/NONE |
| 12 | FSC_FUEL_RECOVERY_SYSTEM | VARCHAR(1) | No | — | RDB$2857 | NONE/NONE |
| 13 | FSC_TABLE_ID | VARCHAR(12) | No | — | RDB$2858 | NONE/NONE |
| 14 | FSC_SCALE_REC_NUM | BIGINT | Yes | — | RDB$2859 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| FSC_CUST | FSC_TYPE, FSC_ACCOUNT, FSC_EFF_DATE, FSC_EXP_DATE | No | Yes | ASC | — |
| FSC_KEY | FSC_TYPE, FSC_ACCOUNT, FSC_LOCATION, FSC_EFF_DATE | Yes | Yes | ASC | — |
| FSC_NUM_KEY | FSC_SCALE_REC_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R136 | FUEL_SURCHARGE.(FSC_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R137 | FUEL_SURCHARGE_DATA.(FSCD_REC_NUM) | FUEL_SURCHARGE.(FSC_SCALE_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`FSC_BEFORE_INSERT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
