# DRVSETL

Codes and configuration. Driver settlement entries.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DS_CO, DS_DRIV, DS_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DS_CO | VARCHAR(2) | Yes | — | RDB$3919 | NONE/NONE |
| 2 | DS_DRIV | BIGINT | Yes | — | RDB$3920 | — |
| 3 | DS_SEQ | BIGINT | Yes | — | RDB$3921 | — |
| 4 | DS_SHIPNUM | BIGINT | No | — | RDB$3922 | — |
| 5 | DS_PROLOC | VARCHAR(12) | No | — | RDB$3923 | NONE/NONE |
| 6 | DS_PRONUM | INTEGER | No | — | RDB$3924 | — |
| 7 | DS_PROSUF | VARCHAR(2) | No | — | RDB$3925 | NONE/NONE |
| 9 | DS_TYPE | VARCHAR(4) | No | — | RDB$3927 | NONE/NONE |
| 10 | DS_DESC1 | VARCHAR(80) | No | — | RDB$3928 | NONE/NONE |
| 11 | DS_DESC2 | VARCHAR(80) | No | — | RDB$3929 | NONE/NONE |
| 12 | DS_DESC3 | VARCHAR(80) | No | — | RDB$3930 | NONE/NONE |
| 13 | DS_REV | NUMERIC(16,2) | No | — | RDB$3931 | — |
| 14 | DS_REV_DT | DATE | No | — | RDB$3932 | — |
| 15 | DS_BOL | VARCHAR(80) | No | — | RDB$3933 | NONE/NONE |
| 16 | DS_KW | VARCHAR(8) | No | — | RDB$3934 | NONE/NONE |
| 17 | DS_AMT | NUMERIC(16,2) | No | — | RDB$3935 | — |
| 18 | DS_EFF_DT | DATE | No | — | RDB$3936 | — |
| 19 | DS_STATUS | VARCHAR(2) | No | — | RDB$3937 | NONE/NONE |
| 20 | DS_DT_PAID | DATE | No | — | RDB$3938 | — |
| 21 | DS_PROCESS_IND | VARCHAR(1) | No | — | RDB$3939 | NONE/NONE |
| 22 | DS_CHK_NUM | INTEGER | No | — | RDB$3940 | — |
| 23 | DS_CHK_DT | DATE | No | — | RDB$3941 | — |
| 24 | DS_SOURCE | VARCHAR(12) | No | — | RDB$3942 | NONE/NONE |
| 25 | DS_CONTRACT | VARCHAR(12) | No | — | RDB$3943 | NONE/NONE |
| 27 | DS_ORIG_DPCH_NUM | BIGINT | No | — | RDB$3945 | — |
| 28 | DS_DEST_DPCH_NUM | BIGINT | No | — | RDB$3946 | — |
| 29 | DS_WEIGHT | NUMERIC(15,3) | No | — | RDB$3947 | — |
| 30 | DS_TRACTOR | BIGINT | No | — | RDB$3948 | — |
| 31 | DS_TRAILER | BIGINT | No | — | RDB$3949 | — |
| 32 | DS_DELETE_DT | TIMESTAMP | No | — | RDB$3950 | — |
| 33 | DS_ENTER_DT | TIMESTAMP | No | — | RDB$3951 | — |
| 34 | DS_ENTER_USER | VARCHAR(12) | No | — | RDB$3952 | NONE/NONE |
| 35 | DS_CHG_DT | TIMESTAMP | No | — | RDB$3953 | — |
| 36 | DS_CHG_USER | VARCHAR(12) | No | — | RDB$3954 | NONE/NONE |
| 37 | DS_AUTH_INV_ID | VARCHAR(80) | No | — | RDB$3955 | NONE/NONE |
| 38 | DS_MILES | NUMERIC(9,1) | No | — | RDB$4170 | — |
| 39 | DS_DAV_SEQ | BIGINT | No | — | RDB$4213 | — |
| 40 | DS_MFST_NUM | BIGINT | No | — | RDB$6475 | — |
| 41 | DS_VMRS_REPAIR_PREF | VARCHAR(1) | No | — | RDB$6484 | NONE/NONE |
| 42 | DS_VMRS_REPAIR_NUM | INTEGER | No | — | RDB$6485 | — |
| 43 | DS_VMRS_REPAIR_SUF | VARCHAR(2) | No | — | RDB$6486 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| DS_PRIME_KEY | DS_CO, DS_DRIV, DS_SEQ | Yes | Yes | ASC | — |
| IDX_DRVSETL_DPCHNUM | DS_ORIG_DPCH_NUM, DS_DRIV | No | Yes | ASC | — |
| IDX_DRVSETL_EFFDT | DS_CO, DS_EFF_DT, DS_DRIV, DS_SEQ | No | Yes | ASC | — |
| IDX_DRVSETL_PAIDDT | DS_CO, DS_DT_PAID, DS_DRIV, DS_SEQ | No | Yes | ASC | — |
| IDX_DRVSETL_SHIPNUM | DS_SHIPNUM, DS_DRIV | No | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R082 | DRVSETL.(DS_TYPE) | DSPAYTYPES.(DST_TYPE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`DRVSETL_BEFORE_INSERT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
