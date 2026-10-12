"""Build an offline table/relationship map from captured metadata; never connects to DB."""
import csv
import json
import re
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
def read(name):
    return json.loads((ROOT / 'metadata' / (name + '.json')).read_text(encoding='utf-8-sig'))
def save(name, value):
    (ROOT / name).write_text(value, encoding='utf-8')
relations, columns, constraints, indexes, segments, triggers = [read(n) for n in ('relations','columns','constraints','indexes','index-segments','triggers')]
cols = defaultdict(dict)
for c in columns:
    cols[c['TABLE_NAME']][c['NAME']] = c
segs = defaultdict(list)
for s in segments:
    segs[s['INDEX_NAME']].append(s['COLUMN_NAME'])
ix = {i['NAME']: i for i in indexes}
cons = defaultdict(list)
for c in constraints:
    cons[c['TABLE_NAME']].append(c)
edges = {}
def add(child, fields, parent, targets, confidence='Medium', evidence='Column roles and key structure; inferred, not data-validated.', note=''):
    fields = fields.split(',') if isinstance(fields, str) else fields
    targets = targets.split(',') if isinstance(targets, str) else targets
    assert child in cols and parent in cols, (child, parent)
    assert all(x in cols[child] for x in fields), (child,fields)
    assert all(x in cols[parent] for x in targets), (parent,targets)
    assert len(fields)==len(targets)
    key = (child,tuple(fields),parent,tuple(targets))
    unique = any(i['TABLE_NAME']==parent and i['IS_UNIQUE']==1 and i['INACTIVE']!=1 and segs[i['NAME']]==targets for i in indexes)
    child_unique = any(i['TABLE_NAME']==child and i['IS_UNIQUE']==1 and i['INACTIVE']!=1 and segs[i['NAME']]==fields for i in indexes)
    types = all(cols[child][a]['TYPE_CODE']==cols[parent][b]['TYPE_CODE'] and cols[child][a]['NUM_SCALE']==cols[parent][b]['NUM_SCALE'] for a,b in zip(fields,targets))
    if key in edges:
        old = edges[key]
        if evidence not in old['evidence']: old['evidence'] += '; ' + evidence
        if confidence=='High': old['confidence']='High'
        return
    edges[key] = dict(child=child, columns=fields, parent=parent, target_columns=targets, confidence=confidence,
        kind='Inferred', evidence=evidence, note=note, target_unique=unique, types_match=types,
        cardinality=('at most 1 to at most 1' if child_unique else 'many to at most 1') if unique else 'target not proven unique')

# Explicit joins and write mappings in the inspected Aurora source. No application code is executed.
aurora = 'Aurora FirebirdRoutingSource.Manifests.cs / FirebirdRoutingSource.cs / FirebirdRoutingSource.Export.cs explicit join or write mapping'
for child, f, parent, target in [
    ('OPS_STOP_REC','OPS_ORDER_ID','OPS_HEADER','PB_ID'),
    ('OPS_MANIFEST_STOPS','OSM_STOP_ID','OPS_STOP_REC','OPS_STOP_ID'),
    ('OPS_MANIFEST_STOPS','OSM_PKUP_DELV_ID','OPS_STOP_REC','OPS_STOP_ID'),
    ('OPS_STOP_REC','OPS_MANIFEST_NUMBER','OPS_HEADER','PB_ID'),
    ('OPS_LINE_ITEMS','OPL_ORDER_ID','OPS_HEADER','PB_ID'),
    ('OPS_AGENTS','OPA_ORDER_ID','OPS_HEADER','PB_ID'),
    ('OPS_HEADER','PB_STATUS','CT_STAT','CTS_ID'),
    ('OPS_HEADER','PB_CURRENT_TRACTOR','EQUIP_HEADER','EH_ID'),
    ('OPS_HEADER','PB_CURRENT_DRIVER','EQUIP_HEADER','EH_ID'),
    ('OPS_HEADER','PBORIG','LOCMAST','LM_ID'),
    ('OPS_HEADER','PB_LOCATION','LOCMAST','LM_ID')]:
    add(child,f,parent,target,'High',aurora)

# Direct trigger equality mappings: strong evidence of intended linkage, not FK enforcement.
for t in triggers:
    source = (t['SOURCE'] or '').upper()
    for m in re.finditer(r'DELETE\s+FROM\s+(\w+)\s+WHERE\s+(.+?);',source,re.S):
        child, predicate = m.groups()
        pairs = re.findall(r'(\w+)\s*=\s*OLD\.(\w+)',predicate)
        if not pairs or child not in cols: continue
        groups = [[p] for p in pairs] if re.search(r'\bOR\b',predicate) else [pairs]
        for group in groups:
            add(child,[p[0] for p in group],t['TABLE_NAME'],[p[1] for p in group], 'High',
                'Trigger '+t['NAME']+' explicitly deletes matching child rows', 'Delete cleanup is not insert/update referential validation.')

