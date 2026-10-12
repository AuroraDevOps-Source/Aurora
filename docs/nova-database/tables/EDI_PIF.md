# EDI_PIF

Integration and reference. Edi Pif records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PIF_ACCT_ID, PIF_TRAN). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PIF_ACCT_ID | VARCHAR(12) | Yes | — | RDB$3384 | NONE/NONE |
| 2 | PIF_CUST_IS | VARCHAR(1) | No | — | RDB$3385 | NONE/NONE |
| 3 | PIF_TRAN | VARCHAR(6) | Yes | — | RDB$3386 | NONE/NONE |
| 4 | PIF_SEND_SHIPPER | VARCHAR(1) | No | — | RDB$3387 | NONE/NONE |
| 5 | PIF_SEND_CONS | VARCHAR(1) | No | — | RDB$3388 | NONE/NONE |
| 6 | PIF_SEND_BILLTO | VARCHAR(1) | No | — | RDB$3389 | NONE/NONE |
| 7 | PIF_HYPHEN | VARCHAR(1) | No | — | RDB$3390 | NONE/NONE |
| 8 | PIF_210_DELAY | INTEGER | No | — | RDB$3391 | — |
| 9 | PIF_CYCLE | VARCHAR(1) | No | — | RDB$3392 | NONE/NONE |
| 10 | PIF_CONT_DELIV | VARCHAR(1) | No | — | RDB$3393 | NONE/NONE |
| 11 | PIF_214_ETA | VARCHAR(1) | No | — | RDB$3394 | NONE/NONE |
| 12 | PIF_214_APPT_PU | VARCHAR(1) | No | — | RDB$3395 | NONE/NONE |
| 13 | PIF_214_APPT_DL | VARCHAR(1) | No | — | RDB$3396 | NONE/NONE |
| 14 | PIF_214_ARIV_PU | VARCHAR(1) | No | — | RDB$3397 | NONE/NONE |
| 15 | PIF_214_ARIV_DL | VARCHAR(1) | No | — | RDB$3398 | NONE/NONE |
| 16 | PIF_214_BACKIN_PU | VARCHAR(1) | No | — | RDB$3399 | NONE/NONE |
| 17 | PIF_214_BACKIN_DL | VARCHAR(1) | No | — | RDB$3400 | NONE/NONE |
| 18 | PIF_214_RELEASE_PU | VARCHAR(1) | No | — | RDB$3401 | NONE/NONE |
| 19 | PIF_214_RELEASE_DL | VARCHAR(1) | No | — | RDB$3402 | NONE/NONE |
| 20 | PIF_214_DEPART_PU | VARCHAR(1) | No | — | RDB$3403 | NONE/NONE |
| 21 | PIF_214_DEPART_DL | VARCHAR(1) | No | — | RDB$3404 | NONE/NONE |
| 22 | PIF_214_TENDER | VARCHAR(1) | No | — | RDB$3405 | NONE/NONE |
| 23 | PIF_PPD_COL | VARCHAR(1) | No | — | RDB$3406 | NONE/NONE |
| 24 | PIF_DELIMETER | VARCHAR(1) | No | — | RDB$3407 | NONE/NONE |
| 25 | PIF_VAN | VARCHAR(14) | No | — | RDB$3408 | NONE/NONE |
| 26 | PIF_RECEIVER_ID | VARCHAR(16) | No | — | RDB$3409 | NONE/NONE |
| 27 | PIF_OUTPUT_FILE_PRE | VARCHAR(24) | No | — | RDB$3410 | NONE/NONE |
| 28 | PIF_OUTPUT_FILE_SUF | VARCHAR(6) | No | — | RDB$3411 | NONE/NONE |
| 29 | PIF_CONTROL_COUNT | NUMERIC(8,2) | No | — | RDB$3412 | — |
| 30 | PIF_LAST_SENT | DATE | No | — | RDB$3413 | — |
| 31 | PIF_DEFAULT_SCAC | VARCHAR(4) | No | — | RDB$3414 | NONE/NONE |
| 32 | PIF_COMM_TYPE | VARCHAR(4) | No | — | RDB$3415 | NONE/NONE |
| 33 | PIF_CUST_OPT | VARCHAR(4) | No | — | RDB$3416 | NONE/NONE |
| 34 | PIF_STM_PREFIX | VARCHAR(24) | No | — | RDB$6525 | NONE/NONE |
| 35 | PIF_STM_SUFFIX | VARCHAR(6) | No | — | RDB$6526 | NONE/NONE |
| 36 | PIF_FSU_EMAIL_ADR | VARCHAR(80) | No | — | RDB$7131 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| EDI_PIF_KEY | PIF_ACCT_ID, PIF_TRAN | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R096 | EDI_PIF.(PIF_ACCT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
