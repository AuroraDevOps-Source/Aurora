# CLAIM_HEADER

Claims and receipts. Claim Header records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CLH_YR_KEY, CLH_NUM_KEY). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CLH_YR_KEY | VARCHAR(2) | Yes | — | RDB$5717 | NONE/NONE |
| 2 | CLH_NUM_KEY | INTEGER | Yes | — | RDB$5718 | — |
| 3 | CLH_CLAIM_TYPE | VARCHAR(2) | No | — | RDB$5719 | NONE/NONE |
| 4 | CLH_STATUS | VARCHAR(2) | No | — | RDB$5720 | NONE/NONE |
| 5 | CLH_SHIP_NUM | BIGINT | No | — | RDB$5721 | — |
| 6 | CLH_PRO_LOC | VARCHAR(6) | No | — | RDB$5722 | NONE/NONE |
| 7 | CLH_PRO_NUM | INTEGER | No | — | RDB$5723 | — |
| 8 | CLH_PRO_SUF | VARCHAR(2) | No | — | RDB$5724 | NONE/NONE |
| 9 | CLH_RECEIVE_DATE | DATE | No | — | RDB$5725 | — |
| 10 | CLH_OVER | INTEGER | No | — | RDB$5726 | — |
| 11 | CLH_SHORT | INTEGER | No | — | RDB$5727 | — |
| 12 | CLH_DAMAGE | INTEGER | No | — | RDB$5728 | — |
| 13 | CLH_COMMODITY | VARCHAR(24) | No | — | RDB$5729 | NONE/NONE |
| 14 | CLH_CLAIMANT_ID | VARCHAR(12) | No | — | RDB$5730 | NONE/NONE |
| 15 | CLH_CLAIMANT_NAME | VARCHAR(36) | No | — | RDB$5731 | NONE/NONE |
| 16 | CLH_CLAIMANT_ADR1 | VARCHAR(32) | No | — | RDB$5732 | NONE/NONE |
| 17 | CLH_CLAIMANT_ADR2 | VARCHAR(32) | No | — | RDB$5733 | NONE/NONE |
| 18 | CLH_CLAIMANT_CITY | VARCHAR(24) | No | — | RDB$5734 | NONE/NONE |
| 19 | CLH_CLAIMANT_ST | VARCHAR(2) | No | — | RDB$5735 | NONE/NONE |
| 20 | CLH_CLAIMANT_ZIP | VARCHAR(9) | No | — | RDB$5736 | NONE/NONE |
| 21 | CLH_CLAIMANT_NBR | VARCHAR(16) | No | — | RDB$5737 | NONE/NONE |
| 22 | CLH_CLAIMANT_DATE | DATE | No | — | RDB$5738 | — |
| 23 | CLH_CLAIMANT_AMT | NUMERIC(10,2) | No | — | RDB$5739 | — |
| 24 | CLH_CLAIMANT_PHONE | VARCHAR(32) | No | — | RDB$5740 | NONE/NONE |
| 25 | CLH_CLAIMANT_FAX | VARCHAR(32) | No | — | RDB$5741 | NONE/NONE |
| 26 | CLH_CLAIMANT_EMAIL | VARCHAR(150) | No | — | RDB$5742 | NONE/NONE |
| 27 | CLH_CLAIM_NOTES | BLOB SUB_TYPE 0 | No | — | RDB$5743 | — |
| 28 | CLH_OVER_CHARGE | VARCHAR(1) | No | — | RDB$5744 | NONE/NONE |
| 29 | CLH_PAID_AMT | NUMERIC(10,2) | No | — | RDB$5745 | — |
| 30 | CLH_PAID_DT | DATE | No | — | RDB$5746 | — |
| 31 | CLH_PAID_CHK_NO | VARCHAR(12) | No | — | RDB$5747 | NONE/NONE |
| 32 | CLH_DENIED_AMT | NUMERIC(10,2) | No | — | RDB$5748 | — |
| 33 | CLH_DENIED_DT | DATE | No | — | RDB$5749 | — |
| 34 | CLH_DENIED_REASON | BLOB SUB_TYPE 0 | No | — | RDB$5750 | — |
| 35 | CLH_RECOV_AMT | NUMERIC(10,2) | No | — | RDB$5751 | — |
| 36 | CLH_RECOV_CODE | VARCHAR(6) | No | — | RDB$5752 | NONE/NONE |
| 37 | CLH_ACK_DATE | DATE | No | — | RDB$5753 | — |
| 38 | CLH_ACK_BY | VARCHAR(36) | No | — | RDB$5754 | NONE/NONE |
| 39 | CLH_CLEARED | VARCHAR(1) | No | — | RDB$5755 | NONE/NONE |
| 40 | CLH_PU_DRIVER | BIGINT | No | — | RDB$5756 | — |
| 41 | CLH_DLV_DRIVER | BIGINT | No | — | RDB$5757 | — |
| 42 | CLH_TRAC | BIGINT | No | — | RDB$5758 | — |
| 43 | CLH_INSPECT_NOTES | BLOB SUB_TYPE 0 | No | — | RDB$5759 | — |
| 44 | CLH_SETTLE_BY_DT | DATE | No | — | RDB$5760 | — |
| 45 | CLH_ENTER_DT | DATE | No | — | RDB$5761 | — |
| 46 | CLH_ENTER_USER | VARCHAR(20) | No | — | RDB$5762 | NONE/NONE |
| 47 | CLH_CHG_DT | DATE | No | — | RDB$5763 | — |
| 48 | CLH_CHG_USER | VARCHAR(20) | No | — | RDB$5764 | NONE/NONE |
| 49 | CLH_INSPECTOR | VARCHAR(36) | No | — | RDB$5765 | NONE/NONE |
| 50 | CLH_INSPECT_DT | DATE | No | — | RDB$5766 | — |
| 54 | CLH_OTH_REF | VARCHAR(32) | No | — | RDB$5770 | NONE/NONE |
| 55 | CLH_CLEARED_DT | DATE | No | — | RDB$5771 | — |
| 56 | CLH_PU_TLR | BIGINT | No | — | RDB$5772 | — |
| 57 | CLH_DLV_TLR | BIGINT | No | — | RDB$5773 | — |
| 58 | CLH_RECOV_FROM | VARCHAR(256) | No | — | RDB$5790 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CLAIM_HDR_CLM_DT_KEY | CLH_CLAIMANT_DATE | No | Yes | ASC | — |
| CLAIM_HDR_SHIP_NUM_KEY | CLH_SHIP_NUM | No | Yes | ASC | — |
| CLAIM_HEADER_KEY | CLH_YR_KEY, CLH_NUM_KEY | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R037 | CLAIM_HEADER.(CLH_CLAIMANT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R038 | CLAIM_HEADER.(CLH_STATUS) | CT_CLAIM_STATUS.(CTCS_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R039 | CLAIM_OTHER_PARTIES.(CLOP_YR_KEY, CLOP_NUM_KEY) | CLAIM_HEADER.(CLH_YR_KEY, CLH_NUM_KEY) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R040 | CLAIM_PAYINFO.(CLPI_YR_KEY, CLPI_NUM_KEY) | CLAIM_HEADER.(CLH_YR_KEY, CLH_NUM_KEY) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
