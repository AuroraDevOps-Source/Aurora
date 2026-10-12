# OPS_STOP_REC

Orders and manifests. Stops belonging to a header; stores pickup/delivery details and manifest routing stops.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (OPS_STOP_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | OPS_STOP_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | OPS_ORDER_ID | NUMERIC(9,0) | Yes | — | RDB$563 | — |
| 3 | OPS_STOP_NUMBER | NUMERIC(4,0) | Yes | — | RDB$564 | — |
| 4 | OPS_QUAL | VARCHAR(4) | No | — | D_QUALIFIER | NONE/NONE |
| 5 | OPS_TYPE | VARCHAR(2) | No | — | RDB$566 | NONE/NONE |
| 6 | OPS_ACCTID | VARCHAR(12) | No | — | D_ACCT_ID | NONE/NONE |
| 7 | OPS_NAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 8 | OPS_ADDR1 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 9 | OPS_ADDR2 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 10 | OPS_ADDR3 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 11 | OPS_CITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 12 | OPS_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 13 | OPS_ZIP | VARCHAR(9) | No | — | D_ZIP | NONE/NONE |
| 14 | OPS_COUNTRY | VARCHAR(4) | No | — | D_COUNTRY_CODE | NONE/NONE |
| 15 | OPS_CONTACT | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 16 | OPS_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 17 | OPS_EXTN | VARCHAR(5) | No | — | D_PHONE_EXTN | NONE/NONE |
| 18 | OPS_BOL | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 19 | OPS_PO | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 20 | OPS_APPT_LOW | TIMESTAMP | No | — | RDB$567 | — |
| 21 | OPS_ROUTE | VARCHAR(12) | No | — | D_ROUTE_ID | NONE/NONE |
| 22 | OPS_ROUTE_STOP | INTEGER | No | — | RDB$570 | — |
| 23 | OPS_SUN_OPEN | TIME | No | — | RDB$571 | — |
| 24 | OPS_SUN_CLOSE | TIME | No | — | RDB$572 | — |
| 25 | OPS_MON_OPEN | TIME | No | — | RDB$573 | — |
| 26 | OPS_MON_CLOSE | TIME | No | — | RDB$574 | — |
| 27 | OPS_TUE_OPEN | TIME | No | — | RDB$575 | — |
| 28 | OPS_TUE_CLOSE | TIME | No | — | RDB$576 | — |
| 29 | OPS_WED_OPEN | TIME | No | — | RDB$577 | — |
| 30 | OPS_WED_CLOSE | TIME | No | — | RDB$578 | — |
| 31 | OPS_THU_OPEN | TIME | No | — | RDB$579 | — |
| 32 | OPS_THU_CLOSE | TIME | No | — | RDB$580 | — |
| 33 | OPS_FRI_OPEN | TIME | No | — | RDB$581 | — |
| 34 | OPS_FRI_CLOSE | TIME | No | — | RDB$582 | — |
| 35 | OPS_SAT_OPEN | TIME | No | — | RDB$583 | — |
| 36 | OPS_SAT_CLOSE | TIME | No | — | RDB$584 | — |
| 37 | OPS_READY_TIME | TIMESTAMP | No | — | RDB$585 | — |
| 38 | OPS_CLOSE_TIME | TIME | No | — | RDB$586 | — |
| 39 | OPS_DRIVER1 | BIGINT | No | — | D_SYSID | — |
| 40 | OPS_DRIVER2 | BIGINT | No | — | D_SYSID | — |
| 41 | OPS_TRACTOR | BIGINT | No | — | D_SYSID | — |
| 42 | OPS_TRAILER | BIGINT | No | — | D_SYSID | — |
| 43 | OPS_ARRIVE_TIME | TIMESTAMP | No | — | RDB$587 | — |
| 44 | OPS_DEPART_TIME | TIMESTAMP | No | — | RDB$588 | — |
| 45 | OPS_BACKIN_TIME | TIMESTAMP | No | — | RDB$589 | — |
| 46 | OPS_RELEASE_TIME | TIMESTAMP | No | — | RDB$590 | — |
| 47 | OPS_DISPATCH_TIME | TIMESTAMP | No | — | RDB$591 | — |
| 48 | OPS_ONBOARD_TIME | TIMESTAMP | No | — | RDB$592 | — |
| 49 | OPS_DELIVERED_TIME | TIMESTAMP | No | — | RDB$593 | — |
| 50 | OPS_RECOVERED_TIME | TIMESTAMP | No | — | RDB$594 | — |
| 51 | OPS_NOTES | BLOB SUB_TYPE 1 | No | — | RDB$595 | NONE/NONE |
| 52 | OPS_DELIVERY_TERMINAL | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 53 | OPS_MFST_LOC_TYPE | VARCHAR(1) | No | — | D_LOC_TYPE | NONE/NONE |
| 54 | OPS_MFST_LOCATION | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 55 | OPS_TTL_PIECES | NUMERIC(15,3) | No | — | D_PIECES | — |
| 56 | OPS_TTL_WEIGHT | NUMERIC(15,3) | No | — | D_WEIGHT | — |
| 57 | OPS_TTL_CUBE | NUMERIC(15,3) | No | — | RDB$700 | — |
| 58 | OPS_TTL_PALLETS | INTEGER | No | — | RDB$701 | — |
| 59 | OPS_TTL_PALLET_POSN | INTEGER | No | — | RDB$702 | — |
| 60 | OPS_OVER | NUMERIC(15,3) | No | — | D_PIECES | — |
| 61 | OPS_SHORT | NUMERIC(15,3) | No | — | D_PIECES | — |
| 62 | OPS_DAMAGED | NUMERIC(15,3) | No | — | D_PIECES | — |
| 63 | OPS_UPDATED_BY | VARCHAR(8) | No | — | D_INITIALS | NONE/NONE |
| 64 | OPS_STATUS | BIGINT | No | — | D_SYSID | — |
| 65 | OPS_STATUS_TIME | TIMESTAMP | No | — | RDB$715 | — |
| 66 | OPS_STATUS_REASON | VARCHAR(200) | No | — | OPH_REASON | NONE/NONE |
| 67 | OPS_LOC_TYPE | VARCHAR(1) | No | — | D_LOC_TYPE | NONE/NONE |
| 68 | OPS_LOCATION | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 69 | OPS_PICKED_UP | VARCHAR(1) | No | — | RDB$1961 | NONE/NONE |
| 70 | OPS_AGENT | VARCHAR(1) | No | — | RDB$2107 | NONE/NONE |
| 71 | OPS_RATE | CHAR(1) | No | — | RDB$2759 | NONE/NONE |
| 72 | OPS_APPT_HIGH | TIMESTAMP | No | — | RDB$2760 | — |
| 73 | OPS_PU_SIGNATURE | VARCHAR(42) | No | — | RDB$3232 | NONE/NONE |
| 74 | OPS_DLV_SIGNATURE | VARCHAR(42) | No | — | RDB$3233 | NONE/NONE |
| 75 | OPS_WRITE_HISTORY | VARCHAR(1) | No | — | RDB$3455 | NONE/NONE |
| 76 | OPS_CONTACT_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 77 | OPS_CONTACT_FAX | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 78 | OPS_EDI_REASON | VARCHAR(4) | No | — | RDB$3689 | NONE/NONE |
| 79 | OPS_STATUS_COMMENT | VARCHAR(250) | No | — | RDB$3690 | NONE/NONE |
| 80 | OPS_MANIFEST_NUMBER | INTEGER | No | — | RDB$4265 | — |
| 81 | OPS_MANIFESTED_TIME | TIMESTAMP | No | — | RDB$4266 | — |
| 82 | OPS_ARRIVE_REASON | VARCHAR(4) | No | — | RDB$5000 | NONE/NONE |
| 83 | OPS_BACKIN_REASON | VARCHAR(4) | No | — | RDB$5001 | NONE/NONE |
| 84 | OPS_RELEASE_REASON | VARCHAR(4) | No | — | RDB$5002 | NONE/NONE |
| 85 | OPS_DEPART_REASON | VARCHAR(4) | No | — | RDB$5003 | NONE/NONE |
| 86 | OPS_APPT_REASON | VARCHAR(4) | No | — | RDB$5004 | NONE/NONE |
| 87 | OPS_NOTIFICATION_PROCESSED | TIMESTAMP | No | — | RDB$5024 | — |
| 88 | OPS_REGION | VARCHAR(4) | No | — | RDB$5319 | NONE/NONE |
| 89 | OPS_FROM_AGENT_TIME | TIMESTAMP | No | — | RDB$5400 | — |
| 90 | OPS_TO_AGENT_TIME | TIMESTAMP | No | — | RDB$5401 | — |
| 91 | OPS_CHEETAH | CHAR(1) | No | — | RDB$5515 | NONE/NONE |
| 92 | OPS_AIRPORT | VARCHAR(12) | No | — | RDB$6060 | NONE/NONE |
| 93 | OPS_IM_WORKCODE | VARCHAR(2) | No | — | RDB$6446 | NONE/NONE |
| 94 | OPS_IM_EQUIP_TYPE | INTEGER | No | — | RDB$6447 | — |
| 95 | OPS_IM_EQUIP_ID | VARCHAR(14) | No | — | RDB$6448 | NONE/NONE |
| 96 | OPS_IM_EQUIP_DESC | INTEGER | No | — | RDB$6592 | — |
| 97 | OPS_TLR_UNIT_ID | VARCHAR(24) | No | — | RDB$6619 | NONE/NONE |
| 98 | OPS_DOCK_WORKER | VARCHAR(20) | No | — | RDB$6802 | NONE/NONE |
| 99 | OPS_LATITUDE | NUMERIC(10,6) | No | — | RDB$6839 | — |
| 100 | OPS_LONGITUDE | NUMERIC(10,6) | No | — | RDB$6840 | — |
| 101 | OPS_GEOCODE_ERROR_IND | VARCHAR(1) | No | — | RDB$6842 | NONE/NONE |
| 102 | OPS_GEO_ARI_TIME | TIMESTAMP | No | — | RDB$6847 | — |
| 103 | OPS_GEO_DEP_TIME | TIMESTAMP | No | — | RDB$6848 | — |
| 104 | OPS_STOP_IDENTIFIER | VARCHAR(36) | No | — | RDB$6970 | NONE/NONE |
| 105 | OPS_BIN_ID | VARCHAR(36) | No | — | RDB$7160 | NONE/NONE |
| 106 | OPS_BIN_QUANTITY | VARCHAR(36) | No | — | RDB$7195 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| OPS_STOP_APPT_KEY | OPS_APPT_LOW, OPS_STOP_ID | No | Yes | ASC | — |
| OPS_STOP_NUMBER_KEY | OPS_ORDER_ID, OPS_STOP_NUMBER | Yes | Yes | ASC | — |
| PK_OPS_STOP_REC | OPS_STOP_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R072 | DRIVER_PKUP_DELV.(DPD_STOP_ID) | OPS_STOP_REC.(OPS_STOP_ID) | High | Trigger OPS_STOP_REC_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R117 | EQUIP_HISTORY.(EH_STOP_ID) | OPS_STOP_REC.(OPS_STOP_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R128 | EQUIP_STATUS.(ES_STOP_ID) | OPS_STOP_REC.(OPS_STOP_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R199 | OPS_HISTORY.(OPH_STOP_ID) | OPS_STOP_REC.(OPS_STOP_ID) | High | OPS_STOP_REC_BEFORE_UPDATE writes NEW.OPS_STOP_ID into OPH_STOP_ID Header-level history uses OPH_STOP_ID=0; do not classify zero as an orphan. |
| R205 | OPS_LINE_ITEMS.(OPL_STOP_ID) | OPS_STOP_REC.(OPS_STOP_ID) | High | Trigger OPS_STOP_REC_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R206 | OPS_MANIFEST_STOPS.(OSM_PKUP_DELV_ID) | OPS_STOP_REC.(OPS_STOP_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping  |
| R207 | OPS_MANIFEST_STOPS.(OSM_STOP_ID) | OPS_STOP_REC.(OPS_STOP_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping; Trigger OPS_STOP_REC_AFTER_DELETE explicitly deletes matching child rows  |
| R211 | OPS_NUMBERS.(OPN_STOP_ID) | OPS_STOP_REC.(OPS_STOP_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R217 | OPS_STOP_REC.(OPS_ACCTID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R218 | OPS_STOP_REC.(OPS_AGENT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R219 | OPS_STOP_REC.(OPS_DELIVERY_TERMINAL) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R220 | OPS_STOP_REC.(OPS_DRIVER1) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R221 | OPS_STOP_REC.(OPS_DRIVER2) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R222 | OPS_STOP_REC.(OPS_EDI_REASON) | CT_214_REASONS.(CT2R_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R223 | OPS_STOP_REC.(OPS_MANIFEST_NUMBER) | OPS_HEADER.(PB_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping  |
| R224 | OPS_STOP_REC.(OPS_ORDER_ID) | OPS_HEADER.(PB_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping; Trigger OPS_HEADER_AFTER_DELETE explicitly deletes matching child rows  |
| R225 | OPS_STOP_REC.(OPS_QUAL) | QUALN1_98.(Q_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R226 | OPS_STOP_REC.(OPS_REGION) | CT_REGION_IDS.(CTR_REGION_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R227 | OPS_STOP_REC.(OPS_ROUTE) | CT_ROUTES.(CTR_ROUTE_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R228 | OPS_STOP_REC.(OPS_STATUS) | CT_STAT.(CTS_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R229 | OPS_STOP_REC.(OPS_TRACTOR) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R230 | OPS_STOP_REC.(OPS_TRAILER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R234 | OPS_STOP_TLR_SPACES.(OSB_STOP_ID) | OPS_STOP_REC.(OPS_STOP_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`OPS_STOP_REC_AFTER_DELETE`, `OPS_STOP_REC_AFTER_MOD`, `OPS_STOP_REC_AFTER_UPDATE`, `OPS_STOP_REC_BEFORE_UPDATE`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
