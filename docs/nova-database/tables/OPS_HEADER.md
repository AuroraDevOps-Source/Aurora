# OPS_HEADER

Orders and manifests. Shared header for orders, manifests, templates and rate quotes (PB_TYPE O/M/T/Q, established by trigger source).

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PB_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PB_ID | NUMERIC(9,0) | Yes | — | RDB$495 | — |
| 2 | PB_TYPE | VARCHAR(1) | Yes | — | RDB$496 | NONE/NONE |
| 3 | PB_LOCATION | VARCHAR(12) | Yes | — | D_LOCATION | NONE/NONE |
| 4 | PB_LOAD_TYPE | VARCHAR(1) | Yes | — | RDB$497 | NONE/NONE |
| 5 | PBLOCAT | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 6 | PBNUM | NUMERIC(9,0) | No | — | RDB$498 | — |
| 7 | PBSUFFIX | VARCHAR(2) | No | — | D_SUFFIX | NONE/NONE |
| 8 | PBDEST | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 9 | PB_PU_ROUTE | VARCHAR(12) | No | — | RDB$499 | NONE/NONE |
| 10 | PB_DEL_ROUTE | VARCHAR(12) | No | — | RDB$500 | NONE/NONE |
| 11 | PBCSTAT | VARCHAR(1) | No | — | RDB$501 | NONE/NONE |
| 12 | PBRSTAT | VARCHAR(1) | No | — | RDB$502 | NONE/NONE |
| 13 | PBSEAL | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 14 | PBPURORD | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 15 | PBBOL | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 16 | PB_BOL_DT | DATE | No | — | RDB$503 | — |
| 17 | PB_DT_ENT | TIMESTAMP | No | — | RDB$504 | — |
| 18 | PB_DT_RATE | TIMESTAMP | No | — | RDB$505 | — |
| 19 | PB_DT_AUD | TIMESTAMP | No | — | RDB$506 | — |
| 20 | PBINIT | VARCHAR(8) | No | — | D_INITIALS | NONE/NONE |
| 21 | PBRINIT | VARCHAR(8) | No | — | D_INITIALS | NONE/NONE |
| 22 | PBAINIT | VARCHAR(8) | No | — | D_INITIALS | NONE/NONE |
| 23 | PB_DT_CANC | TIMESTAMP | No | — | RDB$507 | — |
| 24 | PBCINIT | VARCHAR(8) | No | — | D_INITIALS | NONE/NONE |
| 25 | PB_DLIV_AGENT | VARCHAR(12) | No | — | D_ACCT_ID | NONE/NONE |
| 26 | PB_IL_TRANS_INIT | VARCHAR(8) | No | — | D_INITIALS | NONE/NONE |
| 27 | PB_CART_DATE | DATE | No | — | RDB$508 | — |
| 28 | PB_INV_DATE | DATE | No | — | RDB$509 | — |
| 29 | PB_DLIV_DRIV | VARCHAR(14) | No | — | D_UNIT_ID | NONE/NONE |
| 30 | PB_DLIV_DT | DATE | No | — | RDB$510 | — |
| 31 | PBOVER | NUMERIC(9,0) | No | — | RDB$511 | — |
| 32 | PBSHORT | NUMERIC(9,0) | No | — | RDB$512 | — |
| 33 | PBDAM | NUMERIC(9,0) | No | — | RDB$513 | — |
| 34 | PB_OSD_STAT | VARCHAR(36) | No | — | RDB$514 | NONE/NONE |
| 35 | PB_NEW_ORIG | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 36 | PB_NEW_NUM | NUMERIC(9,0) | No | — | RDB$515 | — |
| 37 | PBNSUFF | VARCHAR(2) | No | — | RDB$516 | NONE/NONE |
| 38 | PBDLIVST | VARCHAR(2) | No | — | RDB$517 | NONE/NONE |
| 39 | PB_BILL_ID | VARCHAR(12) | No | — | D_ACCT_ID | NONE/NONE |
| 40 | PBBNAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 41 | PBBADDR1 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 42 | PBBADDR2 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 43 | PBBADDR3 | VARCHAR(50) | No | — | D_ADDRESS | NONE/NONE |
| 44 | PBBCITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 45 | PBBSTATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 46 | PBBZIP | VARCHAR(9) | No | — | D_ZIP | NONE/NONE |
| 47 | PBBCOUNTRY | VARCHAR(4) | No | — | D_COUNTRY_CODE | NONE/NONE |
| 48 | PB_ASSIGN_TO | VARCHAR(12) | No | — | D_ACCT_ID | NONE/NONE |
| 49 | PB_REMIT_TO | VARCHAR(12) | No | — | D_ACCT_ID | NONE/NONE |
| 50 | PB_PU_DRIV | VARCHAR(14) | No | — | D_UNIT_ID | NONE/NONE |
| 51 | PB_DT_RECV | DATE | No | — | RDB$518 | — |
| 52 | PB_DT_ARIV | DATE | No | — | RDB$519 | — |
| 53 | PB_DT_BILL | DATE | No | — | RDB$520 | — |
| 54 | PB_DT_LPAY | DATE | No | — | RDB$521 | — |
| 55 | PB_DT_ZBAL | DATE | No | — | RDB$522 | — |
| 56 | PB_COD_AMT | NUMERIC(16,2) | No | — | D_BILL_AMOUNT | — |
| 57 | PB_SPECIAL | VARCHAR(1) | No | — | RDB$524 | NONE/NONE |
| 58 | PB_COD_PDT | DATE | No | — | RDB$525 | — |
| 59 | PBCODSTA | VARCHAR(2) | No | — | RDB$526 | NONE/NONE |
| 60 | PB_MIN | NUMERIC(16,2) | No | — | D_BILL_AMOUNT | — |
| 61 | PB_CLEAR | VARCHAR(1) | No | — | RDB$528 | NONE/NONE |
| 62 | PBRATEQ | VARCHAR(4) | No | — | RDB$529 | NONE/NONE |
| 63 | PBHAZD | VARCHAR(4) | No | — | RDB$530 | NONE/NONE |
| 64 | PBTARIF | VARCHAR(6) | No | — | RDB$531 | NONE/NONE |
| 65 | PB_COR_DT | DATE | No | — | RDB$532 | — |
| 66 | PBPAYTYP | VARCHAR(1) | No | — | RDB$533 | NONE/NONE |
| 67 | PBTHAUL | VARCHAR(1) | No | — | RDB$534 | NONE/NONE |
| 68 | PBTOTBIL | NUMERIC(16,2) | No | — | D_BILL_AMOUNT | — |
| 69 | PBTOTPD | NUMERIC(16,2) | No | — | D_BILL_AMOUNT | — |
| 70 | PBILPAY | NUMERIC(16,2) | No | — | D_BILL_AMOUNT | — |
| 71 | PBILREC | NUMERIC(16,2) | No | — | D_BILL_AMOUNT | — |
| 72 | PBCREV | NUMERIC(16,2) | No | — | D_BILL_AMOUNT | — |
| 73 | PBTWEIGH | NUMERIC(15,3) | No | — | D_WEIGHT | — |
| 74 | PB_COD_PPD_COL | VARCHAR(1) | No | — | RDB$540 | NONE/NONE |
| 75 | PBSALMAN | VARCHAR(12) | No | — | D_ACCT_ID | NONE/NONE |
| 76 | PB_CC | VARCHAR(2) | No | — | RDB$541 | NONE/NONE |
| 77 | PBTPIECE | NUMERIC(15,3) | No | — | D_PIECES | — |
| 78 | PB_COMMODITY | VARCHAR(24) | No | — | RDB$542 | NONE/NONE |
| 79 | PB_CUST_ORD | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 80 | PBMILES | NUMERIC(11,2) | No | — | RDB$543 | — |
| 81 | PB_COD_PDT_CHK | VARCHAR(8) | No | — | RDB$544 | NONE/NONE |
| 82 | PB_COD_REC_CHK | VARCHAR(8) | No | — | RDB$545 | NONE/NONE |
| 83 | PB_COD_CDT | DATE | No | — | RDB$546 | — |
| 84 | PB_MOVE_DT | DATE | No | — | RDB$547 | — |
| 85 | PB_COD_FEE | NUMERIC(16,2) | No | — | D_BILL_AMOUNT | — |
| 86 | PB_ZONE | VARCHAR(4) | No | — | RDB$549 | NONE/NONE |
| 87 | PB_COD_REC_INIT | VARCHAR(8) | No | — | D_INITIALS | NONE/NONE |
| 88 | PB_COD_PD_INIT | VARCHAR(8) | No | — | D_INITIALS | NONE/NONE |
| 89 | PBORIG | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 90 | PB_CALLER | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 91 | PB_NOTES | BLOB SUB_TYPE 1 | No | — | RDB$551 | NONE/NONE |
| 92 | PB_REGION | VARCHAR(4) | No | — | D_REGION_ID | NONE/NONE |
| 93 | PB_CURRENT_DRIVER | BIGINT | No | — | D_SYSID | — |
| 94 | PB_CURRENT_TRACTOR | BIGINT | No | — | D_SYSID | — |
| 95 | PB_CURRENT_TRAILER | BIGINT | No | — | D_SYSID | — |
| 96 | PB_MFST_DATE | DATE | No | — | RDB$696 | — |
| 97 | PB_TTL_CUBE | NUMERIC(15,3) | No | — | RDB$697 | — |
| 98 | PB_TTL_PALLETS | INTEGER | No | — | RDB$698 | — |
| 99 | PB_TTL_PALLET_POSN | INTEGER | No | — | RDB$699 | — |
| 100 | PB_STATUS | BIGINT | No | — | D_SYSID | — |
| 101 | PB_STATUS_TIME | TIMESTAMP | No | — | RDB$2037 | — |
| 102 | PB_STATUS_INIT | VARCHAR(8) | No | — | D_INITIALS | NONE/NONE |
| 103 | PB_STATUS_LOCATION | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 104 | PB_STATUS_LOC_TYPE | VARCHAR(1) | No | — | D_LOC_TYPE | NONE/NONE |
| 105 | PB_ORDER_ENTERED_INIT | VARCHAR(8) | No | — | D_INITIALS | NONE/NONE |
| 106 | PB_ORDER_MODIFIED_TIME | TIMESTAMP | No | — | RDB$2038 | — |
| 107 | PB_ORDER_MODIFIED_INIT | VARCHAR(8) | No | — | D_INITIALS | NONE/NONE |
| 108 | PB_TOTAL_FRT | NUMERIC(16,2) | No | — | D_BILL_AMOUNT | — |
| 109 | PB_TOTAL_ASC | NUMERIC(16,2) | No | — | D_BILL_AMOUNT | — |
| 110 | PB_DISCOUNT_AMT | NUMERIC(16,2) | No | — | D_BILL_AMOUNT | — |
| 111 | PB_DISCOUNT_PERC | NUMERIC(9,4) | No | — | D_PERCENTAGE | — |
| 112 | PB_FUEL_PERC | NUMERIC(9,4) | No | — | D_PERCENTAGE | — |
| 113 | PB_TOTAL_DISC_REV | NUMERIC(16,2) | No | — | D_BILL_AMOUNT | — |
| 114 | PB_TOTAL_FUEL_REV | NUMERIC(16,2) | No | — | D_BILL_AMOUNT | — |
| 115 | PB_NET_REV | NUMERIC(16,2) | No | — | D_BILL_AMOUNT | — |
| 116 | PB_IL_REV | NUMERIC(16,2) | No | — | D_BILL_AMOUNT | — |
| 117 | PBAUDITQ | VARCHAR(12) | No | — | RDB$3611 | NONE/NONE |
| 118 | PBPRINTQ | VARCHAR(12) | No | — | RDB$3612 | NONE/NONE |
| 119 | PB_EDI_REASON | VARCHAR(4) | No | — | RDB$3683 | NONE/NONE |
| 120 | PB_STATUS_COMMENT | VARCHAR(250) | No | — | RDB$3684 | NONE/NONE |
| 121 | PB_204_ACCEPTED | VARCHAR(1) | No | — | RDB$3685 | NONE/NONE |
| 122 | PB_EDI_REPLY_TIME | TIMESTAMP | No | — | RDB$3723 | — |
| 123 | PB_RATE_BASE | CHAR(6) | No | — | RDB$3721 | NONE/NONE |
| 124 | PB_INV_NUM | BIGINT | No | — | RDB$3735 | — |
| 125 | PB_PRT_RUN_DT | DATE | No | — | RDB$3736 | — |
| 126 | PB_PRT_RUN_NUM | INTEGER | No | — | RDB$3737 | — |
| 127 | PB_REPRINT_FLG | VARCHAR(1) | No | — | RDB$3738 | NONE/NONE |
| 128 | PB_IM_SEAL_1 | VARCHAR(50) | No | — | RDB$3758 | NONE/NONE |
| 129 | PB_IM_CHASSIS | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 130 | PB_IM_AGENT_REF_NUM | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 131 | PB_IM_SHIPPER_REF_NUM | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 132 | PB_IM_BOOKING_NUM | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 133 | PB_IM_CHASSIS_PICKUPAT | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 134 | PB_IM_CHASSIS_RETURNTO | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 135 | PB_IM_YARD_DETENTION | DATE | No | — | RDB$3760 | — |
| 136 | PB_IM_PER_DIEM | DATE | No | — | RDB$3761 | — |
| 137 | PB_IM_WORK_CODE | VARCHAR(2) | No | — | RDB$3762 | NONE/NONE |
| 138 | PB_IM_LOAD_TYPE | VARCHAR(2) | No | — | RDB$3763 | NONE/NONE |
| 139 | PB_IM_VESSEL_RAIL | VARCHAR(80) | No | — | RDB$3764 | NONE/NONE |
| 140 | PB_IM_LAST_FREE_DAY | TIMESTAMP | No | — | RDB$3765 | — |
| 141 | PB_IM_VOYAGE_NUM | CHAR(20) | No | — | RDB$3845 | NONE/NONE |
| 142 | PB_IM_GENSET | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 143 | PB_IM_GENSET_PICKUPAT | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 144 | PB_IM_GENSET_RETURNTO | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 145 | PB_IM_C1_PICKUPAT | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 146 | PB_IM_C1_RETURNTO | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 147 | PB_IM_MAN_YARD_DET | VARCHAR(1) | No | — | RDB$3993 | NONE/NONE |
| 148 | PB_IM_MANUAL_PER_DIEM | VARCHAR(1) | No | — | RDB$3994 | NONE/NONE |
| 149 | PB_IM_DRIVER_ETA | TIMESTAMP | No | — | RDB$4088 | — |
| 150 | PB_IM_DESC_1_1 | BIGINT | No | — | RDB$4082 | — |
| 151 | PB_IM_DESC_1_2 | BIGINT | No | — | RDB$4083 | — |
| 152 | PB_IM_DESC_1_3 | BIGINT | No | — | RDB$4084 | — |
| 153 | PB_IM_CONTAINER_1 | BIGINT | No | — | RDB$4167 | — |
| 154 | PB_IM_GROUNDED_TIME | TIMESTAMP | No | — | RDB$4215 | — |
| 155 | PB_IM_RELEASE_NUMBER | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 156 | PB_TEMPLATE_NAME | VARCHAR(50) | No | — | RDB$4222 | NONE/NONE |
| 157 | PB_MI_OVERRIDE | CHAR(1) | No | — | RDB$4258 | NONE/NONE |
| 158 | PB_OTHER_CARRIERS | VARCHAR(1) | No | — | RDB$4261 | NONE/NONE |
| 159 | PB_CURRENT_DRIVER2 | BIGINT | No | — | D_SYSID | — |
| 160 | PB_IM_CONTAINER_2 | BIGINT | No | — | RDB$4746 | — |
| 161 | PB_IM_DESC_2_1 | BIGINT | No | — | RDB$4747 | — |
| 162 | PB_IM_DESC_2_2 | BIGINT | No | — | RDB$4748 | — |
| 163 | PB_IM_DESC_2_3 | BIGINT | No | — | RDB$4749 | — |
| 164 | PB_IM_C2_PICKUPAT | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 165 | PB_IM_C2_RETURNTO | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 166 | PB_IM_SEAL_1_2 | VARCHAR(50) | No | — | RDB$4750 | NONE/NONE |
| 167 | PB_IM_CHASSIS_2 | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 168 | PB_IM_CHASSIS_PICKUPAT_2 | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 169 | PB_IM_CHASSIS_RETURNTO_2 | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 170 | PB_IM_GENSET_2 | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 171 | PB_IM_GENSET_PICKUPAT_2 | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 172 | PB_IM_GENSET_RETURNTO_2 | VARCHAR(12) | No | — | D_LOCATION | NONE/NONE |
| 173 | PB_IM_RELEASE_NUMBER_2 | VARCHAR(80) | No | — | D_CONTROL_NUMBER | NONE/NONE |
| 174 | PB_OFF_ROUTE_MILES | NUMERIC(11,2) | No | — | RDB$5069 | — |
| 175 | PB_POD_NOTIFIED | TIMESTAMP | No | — | RDB$5062 | — |
| 176 | PB_LAST_GL_POST_DT | DATE | No | — | RDB$5317 | — |
| 177 | PB_DISCOUNT_SOURCE | VARCHAR(250) | No | — | RDB$5346 | NONE/NONE |
| 178 | PB_PLANNED_DRIVER | BIGINT | No | — | D_SYSID | — |
| 179 | PB_PLANNED_DRIVER2 | BIGINT | No | — | D_SYSID | — |
| 180 | PB_PLANNED_TRACTOR | BIGINT | No | — | D_SYSID | — |
| 181 | PB_PLANNED_TRAILER | BIGINT | No | — | D_SYSID | — |
| 182 | PB_BROKERED | VARCHAR(1) | No | — | RDB$5474 | NONE/NONE |
| 183 | PBBCONTACTNAME | VARCHAR(40) | No | — | D_NAME | NONE/NONE |
| 184 | PBBCONTACTPHONE | VARCHAR(32) | No | — | D_PHONE | NONE/NONE |
| 185 | PBBCONTACTEMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 186 | PBBCONTACTPUN | VARCHAR(1) | No | — | D_YESNO | NONE/NONE |
| 187 | PBBCONTACTDELVN | VARCHAR(1) | No | — | D_YESNO | NONE/NONE |
| 188 | PBBCONTACTPODN | VARCHAR(1) | No | — | D_YESNO | NONE/NONE |
| 190 | PB_Q_EXPIRATION_DT | DATE | No | — | RDB$5657 | — |
| 193 | PB_VIA_1 | VARCHAR(12) | No | — | RDB$6093 | NONE/NONE |
| 194 | PB_VIA_2 | VARCHAR(12) | No | — | RDB$6094 | NONE/NONE |
| 195 | PB_AF_SVC_CODE | INTEGER | No | — | RDB$6108 | — |
| 196 | PB_DPCH_BOARD_NOTES | VARCHAR(32) | No | — | RDB$6112 | NONE/NONE |
| 197 | PB_CARGO_VALUE | NUMERIC(16,2) | No | — | RDB$6358 | — |
| 198 | PB_RATE_NOTES | BLOB SUB_TYPE 1 | No | — | RDB$6413 | NONE/NONE |
| 199 | PB_TIME_SENSITIVE_CHRG_IND | VARCHAR(1) | No | — | RDB$6414 | NONE/NONE |
| 200 | PB_AF_PU_REV | NUMERIC(16,2) | No | — | RDB$6416 | — |
| 201 | PB_AF_LH_REV | NUMERIC(16,2) | No | — | RDB$6417 | — |
| 202 | PB_AF_DLV_REV | NUMERIC(16,2) | No | — | RDB$6418 | — |
| 203 | PB_AUTORATE_OVERRIDE_IND | VARCHAR(1) | No | — | RDB$6419 | NONE/NONE |
| 204 | PB_AF_PU_TARIFF | VARCHAR(12) | No | — | RDB$6420 | NONE/NONE |
| 205 | PB_AF_PU_RATEBASE | INTEGER | No | — | RDB$6421 | — |
| 206 | PB_AF_LH_TARIFF | VARCHAR(12) | No | — | RDB$6422 | NONE/NONE |
| 207 | PB_AF_LH_RATEBASE | INTEGER | No | — | RDB$6423 | — |
| 208 | PB_AF_DLV_TARIFF | VARCHAR(12) | No | — | RDB$6424 | NONE/NONE |
| 209 | PB_AF_DLV_RATEBASE | INTEGER | No | — | RDB$6425 | — |
| 210 | PB_DROP_HOOK | CHAR(1) | No | — | RDB$6457 | NONE/NONE |
| 211 | PB_MASTER_ID | BIGINT | No | — | RDB$6473 | — |
| 212 | PB_NO_CHRG | VARCHAR(1) | No | — | RDB$6479 | NONE/NONE |
| 213 | PB_IM_PRE_PULL | CHAR(1) | No | — | RDB$6598 | NONE/NONE |
| 214 | PB_IM_STREET_TURN | CHAR(1) | No | — | RDB$6599 | NONE/NONE |
| 215 | PB_IM_CHAS_DESC_1 | BIGINT | No | — | RDB$6604 | — |
| 216 | PB_IM_CHAS_DESC_2 | BIGINT | No | — | RDB$6605 | — |
| 217 | PB_IM_CHAS_DESC_3 | BIGINT | No | — | RDB$6606 | — |
| 218 | PB_DOCK_LOC | VARCHAR(32) | No | — | RDB$6607 | NONE/NONE |
| 219 | PB_WHSE_BUILD | VARCHAR(12) | No | — | RDB$6608 | NONE/NONE |
| 220 | PB_WHSE_AISLE | VARCHAR(4) | No | — | RDB$6609 | NONE/NONE |
| 221 | PB_WHSE_ROW | VARCHAR(4) | No | — | RDB$6610 | NONE/NONE |
| 222 | PB_WHSE_LEVEL | VARCHAR(4) | No | — | RDB$6611 | NONE/NONE |
| 223 | PB_WHSE_EXTRA | VARCHAR(4) | No | — | RDB$6612 | NONE/NONE |
| 224 | PB_IM_TRANS_LOAD | CHAR(1) | No | — | RDB$6808 | NONE/NONE |
| 225 | PB_EDI_BILL_ID | VARCHAR(12) | No | — | RDB$6833 | NONE/NONE |
| 226 | PB_TEMP | VARCHAR(32) | No | — | RDB$6858 | NONE/NONE |
| 227 | PB_EQUIP_REQ1 | VARCHAR(4) | No | — | RDB$6864 | NONE/NONE |
| 228 | PB_EQUIP_REQ2 | VARCHAR(4) | No | — | RDB$6865 | NONE/NONE |
| 229 | PB_CONTRACT_ID | VARCHAR(36) | No | — | RDB$6971 | NONE/NONE |
| 230 | PB_AG_MEDICATED | VARCHAR(50) | No | — | RDB$6972 | NONE/NONE |
| 231 | PB_AG_FEED_OR_GRAIN | VARCHAR(1) | No | — | RDB$6973 | NONE/NONE |
| 232 | PB_AG_PROD_CLASS | VARCHAR(1) | No | — | RDB$6974 | NONE/NONE |
| 233 | PB_AG_FEED_TYPE | VARCHAR(1) | No | — | RDB$6975 | NONE/NONE |
| 234 | PB_IM_STEAMSHIP_LINE | VARCHAR(32) | No | — | RDB$7068 | NONE/NONE |
| 235 | PB_EXTERNAL_REF | VARCHAR(200) | No | — | RDB$7178 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PH_BOL_DATE_KEY | PB_BOL_DT | No | Yes | ASC | — |
| PH_DLIV_DATE_KEY | PB_DLIV_DT | No | Yes | ASC | — |
| PH_ENT_DATE_KEY | PB_DT_ENT | No | Yes | ASC | — |
| PH_PRO_KEY | PBLOCAT, PBNUM, PBSUFFIX | No | Yes | ASC | — |
| PH_RATE_DATE_KEY | PB_DT_RATE | No | Yes | ASC | — |
| PH_STATUS_KEY | PB_STATUS, PB_STATUS_TIME | No | Yes | ASC | — |
| PH_TEMPLATE_NAME | PB_TYPE, PB_TEMPLATE_NAME, PB_ID | No | Yes | ASC | — |
| PK_OPS_HEADER | PB_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R032 | CA_HISTORY.(CAH_ORDER_ID) | OPS_HEADER.(PB_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R071 | DRIVER_PKUP_DELV.(DPD_LOAD_NUMBER) | OPS_HEADER.(PB_ID) | High | Trigger OPS_HEADER_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R078 | DRIV_ADVANCE.(DAV_PB_ID) | OPS_HEADER.(PB_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R080 | DRIV_EXPENSE.(DX_PB_ID) | OPS_HEADER.(PB_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R114 | EQUIP_HISTORY.(EH_MANIFEST_NUMBER) | OPS_HEADER.(PB_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R126 | EQUIP_STATUS.(ES_MANIFEST_NUMBER) | OPS_HEADER.(PB_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R144 | IMSENT.(IST_SHIPNUM) | OPS_HEADER.(PB_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R164 | OPS_AGENTS.(OPA_ORDER_ID) | OPS_HEADER.(PB_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping; Trigger OPS_HEADER_AFTER_DELETE explicitly deletes matching child rows  |
| R169 | OPS_DRVPAY.(ODP_ORDER_ID) | OPS_HEADER.(PB_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R171 | OPS_EDI_HISTORY.(OPEH_ORDER_ID) | OPS_HEADER.(PB_ID) | High | Trigger OPS_HEADER_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |
| R172 | OPS_EDI_RESEND.(OPER_ORDER_ID) | OPS_HEADER.(PB_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R173 | OPS_HEADER.(PBDEST) | LOCMAST.(LM_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R174 | OPS_HEADER.(PBORIG) | LOCMAST.(LM_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping  |
| R175 | OPS_HEADER.(PBSALMAN) | CT_SALESMAN.(CTSM_SALESMAN_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R176 | OPS_HEADER.(PB_BILL_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R177 | OPS_HEADER.(PB_CURRENT_DRIVER) | EQUIP_HEADER.(EH_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping  |
| R178 | OPS_HEADER.(PB_CURRENT_DRIVER2) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R179 | OPS_HEADER.(PB_CURRENT_TRACTOR) | EQUIP_HEADER.(EH_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping  |
| R180 | OPS_HEADER.(PB_CURRENT_TRAILER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R181 | OPS_HEADER.(PB_DEL_ROUTE) | CT_ROUTES.(CTR_ROUTE_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R182 | OPS_HEADER.(PB_EDI_REASON) | CT_214_REASONS.(CT2R_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R183 | OPS_HEADER.(PB_LOCATION) | LOCMAST.(LM_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping  |
| R184 | OPS_HEADER.(PB_MASTER_ID) | OPS_HEADER.(PB_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R185 | OPS_HEADER.(PB_PLANNED_DRIVER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R186 | OPS_HEADER.(PB_PLANNED_DRIVER2) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R187 | OPS_HEADER.(PB_PLANNED_TRACTOR) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R188 | OPS_HEADER.(PB_PLANNED_TRAILER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R189 | OPS_HEADER.(PB_PU_ROUTE) | CT_ROUTES.(CTR_ROUTE_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R190 | OPS_HEADER.(PB_REGION) | CT_REGION_IDS.(CTR_REGION_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R191 | OPS_HEADER.(PB_REMIT_TO) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R192 | OPS_HEADER.(PB_STATUS) | CT_STAT.(CTS_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping  |
| R195 | OPS_HISTORY.(OPH_MANIFEST_NUMBER) | OPS_HEADER.(PB_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R196 | OPS_HISTORY.(OPH_ORDER_ID) | OPS_HEADER.(PB_ID) | High | OPS_HEADER_AFTER_INSERT and OPS_HEADER_AFTER_UPDATE insert NEW.PB_ID into OPH_ORDER_ID OPS_HEADER_AFTER_DELETE deliberately retains history; a missing header can be intentional. |
| R204 | OPS_LINE_ITEMS.(OPL_ORDER_ID) | OPS_HEADER.(PB_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping; Trigger OPS_HEADER_AFTER_DELETE explicitly deletes matching child rows  |
| R208 | OPS_MFST_INFO.(OMI_MFST_ID) | OPS_HEADER.(PB_ID) | High | Trigger OPS_HEADER_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R209 | OPS_NUMBERS.(OPN_ORDER_ID) | OPS_HEADER.(PB_ID) | High | Trigger OPS_HEADER_AFTER_DELETE explicitly deletes matching child rows; Column roles and key structure; inferred, not data-validated. Delete cleanup is not insert/update referential validation. |
| R215 | OPS_OTHER_PARTIES.(OOP_ORDER_ID) | OPS_HEADER.(PB_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R223 | OPS_STOP_REC.(OPS_MANIFEST_NUMBER) | OPS_HEADER.(PB_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping  |
| R224 | OPS_STOP_REC.(OPS_ORDER_ID) | OPS_HEADER.(PB_ID) | High | Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping; Trigger OPS_HEADER_AFTER_DELETE explicitly deletes matching child rows  |
| R231 | OPS_STOP_TLR_SPACES.(OSB_MFST_ID) | OPS_HEADER.(PB_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R232 | OPS_STOP_TLR_SPACES.(OSB_ORDER_ID) | OPS_HEADER.(PB_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R273 | TRIPMILES.(TMI_SHIP_NUM) | OPS_HEADER.(PB_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R292 | WEBHOOK_QUEUE.(WHQ_ORDER_ID) | OPS_HEADER.(PB_ID) | High | Inactive triggers TRG_OPS_HEADER_WH_AI and TRG_OPS_HEADER_WH_AU map NEW.PB_ID to WHQ_ORDER_ID Mapping is explicit; both webhook triggers are inactive in this snapshot. |

## Trigger evidence

`OPS_HEADER_AFTER_DELETE`, `OPS_HEADER_AFTER_INSERT`, `OPS_HEADER_AFTER_MOD`, `OPS_HEADER_AFTER_UPDATE`, `TRG_OPS_HEADER_WH_AI` (inactive), `TRG_OPS_HEADER_WH_AU` (inactive)

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
