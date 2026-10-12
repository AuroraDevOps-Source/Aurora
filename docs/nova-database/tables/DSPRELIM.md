# DSPRELIM

Drivers and settlement. Preliminary driver settlement entries.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DP_CO, DP_DRIV, DP_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DP_CO | VARCHAR(2) | Yes | — | RDB$3956 | NONE/NONE |
| 2 | DP_DRIV | BIGINT | Yes | — | RDB$3957 | — |
| 3 | DP_SEQ | BIGINT | Yes | — | RDB$3958 | — |
| 4 | DP_SHIPNUM | BIGINT | No | — | RDB$3959 | — |
| 5 | DP_PROLOC | VARCHAR(12) | No | — | RDB$3960 | NONE/NONE |
| 6 | DP_PRONUM | INTEGER | No | — | RDB$3961 | — |
| 7 | DP_PROSUF | VARCHAR(2) | No | — | RDB$3962 | NONE/NONE |
| 9 | DP_TYPE | VARCHAR(4) | No | — | RDB$3964 | NONE/NONE |
| 10 | DP_DESC1 | VARCHAR(80) | No | — | RDB$3965 | NONE/NONE |
| 11 | DP_DESC2 | VARCHAR(80) | No | — | RDB$3966 | NONE/NONE |
| 12 | DP_DESC3 | VARCHAR(80) | No | — | RDB$3967 | NONE/NONE |
| 13 | DP_REV | NUMERIC(16,2) | No | — | RDB$3968 | — |
| 14 | DP_REV_DT | DATE | No | — | RDB$3969 | — |
| 15 | DP_BOL | VARCHAR(80) | No | — | RDB$3970 | NONE/NONE |
| 16 | DP_KW | VARCHAR(8) | No | — | RDB$3971 | NONE/NONE |
| 17 | DP_AMT | NUMERIC(16,2) | No | — | RDB$3972 | — |
| 18 | DP_EFF_DT | DATE | No | — | RDB$3973 | — |
| 19 | DP_STATUS | VARCHAR(2) | No | — | RDB$3974 | NONE/NONE |
| 20 | DP_DT_PAID | DATE | No | — | RDB$3975 | — |
| 21 | DP_PROCESS_IND | VARCHAR(1) | No | — | RDB$3976 | NONE/NONE |
| 22 | DP_CHK_NUM | INTEGER | No | — | RDB$3977 | — |
| 23 | DP_CHK_DT | DATE | No | — | RDB$3978 | — |
| 24 | DP_SOURCE | VARCHAR(12) | No | — | RDB$3979 | NONE/NONE |
| 25 | DP_CONTRACT | VARCHAR(12) | No | — | RDB$3980 | NONE/NONE |
| 27 | DP_ORIG_DPCH_NUM | BIGINT | No | — | RDB$3982 | — |
| 28 | DP_DEST_DPCH_NUM | BIGINT | No | — | RDB$3983 | — |
| 29 | DP_WEIGHT | NUMERIC(15,3) | No | — | RDB$3984 | — |
| 30 | DP_TRACTOR | BIGINT | No | — | RDB$3985 | — |
| 31 | DP_TRAILER | BIGINT | No | — | RDB$3986 | — |
| 32 | DP_DELETE_DT | TIMESTAMP | No | — | RDB$3987 | — |
| 33 | DP_ENTER_DT | TIMESTAMP | No | — | RDB$3988 | — |
| 34 | DP_ENTER_USER | VARCHAR(12) | No | — | RDB$3989 | NONE/NONE |
| 35 | DP_CHG_DT | TIMESTAMP | No | — | RDB$3990 | — |
| 36 | DP_CHG_USER | VARCHAR(12) | No | — | RDB$3991 | NONE/NONE |
| 37 | DP_AUTH_INV_ID | VARCHAR(80) | No | — | RDB$3992 | NONE/NONE |
| 38 | DP_MILES | NUMERIC(9,1) | No | — | RDB$4169 | — |
| 39 | DP_DAV_SEQ | BIGINT | No | — | RDB$4212 | — |
| 40 | DP_MFST_NUM | BIGINT | No | — | RDB$6476 | — |
| 41 | DP_VMRS_REPAIR_PREF | VARCHAR(1) | No | — | RDB$6481 | NONE/NONE |
| 42 | DP_VMRS_REPAIR_NUM | INTEGER | No | — | RDB$6482 | — |
| 43 | DP_VMRS_REPAIR_SUF | VARCHAR(2) | No | — | RDB$6483 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| DP_PRIME_KEY | DP_CO, DP_DRIV, DP_SEQ | Yes | Yes | ASC | — |
| IDX_DSPRELIM_EFFDT | DP_CO, DP_EFF_DT, DP_DRIV, DP_SEQ | No | Yes | ASC | — |
| IDX_DSPRELIM_PAIDDT | DP_CO, DP_DT_PAID, DP_DRIV, DP_SEQ | No | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R088 | DSPRELIM.(DP_TYPE) | DSPAYTYPES.(DST_TYPE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`DSPRELIM_INSERT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
