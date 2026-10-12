# AC_NOTES

Accounts and carriers. Ac Notes records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (ACNO_ACCOUNT, ACNO_TYPE, ACNO_OTHER_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | ACNO_ACCOUNT | VARCHAR(12) | Yes | — | RDB$2179 | NONE/NONE |
| 2 | ACNO_TYPE | CHAR(1) | Yes | — | RDB$2180 | NONE/NONE |
| 3 | ACNO_OTHER_ID | VARCHAR(12) | Yes | — | RDB$7263 | NONE/NONE |
| 4 | ACNO_NOTES | BLOB SUB_TYPE 1 | No | — | RDB$2181 | NONE/NONE |
| 5 | ACNO_SHIPPER | CHAR(1) | No | — | RDB$5078 | NONE/NONE |
| 6 | ACNO_CONS | CHAR(1) | No | — | RDB$5079 | NONE/NONE |
| 7 | ACNO_B2 | CHAR(1) | No | — | RDB$5080 | NONE/NONE |
| 8 | ACNO_NOTES_SHIPPER | BLOB SUB_TYPE 1 | No | — | RDB$6128 | NONE/NONE |
| 9 | ACNO_NOTES_CONS | BLOB SUB_TYPE 1 | No | — | RDB$6129 | NONE/NONE |
| 10 | ACNO_NOTES_BILLTO | BLOB SUB_TYPE 1 | No | — | RDB$6130 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_AC_NOTES | ACNO_ACCOUNT, ACNO_TYPE, ACNO_OTHER_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R019 | AC_NOTES.(ACNO_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
