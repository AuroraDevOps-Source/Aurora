# MENU_RECENT

Users and access. Menu Recent records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (MR_USER, MR_TIME). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | MR_ID | VARCHAR(10) | Yes | — | RDB$5510 | NONE/NONE |
| 2 | MR_SEQUENCE | SMALLINT | No | — | RDB$5511 | — |
| 3 | MR_USER | VARCHAR(50) | Yes | — | RDB$5512 | NONE/NONE |
| 4 | MR_DESC | VARCHAR(50) | No | — | RDB$5513 | NONE/NONE |
| 5 | MR_TIME | TIMESTAMP | Yes | — | RDB$5514 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_MENU_RECENT_1 | MR_USER, MR_TIME | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R159 | MENU_RECENT.(MR_USER) | USERS.(U_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
