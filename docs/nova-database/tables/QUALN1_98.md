# QUALN1_98

Integration and reference. Qualn1 98 records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (Q_CODE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Q_CODE | VARCHAR(4) | Yes | — | RDB$3870 | NONE/NONE |
| 2 | Q_SEQUENCE | BIGINT | Yes | — | RDB$3871 | — |
| 3 | Q_DESCRIPTION | VARCHAR(30) | No | — | RDB$3872 | NONE/NONE |
| 4 | Q_I | CHAR(1) | No | — | RDB$3873 | NONE/NONE |
| 5 | Q_T | CHAR(1) | No | — | RDB$3874 | NONE/NONE |
| 6 | Q_L | CHAR(1) | No | — | RDB$3875 | NONE/NONE |
| 7 | Q_AF | CHAR(1) | No | — | RDB$7144 | NONE/NONE |
| 8 | Q_AG | CHAR(1) | No | — | RDB$7145 | NONE/NONE |
| 9 | Q_NO | CHAR(1) | No | — | RDB$7146 | NONE/NONE |
| 10 | Q_WH | CHAR(1) | No | — | RDB$7147 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| QUALN1_98_KEY | Q_CODE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R216 | OPS_OTHER_PARTIES.(OOP_QUALIFIER) | QUALN1_98.(Q_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R225 | OPS_STOP_REC.(OPS_QUAL) | QUALN1_98.(Q_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
