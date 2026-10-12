# DAT_HISTORY

Integration and reference. Dat History records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DH_TIMESTAMP, DH_SEQUENCE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DH_MESSAGE | VARCHAR(8096) | No | — | RDB$6085 | NONE/NONE |
| 2 | DH_NOVA_UID | CHAR(8) | No | — | RDB$6086 | NONE/NONE |
| 3 | DH_PROGRAM_ID | VARCHAR(255) | No | — | RDB$6087 | NONE/NONE |
| 4 | DH_SEQUENCE | BIGINT | Yes | — | RDB$6088 | — |
| 5 | DH_TIMESTAMP | TIMESTAMP | Yes | — | RDB$6089 | — |
| 6 | DH_TYPE | CHAR(1) | Yes | — | RDB$6090 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_DAT_HISTORY | DH_TIMESTAMP, DH_SEQUENCE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R061 | DAT_HISTORY.(DH_NOVA_UID) | USERS.(U_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
