# LOCMAST

Locations and routing. Terminal/location master.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (LM_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | LM_ID | VARCHAR(12) | Yes | — | D_LOCATION | NONE/NONE |
| 2 | LM_CC | VARCHAR(2) | No | — | RDB$136 | NONE/NONE |
| 3 | LM_DIV | VARCHAR(4) | No | — | RDB$137 | NONE/NONE |
| 4 | LM_DEPT | VARCHAR(6) | No | — | RDB$138 | NONE/NONE |
| 5 | LM_REGION | VARCHAR(4) | No | — | D_REGION_ID | NONE/NONE |
| 6 | LM_NAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 7 | LM_NAME2 | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 8 | LM_ADDR1 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 9 | LM_ADDR2 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 10 | LM_CITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 11 | LM_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 12 | LM_ZIP | VARCHAR(9) | No | — | D_ZIP | NONE/NONE |
| 13 | LM_COUNTRY | VARCHAR(4) | No | — | D_COUNTRY_CODE | NONE/NONE |
| 14 | LM_MANAGER | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 15 | LM_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 16 | LM_FAX | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 17 | LM_SCAC | VARCHAR(4) | No | — | D_SCAC | NONE/NONE |
| 18 | LM_GBL | CHAR(1) | No | — | RDB$141 | NONE/NONE |
| 19 | LM_BILL | CHAR(1) | No | — | RDB$142 | NONE/NONE |
| 20 | LM_RATE | CHAR(1) | No | — | RDB$143 | NONE/NONE |
| 21 | LM_MAN_ASSGN | CHAR(1) | No | — | RDB$144 | NONE/NONE |
| 22 | LM_204 | CHAR(1) | No | — | RDB$145 | NONE/NONE |
| 23 | LM_214 | CHAR(1) | No | — | RDB$146 | NONE/NONE |
| 24 | LM_990 | CHAR(1) | No | — | RDB$147 | NONE/NONE |
| 25 | LM_CLASS | VARCHAR(2) | No | — | RDB$148 | NONE/NONE |
| 26 | LM_TYPE | VARCHAR(2) | No | — | RDB$149 | NONE/NONE |
| 27 | LM_RATEQ | VARCHAR(4) | No | — | RDB$150 | NONE/NONE |
| 28 | LM_PRINTQ | VARCHAR(20) | No | — | RDB$151 | NONE/NONE |
| 29 | LM_INC_BY | SMALLINT | No | — | RDB$152 | — |
| 30 | LM_NEXT_NUM | INTEGER | No | — | RDB$153 | — |
| 31 | LM_MAN_LOW | INTEGER | No | — | RDB$154 | — |
| 32 | LM_MAN_HIGH | INTEGER | No | — | RDB$155 | — |
| 33 | LM_NEXT_PRO | INTEGER | No | — | RDB$156 | — |
| 34 | LM_NEXT_204 | INTEGER | No | — | RDB$157 | — |
| 35 | LM_DEFAULT_PRT | VARCHAR(60) | No | — | RDB$158 | NONE/NONE |
| 36 | LM_PRT_DS_REV | CHAR(1) | No | — | RDB$159 | NONE/NONE |
| 37 | LM_DS_RPT | VARCHAR(24) | No | — | RDB$160 | NONE/NONE |
| 38 | LM_SYSCON | VARCHAR(12) | No | — | RDB$161 | NONE/NONE |
| 39 | LM_DAT_ACCT | VARCHAR(12) | No | — | RDB$162 | NONE/NONE |
| 40 | LM_DAT_SUB_ACCT | VARCHAR(12) | No | — | RDB$163 | NONE/NONE |
| 41 | LM_AGNT_SUB1 | VARCHAR(6) | No | — | RDB$164 | NONE/NONE |
| 42 | LM_AGNT_SUB2 | VARCHAR(6) | No | — | RDB$165 | NONE/NONE |
| 43 | LM_AGNT_SUB3 | VARCHAR(6) | No | — | RDB$166 | NONE/NONE |
| 44 | LM_AGNT_SUB4 | VARCHAR(6) | No | — | RDB$167 | NONE/NONE |
| 45 | LM_LATITUDE | DOUBLE PRECISION | No | — | D_LAT_LONG | — |
| 46 | LM_LONGITUDE | DOUBLE PRECISION | No | — | D_LAT_LONG | — |
| 47 | LM_DR_OVERLAY | VARCHAR(50) | No | — | RDB$3023 | NONE/NONE |
| 48 | LM_FB_OVERLAY | VARCHAR(50) | No | — | RDB$5364 | NONE/NONE |
| 49 | LM_IS_AIRPORT | VARCHAR(1) | No | — | RDB$6059 | NONE/NONE |
| 50 | LM_IS_WAREHOUSE | VARCHAR(1) | No | — | RDB$6171 | NONE/NONE |
| 51 | LM_BOL_OVERLAY | VARCHAR(50) | No | — | RDB$6439 | NONE/NONE |
| 52 | LM_PAST_DUE_FB_OVERLAY | VARCHAR(50) | No | — | RDB$6440 | NONE/NONE |
| 53 | LM_STMT_OVERLAY | VARCHAR(50) | No | — | RDB$6441 | NONE/NONE |
| 54 | LM_PAST_DUE_STMT_OVERLAY | VARCHAR(50) | No | — | RDB$6442 | NONE/NONE |
| 55 | LM_OUTSIDE_OE_EMAIL_NOTIFY | VARCHAR(255) | No | — | RDB$7048 | NONE/NONE |
| 56 | LM_AUTO_ASSIGN_RATEQ | CHAR(1) | No | — | RDB$7161 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| LOCMAST_PRIMARY | LM_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R009 | ACCOUNT.(AC_TERM_LOC) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R053 | CT_ROUTES.(CTR_LOCATION) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R054 | CT_SERV_AREAS.(CTSA_LOCATION) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R103 | EQUIP_HEADER.(EH_DOMICILE) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R107 | EQUIP_HEADER.(EH_SHOP) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R142 | ILOG_REPEAT_SHIP.(IRS_TERM) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R148 | LOCMAST.(LM_REGION) | CT_REGION_IDS.(CTR_REGION_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R149 | LOCMAST.(LM_STATE) | CT_STATE.(CTST_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R162 | OPS_AGENTS.(OPA_DEST_TERM) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R165 | OPS_AGENTS.(OPA_ORIG_TERM) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R173 | OPS_HEADER.(PBDEST) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R174 | OPS_HEADER.(PBORIG) | LOCMAST.(LM_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping  |
| R183 | OPS_HEADER.(PB_LOCATION) | LOCMAST.(LM_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping  |
| R212 | OPS_NUMBERS.(OPN_TERMINAL) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R219 | OPS_STOP_REC.(OPS_DELIVERY_TERMINAL) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R266 | TERMINALSERVICE.(TS_DESTLOC) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R267 | TERMINALSERVICE.(TS_ORIGLOC) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R268 | TERMINALSERVICE.(TS_VIA1) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R269 | TERMINALSERVICE.(TS_VIA2) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R274 | USERS.(U_LOCATION) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R298 | WEB_USERS.(WU_TERMINAL) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