# Reviewed relationship candidates. Each endpoint is checked against captured metadata.
targets = {
 'OPS_HEADER.PB_ID': '''OPS_HISTORY.OPH_ORDER_ID OPS_HISTORY.OPH_MANIFEST_NUMBER OPS_NUMBERS.OPN_ORDER_ID OPS_OTHER_PARTIES.OOP_ORDER_ID OPS_DRVPAY.ODP_ORDER_ID OPS_EDI_RESEND.OPER_ORDER_ID OPS_STOP_TLR_SPACES.OSB_ORDER_ID OPS_STOP_TLR_SPACES.OSB_MFST_ID CA_HISTORY.CAH_ORDER_ID DRIV_ADVANCE.DAV_PB_ID DRIV_EXPENSE.DX_PB_ID EQUIP_STATUS.ES_MANIFEST_NUMBER EQUIP_HISTORY.EH_MANIFEST_NUMBER WEBHOOK_QUEUE.WHQ_ORDER_ID OPS_HEADER.PB_MASTER_ID''',
 'OPS_STOP_REC.OPS_STOP_ID': '''OPS_HISTORY.OPH_STOP_ID OPS_NUMBERS.OPN_STOP_ID OPS_STOP_TLR_SPACES.OSB_STOP_ID EQUIP_STATUS.ES_STOP_ID EQUIP_HISTORY.EH_STOP_ID''',
 'ACCOUNT.AC_CUST': '''OPS_HEADER.PB_BILL_ID OPS_HEADER.PB_REMIT_TO OPS_STOP_REC.OPS_ACCTID OPS_STOP_REC.OPS_AGENT OPS_AGENTS.OPA_AGENT_ID OPS_OTHER_PARTIES.OOP_ACCOUNT_ID ACIMAGE.ACIMG_CUST ACMESSFIX.AC_C_ACCOUNT AC_COLLECTION_NOTES.ACN_ACCOUNT AC_CONTACT.ACC_ID AC_NOTES.ACNO_ACCOUNT AC_SKU.AKU_ACCT_ID AC_YARD.ACY_YARD_ID AC_AGENT.ICA_AGENT_ID AC_AGENT_SRV_POINTS.ICAS_AGENT_ID AC_XREF.ACX_OUR_ID AC_RPT_SHIPMENTS.ACRS_ID CA_AVAILABLE_EQUIP.CAAE_ACCT_ID CA_EQUIP_INVENTORY.CAEI_ACCT_ID CA_OPER_STATES.CAOS_ACCT_ID CA_HISTORY.CAH_ACCT_ID EDI_PIF.PIF_ACCT_ID CT_API_CONNECT_INFO.CTAI_ACCOUNT WEB_USERS.WU_ACCT_ID WEB_ALLOWED_ACCTS.WUAA_ACCT_ID IMSEND.IS_CUST CASHRECPTBDY.CP_CUST_ID CLAIM_HEADER.CLH_CLAIMANT_ID ILOG_REPEAT_SHIP.IRS_CONS_ID PRICE_ASC.PASC_ACCOUNT PRICE_FAK.PFAK_ACCOUNT PRICE_FROM_TO.PFT_ACCOUNT PRICE_IL.PIL_ACCOUNT PRICE_SPECIAL.PS_ACCOUNT PRICE_TB.PTB_ACCOUNT PRICING.PRMA_ACCOUNT FUEL_SURCHARGE.FSC_ACCOUNT PALLET_TARIFF_KEYS.PAL_CUSTOMER_ID DS_POINT_PAY.DSPP_ACCT_ID ACCOUNT.AC_HDQ ACCOUNT.AC_REMIT_TO ACCOUNT.AC2_CONS_PARENT''',
 'EQUIP_HEADER.EH_ID': '''DRIVER_HEADER.DH_ID DRIVER_CONTACTS.DC_ID DRIVER_QUAL.DQ_ID DRIVER_HOS_DETAIL.DHD_ID DRIVER_HOS_SUMMARY.DHS_ID DRIV_WORKFLOW_HIST.DWH_DRV_ID EQUIP_AVAIL.EQA_ID EQUIP_PM.EQPM_ID OPS_HEADER.PB_CURRENT_DRIVER2 OPS_HEADER.PB_CURRENT_TRAILER OPS_HEADER.PB_PLANNED_DRIVER OPS_HEADER.PB_PLANNED_DRIVER2 OPS_HEADER.PB_PLANNED_TRACTOR OPS_HEADER.PB_PLANNED_TRAILER OPS_STOP_REC.OPS_DRIVER1 OPS_STOP_REC.OPS_DRIVER2 OPS_STOP_REC.OPS_TRACTOR OPS_STOP_REC.OPS_TRAILER OPS_HISTORY.OPH_DRIVER1 OPS_HISTORY.OPH_DRIVER2 OPS_HISTORY.OPH_TRACTOR OPS_HISTORY.OPH_TRAILER EQUIP_STATUS.ES_ASG_DRIVER EQUIP_STATUS.ES_ASG_DRIVER2 EQUIP_STATUS.ES_ASG_TRACTOR EQUIP_STATUS.ES_ASG_TRAILER EQUIP_STATUS.ES_ASG_CHASSIS_ID EQUIP_HISTORY.EH_ASG_DRIVER EQUIP_HISTORY.EH_ASG_DRIVER2 EQUIP_HISTORY.EH_ASG_TRACTOR EQUIP_HISTORY.EH_ASG_TRAILER OPS_STOP_TLR_SPACES.OSB_TLR_ID WEB_USERS.WU_DRIVER_ID VREPAIRHDR.VRH_UNIT_EH_ID DYMAST.DYM_EH_ID''',
 'CT_STAT.CTS_ID': '''OPS_STOP_REC.OPS_STATUS OPS_HISTORY.OPH_STATUS OPS_HISTORY.OPH_PRIOR_STATUS EQUIP_STATUS.ES_STATUS EQUIP_HISTORY.EH_STATUS EQUIP_HISTORY.EH_PRIOR_STATUS EQUIP_AVAIL.EQA_STATUS CT_STAT_FROM.CTSF_ID CT_STAT_FROM.CTSF_FROM''',
 'CT_TYPE.CTT_ID': '''EQUIP_HEADER.EH_UNIT_TYPE VREPAIRHDR.VRH_UNIT_TYPE_CTT_ID''',
 'CT_DESC.CTD_ID': '''EQUIP_DESC.ED_DESC_ID CA_AVAILABLE_EQUIP.CAAE_EQUIP_DESC_ID''',
 'CT_FLEETS.CTF_ID': 'EQUIP_HEADER.EH_FLEET',
 'CT_EVENT_CODES.CTEC_CODE': 'EQUIP_EVENT.EQE_CODE',
 'CT_DQ_DESC.CTDD_CODE': 'DRIVER_QUAL.DQ_CODE',
 'CT_DQ_STATUS.CTDS_CODE': 'DRIVER_QUAL.DQ_STATUS',
 'CT_CLAIM_STATUS.CTCS_CODE': 'CLAIM_HEADER.CLH_STATUS',
 'CT_INS_TYPES.CTI_ID': 'CA_INSURANCE.CAI_TYPE',
 'CT_SALESMAN.CTSM_SALESMAN_ID': 'ACCOUNT.AC_SALESMAN_ID OPS_HEADER.PBSALMAN',
 'CT_REGION_IDS.CTR_REGION_ID': 'ACCOUNT.AC_REGION EQUIP_HEADER.EH_REGION OPS_HEADER.PB_REGION OPS_STOP_REC.OPS_REGION LOCMAST.LM_REGION',
 'CT_ROUTES.CTR_ROUTE_ID': 'ACCOUNT.AC_ROUTE_ID OPS_STOP_REC.OPS_ROUTE OPS_HEADER.PB_PU_ROUTE OPS_HEADER.PB_DEL_ROUTE',
 'LOCMAST.LM_ID': '''OPS_HEADER.PBDEST ACCOUNT.AC_TERM_LOC CT_ROUTES.CTR_LOCATION CT_SERV_AREAS.CTSA_LOCATION EQUIP_HEADER.EH_DOMICILE EQUIP_HEADER.EH_SHOP OPS_STOP_REC.OPS_DELIVERY_TERMINAL OPS_NUMBERS.OPN_TERMINAL OPS_AGENTS.OPA_ORIG_TERM OPS_AGENTS.OPA_DEST_TERM TERMINALSERVICE.TS_ORIGLOC TERMINALSERVICE.TS_DESTLOC TERMINALSERVICE.TS_VIA1 TERMINALSERVICE.TS_VIA2 ILOG_REPEAT_SHIP.IRS_TERM USERS.U_LOCATION WEB_USERS.WU_TERMINAL''',
 'CT_STATE.CTST_CODE': 'ACCOUNT.AC_STATE LOCMAST.LM_STATE CA_OPER_STATES.CAOS_STATE IFTA_RATES.IFTA_STATE',
 'CT_COUNTRIES.CTC_CODE': 'ACCOUNT.AC_COUNTRY_CD CT_STATE.CTST_COUNTRY',
 'CT_NUMBER_TYPES.CTN_TYPE': 'OPS_NUMBERS.OPN_TYPE',
 'KEYWORDB.KW_ID': 'OPS_LINE_ITEMS.OPL_KEYWORD OPS_AG_LINES.OPAL_KEYWORD CT_COMMODITY.CTCD_KEYWORD PRICE_ASC_DATA.PASCD_KW PRICE_SPECIAL_DATA.PSD_KEYWORD',
 'DSCONTRACT.DSC_CONT_ID': 'DSCONTKW.DSCK_CONT_ID DSCONTMI.DSCM_CONT_ID DSCONTREV.DSCR_CONT_ID DSCONTSTAT.DSCS_CONT_ID DRIVER_HEADER.DH_CONTRACT',
 'DSPAYTYPES.DST_TYPE': 'DSADJUST.DSJ_DSTYPE DRVSETL.DS_TYPE DSPRELIM.DP_TYPE OPS_DRVPAY.ODP_PAYCODE',
 'PRICING.PRMA_REC_NUM': 'PRICE_WGT_BRKS.PRWB_REC_NUM PRICE_MAX_CHGS.PRMC_REC_NUM',
 'PRICE_ASC.PASC_REC_NUM': 'PRICE_ASC_DATA.PASCD_REC_NUM',
 'PRICE_FAK.PFAK_REC_NUM': 'PRICE_FAK_DATA.PFAKD_REC_NUM',
 'PRICE_FROM_TO.PFT_REC_NUM': 'PRICE_FROM_TO_DATA.PFTD_REC_NUM',
 'PRICE_IL.PIL_REC_NUM': 'PRICE_IL_DATA.PILD_REC_NUM',
 'PRICE_SPECIAL.PS_REC_NUM': 'PRICE_SPECIAL_DATA.PSD_REC_NUM',
 'PRICE_TB.PTB_REC_NUM': 'PTB_INTRA.PTBI_REC_NUM PTB_TAR_DISC.PTBTD_REC_NUM PTB_TAR_PRIO.PTBT_REC_NUM',
 'FUEL_SURCHARGE.FSC_SCALE_REC_NUM': 'FUEL_SURCHARGE_DATA.FSCD_REC_NUM',
 'COMOD_TARIFF_CONTROL.CT_TARIFF_ID': 'COMOD_TARIFF_RATE.CTR_TARIFF_ID COMOD_TARIFF_GROUPING.CTG_TARIFF_ID COMOD_TARIFF_TABLE.CTT_TARIFF_ID',
 'COMOD_TARIFF_TABLE.CTT_TABLE_REC_NUM': 'COMOD_TABLE_DATA.CTD_REC_NUM',
 'COMOD_TARIFF_RATE.CTR_PAY_REC_NUM': 'COMOD_PAY_DATA.CTP_REC_NUM',
 'PALLET_TARIFF_TABLE.PALT_TABLE_REC_NUM': 'PALLET_TABLE_DATA.PALTD_REC_NUM PALLET_MILEAGE_DATA.PALMD_REC_NUM',
 'TARIFF_MASTER.TM_TARIFF_ID': 'TARIFF_CONTROL.TC_TARIFF_ID',
 'HAZ.HAZ_DESC_REC_NUM': 'HAZ_DESC_DATA.HAZD_REC_NUM',
 'MENUS.M_ID': 'MENU_ITEMS.MI_ID USERS.U_MAIN_MENU',
 'USERS.U_ID': 'USER_SUBSYSTEMS.US_ID USERS_CONTROL_CONFIG.UCC_ID MENU_FAVORITES.MF_USER MENU_RECENT.MR_USER MENU_TODO.MT_USER CT_GRID_COLUMNS.CGC_USER_ID DAT_ACCOUNT.DA_NOVA_UID DAT_HISTORY.DH_NOVA_UID DAT_QUEUE.DQ_NOVA_UID',
 'SUBSYSTEM_TYPES.ST_ID': 'USER_SUBSYSTEMS.US_SUBSYSTEM',
 'MC_MESSAGES.MM_MSG_NUMBER': 'MC_QUEUE_ITEMS.MCQI_MSG_NUMBER PN_ACTIVITIES.PNA_AURORA_MSG_ID',
 'MC_QUEUES.MCQ_ID': 'MC_QUEUE_ITEMS.MCQI_ID MC_ROUTING.MCR_QUEUE MC_ROUTING.MCR_ERROR_QUEUE EQUIP_HEADER.EH_MC_QUEUE_ID USERS.U_MOBILE_MSG_QUEUE',
 'DAT_EQUIPMENT.DE_EQ_TYPE': 'DAT_QUEUE.DQ_EQUIPMENT_TYPE',
 'VRREPAIRCODE.VRRC_CODE': 'VREPAIRHDR.VRH_REPAIR_CODE',
 'VRWORKACCCODE.VRWAC_CODE': 'VREPAIRLABOR.VRBL_WORK_ACC VREPAIROUTSIDE.VRBO_WORK_ACC',
 'NOTIFY_TEXT_CARRIER.NTC_ID': 'DRIVER_HEADER.DH_CELL_TEXT_CARRIER AC_CONTACT.ACC_NOTIFY_TEXT_CARRIER OPS_AGENTS.OPA_DRIVER_CELL_CARRIER',
 'CNTR_TRACK_BOLS.CTB_CARRIER_BOL': 'CNTR_TRACK_CONTAINER.CTC_BOL'
}
for target, sources in targets.items():
    p, pc = target.split('.')
    for source in sources.split():
        c, cc = source.split('.')
        add(c,cc,p,pc)

