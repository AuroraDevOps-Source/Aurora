# AC_AGENT

Accounts and carriers. Ac Agent records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (ICA_REC_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | ICA_AGENT_ID | VARCHAR(12) | Yes | — | RDB$2551 | NONE/NONE |
| 2 | ICA_TYPE | VARCHAR(1) | No | — | RDB$2552 | NONE/NONE |
| 3 | ICA_ADV_BYD_CODE | VARCHAR(2) | No | — | RDB$2553 | NONE/NONE |
| 4 | ICA_AR_AP | VARCHAR(1) | No | — | RDB$2554 | NONE/NONE |
| 5 | ICA_REC_NUM | NUMERIC(18,0) | Yes | — | RDB$2555 | — |
| 6 | ICA_SCAC_CODE | VARCHAR(8) | No | — | RDB$2556 | NONE/NONE |
| 7 | ICA_COMPUTE_TYPE | VARCHAR(1) | No | — | RDB$2557 | NONE/NONE |
| 8 | ICA_AMOUNT | NUMERIC(9,2) | No | — | RDB$2558 | — |
| 9 | ICA_PERCENT | NUMERIC(9,4) | No | — | D_PERCENTAGE | — |
| 10 | ICA_TARIFF | VARCHAR(6) | No | — | RDB$2560 | NONE/NONE |
| 11 | ICA_MINIMUM | NUMERIC(9,2) | No | — | RDB$2561 | — |
| 12 | ICA_MAXIMUM | NUMERIC(9,2) | No | — | RDB$2562 | — |
| 13 | ICA_OUR_MINIMUM | NUMERIC(9,2) | No | — | RDB$2563 | — |
| 14 | ICA_MIN_50_50 | CHAR(1) | No | — | RDB$2564 | NONE/NONE |
| 15 | ICA_D83_SPLIT | CHAR(1) | No | — | RDB$2565 | NONE/NONE |
| 16 | ICA_D83_TYPE | CHAR(1) | No | — | RDB$2566 | NONE/NONE |
| 17 | ICA_D83_USE_MINS | CHAR(1) | No | — | RDB$2567 | NONE/NONE |
| 18 | ICA_REBATE | CHAR(1) | No | — | RDB$2568 | NONE/NONE |
| 19 | ICA_MINUS_IL | CHAR(1) | No | — | RDB$2569 | NONE/NONE |
| 20 | ICA_MINUS_CARTAGE | CHAR(1) | No | — | RDB$2570 | NONE/NONE |
| 21 | ICA_MINUS_AGENT | CHAR(1) | No | — | RDB$2571 | NONE/NONE |
| 22 | ICA_CART_CALC_METH | CHAR(1) | No | — | RDB$2572 | NONE/NONE |
| 23 | ICA_CONTRACT_ID | VARCHAR(12) | No | — | RDB$2573 | NONE/NONE |
| 24 | ICA_PAY_THRU | CHAR(1) | No | — | RDB$2574 | NONE/NONE |
| 25 | ICA_VENDOR_ID | VARCHAR(12) | No | — | RDB$2575 | NONE/NONE |
| 26 | ICA_PROT_DISC | NUMERIC(9,4) | No | — | D_PERCENTAGE | — |
| 27 | ICA_OUR_PROT_DISC | NUMERIC(9,4) | No | — | D_PERCENTAGE | — |
| 28 | ICA_BILL_ALWAYS | VARCHAR(1) | No | — | RDB$4260 | NONE/NONE |
| 29 | ICA_MINUS_PICKUP | CHAR(1) | No | — | RDB$6361 | NONE/NONE |
| 30 | ICA_MINUS_DELIVERY | CHAR(1) | No | — | RDB$6362 | NONE/NONE |
| 31 | ICA_TARIFF_PERCENT | NUMERIC(9,4) | No | — | RDB$6363 | — |
| 32 | ICA_CYCLE_CODE | CHAR(1) | No | — | RDB$6564 | NONE/NONE |
| 33 | ICA_DAYS_BACK | CHAR(2) | No | — | RDB$6565 | NONE/NONE |
| 34 | ICA_REQ_AMT_SPREADSHEET | CHAR(1) | No | — | RDB$6566 | NONE/NONE |
| 35 | ICA_SEND_AMT_SPREADSHEET | CHAR(1) | No | — | RDB$6567 | NONE/NONE |
| 36 | ICA_INCL_TOT_REV | CHAR(1) | No | — | RDB$6568 | NONE/NONE |
| 37 | ICA_FILE_FORMAT | CHAR(1) | No | — | RDB$6569 | NONE/NONE |
| 38 | ICA_TRANSMIT_FORMAT | CHAR(1) | No | — | RDB$6570 | NONE/NONE |
| 39 | ICA_REQ_AMT_EMAIL_1 | CHAR(50) | No | — | RDB$6571 | NONE/NONE |
| 40 | ICA_REQ_AMT_EMAIL_2 | CHAR(50) | No | — | RDB$6572 | NONE/NONE |
| 41 | ICA_REQ_AMT_EMAIL_SUBJECT | CHAR(50) | No | — | RDB$6573 | NONE/NONE |
| 42 | ICA_SEND_AMT_EMAIL_1 | CHAR(50) | No | — | RDB$6574 | NONE/NONE |
| 43 | ICA_SEND_AMT_EMAIL_2 | CHAR(50) | No | — | RDB$6575 | NONE/NONE |
| 44 | ICA_SEND_AMT_EMAIL_SUBJECT | CHAR(50) | No | — | RDB$6576 | NONE/NONE |
| 45 | ICA_DAYS_BACK_SEND | CHAR(2) | No | — | RDB$6807 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| ICA_CUST | ICA_AGENT_ID, ICA_TYPE, ICA_ADV_BYD_CODE, ICA_AR_AP | No | Yes | ASC | — |
| ICA_KEY | ICA_REC_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R012 | AC_AGENT.(ICA_AGENT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R013 | AC_AGENT_CONTRACTS.(ICAC_CONTRACT_ID) | AC_AGENT.(ICA_CONTRACT_ID) | Low | Shared contract identifier; AC_AGENT has no unique contract key Could be a shared contract definition rather than a child owned by one agent. Referenced fields have no exact active unique index; multiple matches are possible. |
| R014 | AC_AGENT_INTER.(ICA_D83_REC_NUM) | AC_AGENT.(ICA_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
