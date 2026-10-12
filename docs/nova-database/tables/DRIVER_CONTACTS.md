# DRIVER_CONTACTS

Drivers and settlement. Driver Contacts records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DC_ID, DC_SEQUENCE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DC_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | DC_SEQUENCE | INTEGER | Yes | — | RDB$89 | — |
| 3 | DC_NAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 4 | DC_HOME_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 5 | DC_WORK_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 6 | DC_WORK_EXTN | VARCHAR(5) | No | — | D_PHONE_EXTN | NONE/NONE |
| 7 | DC_FAX | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 8 | DC_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 9 | DC_RELATION | VARCHAR(20) | No | — | RDB$88 | NONE/NONE |
| 10 | DC_EMERG_NOTIFY | CHAR(1) | No | — | RDB$321 | NONE/NONE |
| 11 | DC_CELL_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_DRIVER_CONTACTS | DC_ID, DC_SEQUENCE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R064 | DRIVER_CONTACTS.(DC_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  Driver/equipment identity inferred; driver subtype rules require Nova confirmation. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