for child,fields,parent,target in [
 ('OPS_AG_LINES','OPAL_ORDER_ID,OPAL_OPA_SEQUENCE','OPS_AGENTS','OPA_ORDER_ID,OPA_SEQUENCE'),
 ('CASHRECPTBDY','CP_DATE,CP_BATCH','CASHRECPTHDR','CH_DATE,CH_BATCH'),
 ('CLAIM_OTHER_PARTIES','CLOP_YR_KEY,CLOP_NUM_KEY','CLAIM_HEADER','CLH_YR_KEY,CLH_NUM_KEY'),
 ('CLAIM_PAYINFO','CLPI_YR_KEY,CLPI_NUM_KEY','CLAIM_HEADER','CLH_YR_KEY,CLH_NUM_KEY'),
 ('CNTR_TRACK_EVENTS','CTE_BOL,CTE_CNTR_ID','CNTR_TRACK_CONTAINER','CTC_BOL,CTC_CNTR_ID'),
 ('MENU_ITEM_SECURITY','MIS_ID,MIS_SEQUENCE','MENU_ITEMS','MI_ID,MI_SEQUENCE'),
 ('VPOBODY','VPOB_LOC,VPOB_PO_NUM','VPOHEAD','VPOH_LOC,VPOH_PO_NUM'),
 ('VINVHIST','VH_VPN_LOC,VH_VPN_VENDOR,VH_VPN_ID','VINVENTORY','VIN_VPN_LOC,VIN_VPN_VENDOR,VIN_VPN_ID'),
 ('VREPAIRPARTS','VRBP_IK_LOC,VRBP_IK_VEND,VRBP_IK_PART','VINVENTORY','VIN_VPN_LOC,VIN_VPN_VENDOR,VIN_VPN_ID'),
 ('MC_MESSAGES','MM_SYSTEM_TYPE,MM_FORM_NUMBER,MM_FORM_VERSION,MM_FORM_TYPE','MC_FORMS','MCF_SYSTEM_TYPE,MCF_NUMBER,MCF_VERSION,MCF_TYPE'),
 ('HAZ','HAZ_IMO_CLASS_NBR,HAZ_IMO_DIV_NBR','HAZ_CLASS_DESC','HAZC_CLASS,HAZC_DIVISION')]:
    add(child,fields,parent,target)
