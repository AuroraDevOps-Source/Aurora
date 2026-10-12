# WEB_USERS

Users and access. Web Users records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (WU_USER_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | WU_USER_ID | VARCHAR(50) | Yes | — | RDB$4282 | NONE/NONE |
| 2 | WU_PASSWORD | VARCHAR(50) | No | — | RDB$4283 | NONE/NONE |
| 3 | WU_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 4 | WU_ACCT_ID | VARCHAR(12) | No | — | D_ACCT_ID | NONE/NONE |
| 5 | WU_CONTACT | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 6 | WU_CONTACT_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 7 | WU_CONTACT_EXTN | VARCHAR(5) | No | — | D_PHONE_EXTN | NONE/NONE |
| 8 | WU_COMMENTS | BLOB SUB_TYPE 0 | No | — | RDB$4284 | — |
| 9 | WU_ALLOW_TRACKING | VARCHAR(1) | No | — | RDB$4285 | NONE/NONE |
| 10 | WU_ALLOW_ADMIN | VARCHAR(1) | No | — | RDB$4286 | NONE/NONE |
| 11 | WU_ALLOW_MOBILE_DISPATCH | VARCHAR(1) | No | — | RDB$4510 | NONE/NONE |
| 12 | WU_DRIVER_ID | BIGINT | No | — | D_SYSID | — |
| 13 | WU_ALLOW_OPS_DASHBOARD | VARCHAR(1) | No | — | RDB$5006 | NONE/NONE |
| 14 | WU_ALLOW_ACTNG_DASHBOARD | VARCHAR(1) | No | — | RDB$5023 | NONE/NONE |
| 17 | WU_ALLOW_WAREHOUSE | VARCHAR(1) | No | — | RDB$6054 | NONE/NONE |
| 18 | WU_SHIPMENT_TYPE | VARCHAR(2) | No | — | RDB$6364 | NONE/NONE |
| 19 | WU_ALLOW_QUOTES_PICKUPS | VARCHAR(1) | No | — | RDB$6390 | NONE/NONE |
| 20 | WU_MOB_DPCH_CAT_IMG_PRO_KEY | VARCHAR(1) | No | — | RDB$6434 | NONE/NONE |
| 21 | WU_NEW_ACCT_ID_PREFIX | VARCHAR(3) | No | — | RDB$6472 | NONE/NONE |
| 22 | WU_ALLOW_ACCOUNTING | VARCHAR(1) | No | — | RDB$6478 | NONE/NONE |
| 23 | WU_DISPLAY_REVENUE | VARCHAR(1) | No | — | RDB$6563 | NONE/NONE |
| 24 | WU_ALLOW_PRESET_LINE_ITEMS | VARCHAR(1) | No | — | RDB$6595 | NONE/NONE |
| 25 | WU_ENABLE_MOBILE_PHOTO_EDITING | VARCHAR(1) | No | — | RDB$6616 | NONE/NONE |
| 26 | WU_ALLOW_MOBILE_DOCK_OPS | VARCHAR(1) | No | — | RDB$6803 | NONE/NONE |
| 27 | WU_TERMINAL | VARCHAR(12) | No | — | RDB$6962 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_WEB_USERS | WU_USER_ID | Yes | Yes | ASC | — |
| WU_DRIVER_KEY | WU_DRIVER_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R294 | WEB_ALLOWED_ACCTS.(WUAA_USER_ID) | WEB_USERS.(WU_USER_ID) | High | Trigger WEB_USER_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R295 | WEB_DASHBOARD_ITEMS.(WDI_USER_ID) | WEB_USERS.(WU_USER_ID) | High | Trigger WEB_USER_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R296 | WEB_USERS.(WU_ACCT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R297 | WEB_USERS.(WU_DRIVER_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R298 | WEB_USERS.(WU_TERMINAL) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`WEB_USER_AFTER_DELETE`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
