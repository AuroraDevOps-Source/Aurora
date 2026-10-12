# OPS_AGENTS

Orders and manifests. Agents/carriers associated with an order, with amounts and work roles.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (OPA_ORDER_ID, OPA_SEQUENCE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | OPA_ORDER_ID | INTEGER | Yes | — | RDB$2807 | — |
| 2 | OPA_SEQUENCE | SMALLINT | Yes | — | RDB$2808 | — |
| 3 | OPA_TYPE | VARCHAR(2) | Yes | — | RDB$2809 | NONE/NONE |
| 4 | OPA_WORK_CODE | VARCHAR(2) | No | — | RDB$2810 | NONE/NONE |
| 5 | OPA_AR_AP | VARCHAR(1) | Yes | — | RDB$2811 | NONE/NONE |
| 6 | OPA_AGENT_ID | VARCHAR(12) | Yes | — | RDB$2812 | NONE/NONE |
| 7 | OPA_INTERCHANGE_DATE | DATE | No | — | RDB$2813 | — |
| 8 | OPA_AMOUNT | NUMERIC(9,2) | No | — | RDB$2814 | — |
| 9 | OPA_ASC_AMOUNT | NUMERIC(9,2) | No | — | RDB$2815 | — |
| 10 | OPA_MINIMUM | NUMERIC(9,2) | No | — | RDB$2816 | — |
| 11 | OPA_MAXIMUM | NUMERIC(9,2) | No | — | RDB$2817 | — |
| 12 | OPA_PERCENT | NUMERIC(9,4) | No | — | D_PERCENTAGE | — |
| 13 | OPA_COMPUTE_TYPE | VARCHAR(1) | No | — | RDB$2819 | NONE/NONE |
| 14 | OPA_TARIFF_ID | VARCHAR(6) | No | — | RDB$2820 | NONE/NONE |
| 15 | OPA_INTERCHANGE | VARCHAR(6) | No | — | RDB$2821 | NONE/NONE |
| 16 | OPA_D83_TYPE | VARCHAR(1) | No | — | RDB$2822 | NONE/NONE |
| 17 | OPA_ADJUST_TYPE | VARCHAR(1) | No | — | RDB$2823 | NONE/NONE |
| 18 | OPA_ADJUST_PERC | NUMERIC(9,4) | No | — | D_PERCENTAGE | — |
| 19 | OPA_214 | VARCHAR(1) | No | — | RDB$2825 | NONE/NONE |
| 20 | OPA_DIRECTION | VARCHAR(1) | No | — | RDB$2826 | NONE/NONE |
| 21 | OPA_AGENT_PRO_ID | VARCHAR(30) | No | — | RDB$2827 | NONE/NONE |
| 22 | OPA_DELIVERY_DATE | TIMESTAMP | No | — | RDB$2828 | — |
| 23 | OPA_PICKUP_DATE | TIMESTAMP | No | — | RDB$2829 | — |
| 24 | OPA_PAID_AMOUNT | NUMERIC(9,2) | No | — | RDB$2830 | — |
| 25 | OPA_PAID_DATE | DATE | No | — | RDB$2831 | — |
| 26 | OPA_DRIVER_NAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 27 | OPA_DRIVER_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 28 | OPA_DRIVER_CELL | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 29 | OPA_DRIVER_CELL_CARRIER | INTEGER | No | — | RDB$6172 | — |
| 30 | OPA_OVERRIDE_AMT_IND | VARCHAR(1) | No | — | RDB$6394 | NONE/NONE |
| 31 | OPA_AP_CO | VARCHAR(2) | No | — | RDB$6529 | NONE/NONE |
| 32 | OPA_AP_VENDOR_NO | VARCHAR(12) | No | — | RDB$6530 | NONE/NONE |
| 33 | OPA_AP_INVOICE_ID | VARCHAR(32) | No | — | RDB$6531 | NONE/NONE |
| 34 | OPA_AP_INV_DATE | DATE | No | — | RDB$6532 | — |
| 35 | OPA_AP_REC_TYPE | VARCHAR(2) | No | — | RDB$6533 | NONE/NONE |
| 36 | OPA_ORIG_TERM | VARCHAR(12) | No | — | RDB$6938 | NONE/NONE |
| 37 | OPA_DEST_TERM | VARCHAR(12) | No | — | RDB$6939 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| OPS_AGENT_PRO_KEY | OPA_AGENT_PRO_ID, OPA_AGENT_ID | No | Yes | ASC | — |
| PK_OPS_AGENTS_ID_SEQ | OPA_ORDER_ID, OPA_SEQUENCE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R033 | CA_HISTORY.(CAH_ORDER_ID, CAH_ACCT_ID) | OPS_AGENTS.(OPA_ORDER_ID, OPA_AGENT_ID) | High | Trigger OPS_AGENTS_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. Referenced fields have no exact active unique index; multiple matches are possible. |
| R161 | OPS_AGENTS.(OPA_AGENT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R162 | OPS_AGENTS.(OPA_DEST_TERM) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R163 | OPS_AGENTS.(OPA_DRIVER_CELL_CARRIER) | NOTIFY_TEXT_CARRIER.(NTC_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R164 | OPS_AGENTS.(OPA_ORDER_ID) | OPS_HEADER.(PB_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping; Trigger OPS_HEADER_AFTER_DELETE explicitly deletes matching child rows  |
| R165 | OPS_AGENTS.(OPA_ORIG_TERM) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R167 | OPS_AG_LINES.(OPAL_ORDER_ID, OPAL_OPA_SEQUENCE) | OPS_AGENTS.(OPA_ORDER_ID, OPA_SEQUENCE) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |

## Trigger evidence

`OPS_AGENTS_AFTER_DELETE`, `OPS_AGENTS_AFTER_MOD`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
