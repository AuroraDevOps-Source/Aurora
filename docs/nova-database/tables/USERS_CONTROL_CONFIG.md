# USERS_CONTROL_CONFIG

Users and access. Users Control Config records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (UCC_ID, UCC_PROGRAM_ID, UCC_CONTROL_ID, UCC_PROPERTY_NAME). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | UCC_ID | VARCHAR(8) | Yes | — | RDB$4507 | NONE/NONE |
| 2 | UCC_PROGRAM_ID | VARCHAR(50) | Yes | — | D_DESC | NONE/NONE |
| 3 | UCC_CONTROL_ID | VARCHAR(50) | Yes | — | D_DESC | NONE/NONE |
| 4 | UCC_PROPERTY_NAME | VARCHAR(255) | Yes | — | RDB$4508 | NONE/NONE |
| 5 | UCC_PROPERTY_VALUE | VARCHAR(2048) | No | — | RDB$4509 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_USERS_CONTROL_CONFIG_1 | UCC_ID, UCC_PROGRAM_ID, UCC_CONTROL_ID, UCC_PROPERTY_NAME | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R277 | USERS_CONTROL_CONFIG.(UCC_ID) | USERS.(U_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
