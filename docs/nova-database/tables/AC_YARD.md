# AC_YARD

Accounts and carriers. Ac Yard records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (ACY_YARD_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | ACY_YARD_ID | VARCHAR(12) | Yes | — | D_ACCT_ID | NONE/NONE |
| 2 | ACY_SCAC | VARCHAR(8) | No | — | RDB$4054 | NONE/NONE |
| 3 | ACY_NOTIFY_NAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 4 | ACY_NOTIFY_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 5 | ACY_NOTIFY_FAX | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 6 | ACY_NOTIFY_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 7 | ACY_INBOUND_NAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 8 | ACY_INBOUND_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 9 | ACY_INBOUND_FAX | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 10 | ACY_INBOUND_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 11 | ACY_OUTBOUND_NAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 12 | ACY_OUTBOUND_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 13 | ACY_OUTBOUND_FAX | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 14 | ACY_OUTBOUND_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 15 | ACY_MAINT_NAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 16 | ACY_MAINT_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 17 | ACY_MAINT_FAX | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 18 | ACY_MAINT_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 19 | ACY_SHOP_NAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 20 | ACY_SHOP_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 21 | ACY_SHOP_FAX | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 22 | ACY_SHOP_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 23 | ACY_CHAS_NAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 24 | ACY_CHAS_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 25 | ACY_CHAS_FAX | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 26 | ACY_CHAS_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 27 | ACY_TRACE_NAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 28 | ACY_TRACE_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 29 | ACY_TRACE_FAX | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 30 | ACY_TRACE_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 31 | ACY_NOTES | BLOB SUB_TYPE 1 | No | — | RDB$4055 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| ACYARD_PRIME_KEY | ACY_YARD_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R024 | AC_YARD.(ACY_YARD_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
