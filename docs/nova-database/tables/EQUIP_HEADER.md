# EQUIP_HEADER

Equipment and fuel. Equipment identity master; Aurora also joins current driver IDs here.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (EH_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | EH_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | EH_UNIT_ID | VARCHAR(14) | Yes | — | D_UNIT_ID | NONE/NONE |
| 3 | EH_UNIT_TYPE | BIGINT | Yes | — | D_SYSID | — |
| 4 | EH_DELETE_DATE | DATE | No | — | RDB$61 | — |
| 5 | EH_DOMICILE | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 6 | EH_SHOP | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 7 | EH_REGION | VARCHAR(4) | No | — | D_REGION_ID | NONE/NONE |
| 8 | EH_COMMENTS | BLOB SUB_TYPE 1 | No | — | RDB$63 | NONE/NONE |
| 9 | EH_FLEET | BIGINT | No | — | D_SYSID | — |
| 10 | EH_EMPTY_WEIGHT | NUMERIC(15,3) | No | — | D_WEIGHT | — |
| 11 | EH_MAX_WEIGHT | NUMERIC(15,3) | No | — | D_WEIGHT | — |
| 12 | EH_LENGTH | NUMERIC(9,1) | No | — | D_DIMENSION | — |
| 13 | EH_WIDTH | NUMERIC(9,1) | No | — | D_DIMENSION | — |
| 14 | EH_HEIGHT | NUMERIC(9,1) | No | — | D_DIMENSION | — |
| 15 | EH_INT_LENGTH | NUMERIC(9,1) | No | — | D_DIMENSION | — |
| 16 | EH_INT_WIDTH | NUMERIC(9,1) | No | — | D_DIMENSION | — |
| 17 | EH_INT_HEIGHT | NUMERIC(9,1) | No | — | D_DIMENSION | — |
| 18 | EH_TAG_NUMBER | VARCHAR(15) | No | — | RDB$64 | NONE/NONE |
| 19 | EH_TAG_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 20 | EH_TAG_EXPIRY | DATE | No | — | RDB$65 | — |
| 21 | EH_OWNER | VARCHAR(12) | No | — | D_ACCT_ID | NONE/NONE |
| 22 | EH_INSP_DATE | DATE | No | — | RDB$66 | — |
| 23 | EH_LEASE_DATE | DATE | No | — | RDB$69 | — |
| 24 | EH_LEASE_TYPE | VARCHAR(1) | No | — | RDB$70 | NONE/NONE |
| 25 | EH_LESSOR | VARCHAR(12) | No | — | D_ACCT_ID | NONE/NONE |
| 26 | EH_VIN | VARCHAR(50) | No | — | RDB$173 | NONE/NONE |
| 27 | EH_DRIVE_TIRES | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 28 | EH_STEERING_TIRES | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 29 | EH_MAKE | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 30 | EH_MODEL | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 31 | EH_YEAR | INTEGER | No | — | RDB$3995 | — |
| 32 | EH_MC_TERMINAL_ID | VARCHAR(25) | No | — | RDB$4207 | NONE/NONE |
| 33 | EH_MC_SYSTEM_TYPE | VARCHAR(1) | No | — | RDB$4208 | NONE/NONE |
| 34 | EH_MC_QUEUE_ID | VARCHAR(25) | No | — | D_QUEUE_ID | NONE/NONE |
| 35 | EH_HUB | NUMERIC(10,2) | No | — | RDB$4259 | — |
| 36 | EH_DATE_ADDED | DATE | No | — | RDB$5068 | — |
| 37 | EH_DATE_IN_SERVICE | TIMESTAMP | No | — | RDB$5147 | — |
| 38 | EH_CONTRACT_ID | VARCHAR(32) | No | — | RDB$5617 | NONE/NONE |
| 39 | EH_SOURCE_INIT | VARCHAR(9) | No | — | RDB$6432 | NONE/NONE |
| 40 | EH_PERDIEM_DT | TIMESTAMP | No | — | RDB$6600 | — |
| 41 | EH_PERDIEM_MAN | CHAR(1) | No | — | RDB$6601 | NONE/NONE |
| 42 | EH_YARDDET_DT | TIMESTAMP | No | — | RDB$6602 | — |
| 43 | EH_YARDDET_MAN | CHAR(1) | No | — | RDB$6603 | NONE/NONE |
| 44 | EH_GOMOTIVE_EQUIP_ID | INTEGER | No | — | RDB$6845 | — |
| 45 | EH_GOMOTIVE_DISPATCH_ID | INTEGER | No | — | RDB$6846 | — |
| 46 | EH_AXLES | INTEGER | No | — | RDB$6937 | — |
| 47 | EH_BIN1_WGT | NUMERIC(15,3) | No | — | RDB$6978 | — |
| 48 | EH_BIN1_CUFT | INTEGER | No | — | RDB$6979 | — |
| 49 | EH_BIN2_WGT | NUMERIC(15,3) | No | — | RDB$6980 | — |
| 50 | EH_BIN2_CUFT | INTEGER | No | — | RDB$6981 | — |
| 51 | EH_BIN3_WGT | NUMERIC(15,3) | No | — | RDB$6982 | — |
| 52 | EH_BIN3_CUFT | INTEGER | No | — | RDB$6983 | — |
| 53 | EH_BIN4_WGT | NUMERIC(15,3) | No | — | RDB$6984 | — |
| 54 | EH_BIN4_CUFT | INTEGER | No | — | RDB$6985 | — |
| 55 | EH_BIN5_WGT | NUMERIC(15,3) | No | — | RDB$6986 | — |
| 56 | EH_BIN5_CUFT | INTEGER | No | — | RDB$6987 | — |
| 57 | EH_BIN6_WGT | NUMERIC(15,3) | No | — | RDB$6988 | — |
| 58 | EH_BIN6_CUFT | INTEGER | No | — | RDB$6989 | — |
| 59 | EH_BIN7_WGT | NUMERIC(15,3) | No | — | RDB$6990 | — |
| 60 | EH_BIN7_CUFT | INTEGER | No | — | RDB$6991 | — |
| 61 | EH_BIN8_WGT | NUMERIC(15,3) | No | — | RDB$6992 | — |
| 62 | EH_BIN8_CUFT | INTEGER | No | — | RDB$6993 | — |
| 63 | EH_BIN9_WGT | NUMERIC(15,3) | No | — | RDB$6994 | — |
| 64 | EH_BIN9_CUFT | INTEGER | No | — | RDB$6995 | — |
| 65 | EH_BIN10_WGT | NUMERIC(15,3) | No | — | RDB$6996 | — |
| 66 | EH_BIN10_CUFT | INTEGER | No | — | RDB$6997 | — |
| 67 | EH_BIN11_WGT | NUMERIC(15,3) | No | — | RDB$6998 | — |
| 68 | EH_BIN11_CUFT | INTEGER | No | — | RDB$6999 | — |
| 69 | EH_BIN12_WGT | NUMERIC(15,3) | No | — | RDB$7000 | — |
| 70 | EH_BIN12_CUFT | INTEGER | No | — | RDB$7001 | — |
| 71 | EH_MAX_CUBE | INTEGER | No | — | RDB$7002 | — |
| 74 | EH_STD_START_TM | VARCHAR(4) | No | — | RDB$7005 | NONE/NONE |
| 75 | EH_STD_END_TM | VARCHAR(4) | No | — | RDB$7006 | NONE/NONE |
| 76 | EH_MAX_OPS_MINS_PER_DAY | INTEGER | No | — | RDB$7007 | — |
| 77 | EH_MAX_ROUTE_DURATION | INTEGER | No | — | RDB$7008 | — |
| 78 | EH_ALLOW_RTE_START_TM_ADJ | INTEGER | No | — | RDB$7009 | — |
| 80 | EH_SLIP_SEAT | VARCHAR(1) | No | — | RDB$7011 | NONE/NONE |
| 81 | EH_BREAK_DURATION | INTEGER | No | — | RDB$7012 | — |
| 82 | EH_START_BREAK_AFTER_MINS | INTEGER | No | — | RDB$7013 | — |
| 83 | EH_EST_ANNUAL_MILES | INTEGER | No | — | RDB$7014 | — |
| 84 | EH_AVG_MPG | NUMERIC(7,4) | No | — | RDB$7015 | — |
| 85 | EH_IN_USE_PER_DAY | NUMERIC(7,4) | No | — | RDB$7074 | — |
| 86 | EH_START_LOC_ADR | VARCHAR(36) | No | — | RDB$7075 | NONE/NONE |
| 87 | EH_START_LOC_CITY | VARCHAR(32) | No | — | RDB$7076 | NONE/NONE |
| 88 | EH_START_LOC_ST | VARCHAR(2) | No | — | RDB$7077 | NONE/NONE |
| 89 | EH_START_LOC_ZIP | VARCHAR(9) | No | — | RDB$7078 | NONE/NONE |
| 90 | EH_END_LOC_ADR | VARCHAR(36) | No | — | RDB$7079 | NONE/NONE |
| 91 | EH_END_LOC_CITY | VARCHAR(32) | No | — | RDB$7080 | NONE/NONE |
| 92 | EH_END_LOC_ST | VARCHAR(2) | No | — | RDB$7081 | NONE/NONE |
| 93 | EH_END_LOC_ZIP | VARCHAR(9) | No | — | RDB$7082 | NONE/NONE |
| 94 | EH_OPS_DAYS_MO | NUMERIC(7,4) | No | — | RDB$7083 | — |
| 95 | EH_POWER_UNIT_COST_YR | NUMERIC(9,4) | No | — | RDB$7084 | — |
| 96 | EH_TLR_FLEET_COST_YR | NUMERIC(9,4) | No | — | RDB$7085 | — |
| 97 | EH_OVERHEAD_COST_YR | NUMERIC(9,4) | No | — | RDB$7086 | — |
| 98 | EH_PHY_DAM_COST_YR | NUMERIC(9,4) | No | — | RDB$7087 | — |
| 99 | EH_LIAB_INS_COST_YR | NUMERIC(9,4) | No | — | RDB$7088 | — |
| 100 | EH_CARGO_INS_COST_YR | NUMERIC(9,4) | No | — | RDB$7089 | — |
| 101 | EH_PERMITS_COST_YR | NUMERIC(9,4) | No | — | RDB$7090 | — |
| 102 | EH_TAGS_COST_YR | NUMERIC(9,4) | No | — | RDB$7091 | — |
| 103 | EH_HVY_ROAD_USE_COST_YR | NUMERIC(9,4) | No | — | RDB$7092 | — |
| 104 | EH_TOT_FIXED_COST_HR | NUMERIC(9,4) | No | — | RDB$7093 | — |
| 105 | EH_VAR_MNT_COST_MI | NUMERIC(9,4) | No | — | RDB$7094 | — |
| 106 | EH_VAR_TIRE_COST_MI | NUMERIC(9,4) | No | — | RDB$7095 | — |
| 107 | EH_VAR_TLR_MNT_COST_MI | NUMERIC(9,4) | No | — | RDB$7096 | — |
| 108 | EH_VAR_LIAB_INS_COST_MI | NUMERIC(9,4) | No | — | RDB$7097 | — |
| 109 | EH_VAR_CARGO_INS_COST_MI | NUMERIC(9,4) | No | — | RDB$7098 | — |
| 110 | EH_VAR_PHY_DAM_COST_MI | NUMERIC(9,4) | No | — | RDB$7099 | — |
| 111 | EH_VAR_FUEL_COST_MI | NUMERIC(9,4) | No | — | RDB$7100 | — |
| 112 | EH_TOT_VAR_COST_MI | NUMERIC(9,4) | No | — | RDB$7101 | — |
| 113 | EH_OV_WGT_PERMIT | VARCHAR(32) | No | — | RDB$7129 | NONE/NONE |
| 114 | EH_OV_WGT_PERMIT_EXP_DT | DATE | No | — | RDB$7130 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| EH_DELETE_KEY | EH_DELETE_DATE, EH_UNIT_ID, EH_UNIT_TYPE | Yes | Yes | ASC | — |
| EH_TERMINAL_ID_KEY | EH_MC_TERMINAL_ID, EH_ID | No | Yes | ASC | — |
| EH_TYPE_KEY | EH_UNIT_TYPE, EH_UNIT_ID | Yes | Yes | ASC | — |
| EH_UNIT_KEY | EH_UNIT_ID | Yes | Yes | ASC | — |
| PK_EQUIP_HEADER | EH_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R064 | DRIVER_CONTACTS.(DC_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  Driver/equipment identity inferred; driver subtype rules require Nova confirmation. |
| R067 | DRIVER_HEADER.(DH_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  Driver/equipment identity inferred; driver subtype rules require Nova confirmation. |
| R068 | DRIVER_HOS_DETAIL.(DHD_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  Driver/equipment identity inferred; driver subtype rules require Nova confirmation. |
| R069 | DRIVER_HOS_SUMMARY.(DHS_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  Driver/equipment identity inferred; driver subtype rules require Nova confirmation. |
| R070 | DRIVER_PKUP_DELV.(DPD_ID) | EQUIP_HEADER.(EH_ID) | High | Trigger EQUIP_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R073 | DRIVER_PKUP_DELV.(DPD_TRAILER_ID) | EQUIP_HEADER.(EH_ID) | High | Trigger EQUIP_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R075 | DRIVER_QUAL.(DQ_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  Driver/equipment identity inferred; driver subtype rules require Nova confirmation. |
| R077 | DRIV_ADVANCE.(DAV_DRIV_NUM) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R079 | DRIV_EXPENSE.(DX_DRIV_NUM) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R081 | DRIV_WORKFLOW_HIST.(DWH_DRV_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R089 | DS_CALLIN.(DSC_DRIVER1) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R090 | DS_CALLIN.(DSC_DRIVER2) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R091 | DS_CALLIN.(DSC_TRACTOR) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R092 | DS_CALLIN.(DSC_TRAILER1) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R093 | DS_CALLIN.(DSC_TRAILER2) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R095 | DYMAST.(DYM_EH_ID) | EQUIP_HEADER.(EH_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R097 | EQUIP_AVAIL.(EQA_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R100 | EQUIP_DESC.(ED_ID) | EQUIP_HEADER.(EH_ID) | High | Trigger EQUIP_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R102 | EQUIP_EVENT.(EQE_ID) | EQUIP_HEADER.(EH_ID) | High | Trigger EQUIP_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R103 | EQUIP_HEADER.(EH_DOMICILE) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R104 | EQUIP_HEADER.(EH_FLEET) | CT_FLEETS.(CTF_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R105 | EQUIP_HEADER.(EH_MC_QUEUE_ID) | MC_QUEUES.(MCQ_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R106 | EQUIP_HEADER.(EH_REGION) | CT_REGION_IDS.(CTR_REGION_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R107 | EQUIP_HEADER.(EH_SHOP) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R108 | EQUIP_HEADER.(EH_UNIT_TYPE) | CT_TYPE.(CTT_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R109 | EQUIP_HISTORY.(EH_ASG_DRIVER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R110 | EQUIP_HISTORY.(EH_ASG_DRIVER2) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R111 | EQUIP_HISTORY.(EH_ASG_TRACTOR) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R112 | EQUIP_HISTORY.(EH_ASG_TRAILER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R113 | EQUIP_HISTORY.(EH_ID) | EQUIP_HEADER.(EH_ID) | High | Trigger EQUIP_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R118 | EQUIP_PM.(EQPM_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R119 | EQUIP_POSITIONS.(EP_ID) | EQUIP_HEADER.(EH_ID) | High | Trigger EQUIP_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R120 | EQUIP_STATUS.(ES_ASG_CHASSIS_ID) | EQUIP_HEADER.(EH_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R121 | EQUIP_STATUS.(ES_ASG_DRIVER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R122 | EQUIP_STATUS.(ES_ASG_DRIVER2) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R123 | EQUIP_STATUS.(ES_ASG_TRACTOR) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R124 | EQUIP_STATUS.(ES_ASG_TRAILER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R125 | EQUIP_STATUS.(ES_ID) | EQUIP_HEADER.(EH_ID) | High | Trigger EQUIP_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R130 | FUEL.(FP_DRIVER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R131 | FUEL.(FP_EQUIP_NUM) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R133 | FUEL_CARD.(FC_DRIVER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R134 | FUEL_CARD.(FC_POWER_UNIT) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R168 | OPS_DRVPAY.(ODP_DRVID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R177 | OPS_HEADER.(PB_CURRENT_DRIVER) | EQUIP_HEADER.(EH_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping  |
| R178 | OPS_HEADER.(PB_CURRENT_DRIVER2) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R179 | OPS_HEADER.(PB_CURRENT_TRACTOR) | EQUIP_HEADER.(EH_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping  |
| R180 | OPS_HEADER.(PB_CURRENT_TRAILER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R185 | OPS_HEADER.(PB_PLANNED_DRIVER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R186 | OPS_HEADER.(PB_PLANNED_DRIVER2) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R187 | OPS_HEADER.(PB_PLANNED_TRACTOR) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R188 | OPS_HEADER.(PB_PLANNED_TRAILER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R193 | OPS_HISTORY.(OPH_DRIVER1) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R194 | OPS_HISTORY.(OPH_DRIVER2) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R200 | OPS_HISTORY.(OPH_TRACTOR) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R201 | OPS_HISTORY.(OPH_TRAILER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R220 | OPS_STOP_REC.(OPS_DRIVER1) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R221 | OPS_STOP_REC.(OPS_DRIVER2) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R229 | OPS_STOP_REC.(OPS_TRACTOR) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R230 | OPS_STOP_REC.(OPS_TRAILER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R235 | OPS_STOP_TLR_SPACES.(OSB_TLR_ID) | EQUIP_HEADER.(EH_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R270 | TRIPMILES.(TMI_DRIV_NUM) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R272 | TRIPMILES.(TMI_POWER_UNIT) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R284 | VREPAIRHDR.(VRH_UNIT_EH_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R297 | WEB_USERS.(WU_DRIVER_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`EQUIP_AFTER_DELETE`, `EQUIP_HEADER_BI0`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