for table,prefix in [('VREPAIRDRVEXP','VRDE'),('VREPAIRLABOR','VRBL'),('VREPAIROUTSIDE','VRBO'),('VREPAIRPARTS','VRBP')]:
    add(table,','.join(prefix+'_RN_'+x for x in ('PREF','NUM','SUF')),'VREPAIRHDR','VRH_RN_PREF,VRH_RN_NUM,VRH_RN_SUF')
for table,prefix in [('TARIFF_ADJUSTMENTS','TA'),('TARIFF_BASES','TB'),('TARIFF_POINTS','TP'),('TARIFF_POI_RATES','TPR'),('TARIFF_RATES','TR')]:
    add(table,prefix+'_TARIFF_ID,'+prefix+'_EFF_DT','TARIFF_CONTROL','TC_TARIFF_ID,TC_EFF_DT')
add('AC_AGENT_INTER','ICA_D83_REC_NUM','AC_AGENT','ICA_REC_NUM')
add('CT_ZIP','CTZ_ROUTE','CT_ROUTES','CTR_ROUTE_ID','High','CT_ROUTES_AD0 clears CTZ_ROUTE where it equals OLD.CTR_ROUTE_ID')
for target,sources in {
 'EQUIP_HEADER.EH_ID': 'DS_CALLIN.DSC_DRIVER1 DS_CALLIN.DSC_DRIVER2 DS_CALLIN.DSC_TRACTOR DS_CALLIN.DSC_TRAILER1 DS_CALLIN.DSC_TRAILER2 FUEL.FP_EQUIP_NUM FUEL.FP_DRIVER FUEL_CARD.FC_DRIVER FUEL_CARD.FC_POWER_UNIT TRIPMILES.TMI_POWER_UNIT TRIPMILES.TMI_DRIV_NUM DRIV_ADVANCE.DAV_DRIV_NUM DRIV_EXPENSE.DX_DRIV_NUM OPS_DRVPAY.ODP_DRVID',
 'CT_TYPE.CTT_ID': 'FUEL.FP_EQUIP_TYPE FUEL_CARD.FC_UNIT_TYPE',
 'ACCOUNT.AC_CUST': 'AGENTROUTING.AGR_AGENT1_ID IM_CONT_CHRG_MX.IMCX_SHIP_ID IM_CONT_CHRG_MX.IMCX_CONS_ID IM_CONT_CHRG_MX.IMCX_BILLTO_ID',
 'OPS_HEADER.PB_ID': 'TRIPMILES.TMI_SHIP_NUM IMSENT.IST_SHIPNUM',
 'FUEL.FP_SEQ': 'TRIPMILES.TMI_FP_SEQ',
 'CT_CARD_TYPES.CTC_ID': 'FUEL.FP_CARD_TYPE',
 'ITEM_WEIGHT.IW_ITEM': 'OPS_LINE_ITEMS.OPL_ITEM_NUM',
 'QUAL128.Q_CODE': 'OPS_NUMBERS.OPN_QUALIFIER',
 'QUALN1_98.Q_CODE': 'OPS_OTHER_PARTIES.OOP_QUALIFIER OPS_STOP_REC.OPS_QUAL',
 'CT_214_REASONS.CT2R_CODE': 'OPS_HEADER.PB_EDI_REASON OPS_STOP_REC.OPS_EDI_REASON',
 'CT_WHSE_LOCS.CTWHL_WHSE_ID': 'CT_WHSE_UNITS.CTWHU_LOC',
}.items():
    p,pc=target.split('.')
    for source in sources.split():
        c,cc=source.split('.'); add(c,cc,p,pc)
