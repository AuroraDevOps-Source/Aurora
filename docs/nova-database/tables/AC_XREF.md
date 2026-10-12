# AC_XREF

Accounts and carriers. Ac Xref records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (ACX_IDENTIFIER, ACX_THEIR_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | ACX_IDENTIFIER | VARCHAR(8) | Yes | — | RDB$6907 | NONE/NONE |
| 2 | ACX_THEIR_ID | VARCHAR(24) | Yes | — | RDB$6908 | NONE/NONE |
| 3 | ACX_OUR_ID | VARCHAR(12) | No | — | RDB$6909 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| AC_XREF_KEY | ACX_IDENTIFIER, ACX_THEIR_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R023 | AC_XREF.(ACX_OUR_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
