# VREPAIRPARTS

Maintenance and purchasing. Vrepairparts records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (VRBP_RN_PREF, VRBP_RN_NUM, VRBP_RN_SUF, VRBP_REC_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | VRBP_RN_PREF | VARCHAR(1) | Yes | — | RDB$4953 | NONE/NONE |
| 2 | VRBP_RN_NUM | INTEGER | Yes | — | RDB$4954 | — |
| 3 | VRBP_RN_SUF | VARCHAR(2) | Yes | — | RDB$4955 | NONE/NONE |
| 4 | VRBP_REC_SEQ | INTEGER | Yes | — | RDB$4956 | — |
| 7 | VRBP_PART | VARCHAR(3) | No | — | RDB$4959 | NONE/NONE |
| 8 | VRBP_PN_TYPE | VARCHAR(1) | No | — | RDB$4960 | NONE/NONE |
| 9 | VRBP_PN_ID | VARCHAR(8) | No | — | RDB$4961 | NONE/NONE |
| 10 | VRBP_PN_NUM | VARCHAR(20) | No | — | RDB$4962 | NONE/NONE |
| 11 | VRBP_PN_DESCRP | VARCHAR(27) | No | — | RDB$4963 | NONE/NONE |
| 12 | VRBP_QTY | INTEGER | No | — | RDB$4964 | — |
| 13 | VRBP_UPRICE | NUMERIC(11,2) | No | — | RDB$4965 | — |
| 14 | VRBP_FAIL_CODE | VARCHAR(2) | No | — | RDB$4966 | NONE/NONE |
| 15 | VRBP_IK_LOC | VARCHAR(12) | No | — | RDB$4967 | NONE/NONE |
| 16 | VRBP_IK_VEND | VARCHAR(8) | No | — | RDB$4968 | NONE/NONE |
| 17 | VRBP_IK_PART | VARCHAR(20) | No | — | RDB$4969 | NONE/NONE |
| 18 | VRBP_CONDITION | VARCHAR(1) | No | — | RDB$4970 | NONE/NONE |
| 19 | VRBP_RESPONSIBILITY | VARCHAR(1) | No | — | RDB$4971 | NONE/NONE |
| 20 | VRBP_RAIL_ROAD_MATERIAL | VARCHAR(1) | No | — | RDB$4972 | NONE/NONE |
| 21 | VRBP_VEH_LOC_CODE | VARCHAR(3) | No | — | RDB$4973 | NONE/NONE |
| 22 | VRBP_CURRENT_AVERAGE_COST | NUMERIC(11,2) | No | — | RDB$5621 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| VREPAIRPARTS_KEY | VRBP_RN_PREF, VRBP_RN_NUM, VRBP_RN_SUF, VRBP_REC_SEQ | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R290 | VREPAIRPARTS.(VRBP_IK_LOC, VRBP_IK_VEND, VRBP_IK_PART) | VINVENTORY.(VIN_VPN_LOC, VIN_VPN_VENDOR, VIN_VPN_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R291 | VREPAIRPARTS.(VRBP_RN_PREF, VRBP_RN_NUM, VRBP_RN_SUF) | VREPAIRHDR.(VRH_RN_PREF, VRH_RN_NUM, VRH_RN_SUF) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
