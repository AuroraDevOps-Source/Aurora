# TARIFF_CONTROL

Pricing and tariffs. Tariff Control records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (TC_TARIFF_ID, TC_EFF_DT). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | TC_TARIFF_ID | VARCHAR(6) | Yes | — | RDB$3065 | NONE/NONE |
| 2 | TC_EFF_DT | DATE | Yes | — | RDB$3066 | — |
| 3 | TC_EXP_DT | DATE | Yes | — | RDB$3230 | — |
| 4 | TC_TARIFF_TYPE | VARCHAR(3) | Yes | — | RDB$3067 | NONE/NONE |
| 5 | TC_STD_WGT | VARCHAR(1) | No | — | RDB$3068 | NONE/NONE |
| 6 | TC_STD_CLASSES | VARCHAR(1) | No | — | RDB$3069 | NONE/NONE |
| 7 | TC_WGT_BRK_1 | INTEGER | No | — | RDB$3070 | — |
| 8 | TC_WGT_BRK_2 | NUMERIC(9,0) | No | — | RDB$3071 | — |
| 9 | TC_WGT_BRK_3 | NUMERIC(9,0) | No | — | RDB$3072 | — |
| 10 | TC_WGT_BRK_4 | NUMERIC(9,0) | No | — | RDB$3073 | — |
| 11 | TC_WGT_BRK_5 | NUMERIC(9,0) | No | — | RDB$3074 | — |
| 12 | TC_WGT_BRK_6 | NUMERIC(9,0) | No | — | RDB$3075 | — |
| 13 | TC_WGT_BRK_7 | NUMERIC(9,0) | No | — | RDB$3076 | — |
| 14 | TC_WGT_BRK_8 | NUMERIC(9,0) | No | — | RDB$3077 | — |
| 15 | TC_WGT_BRK_9 | NUMERIC(9,0) | No | — | RDB$3078 | — |
| 16 | TC_WGT_BRK_10 | NUMERIC(9,0) | No | — | RDB$3079 | — |
| 17 | TC_WGT_BRK_11 | NUMERIC(9,0) | No | — | RDB$3080 | — |
| 18 | TC_WGT_BRK_12 | NUMERIC(9,0) | No | — | RDB$3081 | — |
| 19 | TC_WGT_BRK_13 | INTEGER | No | — | RDB$5590 | — |
| 20 | TC_WGT_BRK_14 | INTEGER | No | — | RDB$5591 | — |
| 21 | TC_WGT_BRK_15 | INTEGER | No | — | RDB$5592 | — |
| 22 | TC_WGT_BRK_16 | INTEGER | No | — | RDB$5593 | — |
| 23 | TC_WGT_BRK_17 | INTEGER | No | — | RDB$5594 | — |
| 24 | TC_WGT_BRK_18 | INTEGER | No | — | RDB$5595 | — |
| 25 | TC_WGT_BRK_19 | INTEGER | No | — | RDB$5596 | — |
| 26 | TC_WGT_BRK_20 | INTEGER | No | — | RDB$5597 | — |
| 27 | TC_CLASS_1 | VARCHAR(4) | No | — | RDB$3082 | NONE/NONE |
| 28 | TC_CLASS_2 | VARCHAR(4) | No | — | RDB$3083 | NONE/NONE |
| 29 | TC_CLASS_3 | VARCHAR(4) | No | — | RDB$3084 | NONE/NONE |
| 30 | TC_CLASS_4 | VARCHAR(4) | No | — | RDB$3085 | NONE/NONE |
| 31 | TC_CLASS_5 | VARCHAR(4) | No | — | RDB$3086 | NONE/NONE |
| 32 | TC_CLASS_6 | VARCHAR(4) | No | — | RDB$3087 | NONE/NONE |
| 33 | TC_CLASS_7 | VARCHAR(4) | No | — | RDB$3088 | NONE/NONE |
| 34 | TC_CLASS_8 | VARCHAR(4) | No | — | RDB$3089 | NONE/NONE |
| 35 | TC_CLASS_9 | VARCHAR(4) | No | — | RDB$3090 | NONE/NONE |
| 36 | TC_CLASS_10 | VARCHAR(4) | No | — | RDB$3091 | NONE/NONE |
| 37 | TC_CLASS_11 | VARCHAR(4) | No | — | RDB$3092 | NONE/NONE |
| 38 | TC_CLASS_12 | VARCHAR(4) | No | — | RDB$3093 | NONE/NONE |
| 39 | TC_CLASS_13 | VARCHAR(4) | No | — | RDB$3094 | NONE/NONE |
| 40 | TC_CLASS_14 | VARCHAR(4) | No | — | RDB$3095 | NONE/NONE |
| 41 | TC_CLASS_15 | VARCHAR(4) | No | — | RDB$3096 | NONE/NONE |
| 42 | TC_CLASS_16 | VARCHAR(4) | No | — | RDB$3097 | NONE/NONE |
| 43 | TC_CLASS_17 | VARCHAR(4) | No | — | RDB$6549 | NONE/NONE |
| 44 | TC_CLASS_18 | VARCHAR(4) | No | — | RDB$6550 | NONE/NONE |
| 45 | TC_CLASS_19 | VARCHAR(4) | No | — | RDB$6551 | NONE/NONE |
| 46 | TC_CLASS_20 | VARCHAR(4) | No | — | RDB$6552 | NONE/NONE |
| 47 | TC_CLASS_21 | VARCHAR(4) | No | — | RDB$6553 | NONE/NONE |
| 48 | TC_CLASS_22 | VARCHAR(4) | No | — | RDB$6554 | NONE/NONE |
| 49 | TC_CLASS_23 | VARCHAR(4) | No | — | RDB$6555 | NONE/NONE |
| 50 | TC_CLASS_24 | VARCHAR(4) | No | — | RDB$6556 | NONE/NONE |
| 51 | TC_AS_WGT_RULE | VARCHAR(1) | No | — | RDB$3098 | NONE/NONE |
| 52 | TC_1ST_WGT_BRK | VARCHAR(1) | No | — | RDB$3099 | NONE/NONE |
| 53 | TC_MILES_AS_RATE_BASE | VARCHAR(1) | No | — | RDB$3100 | NONE/NONE |
| 54 | TC_PCS_TARIFF | VARCHAR(1) | No | — | RDB$3101 | NONE/NONE |
| 55 | TC_NO_ZIP_CODE_RULE | VARCHAR(1) | No | — | RDB$3102 | NONE/NONE |
| 56 | TC_MAX_CHARGE_RULE | VARCHAR(1) | No | — | RDB$3103 | NONE/NONE |
| 57 | TC_MAX_CHARGE_WGT_BRKS | VARCHAR(1) | No | — | RDB$3104 | NONE/NONE |
| 58 | TC_WGT_SCALE_PCS_CALC | VARCHAR(1) | No | — | RDB$3105 | NONE/NONE |
| 59 | TC_ORIG_KEY | VARCHAR(2) | No | — | RDB$4584 | NONE/NONE |
| 60 | TC_DEST_KEY | VARCHAR(2) | No | — | RDB$4585 | NONE/NONE |
| 61 | TC_RXL_USER | VARCHAR(50) | No | — | RDB$5311 | NONE/NONE |
| 62 | TC_RXL_PASS | VARCHAR(20) | No | — | RDB$5312 | NONE/NONE |
| 63 | TC_RXL_AUTH | VARCHAR(25) | No | — | RDB$5313 | NONE/NONE |
| 64 | TC_RXL_URL | VARCHAR(100) | No | — | RDB$5314 | NONE/NONE |
| 65 | TC_RXL_TAR_ID | VARCHAR(8) | No | — | RDB$5315 | NONE/NONE |
| 66 | TC_USE_CUBE_RULE | VARCHAR(1) | No | — | RDB$5354 | NONE/NONE |
| 67 | TC_COMMON_PNTS | CHAR(1) | No | — | RDB$5841 | NONE/NONE |
| 68 | TC_KILOS | CHAR(1) | No | — | RDB$6380 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_TARIFF_CONTROL_KEY | TC_TARIFF_ID, TC_EFF_DT | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R260 | TARIFF_ADJUSTMENTS.(TA_TARIFF_ID, TA_EFF_DT) | TARIFF_CONTROL.(TC_TARIFF_ID, TC_EFF_DT) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R261 | TARIFF_BASES.(TB_TARIFF_ID, TB_EFF_DT) | TARIFF_CONTROL.(TC_TARIFF_ID, TC_EFF_DT) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R262 | TARIFF_CONTROL.(TC_TARIFF_ID) | TARIFF_MASTER.(TM_TARIFF_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R263 | TARIFF_POINTS.(TP_TARIFF_ID, TP_EFF_DT) | TARIFF_CONTROL.(TC_TARIFF_ID, TC_EFF_DT) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R264 | TARIFF_POI_RATES.(TPR_TARIFF_ID, TPR_EFF_DT) | TARIFF_CONTROL.(TC_TARIFF_ID, TC_EFF_DT) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R265 | TARIFF_RATES.(TR_TARIFF_ID, TR_EFF_DT) | TARIFF_CONTROL.(TC_TARIFF_ID, TC_EFF_DT) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
