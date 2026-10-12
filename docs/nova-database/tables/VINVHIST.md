# VINVHIST

Maintenance and purchasing. Vinvhist records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (VH_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | VH_ENTRY_TYPE | INTEGER | No | — | RDB$5045 | — |
| 2 | VH_ID | BIGINT | Yes | — | RDB$5046 | — |
| 3 | VH_NEW_QTY | INTEGER | No | — | RDB$5047 | — |
| 4 | VH_NEW_VALUE | NUMERIC(13,2) | No | — | RDB$5048 | — |
| 5 | VH_ORIG_QTY | INTEGER | No | — | RDB$5049 | — |
| 6 | VH_ORIG_VALUE | NUMERIC(13,2) | No | — | RDB$5050 | — |
| 7 | VH_TIMESTAMP | TIMESTAMP | No | — | RDB$5051 | — |
| 8 | VH_UID | VARCHAR(8) | No | — | RDB$5052 | NONE/NONE |
| 9 | VH_VPN_ID | VARCHAR(6) | Yes | — | RDB$5053 | NONE/NONE |
| 10 | VH_VPN_LOC | VARCHAR(8) | Yes | — | RDB$5054 | NONE/NONE |
| 11 | VH_VPN_VENDOR | VARCHAR(20) | Yes | — | RDB$5055 | NONE/NONE |
| 12 | VH_VPOH_LOC | VARCHAR(6) | No | — | RDB$5056 | NONE/NONE |
| 13 | VH_VPOH_PO_NUM | INTEGER | No | — | RDB$5057 | — |
| 14 | VH_VPOH_VEND_ID | VARCHAR(8) | No | — | RDB$5058 | NONE/NONE |
| 15 | VH_VRH_RN_NUMBER | INTEGER | No | — | RDB$5059 | — |
| 16 | VH_VRH_RN_PREF | VARCHAR(1) | No | — | RDB$5060 | NONE/NONE |
| 17 | VH_VRH_RN_SUFF | VARCHAR(2) | No | — | RDB$5061 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| VINVHIST_IK | VH_VPN_ID, VH_VPN_LOC, VH_VPN_VENDOR | No | Yes | ASC | — |
| VINVHIST_PK | VH_ID | Yes | Yes | ASC | — |
| VINVHIST_PO | VH_VPOH_LOC, VH_VPOH_PO_NUM, VH_VPOH_VEND_ID | No | Yes | ASC | — |
| VINVHIST_RO | VH_VRH_RN_PREF, VH_VRH_RN_NUMBER, VH_VRH_RN_SUFF | No | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R280 | VINVHIST.(VH_VPN_LOC, VH_VPN_VENDOR, VH_VPN_ID) | VINVENTORY.(VIN_VPN_LOC, VIN_VPN_VENDOR, VIN_VPN_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
