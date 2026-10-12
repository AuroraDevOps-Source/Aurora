# OPS_OTHER_PARTIES

Orders and manifests. Other parties associated with an order; includes account references and copied contact details.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (OOP_ORDER_ID, OOP_SEQUENCE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | OOP_ORDER_ID | INTEGER | Yes | — | RDB$3846 | — |
| 2 | OOP_SEQUENCE | SMALLINT | Yes | — | RDB$3847 | — |
| 3 | OOP_QUALIFIER | VARCHAR(4) | No | — | RDB$3848 | NONE/NONE |
| 4 | OOP_ACCOUNT_ID | VARCHAR(12) | No | — | D_ACCT_ID | NONE/NONE |
| 5 | OOP_ACCOUNT_NAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 6 | OOP_ADDR_1 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 7 | OOP_ADDR_2 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 8 | OOP_ADDR_3 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 9 | OOP_CITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 10 | OOP_STATE | VARCHAR(4) | No | — | D_STAT_CODE | NONE/NONE |
| 11 | OOP_ZIP | VARCHAR(9) | No | — | D_ZIP | NONE/NONE |
| 12 | OOP_COUNTRY | VARCHAR(4) | No | — | D_COUNTRY_CODE | NONE/NONE |
| 13 | OOP_APT_START_DATE_TIME | TIMESTAMP | No | — | RDB$3849 | — |
| 14 | OOP_APT_END_DATE_TIME | TIMESTAMP | No | — | RDB$3850 | — |
| 15 | OOP_CONTROL_NUM | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 16 | OOP_NOTES | BLOB SUB_TYPE 0 | No | — | RDB$3851 | — |
| 17 | OOP_CONTACT | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 18 | OOP_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 19 | OOP_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 20 | OOP_FAX | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_OOP_ORDER_ID_SEQ | OOP_ORDER_ID, OOP_SEQUENCE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R214 | OPS_OTHER_PARTIES.(OOP_ACCOUNT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R215 | OPS_OTHER_PARTIES.(OOP_ORDER_ID) | OPS_HEADER.(PB_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R216 | OPS_OTHER_PARTIES.(OOP_QUALIFIER) | QUALN1_98.(Q_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`OPS_OTHER_PARTIES_AFTER_MOD`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
