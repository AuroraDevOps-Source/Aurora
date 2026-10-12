# SUBSYSTEM_TYPES

Users and access. Subsystem Types records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (ST_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | ST_ID | VARCHAR(4) | Yes | — | D_SUBSYSTEM | NONE/NONE |
| 2 | ST_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_SUBSYSTEM_TYPES | ST_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R158 | MENU_ITEM_SECURITY.(MIS_SUBSYSTEM) | SUBSYSTEM_TYPES.(ST_ID) | High | Trigger SUBSYSTEM_DELETE_CLEANUP explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R279 | USER_SUBSYSTEMS.(US_SUBSYSTEM) | SUBSYSTEM_TYPES.(ST_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`SUBSYSTEM_DELETE_CLEANUP`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
