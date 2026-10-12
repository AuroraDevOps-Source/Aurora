# USERS

Users and access. Users records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (U_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | U_ID | VARCHAR(8) | Yes | — | RDB$42 | NONE/NONE |
| 2 | U_NAME | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 3 | U_PASSWORD | VARCHAR(8) | No | — | RDB$43 | NONE/NONE |
| 4 | U_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 5 | U_FAX | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 6 | U_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 7 | U_EMAIL_SMTP_ID | VARCHAR(32) | No | — | RDB$6826 | NONE/NONE |
| 8 | U_EMAIL_SMTP_PW | VARCHAR(32) | No | — | RDB$6827 | NONE/NONE |
| 9 | U_DFLT_LEVEL | SMALLINT | No | — | RDB$44 | — |
| 10 | U_LOCATION | VARCHAR(8) | No | — | RDB$45 | NONE/NONE |
| 11 | U_HOME_PAGE | VARCHAR(200) | No | — | RDB$48 | NONE/NONE |
| 12 | U_MAIN_MENU | VARCHAR(10) | No | — | D_MENU_ID | NONE/NONE |
| 13 | U_STYLE | VARCHAR(50) | No | — | RDB$49 | NONE/NONE |
| 14 | U_COMPANY_CODE | VARCHAR(2) | No | — | RDB$716 | NONE/NONE |
| 15 | U_DFLT_LOAD_TYPE | CHAR(1) | No | — | RDB$3741 | NONE/NONE |
| 16 | U_MOBILE_MSG_QUEUE | VARCHAR(25) | No | — | D_QUEUE_ID | NONE/NONE |
| 17 | U_DFLT_BILL_TYPE | CHAR(1) | No | — | RDB$4233 | NONE/NONE |
| 18 | U_PRINT_QUEUE | VARCHAR(25) | No | — | D_QUEUE_ID | NONE/NONE |
| 19 | U_DFLT_BROKERAGE_SHIPMENTS | VARCHAR(1) | No | — | D_YESNO | NONE/NONE |
| 20 | U_DASHBOARD | VARCHAR(200) | No | — | RDB$6164 | NONE/NONE |
| 21 | U_DASH_ITEM_1 | VARCHAR(10) | No | — | RDB$6165 | NONE/NONE |
| 22 | U_DASH_ITEM_2 | VARCHAR(10) | No | — | RDB$6166 | NONE/NONE |
| 23 | U_DASH_ITEM_3 | VARCHAR(10) | No | — | RDB$6167 | NONE/NONE |
| 24 | U_DASH_ITEM_4 | VARCHAR(10) | No | — | RDB$6168 | NONE/NONE |
| 25 | U_DASH_ITEM_5 | VARCHAR(10) | No | — | RDB$6169 | NONE/NONE |
| 26 | U_DASH_ITEM_6 | VARCHAR(10) | No | — | RDB$6170 | NONE/NONE |
| 27 | U_DEFAULT_DELIVERY_ONLY | VARCHAR(1) | No | — | RDB$6517 | NONE/NONE |
| 28 | U_DEFAULT_MFST_TO_DRIVER | VARCHAR(1) | No | — | RDB$6518 | NONE/NONE |
| 29 | U_QB_IS_REMOTE | VARCHAR(1) | No | — | RDB$6557 | NONE/NONE |
| 30 | U_QB_FULL_URL | VARCHAR(60) | No | — | RDB$6558 | NONE/NONE |
| 31 | U_QB_HOST | VARCHAR(60) | No | — | RDB$6559 | NONE/NONE |
| 32 | U_QB_USERNAME | VARCHAR(20) | No | — | RDB$6560 | NONE/NONE |
| 33 | U_QB_PASSWORD | VARCHAR(20) | No | — | RDB$6561 | NONE/NONE |
| 34 | U_USER_LOCATION_DEFAULT | VARCHAR(1) | No | — | RDB$6577 | NONE/NONE |
| 35 | U_OE_NO_AMTS | CHAR(1) | No | — | RDB$6863 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_USERS | U_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R050 | CT_GRID_COLUMNS.(CGC_USER_ID) | USERS.(U_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R060 | DAT_ACCOUNT.(DA_NOVA_UID) | USERS.(U_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R061 | DAT_HISTORY.(DH_NOVA_UID) | USERS.(U_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R063 | DAT_QUEUE.(DQ_NOVA_UID) | USERS.(U_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R155 | MENU_FAVORITES.(MF_USER) | USERS.(U_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R159 | MENU_RECENT.(MR_USER) | USERS.(U_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R160 | MENU_TODO.(MT_USER) | USERS.(U_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R274 | USERS.(U_LOCATION) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R275 | USERS.(U_MAIN_MENU) | MENUS.(M_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R276 | USERS.(U_MOBILE_MSG_QUEUE) | MC_QUEUES.(MCQ_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R277 | USERS_CONTROL_CONFIG.(UCC_ID) | USERS.(U_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R278 | USER_SUBSYSTEMS.(US_ID) | USERS.(U_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
