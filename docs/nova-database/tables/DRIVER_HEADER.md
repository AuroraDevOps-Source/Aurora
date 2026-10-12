# DRIVER_HEADER

Drivers and settlement. Driver personal/employment details; likely an extension of the equipment identity.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DH_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DH_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | DH_FIRST_NAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 3 | DH_LAST_NAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 4 | DH_ADDR1 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 5 | DH_ADDR2 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 6 | DH_CITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 7 | DH_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 8 | DH_ZIP | VARCHAR(9) | No | — | D_ZIP | NONE/NONE |
| 9 | DH_COUNTRY | VARCHAR(4) | No | — | D_COUNTRY_CODE | NONE/NONE |
| 10 | DH_HOME_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 11 | DH_FAX | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 12 | DH_CELL_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 13 | DH_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 14 | DH_WEB_SITE | VARCHAR(150) | No | — | D_URL | NONE/NONE |
| 15 | DH_BIRTH_DATE | DATE | No | — | RDB$72 | — |
| 16 | DH_HIRE_DATE | DATE | No | — | RDB$73 | — |
| 17 | DH_TERM_DATE | DATE | No | — | RDB$74 | — |
| 18 | DH_TERM_REASON | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 19 | DH_TERM_COMMENTS | BLOB SUB_TYPE 1 | No | — | RDB$75 | NONE/NONE |
| 20 | DH_SSN | VARCHAR(10) | No | — | D_SSN | NONE/NONE |
| 21 | DH_PHYS_DATE | DATE | No | — | RDB$76 | — |
| 22 | DH_PHYS_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 23 | DH_DOT_NUMBER | VARCHAR(20) | No | — | RDB$77 | NONE/NONE |
| 24 | DH_DL_EXPIRY | DATE | No | — | RDB$78 | — |
| 25 | DH_DL_NUMBER | VARCHAR(20) | No | — | RDB$79 | NONE/NONE |
| 26 | DH_DL_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 27 | DH_TYPE | VARCHAR(2) | No | — | RDB$80 | NONE/NONE |
| 28 | DH_CONTRACT | VARCHAR(12) | No | — | RDB$81 | NONE/NONE |
| 29 | DH_CLASS | VARCHAR(4) | No | — | RDB$82 | NONE/NONE |
| 30 | DH_ASG_TRACTOR | BIGINT | No | — | D_SYSID | — |
| 31 | DH_ASG_TRAILER1 | BIGINT | No | — | D_SYSID | — |
| 32 | DH_ASG_TRAILER2 | BIGINT | No | — | D_SYSID | — |
| 33 | DH_INS_AGENT | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 34 | DH_INS_AGENT_ADDR1 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 35 | DH_INS_AGENT_ADDR2 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 36 | DH_INS_AGENT_CITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 37 | DH_INS_AGENT_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 38 | DH_INS_AGENT_ZIP | VARCHAR(9) | No | — | D_ZIP | NONE/NONE |
| 39 | DH_INS_AGENT_COUNTRY | VARCHAR(4) | No | — | D_COUNTRY_CODE | NONE/NONE |
| 40 | DH_INS_AGENT_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 41 | DH_INS_AGENT_EXTN | VARCHAR(5) | No | — | D_PHONE_EXTN | NONE/NONE |
| 42 | DH_INS_AGENT_FAX | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 43 | DH_INS_AGENT_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 44 | DH_INS_COMPANY | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 45 | DH_INS_POLICY | VARCHAR(20) | No | — | RDB$83 | NONE/NONE |
| 46 | DH_INS_EFF_DATE | DATE | No | — | RDB$84 | — |
| 47 | DH_INS_EXPIRY | DATE | No | — | RDB$85 | — |
| 48 | DH_INS_AMOUNT | NUMERIC(11,2) | No | — | RDB$86 | — |
| 49 | DH_INS_DEDUCTIBLE | NUMERIC(11,2) | No | — | RDB$87 | — |
| 50 | DH_LNAME_KEY | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 51 | DH_FNAME_KEY | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 53 | DH_PAYTHRU | VARCHAR(2) | No | — | RDB$2549 | NONE/NONE |
| 54 | DH_PAYTHRU_ID | VARCHAR(12) | No | — | RDB$2550 | NONE/NONE |
| 55 | DH_CELL_TEXT_CARRIER | BIGINT | No | — | RDB$3323 | — |
| 56 | DH_PAY_GROUP | VARCHAR(12) | No | — | RDB$6435 | NONE/NONE |
| 57 | DH_DRIVERDOC_ID | BIGINT | No | — | RDB$6835 | — |
| 58 | DH_DRIVERDOC_LOADID | BIGINT | No | — | RDB$6836 | — |
| 59 | DH_FULL_PART | VARCHAR(1) | No | — | RDB$7102 | NONE/NONE |
| 60 | DH_STD_START_TIME | VARCHAR(4) | No | — | RDB$7103 | NONE/NONE |
| 61 | DH_STD_END_TIME | VARCHAR(4) | No | — | RDB$7104 | NONE/NONE |
| 62 | DH_FULL_BURDENED_HR | NUMERIC(9,4) | No | — | RDB$7105 | — |
| 63 | DH_BENEFIT_COST_HR | NUMERIC(9,4) | No | — | RDB$7106 | — |
| 64 | DH_EMPR_TAX_HR | NUMERIC(9,4) | No | — | RDB$7107 | — |
| 65 | DH_WORK_COMP_HR | NUMERIC(9,4) | No | — | RDB$7108 | — |
| 66 | DH_OVERTIME_ALLOW | VARCHAR(1) | No | — | RDB$7109 | NONE/NONE |
| 67 | DH_OVT_AFTER_MINS | INTEGER | No | — | RDB$7110 | — |
| 68 | DH_DOUBLETIME_ALLOW | VARCHAR(1) | No | — | RDB$7111 | NONE/NONE |
| 69 | DH_DOUB_AFTER_MINS | INTEGER | No | — | RDB$7112 | — |
| 70 | DH_PAID_BREAKS | VARCHAR(1) | No | — | RDB$7113 | NONE/NONE |
| 71 | DH_BRK_AFTER_MINS | INTEGER | No | — | RDB$7114 | — |
| 72 | DH_BRK_DURATION_MINS | INTEGER | No | — | RDB$7115 | — |
| 73 | DH_SMOK_YN | CHAR(1) | No | — | RDB$7116 | NONE/NONE |
| 74 | DH_VAPES_YN | CHAR(1) | No | — | RDB$7117 | NONE/NONE |
| 75 | DH_TRAINER_YN | CHAR(1) | No | — | RDB$7118 | NONE/NONE |
| 76 | DH_PET_YN | CHAR(1) | No | — | RDB$7119 | NONE/NONE |
| 77 | DH_DRV_DAY_NITE_BOTH | CHAR(1) | No | — | RDB$7120 | NONE/NONE |
| 78 | DH_TEAM_MALE_FEM_BOTH | CHAR(1) | No | — | RDB$7121 | NONE/NONE |
| 79 | DH_AIRFAREFUND | VARCHAR(1) | No | — | RDB$7148 | NONE/NONE |
| 80 | DH_WRW_SMOKER | VARCHAR(1) | No | — | RDB$7149 | NONE/NONE |
| 81 | DH_WRW_PETS | VARCHAR(1) | No | — | RDB$7150 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| DH_NAME_KEY | DH_LNAME_KEY, DH_FNAME_KEY | No | Yes | ASC | — |
| PK_DRIVER_HEADER | DH_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R065 | DRIVER_HEADER.(DH_CELL_TEXT_CARRIER) | NOTIFY_TEXT_CARRIER.(NTC_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R066 | DRIVER_HEADER.(DH_CONTRACT) | DSCONTRACT.(DSC_CONT_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R067 | DRIVER_HEADER.(DH_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  Driver/equipment identity inferred; driver subtype rules require Nova confirmation. |

## Trigger evidence

`DRIVER_HEADER_BI0`, `DRIVER_HEADER_BU0`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
