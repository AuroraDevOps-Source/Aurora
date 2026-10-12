# AC_COLLECTION_NOTES

Accounts and carriers. Ac Collection Notes records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (ACN_ACCOUNT, ACN_SEQ_NUMBER). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | ACN_ACCOUNT | VARCHAR(12) | Yes | — | RDB$2125 | NONE/NONE |
| 2 | ACN_SEQ_NUMBER | INTEGER | Yes | — | RDB$2126 | — |
| 3 | ACN_DATE | DATE | No | — | RDB$2127 | — |
| 4 | ACN_TIME | TIME | No | — | RDB$2128 | — |
| 5 | ACN_INIT | VARCHAR(6) | Yes | — | RDB$2129 | NONE/NONE |
| 6 | ACN_CALL_BACK_DATE | DATE | No | — | RDB$2130 | — |
| 7 | ACN_CALL_BACK_TIME | TIME | No | — | RDB$2131 | — |
| 8 | ACN_CALL_BACK_CONTACT | VARCHAR(40) | No | — | RDB$2132 | NONE/NONE |
| 9 | ACN_CALL_BACK_PHONE | VARCHAR(10) | No | — | RDB$2133 | NONE/NONE |
| 10 | ACN_NOTES | BLOB SUB_TYPE 1 | No | — | RDB$2134 | NONE/NONE |
| 11 | ACN_RETURNCALL_TIME | TIMESTAMP | No | — | RDB$5371 | — |
| 12 | ACN_RETURNCALL_USER | VARCHAR(12) | No | — | RDB$5372 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| ACN_COL_NOTES | ACN_ACCOUNT, ACN_DATE, ACN_TIME | No | Yes | ASC | — |
| ACN_KEY | ACN_ACCOUNT, ACN_SEQ_NUMBER | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R016 | AC_COLLECTION_NOTES.(ACN_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
