# DRIVER_HOS_DETAIL

Drivers and settlement. Driver Hos Detail records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DHD_ID, DHD_START_TIME). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DHD_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | DHD_ELECTRONIC | VARCHAR(1) | No | — | RDB$4223 | NONE/NONE |
| 3 | DHD_STATUS | INTEGER | Yes | — | RDB$4224 | — |
| 4 | DHD_START_TIME | TIMESTAMP | Yes | — | RDB$4225 | — |
| 5 | DHD_END_TIME | TIMESTAMP | No | — | RDB$4226 | — |
| 6 | DHD_TRACTOR_ID | BIGINT | No | — | D_SYSID | — |
| 7 | DHD_TRAILER1_ID | BIGINT | No | — | D_SYSID | — |
| 8 | DHD_TRAILER2_ID | BIGINT | No | — | D_SYSID | — |
| 9 | DHD_BOL_NUMBER | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 10 | DHD_REMARKS | VARCHAR(255) | No | — | RDB$4227 | NONE/NONE |
| 11 | DHD_DRIVER2_ID | BIGINT | No | — | D_SYSID | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| DHD_END_DATE_KEY | DHD_ID, DHD_END_TIME | No | Yes | ASC | — |
| DHD_REVERSE_START_DATE_KEY | DHD_ID, DHD_START_TIME | No | Yes | DESC | — |
| PK_DRIVER_HOS_DETAIL | DHD_ID, DHD_START_TIME | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R068 | DRIVER_HOS_DETAIL.(DHD_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  Driver/equipment identity inferred; driver subtype rules require Nova confirmation. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
