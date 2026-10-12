# MENU_FAVORITES

Users and access. Menu Favorites records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (MF_USER, MF_DESC). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | MF_SEQUENCE | SMALLINT | No | — | RDB$5516 | — |
| 2 | MF_USER | VARCHAR(50) | Yes | — | RDB$5517 | NONE/NONE |
| 3 | MF_DESC | VARCHAR(50) | Yes | — | RDB$5518 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_MENU_FAVORITES_1 | MF_USER, MF_DESC | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R155 | MENU_FAVORITES.(MF_USER) | USERS.(U_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
