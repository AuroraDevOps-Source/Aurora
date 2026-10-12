# CT_GRID_COLUMNS

Codes and configuration. Ct Grid Columns records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CGC_USER_ID, CGC_GRID_NAME). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CGC_USER_ID | VARCHAR(30) | Yes | — | RDB$170 | NONE/NONE |
| 2 | CGC_GRID_NAME | VARCHAR(30) | Yes | — | RDB$171 | NONE/NONE |
| 3 | CGC_COLUMN_PROPERTIES | BLOB SUB_TYPE 1 | No | — | RDB$172 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CGC_PRIMARY | CGC_USER_ID, CGC_GRID_NAME | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R050 | CT_GRID_COLUMNS.(CGC_USER_ID) | USERS.(U_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
