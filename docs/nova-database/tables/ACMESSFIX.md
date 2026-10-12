# ACMESSFIX

Accounts and carriers. Acmessfix records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (AC_C_ACCOUNT, AC_C_TYPE, AC_C_CONS). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | AC_C_ACCOUNT | VARCHAR(8) | Yes | — | RDB$6139 | NONE/NONE |
| 2 | AC_C_TYPE | VARCHAR(1) | Yes | — | RDB$6140 | NONE/NONE |
| 3 | AC_C_CONS | VARCHAR(8) | Yes | — | RDB$6141 | NONE/NONE |
| 4 | AC_C_BILLING | VARCHAR(1) | No | — | RDB$6142 | NONE/NONE |
| 5 | AC_C_RATING | VARCHAR(1) | No | — | RDB$6143 | NONE/NONE |
| 6 | AC_C_DRS | VARCHAR(1) | No | — | RDB$6144 | NONE/NONE |
| 7 | AC_C_FB | VARCHAR(1) | No | — | RDB$6145 | NONE/NONE |
| 8 | AC_ADD_ACCT | VARCHAR(8) | No | — | RDB$6146 | NONE/NONE |
| 9 | AC_C_MESS_1_ | VARCHAR(80) | No | — | RDB$6147 | NONE/NONE |
| 10 | AC_C_MESS_2_ | VARCHAR(80) | No | — | RDB$6148 | NONE/NONE |
| 11 | AC_C_MESS_3_ | VARCHAR(80) | No | — | RDB$6149 | NONE/NONE |
| 12 | AC_C_MESS_4_ | VARCHAR(80) | No | — | RDB$6150 | NONE/NONE |
| 13 | AC_C_MESS_5_ | VARCHAR(80) | No | — | RDB$6151 | NONE/NONE |
| 14 | AC_C_MESS_6_ | VARCHAR(80) | No | — | RDB$6152 | NONE/NONE |
| 15 | AC_C_MESS_7_ | VARCHAR(80) | No | — | RDB$6153 | NONE/NONE |
| 16 | AC_C_MESS_8_ | VARCHAR(80) | No | — | RDB$6154 | NONE/NONE |
| 17 | AC_C_MESS_9_ | VARCHAR(80) | No | — | RDB$6155 | NONE/NONE |
| 18 | AC_C_MESS_10_ | VARCHAR(80) | No | — | RDB$6156 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| ASMESSFIX_KEY | AC_C_ACCOUNT, AC_C_TYPE, AC_C_CONS | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R011 | ACMESSFIX.(AC_C_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