add('AC_AGENT_CONTRACTS','ICAC_CONTRACT_ID','AC_AGENT','ICA_CONTRACT_ID', 'Low', 'Shared contract identifier; AC_AGENT has no unique contract key', 'Could be a shared contract definition rather than a child owned by one agent.')

# These are ambiguous by design: do not silently present a driver number as an internal equipment ID.
for e in edges.values():
    if not e['types_match']:
        if e['confidence']!='High': e['confidence']='Low'
        e['note'] += ' Storage type/scale differs; verify conversion and identifier semantics before joining.'
    if not e['target_unique']:
        e['note'] += ' Referenced fields have no exact active unique index; multiple matches are possible.'
    if e['child']=='WEBHOOK_QUEUE':
        e['evidence']='Inactive triggers TRG_OPS_HEADER_WH_AI and TRG_OPS_HEADER_WH_AU map NEW.PB_ID to WHQ_ORDER_ID'
        e['confidence']='High'
        e['note']='Mapping is explicit; both webhook triggers are inactive in this snapshot.'
    if e['child']=='OPS_HISTORY' and e['columns']==['OPH_ORDER_ID']:
        e['confidence']='High'; e['evidence']='OPS_HEADER_AFTER_INSERT and OPS_HEADER_AFTER_UPDATE insert NEW.PB_ID into OPH_ORDER_ID'
        e['note']='OPS_HEADER_AFTER_DELETE deliberately retains history; a missing header can be intentional.'
    if e['child']=='OPS_HISTORY' and e['columns']==['OPH_STOP_ID']:
        e['confidence']='High'; e['evidence']='OPS_STOP_REC_BEFORE_UPDATE writes NEW.OPS_STOP_ID into OPH_STOP_ID'
        e['note']='Header-level history uses OPH_STOP_ID=0; do not classify zero as an orphan.'
    if e['parent']=='EQUIP_HEADER' and e['child'].startswith('DRIVER_') and e['confidence']=='Medium':
        e['note'] += ' Driver/equipment identity inferred; driver subtype rules require Nova confirmation.'

