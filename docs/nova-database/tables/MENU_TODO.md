# MENU_TODO

Users and access. Menu Todo records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (MT_USER, MT_TIME). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | MT_TIME | TIMESTAMP | Yes | — | RDB$5524 | — |
| 2 | MT_USER | VARCHAR(50) | Yes | — | RDB$5525 | NONE/NONE |
| 3 | MT_SUBJECT | VARCHAR(50) | No | — | RDB$5526 | NONE/NONE |
| 4 | MT_TODO | BLOB SUB_TYPE 0 | No | — | RDB$5527 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_MENU_TODO_1 | MT_USER, MT_TIME | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R160 | MENU_TODO.(MT_USER) | USERS.(U_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
