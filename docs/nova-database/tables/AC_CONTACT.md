# AC_CONTACT

Accounts and carriers. Ac Contact records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (ACC_ID, ACC_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | ACC_ID | VARCHAR(12) | Yes | — | D_ACCT_ID | NONE/NONE |
| 2 | ACC_SEQ | INTEGER | Yes | — | RDB$324 | — |
| 3 | ACC_LNAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 4 | ACC_FNAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 5 | ACC_OFF_NAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 6 | ACC_ADDR1 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 7 | ACC_ADDR2 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 8 | ACC_CITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 9 | ACC_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 10 | ACC_ZIP | VARCHAR(9) | No | — | D_ZIP | NONE/NONE |
| 11 | ACC_COUNTRY | VARCHAR(4) | No | — | D_COUNTRY_CODE | NONE/NONE |
| 12 | ACC_PHONE1 | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 13 | ACC_PHONE2 | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 14 | ACC_CELL | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 15 | ACC_FAX | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 16 | ACC_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 17 | ACC_TITLE | VARCHAR(24) | No | — | RDB$339 | NONE/NONE |
| 18 | ACC_FUNCTION | VARCHAR(36) | No | — | RDB$340 | NONE/NONE |
| 19 | ACC_CONT_TERM | VARCHAR(6) | No | — | RDB$341 | NONE/NONE |
| 20 | ACC_NOTES | BLOB SUB_TYPE 1 | No | — | RDB$342 | — |
| 21 | ACC_NOTIFY_EMAIL | CHAR(1) | No | — | RDB$3234 | NONE/NONE |
| 22 | ACC_NOTIFY_TEXT | CHAR(1) | No | — | RDB$3235 | NONE/NONE |
| 23 | ACC_NOTIFY_POD | CHAR(1) | No | — | RDB$3236 | NONE/NONE |
| 24 | ACC_NOTIFY_DLIV | CHAR(1) | No | — | RDB$3237 | NONE/NONE |
| 25 | ACC_NOTIFY_PU | CHAR(1) | No | — | RDB$3238 | NONE/NONE |
| 26 | ACC_NOTIFY_TEXT_CARRIER | BIGINT | No | — | RDB$3239 | — |
| 27 | ACC_NOTIFY_APT | CHAR(1) | No | — | RDB$5144 | NONE/NONE |
| 28 | ACC_NOTIFY_POSITION | CHAR(1) | No | — | RDB$6580 | NONE/NONE |
| 29 | ACC_NOTIFY_STATUS1 | INTEGER | No | — | RDB$7180 | — |
| 30 | ACC_NOTIFY_STATUS2 | INTEGER | No | — | RDB$7181 | — |
| 31 | ACC_NOTIFY_STATUS3 | INTEGER | No | — | RDB$7182 | — |
| 32 | ACC_NOTIFY_STATUS4 | INTEGER | No | — | RDB$7183 | — |
| 33 | ACC_NOTIFY_STATUS5 | INTEGER | No | — | RDB$7184 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| AC_CONTACT_PRIMARY | ACC_ID, ACC_SEQ | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R017 | AC_CONTACT.(ACC_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R018 | AC_CONTACT.(ACC_NOTIFY_TEXT_CARRIER) | NOTIFY_TEXT_CARRIER.(NTC_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