def area(t):
    if t.startswith('OPS_') or t=='WEBHOOK_QUEUE': return 'Orders and manifests'
    if t.startswith(('AC','CA_')) or t=='AGENTROUTING': return 'Accounts and carriers'
    if t.startswith(('DRIV','DS')): return 'Drivers and settlement'
    if t.startswith('EQUIP') or t in ('FUEL','FUEL_CARD','FUELCOSTDAILY','TRIPMILES','IFTA_RATES'): return 'Equipment and fuel'
    if t.startswith(('PRICE','PRICING','PTB','TARIFF','COMOD','PALLET','D83','PUC','FUEL_SURCHARGE')) or t in ('CODFEE','KEYWORDB','IM_CONT_CHRG_MX','ITEM_WEIGHT'): return 'Pricing and tariffs'
    if t.startswith(('VREPAIR','VR','VINV','VPO')): return 'Maintenance and purchasing'
    if t.startswith(('CLAIM','CASHRECPT')) or t in ('IMSEND','IMSENT'): return 'Claims and receipts'
    if t.startswith(('CNTR','CT_WHSE')) or t=='DYMAST': return 'Containers and warehouse'
    if t.startswith(('MC_','DAT_','EDI_','QUAL','HAZ','ISAPI','PN_','GOMOTIVE','ILOG')): return 'Integration and reference'
    if t.startswith(('USER','MENU','WEB_','SUBSYSTEM')): return 'Users and access'
    if t in ('LOCMAST','TERMINALSERVICE','ZIPSERVICE') or t.startswith(('CT_ZIP','CT_STATE','CT_COUNTR','CT_ROUTE','CT_REGION','CT_SERV','CT_POINT')): return 'Locations and routing'
    return 'Codes and configuration'

