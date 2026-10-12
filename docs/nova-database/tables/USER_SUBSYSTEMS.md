# USER_SUBSYSTEMS

Users and access. User Subsystems records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (US_ID, US_SUBSYSTEM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | US_ID | VARCHAR(8) | Yes | — | RDB$46 | NONE/NONE |
| 2 | US_SUBSYSTEM | VARCHAR(4) | Yes | — | D_SUBSYSTEM | NONE/NONE |
| 3 | US_LEVEL | SMALLINT | No | — | RDB$47 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_USER_SUBSYSTEMS | US_ID, US_SUBSYSTEM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R278 | USER_SUBSYSTEMS.(US_ID) | USERS.(U_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R279 | USER_SUBSYSTEMS.(US_SUBSYSTEM) | SUBSYSTEM_TYPES.(ST_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
