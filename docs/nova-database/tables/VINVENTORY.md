# VINVENTORY

Maintenance and purchasing. Vinventory records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (VIN_VPN_LOC, VIN_VPN_VENDOR, VIN_VPN_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | VIN_VPN_LOC | VARCHAR(6) | Yes | — | RDB$4658 | NONE/NONE |
| 2 | VIN_VPN_VENDOR | VARCHAR(8) | Yes | — | RDB$4659 | NONE/NONE |
| 3 | VIN_VPN_ID | VARCHAR(20) | Yes | — | RDB$4660 | NONE/NONE |
| 7 | VIN_MPN_MFG | VARCHAR(8) | No | — | RDB$4664 | NONE/NONE |
| 8 | VIN_MPN_ID | VARCHAR(20) | No | — | RDB$4665 | NONE/NONE |
| 9 | VIN_SORTNAME | VARCHAR(12) | No | — | RDB$4666 | NONE/NONE |
| 10 | VIN_DESC1 | VARCHAR(32) | No | — | RDB$4667 | NONE/NONE |
| 11 | VIN_DESC2 | VARCHAR(32) | No | — | RDB$4668 | NONE/NONE |
| 12 | VIN_REORDER_POINT | INTEGER | No | — | RDB$4669 | — |
| 13 | VIN_ORDER_QTY | INTEGER | No | — | RDB$4670 | — |
| 14 | VIN_MAX_QTY | INTEGER | No | — | RDB$4671 | — |
| 15 | VIN_PURCH_UNITS | VARCHAR(8) | No | — | RDB$4672 | NONE/NONE |
| 16 | VIN_ISSUE_UNITS | VARCHAR(8) | No | — | RDB$4673 | NONE/NONE |
| 17 | VIN_COMP_AR_KW | VARCHAR(4) | No | — | RDB$4674 | NONE/NONE |
| 18 | VIN_OUTS_AR_KW | VARCHAR(4) | No | — | RDB$4675 | NONE/NONE |
| 19 | VIN_ESTAB_COST | NUMERIC(9,2) | No | — | RDB$4676 | — |
| 20 | VIN_ON_ORDER | INTEGER | No | — | RDB$4677 | — |
| 21 | VIN_ON_HAND | INTEGER | No | — | RDB$4678 | — |
| 22 | VIN_WARR_DAYS | INTEGER | No | — | RDB$4679 | — |
| 23 | VIN_WARR_MILES | INTEGER | No | — | RDB$4680 | — |
| 24 | VIN_TRACK_SERIAL_NO | VARCHAR(1) | No | — | RDB$4681 | NONE/NONE |
| 25 | VIN_ADJ_COUNT | INTEGER | No | — | RDB$4682 | — |
| 26 | VIN_ADJ_NET | INTEGER | No | — | RDB$4683 | — |
| 27 | VIN_ROOM | VARCHAR(4) | No | — | RDB$4684 | NONE/NONE |
| 28 | VIN_LEVEL | VARCHAR(4) | No | — | RDB$4685 | NONE/NONE |
| 29 | VIN_ISLE | VARCHAR(4) | No | — | RDB$4686 | NONE/NONE |
| 30 | VIN_ROW | VARCHAR(4) | No | — | RDB$4687 | NONE/NONE |
| 31 | VIN_BIN | VARCHAR(4) | No | — | RDB$4688 | NONE/NONE |
| 32 | VIN_INHOUSE_MARKUP | NUMERIC(10,4) | No | — | RDB$4689 | — |
| 33 | VIN_OUTSIDE_MARKUP | NUMERIC(10,4) | No | — | RDB$4690 | — |
| 34 | VIN_FLEET_1 | VARCHAR(8) | No | — | RDB$4691 | NONE/NONE |
| 35 | VIN_FLEET_2 | VARCHAR(8) | No | — | RDB$4692 | NONE/NONE |
| 36 | VIN_FLEET_3 | VARCHAR(8) | No | — | RDB$4693 | NONE/NONE |
| 37 | VIN_FLEET_4 | VARCHAR(8) | No | — | RDB$4694 | NONE/NONE |
| 38 | VIN_FLEET_5 | VARCHAR(8) | No | — | RDB$4695 | NONE/NONE |
| 39 | VIN_FLEET_6 | VARCHAR(8) | No | — | RDB$4696 | NONE/NONE |
| 40 | VIN_GENERIC | VARCHAR(1) | No | — | RDB$4697 | NONE/NONE |
| 41 | VIN_STD_TM1 | NUMERIC(8,2) | No | — | RDB$4698 | — |
| 42 | VIN_STD_TM2 | NUMERIC(8,2) | No | — | RDB$4699 | — |
| 43 | VIN_STD_TM3 | NUMERIC(8,2) | No | — | RDB$4700 | — |
| 44 | VIN_STD_TM4 | NUMERIC(8,2) | No | — | RDB$4701 | — |
| 45 | VIN_NON_STOCK_IND | VARCHAR(1) | No | — | RDB$4702 | NONE/NONE |
| 46 | VIN_ADD_DT | VARCHAR(6) | No | — | RDB$4703 | NONE/NONE |
| 47 | VIN_ADD_USER | VARCHAR(8) | No | — | RDB$4704 | NONE/NONE |
| 48 | VIN_JOB_CODE | VARCHAR(4) | No | — | RDB$4705 | NONE/NONE |
| 49 | VIN_RUN_BAL_COST | NUMERIC(13,2) | No | — | RDB$4706 | — |
| 50 | VIN_AVG_COST | NUMERIC(18,6) | No | — | RDB$4707 | — |
| 51 | VIN_USED_YTD | INTEGER | No | — | RDB$4708 | — |
| 52 | VIN_USED_LY | INTEGER | No | — | RDB$4709 | — |
| 53 | VIN_LAST_WRITTEN | TIMESTAMP | No | — | RDB$5063 | — |
| 54 | VIN_KIT_CODE | VARCHAR(14) | No | — | D_UNIT_ID | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| VINVENTORY_MFG_KEY | VIN_VPN_LOC, VIN_MPN_MFG, VIN_MPN_ID | No | Yes | ASC | — |
| VINVENTORY_NAME_KEY | VIN_SORTNAME | No | Yes | ASC | — |
| VINVENTORY_PRIMARY_KEY | VIN_VPN_LOC, VIN_VPN_VENDOR, VIN_VPN_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R280 | VINVHIST.(VH_VPN_LOC, VH_VPN_VENDOR, VH_VPN_ID) | VINVENTORY.(VIN_VPN_LOC, VIN_VPN_VENDOR, VIN_VPN_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R290 | VREPAIRPARTS.(VRBP_IK_LOC, VRBP_IK_VEND, VRBP_IK_PART) | VINVENTORY.(VIN_VPN_LOC, VIN_VPN_VENDOR, VIN_VPN_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