purposes = {
 'OPS_HEADER':'Shared header for orders, manifests, templates and rate quotes (PB_TYPE O/M/T/Q, established by trigger source).',
 'OPS_STOP_REC':'Stops belonging to a header; stores pickup/delivery details and manifest routing stops.',
 'OPS_MANIFEST_STOPS':'Sequence bridge from a manifest routing stop to an order pickup/delivery stop.',
 'OPS_HISTORY':'Order and stop event history, intentionally retained when a header is deleted.',
 'OPS_LINE_ITEMS':'Order freight and charge detail, optionally attached to a stop.',
 'OPS_AGENTS':'Agents/carriers associated with an order, with amounts and work roles.',
 'OPS_AG_LINES':'Agent-specific line charges within an order.',
 'OPS_MFST_INFO':'Additional manifest timing, mileage, cost and utilization information.',
 'OPS_NUMBERS':'Order/stop reference numbers with type and qualifier.',
 'OPS_OTHER_PARTIES':'Other parties associated with an order; includes account references and copied contact details.',
 'ACCOUNT':'Party master covering customer, bill-to, agent, broker and other roles; role flags are present.',
 'EQUIP_HEADER':'Equipment identity master; Aurora also joins current driver IDs here.',
 'DRIVER_HEADER':'Driver personal/employment details; likely an extension of the equipment identity.',
 'DRIVER_PKUP_DELV':'Driver/equipment assignment list linking loads and stops.',
 'EQUIP_STATUS':'Current equipment state; update triggers write EQUIP_HISTORY.',
 'CT_STAT':'Status code dictionary shared by order and equipment state fields.',
 'CT_TYPE':'Equipment type dictionary.', 'CT_DESC':'Equipment descriptor dictionary.',
 'LOCMAST':'Terminal/location master.', 'KEYWORDB':'Charge/rating keyword rules and accounting attributes.',
 'CASHRECPTHDR':'Cash receipt batches.', 'CASHRECPTBDY':'Cash receipt batch detail.',
 'DRVSETL':'Driver settlement entries.', 'DSPRELIM':'Preliminary driver settlement entries.',
 'WEBHOOK_QUEUE':'Order webhook queue; related header enqueue triggers are inactive.',
 'DB_VERSION':'Database version marker.',
}
types = {7:'SMALLINT',8:'INTEGER',10:'FLOAT',12:'DATE',13:'TIME',14:'CHAR',16:'BIGINT',23:'BOOLEAN',24:'DECFLOAT(16)',25:'DECFLOAT(34)',26:'INT128',27:'DOUBLE PRECISION',28:'TIME WITH TIME ZONE',29:'TIMESTAMP WITH TIME ZONE',35:'TIMESTAMP',37:'VARCHAR',261:'BLOB'}
def dtype(c):
    typ = c['TYPE_CODE']; sub = c.get('SUB_TYPE')
    if typ in (7,8,16,26,27) and sub in (1,2): return ('NUMERIC' if sub==1 else 'DECIMAL')+f"({c['NUM_PRECISION']},{-(c['NUM_SCALE'] or 0)})"
    if typ in (14,37): return types[typ]+f"({c['CHAR_LENGTH']})"
    if typ==261: return 'BLOB SUB_TYPE '+str(sub or 0)
    return types.get(typ,'TYPE '+str(typ))
def cell(v):
    return str('—' if v is None or v=='' else v).replace('|','&#124;').replace('\r','').replace('\n','<br>')
def mdtable(headers, rows):
    return '\n'.join(['| '+' | '.join(headers)+' |','| '+' | '.join('---' for _ in headers)+' |']+['| '+' | '.join(cell(v) for v in row)+' |' for row in rows])+'\n'
