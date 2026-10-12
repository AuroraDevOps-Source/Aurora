# MENU_ITEM_SECURITY

Users and access. Menu Item Security records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (MIS_ID, MIS_SEQUENCE, MIS_SUBSYSTEM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | MIS_ID | VARCHAR(10) | Yes | — | D_MENU_ID | NONE/NONE |
| 2 | MIS_SEQUENCE | SMALLINT | Yes | — | RDB$14 | — |
| 3 | MIS_SUBSYSTEM | VARCHAR(4) | Yes | — | D_SUBSYSTEM | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_MENU_ITEM_SECURITY | MIS_ID, MIS_SEQUENCE, MIS_SUBSYSTEM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R157 | MENU_ITEM_SECURITY.(MIS_ID, MIS_SEQUENCE) | MENU_ITEMS.(MI_ID, MI_SEQUENCE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R158 | MENU_ITEM_SECURITY.(MIS_SUBSYSTEM) | SUBSYSTEM_TYPES.(ST_ID) | High | Trigger SUBSYSTEM_DELETE_CLEANUP explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
