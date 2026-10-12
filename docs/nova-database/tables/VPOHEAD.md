# VPOHEAD

Maintenance and purchasing. Vpohead records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (VPOH_LOC, VPOH_PO_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | VPOH_LOC | VARCHAR(6) | Yes | — | RDB$4710 | NONE/NONE |
| 2 | VPOH_PO_NUM | INTEGER | Yes | — | RDB$4711 | — |
| 3 | VPOH_VEND_ID | VARCHAR(8) | No | — | RDB$4712 | NONE/NONE |
| 4 | VPOH_VEND_NUM | INTEGER | No | — | RDB$4713 | — |
| 5 | VPOH_VEND_NAME | VARCHAR(32) | No | — | RDB$4714 | NONE/NONE |
| 6 | VPOH_VEND_ADDR1 | VARCHAR(32) | No | — | RDB$4715 | NONE/NONE |
| 7 | VPOH_VEND_ADDR2 | VARCHAR(32) | No | — | RDB$4716 | NONE/NONE |
| 8 | VPOH_VEND_CITY | VARCHAR(22) | No | — | RDB$4717 | NONE/NONE |
| 9 | VPOH_VEND_ST | VARCHAR(2) | No | — | RDB$4718 | NONE/NONE |
| 10 | VPOH_VEND_ZIP | VARCHAR(10) | No | — | RDB$4719 | NONE/NONE |
| 11 | VPOH_BILLTO | VARCHAR(8) | No | — | RDB$4720 | NONE/NONE |
| 12 | VPOH_SHIPTO_LOC | VARCHAR(8) | No | — | RDB$4721 | NONE/NONE |
| 13 | VPOH_S_NAME | VARCHAR(32) | No | — | RDB$4722 | NONE/NONE |
| 14 | VPOH_S_ADDR1 | VARCHAR(32) | No | — | RDB$4723 | NONE/NONE |
| 15 | VPOH_S_ADDR2 | VARCHAR(32) | No | — | RDB$4724 | NONE/NONE |
| 16 | VPOH_SA3_CITY | VARCHAR(22) | No | — | RDB$4725 | NONE/NONE |
| 17 | VPOH_SA3_ST | VARCHAR(2) | No | — | RDB$4726 | NONE/NONE |
| 18 | VPOH_SA3_ZIP | VARCHAR(10) | No | — | RDB$4727 | NONE/NONE |
| 19 | VPOH_HOW_SHIP | VARCHAR(5) | No | — | RDB$4728 | NONE/NONE |
| 20 | VPOH_SHIP_COST | NUMERIC(9,2) | No | — | RDB$4729 | — |
| 21 | VPOH_PO_DATE | DATE | No | — | RDB$4730 | — |
| 22 | VPOH_PO_COMPLETE_DT | DATE | No | — | RDB$4731 | — |
| 23 | VPOH_NEEDED_DT | DATE | No | — | RDB$4732 | — |
| 24 | VPOH_ENT_DT | TIMESTAMP | No | — | RDB$4733 | — |
| 25 | VPOH_ENT_INIT | VARCHAR(4) | No | — | RDB$4734 | NONE/NONE |
| 26 | VPOH_CHG_DT | TIMESTAMP | No | — | RDB$4735 | — |
| 27 | VPOH_CHG_INIT | VARCHAR(4) | No | — | RDB$4736 | NONE/NONE |
| 28 | VPOH_TERMS | VARCHAR(10) | No | — | RDB$4737 | NONE/NONE |
| 29 | VPOH_AUTH_BY | VARCHAR(4) | No | — | RDB$4738 | NONE/NONE |
| 30 | VPOH_ACKNOWL_NUM | VARCHAR(12) | No | — | RDB$4739 | NONE/NONE |
| 31 | VPOH_CLOSE_DT | TIMESTAMP | No | — | RDB$4740 | — |
| 32 | VPOH_CLOSE_INIT | VARCHAR(4) | No | — | RDB$4741 | NONE/NONE |
| 33 | VPOH_APINV_KEY | VARCHAR(30) | No | — | RDB$4742 | NONE/NONE |
| 34 | VPOH_NOTES | BLOB SUB_TYPE 1 | No | — | RDB$4743 | NONE/NONE |
| 35 | ISDELETED | CHAR(1) | No | — | RDB$4975 | NONE/NONE |
| 36 | VPOH_TAX | NUMERIC(9,2) | No | — | RDB$5565 | — |
| 37 | VPOH_OTHER | NUMERIC(9,2) | No | — | RDB$5566 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| VPOH_KEY | VPOH_LOC, VPOH_PO_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R281 | VPOBODY.(VPOB_LOC, VPOB_PO_NUM) | VPOHEAD.(VPOH_LOC, VPOH_PO_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
