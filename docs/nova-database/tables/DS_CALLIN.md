# DS_CALLIN

Drivers and settlement. Ds Callin records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DSC_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DSC_SEQ | BIGINT | Yes | — | RDB$3346 | — |
| 2 | DSC_DATE | TIMESTAMP | Yes | — | RDB$3347 | — |
| 3 | DSC_COMMENTS | BLOB SUB_TYPE 1 | No | — | RDB$3348 | NONE/NONE |
| 4 | DSC_DRIVER1 | BIGINT | No | — | D_SYSID | — |
| 5 | DSC_DRIVER2 | BIGINT | No | — | D_SYSID | — |
| 6 | DSC_TRACTOR | BIGINT | No | — | D_SYSID | — |
| 7 | DSC_TRAILER1 | BIGINT | No | — | D_SYSID | — |
| 8 | DSC_TRAILER2 | BIGINT | No | — | D_SYSID | — |
| 9 | DSC_TRC_HUB_MILES | INTEGER | No | — | RDB$3349 | — |
| 10 | DSC_TLR1_HUB_MILES | INTEGER | No | — | RDB$3350 | — |
| 11 | DSC_TLR2_HUB_MILES | INTEGER | No | — | RDB$3351 | — |
| 12 | DSC_LOCATION | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 13 | DSC_LOC_TYPE | VARCHAR(1) | No | — | D_LOC_TYPE | NONE/NONE |
| 14 | DSC_LOC_CITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 15 | DSC_LOC_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 16 | DSC_LOC_ZIP | VARCHAR(9) | No | — | D_ZIP | NONE/NONE |
| 17 | DSC_LOC_LONG | VARCHAR(50) | No | — | RDB$3352 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| DSC_DRIVER_KEY | DSC_DRIVER1, DSC_DATE | No | Yes | ASC | — |
| DSC_PRIM_KEY | DSC_SEQ | Yes | Yes | ASC | — |
| DS_CALLIN_DATE_KEY | DSC_DATE | No | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R089 | DS_CALLIN.(DSC_DRIVER1) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R090 | DS_CALLIN.(DSC_DRIVER2) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R091 | DS_CALLIN.(DSC_TRACTOR) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R092 | DS_CALLIN.(DSC_TRAILER1) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R093 | DS_CALLIN.(DSC_TRAILER2) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`DS_CALLIN_BEFORE_INSERT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
