# Table inventory

Every user table from `RDB$RELATIONS` is listed below. Checked means columns, keys and indexes were captured; it does not certify every inferred relationship. No user views were found.

| Covered | Table | Area | Columns | Primary key | Mapped links |
| --- | --- | --- | --- | --- | --- |
| [x] | [ACCOUNT](tables/ACCOUNT.md) | Accounts and carriers | 184 | AC_CUST | 52 |
| [x] | [ACIMAGE](tables/ACIMAGE.md) | Accounts and carriers | 159 | ACIMG_CUST | 1 |
| [x] | [ACMESSFIX](tables/ACMESSFIX.md) | Accounts and carriers | 18 | AC_C_ACCOUNT, AC_C_TYPE, AC_C_CONS | 1 |
| [x] | [AC_AGENT](tables/AC_AGENT.md) | Accounts and carriers | 45 | ICA_REC_NUM | 3 |
| [x] | [AC_AGENT_CONTRACTS](tables/AC_AGENT_CONTRACTS.md) | Accounts and carriers | 9 | ICAC_CONTRACT_ID, ICAC_SEQ_NUM | 1 |
| [x] | [AC_AGENT_INTER](tables/AC_AGENT_INTER.md) | Accounts and carriers | 6 | ICA_D83_REC_NUM, ICA_D83_SEQ_NUM | 1 |
| [x] | [AC_AGENT_SRV_POINTS](tables/AC_AGENT_SRV_POINTS.md) | Accounts and carriers | 11 | ICAS_REC_NUM | 1 |
| [x] | [AC_COLLECTION_NOTES](tables/AC_COLLECTION_NOTES.md) | Accounts and carriers | 12 | ACN_ACCOUNT, ACN_SEQ_NUMBER | 1 |
| [x] | [AC_CONTACT](tables/AC_CONTACT.md) | Accounts and carriers | 33 | ACC_ID, ACC_SEQ | 2 |
| [x] | [AC_NOTES](tables/AC_NOTES.md) | Accounts and carriers | 10 | ACNO_ACCOUNT, ACNO_TYPE, ACNO_OTHER_ID | 1 |
| [x] | [AC_RPT_SHIPMENTS](tables/AC_RPT_SHIPMENTS.md) | Accounts and carriers | 4 | None declared | 2 |
| [x] | [AC_RPT_SHPMNT_LINES](tables/AC_RPT_SHPMNT_LINES.md) | Accounts and carriers | 13 | ACRSL_SHIPMENT_NUMBER, ACRSL_SEQUENCE | 1 |
| [x] | [AC_SKU](tables/AC_SKU.md) | Accounts and carriers | 6 | AKU_ACCT_ID, AKU_SKU | 1 |
| [x] | [AC_XREF](tables/AC_XREF.md) | Accounts and carriers | 3 | ACX_IDENTIFIER, ACX_THEIR_ID | 1 |
| [x] | [AC_YARD](tables/AC_YARD.md) | Accounts and carriers | 31 | ACY_YARD_ID | 1 |
| [x] | [AGENTROUTING](tables/AGENTROUTING.md) | Accounts and carriers | 5 | AGR_TYPE, AGR_FIELD1, AGR_FIELD2 | 1 |
| [x] | [CASHRECPTBDY](tables/CASHRECPTBDY.md) | Claims and receipts | 20 | CP_DATE, CP_BATCH, CP_SEQ | 2 |
| [x] | [CASHRECPTHDR](tables/CASHRECPTHDR.md) | Claims and receipts | 12 | CH_DATE, CH_BATCH | 1 |
| [x] | [CA_AVAILABLE_EQUIP](tables/CA_AVAILABLE_EQUIP.md) | Accounts and carriers | 2 | CAAE_ACCT_ID, CAAE_EQUIP_DESC_ID | 2 |
| [x] | [CA_EQUIP_INVENTORY](tables/CA_EQUIP_INVENTORY.md) | Accounts and carriers | 15 | CAEI_ID | 1 |
| [x] | [CA_HISTORY](tables/CA_HISTORY.md) | Accounts and carriers | 12 | CAH_ACCT_ID, CAH_ORDER_ID | 3 |
| [x] | [CA_INSURANCE](tables/CA_INSURANCE.md) | Accounts and carriers | 20 | CAI_OWNER_TYPE, CAI_OWNER_ID, CAI_TYPE | 1 |
| [x] | [CA_OPER_STATES](tables/CA_OPER_STATES.md) | Accounts and carriers | 2 | CAOS_ACCT_ID, CAOS_STATE | 2 |
| [x] | [CLAIM_HEADER](tables/CLAIM_HEADER.md) | Claims and receipts | 55 | CLH_YR_KEY, CLH_NUM_KEY | 4 |
| [x] | [CLAIM_OTHER_PARTIES](tables/CLAIM_OTHER_PARTIES.md) | Claims and receipts | 11 | CLOP_YR_KEY, CLOP_NUM_KEY, CLOP_SEQ | 1 |
| [x] | [CLAIM_PAYINFO](tables/CLAIM_PAYINFO.md) | Claims and receipts | 11 | CLPI_YR_KEY, CLPI_NUM_KEY, CLPI_SEQ | 1 |
| [x] | [CNTR_TRACK_BOLS](tables/CNTR_TRACK_BOLS.md) | Containers and warehouse | 10 | CTB_CARRIER_BOL | 1 |
| [x] | [CNTR_TRACK_CONTAINER](tables/CNTR_TRACK_CONTAINER.md) | Containers and warehouse | 28 | CTC_BOL, CTC_CNTR_ID | 2 |
| [x] | [CNTR_TRACK_EVENTS](tables/CNTR_TRACK_EVENTS.md) | Containers and warehouse | 18 | CTE_BOL, CTE_CNTR_ID, CTE_TIME, CTE_SEQUENCE | 1 |
| [x] | [CODFEE](tables/CODFEE.md) | Pricing and tariffs | 8 | CODFEE_TABLE_ID, CODFEE_EFF_DATE, CODFEE_SEQ | 0 |
| [x] | [COMOD_PAY_DATA](tables/COMOD_PAY_DATA.md) | Pricing and tariffs | 4 | CTP_REC_NUM, CTP_SEQ_NUM | 1 |
| [x] | [COMOD_TABLE_DATA](tables/COMOD_TABLE_DATA.md) | Pricing and tariffs | 5 | CTD_REC_NUM, CTD_SEQ_NUM | 1 |
| [x] | [COMOD_TARIFF_CONTROL](tables/COMOD_TARIFF_CONTROL.md) | Pricing and tariffs | 8 | CT_TARIFF_ID | 3 |
| [x] | [COMOD_TARIFF_GROUPING](tables/COMOD_TARIFF_GROUPING.md) | Pricing and tariffs | 3 | CTG_TARIFF_ID, CTG_POINT | 1 |
| [x] | [COMOD_TARIFF_RATE](tables/COMOD_TARIFF_RATE.md) | Pricing and tariffs | 14 | CTR_TARIFF_ID, CTR_KEY_1, CTR_KEY_2, CTR_KEY_3, CTR_KEY_4, CTR_KW, CTR_EFF_DATE | 2 |
| [x] | [COMOD_TARIFF_TABLE](tables/COMOD_TARIFF_TABLE.md) | Pricing and tariffs | 4 | CTT_TARIFF_ID, CTT_TABLE_ID | 2 |
| [x] | [CT_210_REASONS](tables/CT_210_REASONS.md) | Codes and configuration | 2 | CT2R_CODE | 0 |
| [x] | [CT_214_REASONS](tables/CT_214_REASONS.md) | Codes and configuration | 2 | CT2R_CODE | 2 |
| [x] | [CT_990_REASONS](tables/CT_990_REASONS.md) | Codes and configuration | 2 | CT9R_CODE | 0 |
| [x] | [CT_API_CONNECT_INFO](tables/CT_API_CONNECT_INFO.md) | Codes and configuration | 13 | CTAI_ACCOUNT, CTAI_TYPE | 1 |
| [x] | [CT_BATCH](tables/CT_BATCH.md) | Codes and configuration | 7 | CTB_ID | 0 |
| [x] | [CT_CARD_TYPES](tables/CT_CARD_TYPES.md) | Codes and configuration | 2 | CTC_ID | 1 |
| [x] | [CT_CLAIM_STATUS](tables/CT_CLAIM_STATUS.md) | Codes and configuration | 5 | CTCS_CODE | 1 |
| [x] | [CT_CNTR_TRACK](tables/CT_CNTR_TRACK.md) | Codes and configuration | 10 | CCT_KEY | 0 |
| [x] | [CT_CNTR_TRACK_STATUS](tables/CT_CNTR_TRACK_STATUS.md) | Codes and configuration | 2 | CCTS_CODE | 0 |
| [x] | [CT_COMMODITY](tables/CT_COMMODITY.md) | Codes and configuration | 4 | CTCD_COMMOD | 1 |
| [x] | [CT_COUNTRIES](tables/CT_COUNTRIES.md) | Locations and routing | 2 | CTC_CODE | 2 |
| [x] | [CT_DESC](tables/CT_DESC.md) | Codes and configuration | 4 | CTD_ID | 2 |
| [x] | [CT_DQ_DESC](tables/CT_DQ_DESC.md) | Codes and configuration | 5 | CTDD_CODE | 1 |
| [x] | [CT_DQ_STATUS](tables/CT_DQ_STATUS.md) | Codes and configuration | 4 | CTDS_CODE | 1 |
| [x] | [CT_EVENT_CODES](tables/CT_EVENT_CODES.md) | Codes and configuration | 2 | CTEC_CODE | 1 |
| [x] | [CT_EXCEL_INV_LAYOUT](tables/CT_EXCEL_INV_LAYOUT.md) | Codes and configuration | 16 | CEIL_SS_ID, CEIL_SEQ | 0 |
| [x] | [CT_FLEETS](tables/CT_FLEETS.md) | Codes and configuration | 58 | CTF_ID | 1 |
| [x] | [CT_GRID_COLUMNS](tables/CT_GRID_COLUMNS.md) | Codes and configuration | 3 | CGC_USER_ID, CGC_GRID_NAME | 1 |
| [x] | [CT_INS_TYPES](tables/CT_INS_TYPES.md) | Codes and configuration | 3 | CTI_ID | 1 |
| [x] | [CT_NOVA_OPTIONS](tables/CT_NOVA_OPTIONS.md) | Codes and configuration | 2 | CTNO_OPTION | 0 |
| [x] | [CT_NUMBER_TYPES](tables/CT_NUMBER_TYPES.md) | Codes and configuration | 2 | CTN_TYPE | 1 |
| [x] | [CT_OMNITRACS_XRS](tables/CT_OMNITRACS_XRS.md) | Codes and configuration | 15 | CTOX_KEY | 0 |
| [x] | [CT_PEOPLENET](tables/CT_PEOPLENET.md) | Codes and configuration | 23 | CTP_KEY | 0 |
| [x] | [CT_POINTMILES](tables/CT_POINTMILES.md) | Locations and routing | 13 | CTPM_FROM_ZIP, CTPM_FROM_CITY, CTPM_FROM_ST, CTPM_FROM_ADR, CTPM_TO_ZIP, CTPM_TO_CITY, CTPM_TO_ST, CTPM_TO_ADR, CTPM_MI_TYPE | 0 |
| [x] | [CT_QUALCOMM](tables/CT_QUALCOMM.md) | Codes and configuration | 14 | CTQ_KEY | 0 |
| [x] | [CT_RAND_MCNALLY](tables/CT_RAND_MCNALLY.md) | Codes and configuration | 15 | CTRM_KEY | 0 |
| [x] | [CT_REGION_IDS](tables/CT_REGION_IDS.md) | Locations and routing | 2 | CTR_REGION_ID | 7 |
| [x] | [CT_REGION_RANGES](tables/CT_REGION_RANGES.md) | Locations and routing | 3 | None declared | 1 |
| [x] | [CT_REGION_USER_DEFS](tables/CT_REGION_USER_DEFS.md) | Locations and routing | 4 | None declared | 1 |
| [x] | [CT_ROUTES](tables/CT_ROUTES.md) | Locations and routing | 4 | CTR_ROUTE_ID | 6 |
| [x] | [CT_SALESMAN](tables/CT_SALESMAN.md) | Codes and configuration | 14 | CTSM_SALESMAN_ID | 2 |
| [x] | [CT_SERV_AREAS](tables/CT_SERV_AREAS.md) | Locations and routing | 5 | CTSA_AREA_ID, CTSA_LOCATION | 1 |
| [x] | [CT_STAT](tables/CT_STAT.md) | Codes and configuration | 16 | CTS_ID | 10 |
| [x] | [CT_STATE](tables/CT_STATE.md) | Locations and routing | 5 | CTST_CODE | 5 |
| [x] | [CT_STAT_FROM](tables/CT_STAT_FROM.md) | Codes and configuration | 2 | CTSF_ID, CTSF_FROM | 2 |
| [x] | [CT_SYSTEM_OPTIONS](tables/CT_SYSTEM_OPTIONS.md) | Codes and configuration | 173 | CTSO_KEY | 0 |
| [x] | [CT_TYPE](tables/CT_TYPE.md) | Codes and configuration | 4 | CTT_ID | 4 |
| [x] | [CT_VMRS_CTL](tables/CT_VMRS_CTL.md) | Codes and configuration | 13 | CTVC_KEY | 0 |
| [x] | [CT_WHSE](tables/CT_WHSE.md) | Containers and warehouse | 183 | None declared | 0 |
| [x] | [CT_WHSE_LOCS](tables/CT_WHSE_LOCS.md) | Containers and warehouse | 12 | None declared | 1 |
| [x] | [CT_WHSE_UNITS](tables/CT_WHSE_UNITS.md) | Containers and warehouse | 6 | None declared | 1 |
| [x] | [CT_ZIP](tables/CT_ZIP.md) | Locations and routing | 15 | CTZ_ID | 1 |
| [x] | [D83_MINS](tables/D83_MINS.md) | Pricing and tariffs | 8 | D83_MIN_EFF_DATE | 0 |
| [x] | [D83_PNTS](tables/D83_PNTS.md) | Pricing and tariffs | 2 | D83_PNT | 0 |
| [x] | [D83_SEC3](tables/D83_SEC3.md) | Pricing and tariffs | 3 | D83_S3_LOW, D83_S3_HIGH | 0 |
| [x] | [D83_SEC4](tables/D83_SEC4.md) | Pricing and tariffs | 4 | D83_S4_LOW_RANGE_3, D83_S4_HIGH_RANGE_3, D83_S4_ZIP_5 | 0 |
| [x] | [D83_SEC6](tables/D83_SEC6.md) | Pricing and tariffs | 5 | D83_S6_LOW_ZIP, D83_S6_BASE_LOW_ZIP | 0 |
| [x] | [DAT_ACCOUNT](tables/DAT_ACCOUNT.md) | Integration and reference | 9 | DA_NOVA_UID | 1 |
| [x] | [DAT_EQUIPMENT](tables/DAT_EQUIPMENT.md) | Integration and reference | 2 | DE_EQ_TYPE | 1 |
| [x] | [DAT_HISTORY](tables/DAT_HISTORY.md) | Integration and reference | 6 | DH_TIMESTAMP, DH_SEQUENCE | 1 |
| [x] | [DAT_QUEUE](tables/DAT_QUEUE.md) | Integration and reference | 18 | DQ_ID | 2 |
| [x] | [DB_VERSION](tables/DB_VERSION.md) | Codes and configuration | 1 | VERSION | 0 |
| [x] | [DRIVER_CONTACTS](tables/DRIVER_CONTACTS.md) | Drivers and settlement | 11 | DC_ID, DC_SEQUENCE | 1 |
| [x] | [DRIVER_HEADER](tables/DRIVER_HEADER.md) | Drivers and settlement | 80 | DH_ID | 3 |
| [x] | [DRIVER_HOS_DETAIL](tables/DRIVER_HOS_DETAIL.md) | Drivers and settlement | 11 | DHD_ID, DHD_START_TIME | 1 |
| [x] | [DRIVER_HOS_SUMMARY](tables/DRIVER_HOS_SUMMARY.md) | Drivers and settlement | 39 | DHS_ID | 1 |
| [x] | [DRIVER_PKUP_DELV](tables/DRIVER_PKUP_DELV.md) | Drivers and settlement | 7 | DPD_ID, DPD_SEQUENCE | 4 |
| [x] | [DRIVER_QUAL](tables/DRIVER_QUAL.md) | Drivers and settlement | 9 | DQ_ID, DQ_CODE | 3 |
| [x] | [DRIV_ADVANCE](tables/DRIV_ADVANCE.md) | Drivers and settlement | 17 | DAV_SEQ | 2 |
| [x] | [DRIV_EXPENSE](tables/DRIV_EXPENSE.md) | Drivers and settlement | 25 | DX_SEQ | 2 |
| [x] | [DRIV_WORKFLOW_HIST](tables/DRIV_WORKFLOW_HIST.md) | Drivers and settlement | 4 | DWH_DRV_ID, DWH_TIMESTAMP | 1 |
| [x] | [DRVSETL](tables/DRVSETL.md) | Codes and configuration | 41 | DS_CO, DS_DRIV, DS_SEQ | 1 |
| [x] | [DSADJUST](tables/DSADJUST.md) | Drivers and settlement | 12 | DSJ_DRIV, DSJ_DSTYPE | 1 |
| [x] | [DSCNTRLS](tables/DSCNTRLS.md) | Drivers and settlement | 9 | DSCNTRLS | 0 |
| [x] | [DSCONTKW](tables/DSCONTKW.md) | Drivers and settlement | 9 | DSCK_CONT_ID, DSCK_SEQ | 1 |
| [x] | [DSCONTMI](tables/DSCONTMI.md) | Drivers and settlement | 8 | DSCM_CONT_ID, DSCM_SEQ | 1 |
| [x] | [DSCONTRACT](tables/DSCONTRACT.md) | Drivers and settlement | 17 | DSC_CONT_ID | 5 |
| [x] | [DSCONTREV](tables/DSCONTREV.md) | Drivers and settlement | 9 | DSCR_CONT_ID, DSCR_SEQ, DSCR_TYPE | 1 |
| [x] | [DSCONTSTAT](tables/DSCONTSTAT.md) | Drivers and settlement | 9 | DSCS_CONT_ID, DSCS_SEQ | 1 |
| [x] | [DSPAYTYPES](tables/DSPAYTYPES.md) | Drivers and settlement | 10 | DST_TYPE | 4 |
| [x] | [DSPRELIM](tables/DSPRELIM.md) | Drivers and settlement | 41 | DP_CO, DP_DRIV, DP_SEQ | 1 |
| [x] | [DS_CALLIN](tables/DS_CALLIN.md) | Drivers and settlement | 17 | DSC_SEQ | 5 |
| [x] | [DS_POINT_PAY](tables/DS_POINT_PAY.md) | Drivers and settlement | 13 | DSPP_ACCT_ID, DSPP_FROM_TYPE, DSPP_FROM_ID, DSPP_TO_TYPE, DSPP_TO_ID | 1 |
| [x] | [DYMAST](tables/DYMAST.md) | Containers and warehouse | 180 | DYM_LOC, DYM_UNIT_TYPE, DYM_UNIT_ID, DYM_DATE, DYM_TIME | 1 |
| [x] | [EDI_PIF](tables/EDI_PIF.md) | Integration and reference | 36 | PIF_ACCT_ID, PIF_TRAN | 1 |
| [x] | [EQUIP_AVAIL](tables/EQUIP_AVAIL.md) | Equipment and fuel | 7 | EQA_ID, EQA_DATE | 2 |
| [x] | [EQUIP_DESC](tables/EQUIP_DESC.md) | Equipment and fuel | 2 | ED_ID, ED_DESC_ID | 2 |
| [x] | [EQUIP_EVENT](tables/EQUIP_EVENT.md) | Equipment and fuel | 5 | EQE_ID, EQE_SEQ | 2 |
| [x] | [EQUIP_HEADER](tables/EQUIP_HEADER.md) | Equipment and fuel | 111 | EH_ID | 64 |
| [x] | [EQUIP_HISTORY](tables/EQUIP_HISTORY.md) | Equipment and fuel | 20 | EH_ID, EH_SEQUENCE | 9 |
| [x] | [EQUIP_PM](tables/EQUIP_PM.md) | Equipment and fuel | 8 | EQPM_ID, EQPM_PGM | 1 |
| [x] | [EQUIP_POSITIONS](tables/EQUIP_POSITIONS.md) | Equipment and fuel | 12 | EP_ID, EP_DATE | 1 |
| [x] | [EQUIP_STATUS](tables/EQUIP_STATUS.md) | Equipment and fuel | 29 | ES_ID | 9 |
| [x] | [FUEL](tables/FUEL.md) | Equipment and fuel | 38 | FP_SEQ | 5 |
| [x] | [FUELCOSTDAILY](tables/FUELCOSTDAILY.md) | Equipment and fuel | 2 | FCD_DATE | 0 |
| [x] | [FUEL_CARD](tables/FUEL_CARD.md) | Equipment and fuel | 7 | FC_ID | 3 |
| [x] | [FUEL_SURCHARGE](tables/FUEL_SURCHARGE.md) | Pricing and tariffs | 14 | FSC_TYPE, FSC_ACCOUNT, FSC_LOCATION, FSC_EFF_DATE | 2 |
| [x] | [FUEL_SURCHARGE_DATA](tables/FUEL_SURCHARGE_DATA.md) | Pricing and tariffs | 6 | FSCD_REC_NUM, FSCD_SEQ_NUM | 1 |
| [x] | [GOMOTIVELANDING](tables/GOMOTIVELANDING.md) | Integration and reference | 33 | None declared | 0 |
| [x] | [HAZ](tables/HAZ.md) | Integration and reference | 14 | HAZ_ID, HAZ_SEQ | 2 |
| [x] | [HAZ_CLASS_DESC](tables/HAZ_CLASS_DESC.md) | Integration and reference | 9 | HAZC_CLASS, HAZC_DIVISION | 1 |
| [x] | [HAZ_DESC_DATA](tables/HAZ_DESC_DATA.md) | Integration and reference | 3 | HAZD_REC_NUM, HAZD_SEQ_NUM | 1 |
| [x] | [IFTA_RATES](tables/IFTA_RATES.md) | Equipment and fuel | 11 | IFTA_STATE, IFTA_EFF_DATE | 1 |
| [x] | [ILOG_REPEAT_SHIP](tables/ILOG_REPEAT_SHIP.md) | Integration and reference | 13 | IRS_TERM, IRS_ROUTE, IRS_SEQ, IRS_CONS_ID | 2 |
| [x] | [IMSEND](tables/IMSEND.md) | Claims and receipts | 23 | IS_CUST | 1 |
| [x] | [IMSENT](tables/IMSENT.md) | Claims and receipts | 4 | IST_SHIPNUM, IST_DOC_TYPE | 1 |
| [x] | [IM_CONT_CHRG_MX](tables/IM_CONT_CHRG_MX.md) | Pricing and tariffs | 79 | IMCX_PD_OR_STOR, IMCX_SCAC, IMCX_EQ_TYP, IMCX_DESCID_1, IMCX_DESCID_2, IMCX_DESCID_3, IMCX_SHIP_ID, IMCX_CONS_ID, IMCX_BILLTO_ID, IMCX_WORK_CODE | 3 |
| [x] | [ISAPI_QUEUE](tables/ISAPI_QUEUE.md) | Integration and reference | 16 | IQ_ID | 0 |
| [x] | [ITEM_WEIGHT](tables/ITEM_WEIGHT.md) | Pricing and tariffs | 5 | IW_ITEM | 1 |
| [x] | [KEYWORDB](tables/KEYWORDB.md) | Pricing and tariffs | 52 | KW_ID | 5 |
| [x] | [LOCMAST](tables/LOCMAST.md) | Locations and routing | 56 | LM_ID | 21 |
| [x] | [MC_FORMS](tables/MC_FORMS.md) | Integration and reference | 9 | MCF_SYSTEM_TYPE, MCF_NUMBER, MCF_VERSION, MCF_TYPE | 1 |
| [x] | [MC_FORM_CATEGORIES](tables/MC_FORM_CATEGORIES.md) | Integration and reference | 3 | MFC_SYSTEM_TYPE, MFC_ID | 0 |
| [x] | [MC_MESSAGES](tables/MC_MESSAGES.md) | Integration and reference | 31 | MM_MSG_NUMBER | 3 |
| [x] | [MC_QUEUES](tables/MC_QUEUES.md) | Integration and reference | 4 | MCQ_ID | 5 |
| [x] | [MC_QUEUE_ITEMS](tables/MC_QUEUE_ITEMS.md) | Integration and reference | 3 | MCQI_ID, MCQI_MSG_NUMBER | 2 |
| [x] | [MC_ROUTING](tables/MC_ROUTING.md) | Integration and reference | 8 | MCR_SYSTEM_TYPE, MCR_FORM_NUMBER, MCR_FORM_TYPE | 2 |
| [x] | [MENUS](tables/MENUS.md) | Users and access | 2 | M_ID | 2 |
| [x] | [MENU_FAVORITES](tables/MENU_FAVORITES.md) | Users and access | 3 | MF_USER, MF_DESC | 1 |
| [x] | [MENU_ITEMS](tables/MENU_ITEMS.md) | Users and access | 25 | MI_ID, MI_SEQUENCE | 2 |
| [x] | [MENU_ITEM_SECURITY](tables/MENU_ITEM_SECURITY.md) | Users and access | 3 | MIS_ID, MIS_SEQUENCE, MIS_SUBSYSTEM | 2 |
| [x] | [MENU_RECENT](tables/MENU_RECENT.md) | Users and access | 5 | MR_USER, MR_TIME | 1 |
| [x] | [MENU_TODO](tables/MENU_TODO.md) | Users and access | 4 | MT_USER, MT_TIME | 1 |
| [x] | [NOTIFY_TEXT_CARRIER](tables/NOTIFY_TEXT_CARRIER.md) | Codes and configuration | 4 | NTC_ID | 3 |
| [x] | [OPS_AGENTS](tables/OPS_AGENTS.md) | Orders and manifests | 37 | OPA_ORDER_ID, OPA_SEQUENCE | 7 |
| [x] | [OPS_AG_LINES](tables/OPS_AG_LINES.md) | Orders and manifests | 9 | OPAL_ORDER_ID, OPAL_SEQ, OPAL_OPA_SEQUENCE | 2 |
| [x] | [OPS_DRVPAY](tables/OPS_DRVPAY.md) | Orders and manifests | 9 | ODP_ORDER_ID, ODP_SEQ | 3 |
| [x] | [OPS_EDI_HISTORY](tables/OPS_EDI_HISTORY.md) | Orders and manifests | 6 | OPEH_ORDER_ID, OPEH_SEQUENCE, OPEH_PARTNER_ID, OPEH_STATUS | 1 |
| [x] | [OPS_EDI_RESEND](tables/OPS_EDI_RESEND.md) | Orders and manifests | 2 | OPER_ORDER_ID, OPER_TYPE | 1 |
| [x] | [OPS_HEADER](tables/OPS_HEADER.md) | Orders and manifests | 232 | PB_ID | 43 |
| [x] | [OPS_HISTORY](tables/OPS_HISTORY.md) | Orders and manifests | 55 | OPH_ORDER_ID, OPH_SEQUENCE | 9 |
| [x] | [OPS_LINE_ITEMS](tables/OPS_LINE_ITEMS.md) | Orders and manifests | 27 | OPL_ORDER_ID, OPL_SEQUENCE | 4 |
| [x] | [OPS_MANIFEST_STOPS](tables/OPS_MANIFEST_STOPS.md) | Orders and manifests | 3 | OSM_STOP_ID, OSM_SEQUENCE | 3 |
| [x] | [OPS_MFST_INFO](tables/OPS_MFST_INFO.md) | Orders and manifests | 18 | OMI_MFST_ID | 1 |
| [x] | [OPS_NUMBERS](tables/OPS_NUMBERS.md) | Orders and manifests | 9 | OPN_ORDER_ID, OPN_SEQUENCE | 5 |
| [x] | [OPS_OTHER_PARTIES](tables/OPS_OTHER_PARTIES.md) | Orders and manifests | 20 | OOP_ORDER_ID, OOP_SEQUENCE | 3 |
| [x] | [OPS_STOP_REC](tables/OPS_STOP_REC.md) | Orders and manifests | 106 | OPS_STOP_ID | 23 |
| [x] | [OPS_STOP_TLR_SPACES](tables/OPS_STOP_TLR_SPACES.md) | Orders and manifests | 28 | OSB_STOP_ID | 5 |
| [x] | [PALLET_MILEAGE_DATA](tables/PALLET_MILEAGE_DATA.md) | Pricing and tariffs | 4 | PALMD_REC_NUM, PALMD_SEQ_NUM | 1 |
| [x] | [PALLET_TABLE_DATA](tables/PALLET_TABLE_DATA.md) | Pricing and tariffs | 5 | PALTD_REC_NUM, PALTD_SEQ_NUM | 1 |
| [x] | [PALLET_TARIFF_KEYS](tables/PALLET_TARIFF_KEYS.md) | Pricing and tariffs | 9 | PAL_KEYS_REC_NUM | 1 |
| [x] | [PALLET_TARIFF_TABLE](tables/PALLET_TARIFF_TABLE.md) | Pricing and tariffs | 7 | PALT_TARIFF_ID, PALT_TABLE_ID, PALT_EFF_DATE | 2 |
| [x] | [PN_ACTIVITIES](tables/PN_ACTIVITIES.md) | Integration and reference | 7 | PNA_ID | 1 |
| [x] | [PRICE_ASC](tables/PRICE_ASC.md) | Pricing and tariffs | 9 | PASC_REC_NUM | 2 |
| [x] | [PRICE_ASC_DATA](tables/PRICE_ASC_DATA.md) | Pricing and tariffs | 10 | PASCD_REC_NUM, PASCD_SEQ_NUM | 2 |
| [x] | [PRICE_FAK](tables/PRICE_FAK.md) | Pricing and tariffs | 4 | PFAK_REC_NUM | 2 |
| [x] | [PRICE_FAK_DATA](tables/PRICE_FAK_DATA.md) | Pricing and tariffs | 5 | PFAKD_REC_NUM, PFAKD_SEQ_NUM, PFAKD_RULE | 1 |
| [x] | [PRICE_FROM_TO](tables/PRICE_FROM_TO.md) | Pricing and tariffs | 4 | PFT_REC_NUM | 2 |
| [x] | [PRICE_FROM_TO_DATA](tables/PRICE_FROM_TO_DATA.md) | Pricing and tariffs | 12 | PFTD_REC_NUM, PFTD_SEQ_NUM | 1 |
| [x] | [PRICE_IL](tables/PRICE_IL.md) | Pricing and tariffs | 4 | PIL_REC_NUM | 2 |
| [x] | [PRICE_IL_DATA](tables/PRICE_IL_DATA.md) | Pricing and tariffs | 12 | PILD_REC_NUM, PILD_SEQ_NUM | 1 |
| [x] | [PRICE_MAX_CHGS](tables/PRICE_MAX_CHGS.md) | Pricing and tariffs | 5 | PRMC_REC_NUM, PRMC_SEQ_NUM | 1 |
| [x] | [PRICE_SPECIAL](tables/PRICE_SPECIAL.md) | Pricing and tariffs | 4 | PS_REC_NUM | 2 |
| [x] | [PRICE_SPECIAL_DATA](tables/PRICE_SPECIAL_DATA.md) | Pricing and tariffs | 12 | PSD_REC_NUM, PSD_SEQ_NUM | 2 |
| [x] | [PRICE_TB](tables/PRICE_TB.md) | Pricing and tariffs | 13 | PTB_REC_NUM | 4 |
| [x] | [PRICE_WGT_BRKS](tables/PRICE_WGT_BRKS.md) | Pricing and tariffs | 5 | PRWB_REC_NUM, PRWB_SEQ_NUM | 1 |
| [x] | [PRICING](tables/PRICING.md) | Pricing and tariffs | 74 | PRMA_REC_NUM | 3 |
| [x] | [PTB_INTRA](tables/PTB_INTRA.md) | Pricing and tariffs | 5 | PTBI_REC_NUM, PTBI_SEQ_NUM | 1 |
| [x] | [PTB_TAR_DISC](tables/PTB_TAR_DISC.md) | Pricing and tariffs | 8 | PTBTD_REC_NUM, PTBTD_SEQ_NUM | 1 |
| [x] | [PTB_TAR_PRIO](tables/PTB_TAR_PRIO.md) | Pricing and tariffs | 4 | PTBT_REC_NUM, PTBT_SEQ_NUM | 1 |
| [x] | [PUC_RATES](tables/PUC_RATES.md) | Pricing and tariffs | 4 | PUC_STATE, PUC_EFF_DATE | 1 |
| [x] | [PUC_WGT](tables/PUC_WGT.md) | Pricing and tariffs | 9 | PUCW_RATE_NUM, PUCW_WGT_FROM | 1 |
| [x] | [QUAL128](tables/QUAL128.md) | Integration and reference | 15 | Q_CODE | 1 |
| [x] | [QUALG62_176](tables/QUALG62_176.md) | Integration and reference | 10 | Q_CODE | 0 |
| [x] | [QUALG62_432](tables/QUALG62_432.md) | Integration and reference | 10 | Q_CODE | 0 |
| [x] | [QUALN1_98](tables/QUALN1_98.md) | Integration and reference | 10 | Q_CODE | 2 |
| [x] | [SUBSYSTEM_TYPES](tables/SUBSYSTEM_TYPES.md) | Users and access | 2 | ST_ID | 2 |
| [x] | [TARIFF_ADJUSTMENTS](tables/TARIFF_ADJUSTMENTS.md) | Pricing and tariffs | 18 | TA_TARIFF_ID, TA_EFF_DT, TA_FROM, TA_TO, TA_SEQ | 1 |
| [x] | [TARIFF_BASES](tables/TARIFF_BASES.md) | Pricing and tariffs | 6 | TB_TARIFF_ID, TB_EFF_DT, TB_ORIGIN, TB_DEST | 1 |
| [x] | [TARIFF_COMMON_POINTS](tables/TARIFF_COMMON_POINTS.md) | Pricing and tariffs | 4 | TPC_ZIP_POINT | 0 |
| [x] | [TARIFF_CONTROL](tables/TARIFF_CONTROL.md) | Pricing and tariffs | 68 | TC_TARIFF_ID, TC_EFF_DT | 6 |
| [x] | [TARIFF_MASTER](tables/TARIFF_MASTER.md) | Pricing and tariffs | 8 | TM_TARIFF_ID | 1 |
| [x] | [TARIFF_POINTS](tables/TARIFF_POINTS.md) | Pricing and tariffs | 5 | TP_TARIFF_ID, TP_EFF_DT, TP_ZIP_POINT | 1 |
| [x] | [TARIFF_POI_RATES](tables/TARIFF_POI_RATES.md) | Pricing and tariffs | 25 | TPR_TARIFF_ID, TPR_EFF_DT, TPR_ACCOUNT, TPR_TYPE, TPR_KEYWORD, TPR_LOW_ZIP, TPR_HI_ZIP, TPR_STATE_ID | 1 |
| [x] | [TARIFF_RATES](tables/TARIFF_RATES.md) | Pricing and tariffs | 53 | TR_TARIFF_ID, TR_EFF_DT, TR_RATE_BASE, TR_CLASS | 1 |
| [x] | [TERMINALSERVICE](tables/TERMINALSERVICE.md) | Locations and routing | 5 | TS_ORIGLOC, TS_DESTLOC, TS_VIA1, TS_VIA2 | 4 |
| [x] | [TRIPMILES](tables/TRIPMILES.md) | Equipment and fuel | 19 | TMI_KEYNUM | 4 |
| [x] | [USERS](tables/USERS.md) | Users and access | 35 | U_ID | 12 |
| [x] | [USERS_CONTROL_CONFIG](tables/USERS_CONTROL_CONFIG.md) | Users and access | 5 | UCC_ID, UCC_PROGRAM_ID, UCC_CONTROL_ID, UCC_PROPERTY_NAME | 1 |
| [x] | [USER_SUBSYSTEMS](tables/USER_SUBSYSTEMS.md) | Users and access | 3 | US_ID, US_SUBSYSTEM | 2 |
| [x] | [VINVENTORY](tables/VINVENTORY.md) | Maintenance and purchasing | 51 | VIN_VPN_LOC, VIN_VPN_VENDOR, VIN_VPN_ID | 2 |
| [x] | [VINVHIST](tables/VINVHIST.md) | Maintenance and purchasing | 17 | VH_ID | 1 |
| [x] | [VPOBODY](tables/VPOBODY.md) | Maintenance and purchasing | 14 | VPOB_LOC, VPOB_PO_NUM, VPOB_SEQ | 1 |
| [x] | [VPOHEAD](tables/VPOHEAD.md) | Maintenance and purchasing | 37 | VPOH_LOC, VPOH_PO_NUM | 1 |
| [x] | [VREPAIRDRVEXP](tables/VREPAIRDRVEXP.md) | Maintenance and purchasing | 11 | VRDE_RN_PREF, VRDE_RN_NUM, VRDE_RN_SUF, VRDE_SEQ | 1 |
| [x] | [VREPAIRHDR](tables/VREPAIRHDR.md) | Maintenance and purchasing | 46 | VRH_RN_PREF, VRH_RN_NUM, VRH_RN_SUF | 7 |
| [x] | [VREPAIRLABOR](tables/VREPAIRLABOR.md) | Maintenance and purchasing | 14 | VRBL_RN_PREF, VRBL_RN_NUM, VRBL_RN_SUF, VRBL_REC_SEQ | 2 |
| [x] | [VREPAIROUTSIDE](tables/VREPAIROUTSIDE.md) | Maintenance and purchasing | 14 | VRBO_RN_PREF, VRBO_RN_NUM, VRBO_RN_SUF, VRBO_REC_SEQ | 2 |
| [x] | [VREPAIRPARTS](tables/VREPAIRPARTS.md) | Maintenance and purchasing | 20 | VRBP_RN_PREF, VRBP_RN_NUM, VRBP_RN_SUF, VRBP_REC_SEQ | 2 |
| [x] | [VRREPAIRCODE](tables/VRREPAIRCODE.md) | Maintenance and purchasing | 2 | VRRC_CODE | 1 |
| [x] | [VRWORKACCCODE](tables/VRWORKACCCODE.md) | Maintenance and purchasing | 2 | VRWAC_CODE | 2 |
| [x] | [WEBHOOK_QUEUE](tables/WEBHOOK_QUEUE.md) | Orders and manifests | 6 | WHQ_ID | 1 |
| [x] | [WEB_ALLOWED_ACCTS](tables/WEB_ALLOWED_ACCTS.md) | Users and access | 4 | WUAA_USER_ID, WUAA_ACCT_ID | 2 |
| [x] | [WEB_DASHBOARD_ITEMS](tables/WEB_DASHBOARD_ITEMS.md) | Users and access | 4 | WDI_USER_ID, WDI_SEQUENCE | 1 |
| [x] | [WEB_USERS](tables/WEB_USERS.md) | Users and access | 25 | WU_USER_ID | 5 |
| [x] | [ZIPSERVICE](tables/ZIPSERVICE.md) | Locations and routing | 3 | ZS_ORIGZIP, ZS_DESTZIP | 0 |