E = sorted(edges.values(),key=lambda e:(e['child'],e['columns'],e['parent']))
for n,e in enumerate(E,1): e['id']=f'R{n:03}'
tables = []
(ROOT/'tables').mkdir(exist_ok=True)
(ROOT/'diagrams').mkdir(exist_ok=True)
for r in relations:
    t=r['NAME']; cc=list(cols[t].values()); cs=cons[t]; ii=[i for i in indexes if i['TABLE_NAME']==t]
    pk=[segs[c['INDEX_NAME']] for c in cs if c['KIND']=='PRIMARY KEY']
    ee=[e for e in E if t in (e['child'],e['parent'])]
    ts=[tr for tr in triggers if tr['TABLE_NAME']==t]
    purpose=purposes.get(t, t.replace('_',' ').title()+' records; grouped by naming and column context, business interpretation unconfirmed.')
    for c in cc:
        c['sql_type']=dtype(c)
        c['not_null']=bool(c['COLUMN_NOT_NULL'] or c['DOMAIN_NOT_NULL'])
        c['effective_default']=c['COLUMN_DEFAULT'] or c['DOMAIN_DEFAULT']
    data=dict(name=t,area=area(t),purpose=purpose,columns=cc,pk=pk,indexes=[dict(i,columns=segs[i['NAME']]) for i in ii],relationships=[e['id'] for e in ee],triggers=[tr['NAME'] for tr in ts])
    tables.append(data)
    lines=[f'# {t}\n',f'{area(t)}. {purpose}\n', '[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)\n',
      '## Keys\n', 'Primary key: '+(' ; '.join('('+', '.join(p)+')' for p in pk) or '**None declared**')+'. No foreign keys declared.\n',
      '## Columns\n',mdtable(['#','Column','SQL type','Not null','Default','Domain','Character set / collation'],
       [(c['POSITION']+1,c['NAME'],c['sql_type'],'Yes' if c['not_null'] else 'No',c['effective_default'],c['DOMAIN_NAME'], '/'.join(x for x in (c['CHARACTER_SET'],c['COLLATION_NAME']) if x)) for c in cc]),
      '## Indexes\n',mdtable(['Index','Columns (in order)','Unique','Active','Direction','Expression / predicate'],[(i['NAME'],', '.join(segs[i['NAME']]),'Yes' if i['IS_UNIQUE'] else 'No','No' if i['INACTIVE'] else 'Yes','DESC' if i['DIRECTION']==1 else 'ASC',i['EXPRESSION_SOURCE'] or i['CONDITION_SOURCE']) for i in ii]),
      '## Relationships\n',mdtable(['ID','Child columns','Parent columns','Confidence','Evidence / caution'],[(e['id'],e['child']+'.('+', '.join(e['columns'])+')',e['parent']+'.('+', '.join(e['target_columns'])+')',e['confidence'],e['evidence']+' '+e['note']) for e in ee]) if ee else 'No relationship established in this mapping. This does not prove the table is independent.\n',
      '## Trigger evidence\n', ', '.join(f'`{tr["NAME"]}`'+(' (inactive)' if tr['INACTIVE'] else '') for tr in ts) or 'None.', '\nFull metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).\n']
    save('tables/'+t+'.md','\n'.join(lines))

save('TABLES.md','# Table inventory\n\nEvery user table from `RDB$RELATIONS` is listed below. Checked means columns, keys and indexes were captured; it does not certify every inferred relationship. No user views were found.\n\n'+mdtable(['Covered','Table','Area','Columns','Primary key','Mapped links'],[('[x]',f'[{t["name"]}](tables/{t["name"]}.md)',t['area'],len(t['columns']),'; '.join(', '.join(p) for p in t['pk']) or 'None declared',len(t['relationships'])) for t in tables]))
save('RELATIONSHIPS.md','# Table relationships\n\n**Declared foreign keys: 0.** All links below are inferred logical relationships, including links with explicit trigger or application evidence. High = explicit code mapping; Medium = reviewed field/key evidence; Low = ambiguity or conflicting field types. No user records were sampled for the narrowed table-mapping scope.\n\nArrows go from the referencing child to the referenced parent. Cardinality describes key structure, not guaranteed existence. NULL/zero/blank sentinel rules and type discriminators require review.\n\n'+mdtable(['ID','Child columns','Parent columns','Confidence','Cardinality','Evidence / caution'],[(e['id'],e['child']+'.('+', '.join(e['columns'])+')',e['parent']+'.('+', '.join(e['target_columns'])+')',e['confidence'],e['cardinality'],e['evidence']+' '+e['note']) for e in E]))
with (ROOT/'relationships.csv').open('w',newline='',encoding='utf-8') as f:
    w=csv.DictWriter(f,fieldnames=['id','child','columns','parent','target_columns','kind','confidence','cardinality','target_unique','types_match','evidence','note']); w.writeheader()
    for e in E: w.writerow(dict(e,columns=','.join(e['columns']),target_columns=','.join(e['target_columns'])))

groups=sorted(set(t['area'] for t in tables))
for group in groups:
    local={t['name'] for t in tables if t['area']==group}
    ge=[e for e in E if e['child'] in local]
    out=['flowchart LR']
    # All local tables remain visible, including those with no established relationship.
    for n in sorted(local): out.append(f'  {n}["{n}"]')
    for e in ge: out.append(f'  {e["child"]} -. "{e["id"]}: {", ".join(e["columns"])}" .-> {e["parent"]}')
    slug=group.lower().replace(' ','-')
    save('diagrams/'+slug+'.mmd','\n'.join(out)+'\n')
save('map-data.json',json.dumps(dict(tables=tables,relationships=E,groups=groups,counts=dict(tables=len(tables),columns=len(columns),foreign_keys=0,primary_keys=sum(bool(t['pk']) for t in tables),indexes=len(indexes))),ensure_ascii=False,indent=2))
assert len(tables)==len(relations)==222
assert sum(len(t['columns']) for t in tables)==len(columns)==4224
assert not any(c['KIND']=='FOREIGN KEY' for c in constraints)
print(json.dumps(dict(tables=len(tables),columns=len(columns),relationships=len(E),confidence=Counter(e['confidence'] for e in E),unmapped=[t['name'] for t in tables if not t['relationships']],type_mismatches=[e['id']+': '+e['child']+'.'+','.join(e['columns'])+' -> '+e['parent'] for e in E if not e['types_match']]),indent=2))
