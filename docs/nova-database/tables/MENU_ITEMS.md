# MENU_ITEMS

Users and access. Menu Items records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (MI_ID, MI_SEQUENCE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | MI_ID | VARCHAR(10) | Yes | — | D_MENU_ID | NONE/NONE |
| 2 | MI_SEQUENCE | SMALLINT | Yes | — | RDB$10 | — |
| 3 | MI_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 4 | MI_TYPE | VARCHAR(1) | No | — | RDB$11 | NONE/NONE |
| 5 | MI_PATH | VARCHAR(200) | No | — | RDB$12 | NONE/NONE |
| 6 | MI_MENU_VISIBLE | VARCHAR(1) | No | — | RDB$17 | NONE/NONE |
| 7 | MI_TB_VISIBLE | VARCHAR(1) | No | — | RDB$18 | NONE/NONE |
| 8 | MI_TB_ICON | BLOB SUB_TYPE 0 | No | — | RDB$19 | — |
| 9 | MI_OPTION_1 | VARCHAR(1) | No | — | RDB$25 | NONE/NONE |
| 10 | MI_OPTION_2 | VARCHAR(1) | No | — | RDB$26 | NONE/NONE |
| 11 | MI_OPTION_3 | VARCHAR(1) | No | — | RDB$27 | NONE/NONE |
| 12 | MI_OPTION_4 | VARCHAR(1) | No | — | RDB$28 | NONE/NONE |
| 13 | MI_OPTION_5 | VARCHAR(1) | No | — | RDB$29 | NONE/NONE |
| 14 | MI_OPTION_6 | VARCHAR(1) | No | — | RDB$30 | NONE/NONE |
| 15 | MI_OPTION_7 | VARCHAR(1) | No | — | RDB$31 | NONE/NONE |
| 16 | MI_OPTION_8 | VARCHAR(1) | No | — | RDB$32 | NONE/NONE |
| 17 | MI_OPTION_9 | VARCHAR(1) | No | — | RDB$33 | NONE/NONE |
| 18 | MI_OPTION_10 | VARCHAR(1) | No | — | RDB$34 | NONE/NONE |
| 19 | MI_LEVEL | SMALLINT | No | — | RDB$35 | — |
| 20 | MI_ADD_LEVEL | SMALLINT | No | — | RDB$36 | — |
| 21 | MI_CHANGE_LEVEL | SMALLINT | No | — | RDB$37 | — |
| 22 | MI_DELETE_LEVEL | SMALLINT | No | — | RDB$38 | — |
| 23 | MI_HINT | VARCHAR(200) | No | — | RDB$54 | NONE/NONE |
| 24 | MI_COMMAND_LINE | VARCHAR(100) | No | — | RDB$4221 | NONE/NONE |
| 25 | MI_ACCESS_CODE | VARCHAR(8) | No | — | RDB$6126 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_MENU_ITEMS | MI_ID, MI_SEQUENCE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R156 | MENU_ITEMS.(MI_ID) | MENUS.(M_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R157 | MENU_ITEM_SECURITY.(MIS_ID, MIS_SEQUENCE) | MENU_ITEMS.(MI_ID, MI_SEQUENCE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
