# VREPAIRHDR

Maintenance and purchasing. Vrepairhdr records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (VRH_RN_PREF, VRH_RN_NUM, VRH_RN_SUF). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | VRH_RN_PREF | VARCHAR(1) | Yes | — | RDB$4880 | NONE/NONE |
| 2 | VRH_RN_NUM | INTEGER | Yes | — | RDB$4881 | — |
| 3 | VRH_RN_SUF | VARCHAR(2) | Yes | — | RDB$4882 | NONE/NONE |
| 5 | VRH_EDATE | TIMESTAMP | No | — | RDB$4884 | — |
| 6 | VRH_LOC | VARCHAR(6) | No | — | RDB$4885 | NONE/NONE |
| 7 | VRH_DATE | DATE | No | — | RDB$4886 | — |
| 9 | VRH_INSP_DT | DATE | No | — | RDB$4888 | — |
| 10 | VRH_INSPECTOR | VARCHAR(18) | No | — | RDB$4889 | NONE/NONE |
| 11 | VRH_SCAC | VARCHAR(4) | No | — | RDB$4890 | NONE/NONE |
| 12 | VRH_ODO | INTEGER | No | — | RDB$4891 | — |
| 13 | VRH_RESET_ODO | INTEGER | No | — | RDB$4892 | — |
| 14 | VRH_EST_UTL_ODO | INTEGER | No | — | RDB$4893 | — |
| 15 | VRH_ACCT | VARCHAR(12) | No | — | RDB$4894 | NONE/NONE |
| 16 | VRH_AC_NAME | VARCHAR(40) | No | — | RDB$4895 | NONE/NONE |
| 17 | VRH_AC_ADR1 | VARCHAR(50) | No | — | RDB$4896 | NONE/NONE |
| 18 | VRH_AC_ADR2 | VARCHAR(50) | No | — | RDB$4897 | NONE/NONE |
| 19 | VRH_AC_CITY | VARCHAR(35) | No | — | RDB$4898 | NONE/NONE |
| 20 | VRH_AC_ST | VARCHAR(2) | No | — | RDB$4899 | NONE/NONE |
| 21 | VRH_AC_ZIP | VARCHAR(10) | No | — | RDB$4900 | NONE/NONE |
| 22 | VRH_REPAIR_CODE | VARCHAR(2) | No | — | RDB$4901 | NONE/NONE |
| 23 | VRH_REPAIR_CLASS | VARCHAR(1) | No | — | RDB$4902 | NONE/NONE |
| 24 | VRH_REPAIR_SITE | VARCHAR(1) | No | — | RDB$4903 | NONE/NONE |
| 26 | VRH_TIRE_OTHER_IND | VARCHAR(1) | No | — | RDB$4905 | NONE/NONE |
| 27 | VRH_RR_OR_OTHER_PO_IND | VARCHAR(1) | No | — | RDB$4906 | NONE/NONE |
| 28 | VRH_PRO_LOC | VARCHAR(12) | No | — | RDB$4907 | NONE/NONE |
| 29 | VRH_PRO_NUM | INTEGER | No | — | RDB$4908 | — |
| 30 | VRH_PRO_SUF | VARCHAR(2) | No | — | RDB$4909 | NONE/NONE |
| 31 | VRH_ACT_CODE | VARCHAR(1) | No | — | RDB$4910 | NONE/NONE |
| 32 | VRH_COMPLETE_DT | DATE | No | — | RDB$4911 | — |
| 33 | VRH_REF_NUM | VARCHAR(16) | No | — | RDB$4912 | NONE/NONE |
| 34 | VRH_EQ_MAKE | VARCHAR(12) | No | — | RDB$4913 | NONE/NONE |
| 35 | VRH_EQ_YEAR | VARCHAR(4) | No | — | RDB$4914 | NONE/NONE |
| 38 | VRH_PURCH_ORD | VARCHAR(15) | No | — | RDB$4917 | NONE/NONE |
| 39 | VRH_EDI_TIRE_SUF | VARCHAR(3) | No | — | RDB$4918 | NONE/NONE |
| 40 | VRH_EDI_OTH_SUF | VARCHAR(3) | No | — | RDB$4919 | NONE/NONE |
| 41 | VRH_BILLER_ID | VARCHAR(12) | No | — | RDB$4920 | NONE/NONE |
| 42 | VRH_EIR | VARCHAR(8) | No | — | RDB$4921 | NONE/NONE |
| 43 | VRH_INSTRUCTIONS | BLOB SUB_TYPE 0 | No | — | RDB$4922 | — |
| 44 | VRH_REPAIR_DATE | DATE | No | — | RDB$4984 | — |
| 45 | VRH_PRO_ORDER_ID | INTEGER | No | — | RDB$5017 | — |
| 46 | VRH_DRIVER_ID | BIGINT | No | — | RDB$5070 | — |
| 47 | VRH_OUTSIDE_VEHICLE | VARCHAR(25) | No | — | RDB$5071 | NONE/NONE |
| 48 | VRH_UNIT_TYPE_CTT_ID | BIGINT | No | — | RDB$5072 | — |
| 49 | VRH_UNIT_EH_ID | BIGINT | No | — | RDB$5073 | — |
| 50 | VRH_TOTAL_AMT | NUMERIC(16,2) | No | — | RDB$5623 | — |
| 51 | VRH_TAX_AMT | NUMERIC(16,2) | No | — | RDB$5624 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| VRH_DATE_KEY | VRH_DATE | No | Yes | ASC | — |
| VRH_KEY | VRH_RN_PREF, VRH_RN_NUM, VRH_RN_SUF | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R282 | VREPAIRDRVEXP.(VRDE_RN_PREF, VRDE_RN_NUM, VRDE_RN_SUF) | VREPAIRHDR.(VRH_RN_PREF, VRH_RN_NUM, VRH_RN_SUF) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R283 | VREPAIRHDR.(VRH_REPAIR_CODE) | VRREPAIRCODE.(VRRC_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R284 | VREPAIRHDR.(VRH_UNIT_EH_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R285 | VREPAIRHDR.(VRH_UNIT_TYPE_CTT_ID) | CT_TYPE.(CTT_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R286 | VREPAIRLABOR.(VRBL_RN_PREF, VRBL_RN_NUM, VRBL_RN_SUF) | VREPAIRHDR.(VRH_RN_PREF, VRH_RN_NUM, VRH_RN_SUF) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R288 | VREPAIROUTSIDE.(VRBO_RN_PREF, VRBO_RN_NUM, VRBO_RN_SUF) | VREPAIRHDR.(VRH_RN_PREF, VRH_RN_NUM, VRH_RN_SUF) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R291 | VREPAIRPARTS.(VRBP_RN_PREF, VRBP_RN_NUM, VRBP_RN_SUF) | VREPAIRHDR.(VRH_RN_PREF, VRH_RN_NUM, VRH_RN_SUF) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
