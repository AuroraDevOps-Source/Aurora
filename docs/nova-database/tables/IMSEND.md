# IMSEND

Claims and receipts. Imsend records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (IS_CUST). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | IS_CUST | VARCHAR(12) | Yes | — | D_ACCT_ID | NONE/NONE |
| 2 | IS_CYCLE | VARCHAR(1) | No | — | RDB$5320 | NONE/NONE |
| 3 | IS_SEND_TYPE | VARCHAR(1) | No | — | RDB$5321 | NONE/NONE |
| 4 | IS_SEND_FORMAT | VARCHAR(1) | No | — | RDB$5322 | NONE/NONE |
| 5 | IS_SEND_IND_COMBO | VARCHAR(1) | No | — | RDB$5323 | NONE/NONE |
| 6 | IS_EMAIL | VARCHAR(1) | No | — | RDB$5324 | NONE/NONE |
| 7 | IS_EMAIL_ADDR | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 8 | IS_EMAIL_ADDR2 | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 9 | IS_EMAIL_SUB | VARCHAR(1) | No | — | RDB$5325 | NONE/NONE |
| 10 | IS_EMAIL_DR_SUB_MASK | VARCHAR(128) | No | — | RDB$5326 | NONE/NONE |
| 11 | IS_EMAIL_DR_FILE_MASK | VARCHAR(128) | No | — | RDB$5803 | NONE/NONE |
| 12 | IS_EMAIL_BL_SUB_MASK | VARCHAR(128) | No | — | RDB$5804 | NONE/NONE |
| 13 | IS_EMAIL_BL_FILE_MASK | VARCHAR(128) | No | — | RDB$5805 | NONE/NONE |
| 14 | IS_FTP | VARCHAR(1) | No | — | RDB$5327 | NONE/NONE |
| 15 | IS_FTP_PATH | VARCHAR(128) | No | — | RDB$5328 | NONE/NONE |
| 16 | IS_FTP_MASK | VARCHAR(128) | No | — | RDB$5329 | NONE/NONE |
| 17 | IS_FTP_PATH_PLUS_DOC_TYPE | VARCHAR(1) | No | — | RDB$5343 | NONE/NONE |
| 18 | IS_EMAIL_FILE_MASK | VARCHAR(128) | No | — | RDB$5800 | NONE/NONE |
| 19 | IS_MISC_TYPE | CHAR(2) | No | — | RDB$6578 | NONE/NONE |
| 20 | IS_DR_DESC | VARCHAR(12) | No | — | RDB$7250 | NONE/NONE |
| 21 | IS_BL_DESC | VARCHAR(12) | No | — | RDB$7251 | NONE/NONE |
| 22 | IS_MISC_DESC | VARCHAR(12) | No | — | RDB$7252 | NONE/NONE |
| 23 | IS_SIGN_DESC | VARCHAR(12) | No | — | RDB$7253 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| IMSEND_KEY | IS_CUST | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R143 | IMSEND.(IS_CUST) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
