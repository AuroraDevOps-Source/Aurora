# ACCOUNT

Accounts and carriers. Party master covering customer, bill-to, agent, broker and other roles; role flags are present.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (AC_CUST). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | AC_CUST | VARCHAR(12) | Yes | — | D_ACCT_ID | NONE/NONE |
| 2 | AC_NAMEKEY | VARCHAR(32) | No | — | RDB$187 | NONE/NONE |
| 3 | AC_PHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 4 | AC_CC | VARCHAR(2) | No | — | RDB$189 | NONE/NONE |
| 5 | AC_NAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 6 | AC_ADDR1 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 7 | AC_ADDR2 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 8 | AC_ADDR3 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 9 | AC_CITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 10 | AC_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 11 | AC_ZIP | VARCHAR(9) | No | — | D_ZIP | NONE/NONE |
| 12 | AC_COUNTRY_CD | VARCHAR(4) | No | — | D_COUNTRY_CODE | NONE/NONE |
| 13 | AC_COUNTRY_NM | VARCHAR(30) | No | — | RDB$232 | NONE/NONE |
| 14 | AC_TYPE | VARCHAR(2) | No | — | RDB$196 | NONE/NONE |
| 15 | AC_CLASS | VARCHAR(2) | No | — | RDB$197 | NONE/NONE |
| 16 | AC_STATUS | VARCHAR(2) | No | — | RDB$198 | NONE/NONE |
| 17 | AC_SALESMAN_ID | VARCHAR(12) | No | — | RDB$199 | NONE/NONE |
| 18 | AC_HDQ | VARCHAR(12) | No | — | D_ACCT_ID | NONE/NONE |
| 19 | AC_PAY | VARCHAR(12) | No | — | D_ACCT_ID | NONE/NONE |
| 20 | AC_RECEIV | VARCHAR(12) | No | — | D_ACCT_ID | NONE/NONE |
| 21 | AC_CONTRACT | VARCHAR(24) | No | — | RDB$203 | NONE/NONE |
| 22 | AC_CONTRACT_DT | DATE | No | — | RDB$204 | — |
| 23 | AC_REMIT_TO | VARCHAR(12) | No | — | D_ACCT_ID | NONE/NONE |
| 24 | AC_TERM_LOC | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 25 | AC_RATE_QUE | VARCHAR(12) | No | — | RDB$207 | NONE/NONE |
| 26 | AC_ASSIGN_TO | VARCHAR(12) | No | — | D_ACCT_ID | NONE/NONE |
| 27 | AC_WARNING | VARCHAR(1) | No | — | RDB$209 | NONE/NONE |
| 28 | AC_BEGIN_DATE | DATE | No | — | RDB$210 | — |
| 29 | AC_LAST_BUS_DATE | DATE | No | — | RDB$211 | — |
| 30 | AC_LAST_PAID | DATE | No | — | RDB$212 | — |
| 31 | AC_L1 | VARCHAR(1) | No | — | RDB$213 | NONE/NONE |
| 32 | AC_L2 | VARCHAR(1) | No | — | RDB$214 | NONE/NONE |
| 33 | AC_L3 | VARCHAR(1) | No | — | RDB$215 | NONE/NONE |
| 34 | AC_L4 | VARCHAR(1) | No | — | RDB$216 | NONE/NONE |
| 35 | AC_L5 | VARCHAR(1) | No | — | RDB$217 | NONE/NONE |
| 36 | AC_L6 | VARCHAR(1) | No | — | RDB$218 | NONE/NONE |
| 37 | AC_ROUTE_ID | VARCHAR(12) | No | — | D_ROUTE_ID | NONE/NONE |
| 38 | AC_AGE_DATE | DATE | No | — | RDB$220 | — |
| 39 | AC_CREDIT_LEVEL | NUMERIC(1,0) | No | — | RDB$221 | — |
| 40 | AC_CREDIT_STATUS | VARCHAR(1) | No | — | RDB$222 | NONE/NONE |
| 41 | AC_CREDIT_APPROVAL | VARCHAR(6) | No | — | RDB$223 | NONE/NONE |
| 42 | AC_CR_APPROV_DATE | DATE | No | — | RDB$224 | — |
| 43 | AC_CR_LIMIT | NUMERIC(11,2) | No | — | RDB$225 | — |
| 44 | AC_DUN | VARCHAR(12) | No | — | RDB$226 | NONE/NONE |
| 45 | AC_PALLETS | NUMERIC(7,0) | No | — | RDB$227 | — |
| 46 | AC_PALL_FREE_HRS | NUMERIC(7,2) | No | — | RDB$228 | — |
| 47 | AC_FLOR_FREE_HRS | NUMERIC(7,2) | No | — | RDB$229 | — |
| 48 | AC_COLLECTOR_ID | VARCHAR(6) | No | — | RDB$230 | NONE/NONE |
| 49 | AC_R_MILE | VARCHAR(1) | No | — | RDB$233 | NONE/NONE |
| 50 | AC_PALLET_IND | VARCHAR(1) | No | — | RDB$234 | NONE/NONE |
| 51 | AC_WAREHOUSE_CUST | VARCHAR(2) | No | — | RDB$235 | NONE/NONE |
| 52 | AC_WH_PLT_IN | NUMERIC(6,0) | No | — | RDB$236 | — |
| 53 | AC_WH_PLT_OUT | NUMERIC(6,0) | No | — | RDB$237 | — |
| 54 | AC_EXT_SYS | VARCHAR(1) | No | — | RDB$238 | NONE/NONE |
| 55 | AC_LATITUDE | NUMERIC(10,6) | No | — | RDB$239 | — |
| 56 | AC_LONGITUDE | NUMERIC(10,6) | No | — | RDB$240 | — |
| 57 | AC_SOURCE_INIT | VARCHAR(9) | No | — | RDB$241 | NONE/NONE |
| 58 | AC2_FAX | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 59 | AC2_SRV_PROMISE | NUMERIC(3,0) | No | — | RDB$243 | — |
| 60 | AC2_SRV_MATRIX | VARCHAR(8) | No | — | RDB$244 | NONE/NONE |
| 61 | AC2_BAY | VARCHAR(7) | No | — | RDB$245 | NONE/NONE |
| 62 | AC2_HAZ_TELE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 63 | AC2_HAZ_CO | VARCHAR(24) | No | — | RDB$247 | NONE/NONE |
| 64 | AC2_SOB_SALE | VARCHAR(12) | No | — | RDB$248 | NONE/NONE |
| 65 | AC2_CIB_SALE | VARCHAR(12) | No | — | RDB$249 | NONE/NONE |
| 66 | AC2_L_TRANS_DT | DATE | No | — | RDB$250 | — |
| 67 | AC2_AGREEMENT_ID | VARCHAR(16) | No | — | RDB$251 | NONE/NONE |
| 68 | AC2_VMRS_INH_MARKUP | NUMERIC(10,4) | No | — | RDB$252 | — |
| 69 | AC2_VMRS_OUTS_MARKUP | NUMERIC(10,4) | No | — | RDB$253 | — |
| 70 | AC2_AUDIT_QUE | VARCHAR(12) | No | — | RDB$254 | NONE/NONE |
| 71 | AC2_AUDIT_RULE | VARCHAR(1) | No | — | RDB$255 | NONE/NONE |
| 72 | AC2_CONSOLIDATER | VARCHAR(1) | No | — | RDB$256 | NONE/NONE |
| 73 | AC2_CONS_PARENT | VARCHAR(12) | No | — | RDB$257 | NONE/NONE |
| 74 | AC2_IM_BILL_METH | VARCHAR(1) | No | — | RDB$258 | NONE/NONE |
| 75 | AC2_IM_1_OR_4 | NUMERIC(1,0) | No | — | RDB$259 | — |
| 76 | AC2_IM_PAPER_REQ | VARCHAR(1) | No | — | RDB$260 | NONE/NONE |
| 77 | AC2_IM_SEND | VARCHAR(24) | No | — | RDB$261 | NONE/NONE |
| 78 | AC2_IM_REQ_TYPE | VARCHAR(24) | No | — | RDB$262 | NONE/NONE |
| 79 | AC2_IM_CYCLE | VARCHAR(1) | No | — | RDB$263 | NONE/NONE |
| 80 | AC2_TIME_ZONE | VARCHAR(4) | No | — | RDB$264 | NONE/NONE |
| 81 | AC2_WWW_PASS | VARCHAR(10) | No | — | RDB$265 | NONE/NONE |
| 82 | AC2_IM_AP_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 83 | AC2_IM_EX_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 84 | AC2_IM_PRT_EMAIL | VARCHAR(1) | No | — | RDB$268 | NONE/NONE |
| 85 | AC2_IM_PDUE_PRT_EMAIL | VARCHAR(1) | No | — | RDB$269 | NONE/NONE |
| 86 | AC2_IM_ATTACH_1_ALL | VARCHAR(1) | No | — | RDB$270 | NONE/NONE |
| 87 | AC2_IM_PD_METH | VARCHAR(1) | No | — | RDB$271 | NONE/NONE |
| 88 | AC2_IM_PD_1_OR_4 | NUMERIC(1,0) | No | — | RDB$272 | — |
| 89 | AC2_IM_PD_CYCLE | VARCHAR(1) | No | — | RDB$273 | NONE/NONE |
| 90 | AC2_IM_PD_SEND | VARCHAR(24) | No | — | RDB$274 | NONE/NONE |
| 91 | AC2_IM_PD_REQ | VARCHAR(24) | No | — | RDB$275 | NONE/NONE |
| 92 | AC2_WEBSITE | VARCHAR(150) | No | — | D_URL | NONE/NONE |
| 93 | AC2_IM_TIF_PDF | VARCHAR(1) | No | — | RDB$277 | NONE/NONE |
| 94 | AC2_FTP_PATH | VARCHAR(120) | No | — | RDB$278 | NONE/NONE |
| 95 | AC2_FTP_FORMAT | VARCHAR(2) | No | — | RDB$279 | NONE/NONE |
| 96 | AC2_TRACE_FORMAT | VARCHAR(1) | No | — | RDB$280 | NONE/NONE |
| 97 | AC2_WWW_PASS_ADMIN | VARCHAR(10) | No | — | RDB$281 | NONE/NONE |
| 98 | AC2_IM_SUBJ_TYPE | VARCHAR(1) | No | — | RDB$282 | NONE/NONE |
| 99 | AC2_FTP_MASK | VARCHAR(120) | No | — | RDB$283 | NONE/NONE |
| 100 | AC2_EMAIL_SUB_MASK | VARCHAR(120) | No | — | RDB$284 | NONE/NONE |
| 101 | AC_MON_OPEN_DELIV | TIME | No | — | RDB$286 | — |
| 102 | AC_MON_CLOSE_DELIV | TIME | No | — | RDB$287 | — |
| 103 | AC_TUE_OPEN_DELIV | TIME | No | — | RDB$288 | — |
| 104 | AC_TUE_CLOSE_DELIV | TIME | No | — | RDB$289 | — |
| 105 | AC_WED_OPEN_DELIV | TIME | No | — | RDB$290 | — |
| 106 | AC_WED_CLOSE_DELIV | TIME | No | — | RDB$291 | — |
| 107 | AC_THU_OPEN_DELIV | TIME | No | — | RDB$292 | — |
| 108 | AC_THU_CLOSE_DELIV | TIME | No | — | RDB$293 | — |
| 109 | AC_FRI_OPEN_DELIV | TIME | No | — | RDB$294 | — |
| 110 | AC_FRI_CLOSE_DELIV | TIME | No | — | RDB$295 | — |
| 111 | AC_SAT_OPEN_DELIV | TIME | No | — | RDB$296 | — |
| 112 | AC_SAT_CLOSE_DELIV | TIME | No | — | RDB$297 | — |
| 113 | AC_SUN_OPEN_DELIV | TIME | No | — | RDB$298 | — |
| 114 | AC_SUN_CLOSE_DELIV | TIME | No | — | RDB$299 | — |
| 115 | AC_SAT_DLIV | CHAR(1) | No | — | RDB$300 | NONE/NONE |
| 116 | AC_SUN_DLIV | CHAR(1) | No | — | RDB$301 | NONE/NONE |
| 117 | AC_DRV_DIR | BLOB SUB_TYPE 1 | No | — | RDB$302 | — |
| 118 | AC_CRED_COL | BLOB SUB_TYPE 1 | No | — | RDB$303 | — |
| 119 | AC_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 120 | AC_WEB_SITE | VARCHAR(150) | No | — | D_URL | NONE/NONE |
| 121 | AC_CB_BILLTO | CHAR(1) | No | — | RDB$306 | NONE/NONE |
| 122 | AC_CB_HDQ | CHAR(1) | No | — | RDB$307 | NONE/NONE |
| 123 | AC_CB_INTERLINE | CHAR(1) | No | — | RDB$308 | NONE/NONE |
| 124 | AC_CB_CARTAGE | CHAR(1) | No | — | RDB$309 | NONE/NONE |
| 125 | AC_CB_YARD | CHAR(1) | No | — | RDB$310 | NONE/NONE |
| 126 | AC_CB_BROKER | CHAR(1) | No | — | RDB$311 | NONE/NONE |
| 127 | AC_CB_POOL | CHAR(1) | No | — | RDB$312 | NONE/NONE |
| 128 | AC_AR_DUE | NUMERIC(15,2) | No | — | RDB$313 | — |
| 129 | AC_ROUTE_STOP | INTEGER | No | — | RDB$451 | — |
| 130 | ISDELETED | CHAR(1) | No | — | RDB$2124 | NONE/NONE |
| 131 | AC_CB_AGENT | CHAR(1) | No | — | RDB$2548 | NONE/NONE |
| 132 | AC_PRINT_QUE | VARCHAR(12) | No | — | RDB$3608 | NONE/NONE |
| 133 | AC_STORE_PLANT | VARCHAR(30) | No | — | RDB$3722 | NONE/NONE |
| 134 | AC_DR_PRT_AMT_OPT | VARCHAR(1) | No | — | RDB$5075 | NONE/NONE |
| 135 | AC_PU_APPT_REQ | VARCHAR(1) | No | — | RDB$5125 | NONE/NONE |
| 136 | AC_DLV_APPT_REQ | VARCHAR(1) | No | — | RDB$5126 | NONE/NONE |
| 137 | AC_ICC_MC_NUMBER | VARCHAR(12) | No | — | RDB$5540 | NONE/NONE |
| 138 | AC_PUC1 | VARCHAR(14) | No | — | RDB$5541 | NONE/NONE |
| 139 | AC_PUC2 | VARCHAR(14) | No | — | RDB$5542 | NONE/NONE |
| 140 | AC_FED_TAX_ID | VARCHAR(12) | No | — | RDB$5543 | NONE/NONE |
| 141 | AC_CONTRACT_AUTH | VARCHAR(1) | No | — | RDB$5544 | NONE/NONE |
| 142 | AC_SURETY_BOND | VARCHAR(1) | No | — | RDB$5545 | NONE/NONE |
| 143 | AC_BROKER_LICENSE | VARCHAR(1) | No | — | RDB$5546 | NONE/NONE |
| 144 | AC_AGREE_ON_FILE | VARCHAR(1) | No | — | RDB$5547 | NONE/NONE |
| 145 | AC_W9_REQUIRED | VARCHAR(1) | No | — | RDB$5548 | NONE/NONE |
| 146 | AC_BROKERAGE_COMMENTS | BLOB SUB_TYPE 0 | No | — | RDB$5549 | — |
| 147 | AC_IS_PROSPECT | CHAR(1) | No | — | RDB$5840 | NONE/NONE |
| 148 | AC_IS_TEMP | CHAR(1) | No | — | RDB$6115 | NONE/NONE |
| 149 | AC_CREATE_INIT | CHAR(8) | No | — | RDB$6116 | NONE/NONE |
| 150 | AC_CREATED | TIMESTAMP | No | — | RDB$6117 | — |
| 151 | AC_CHANGED_INIT | CHAR(8) | No | — | RDB$6118 | NONE/NONE |
| 152 | AC_CHANGED | TIMESTAMP | No | — | RDB$6119 | — |
| 153 | AC_DR_BLINDSHIP | VARCHAR(1) | No | — | RDB$6125 | NONE/NONE |
| 154 | AC_CB_WHSE | VARCHAR(1) | No | — | RDB$6163 | NONE/NONE |
| 155 | AC_AF_AGENT | VARCHAR(1) | No | — | RDB$6359 | NONE/NONE |
| 156 | AC_PAYMENT_TERMS | VARCHAR(128) | No | — | RDB$6381 | NONE/NONE |
| 157 | AC_FB_STMT_OPT | VARCHAR(1) | No | — | RDB$6433 | NONE/NONE |
| 158 | AC2_EMAIL_FILE_MASK | VARCHAR(120) | No | — | RDB$6451 | NONE/NONE |
| 159 | AC_STMT_FB_MAX_CNT | INTEGER | No | — | RDB$6460 | — |
| 160 | AC_MON_OPEN_PICKUP | TIME | No | — | RDB$6498 | — |
| 161 | AC_MON_CLOSE_PICKUP | TIME | No | — | RDB$6499 | — |
| 162 | AC_TUE_OPEN_PICKUP | TIME | No | — | RDB$6500 | — |
| 163 | AC_TUE_CLOSE_PICKUP | TIME | No | — | RDB$6501 | — |
| 164 | AC_WED_OPEN_PICKUP | TIME | No | — | RDB$6502 | — |
| 165 | AC_WED_CLOSE_PICKUP | TIME | No | — | RDB$6503 | — |
| 166 | AC_THU_OPEN_PICKUP | TIME | No | — | RDB$6504 | — |
| 167 | AC_THU_CLOSE_PICKUP | TIME | No | — | RDB$6505 | — |
| 168 | AC_FRI_OPEN_PICKUP | TIME | No | — | RDB$6506 | — |
| 169 | AC_FRI_CLOSE_PICKUP | TIME | No | — | RDB$6507 | — |
| 170 | AC_SAT_OPEN_PICKUP | TIME | No | — | RDB$6508 | — |
| 171 | AC_SAT_CLOSE_PICKUP | TIME | No | — | RDB$6509 | — |
| 172 | AC_SUN_OPEN_PICKUP | TIME | No | — | RDB$6510 | — |
| 173 | AC_SUN_CLOSE_PICKUP | TIME | No | — | RDB$6511 | — |
| 174 | AC_DOT_NUMBER | VARCHAR(36) | No | — | RDB$6814 | NONE/NONE |
| 175 | AC_REGION | VARCHAR(4) | No | — | RDB$6837 | NONE/NONE |
| 176 | AC_MOBILE_ID | VARCHAR(50) | No | — | RDB$6861 | NONE/NONE |
| 177 | AC_OUTSIDE_CUST_ID | VARCHAR(32) | No | — | RDB$6905 | NONE/NONE |
| 178 | AC_CB_STEAMSHIP_LINE | CHAR(1) | No | — | RDB$7069 | NONE/NONE |
| 179 | AC_DUE_DAYS | INTEGER | No | — | RDB$7122 | — |
| 180 | AC_BIN_ID | VARCHAR(36) | No | — | RDB$7153 | NONE/NONE |
| 181 | AC_DR_OVERLAY | VARCHAR(50) | No | — | RDB$7159 | NONE/NONE |
| 182 | AC_DR_CREATE_IMAGE | VARCHAR(1) | No | — | RDB$7209 | NONE/NONE |
| 183 | AC_INV_SPREADSHEET_FMT | VARCHAR(36) | No | — | RDB$7210 | NONE/NONE |
| 184 | AC_SKU_ACCUMULATE | VARCHAR(1) | No | — | RDB$7256 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| ACCOUNT_PRIMARY | AC_CUST | Yes | Yes | ASC | — |
| AC_ALTKEY | AC_NAMEKEY | No | Yes | ASC | — |
| AC_PHONEKEY | AC_PHONE | No | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R001 | ACCOUNT.(AC2_CONS_PARENT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R002 | ACCOUNT.(AC_COUNTRY_CD) | CT_COUNTRIES.(CTC_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R003 | ACCOUNT.(AC_HDQ) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R004 | ACCOUNT.(AC_REGION) | CT_REGION_IDS.(CTR_REGION_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R005 | ACCOUNT.(AC_REMIT_TO) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R006 | ACCOUNT.(AC_ROUTE_ID) | CT_ROUTES.(CTR_ROUTE_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R007 | ACCOUNT.(AC_SALESMAN_ID) | CT_SALESMAN.(CTSM_SALESMAN_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R008 | ACCOUNT.(AC_STATE) | CT_STATE.(CTST_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R009 | ACCOUNT.(AC_TERM_LOC) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R010 | ACIMAGE.(ACIMG_CUST) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R011 | ACMESSFIX.(AC_C_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R012 | AC_AGENT.(ICA_AGENT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R015 | AC_AGENT_SRV_POINTS.(ICAS_AGENT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R016 | AC_COLLECTION_NOTES.(ACN_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R017 | AC_CONTACT.(ACC_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R019 | AC_NOTES.(ACNO_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R020 | AC_RPT_SHIPMENTS.(ACRS_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R022 | AC_SKU.(AKU_ACCT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R023 | AC_XREF.(ACX_OUR_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R024 | AC_YARD.(ACY_YARD_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R025 | AGENTROUTING.(AGR_AGENT1_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R026 | CASHRECPTBDY.(CP_CUST_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R028 | CA_AVAILABLE_EQUIP.(CAAE_ACCT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R030 | CA_EQUIP_INVENTORY.(CAEI_ACCT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R031 | CA_HISTORY.(CAH_ACCT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R035 | CA_OPER_STATES.(CAOS_ACCT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R037 | CLAIM_HEADER.(CLH_CLAIMANT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R048 | CT_API_CONNECT_INFO.(CTAI_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R094 | DS_POINT_PAY.(DSPP_ACCT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R096 | EDI_PIF.(PIF_ACCT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R136 | FUEL_SURCHARGE.(FSC_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R141 | ILOG_REPEAT_SHIP.(IRS_CONS_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R143 | IMSEND.(IS_CUST) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R145 | IM_CONT_CHRG_MX.(IMCX_BILLTO_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R146 | IM_CONT_CHRG_MX.(IMCX_CONS_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R147 | IM_CONT_CHRG_MX.(IMCX_SHIP_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R161 | OPS_AGENTS.(OPA_AGENT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R176 | OPS_HEADER.(PB_BILL_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R191 | OPS_HEADER.(PB_REMIT_TO) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R214 | OPS_OTHER_PARTIES.(OOP_ACCOUNT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R217 | OPS_STOP_REC.(OPS_ACCTID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R218 | OPS_STOP_REC.(OPS_AGENT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R238 | PALLET_TARIFF_KEYS.(PAL_CUSTOMER_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R240 | PRICE_ASC.(PASC_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R243 | PRICE_FAK.(PFAK_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R245 | PRICE_FROM_TO.(PFT_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R247 | PRICE_IL.(PIL_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R250 | PRICE_SPECIAL.(PS_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R253 | PRICE_TB.(PTB_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R255 | PRICING.(PRMA_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R293 | WEB_ALLOWED_ACCTS.(WUAA_ACCT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R296 | WEB_USERS.(WU_ACCT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
