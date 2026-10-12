# VREPAIRLABOR

Maintenance and purchasing. Vrepairlabor records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (VRBL_RN_PREF, VRBL_RN_NUM, VRBL_RN_SUF, VRBL_REC_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | VRBL_RN_PREF | VARCHAR(1) | Yes | — | RDB$4923 | NONE/NONE |
| 2 | VRBL_RN_NUM | INTEGER | Yes | — | RDB$4924 | — |
| 3 | VRBL_RN_SUF | VARCHAR(2) | Yes | — | RDB$4925 | NONE/NONE |
| 4 | VRBL_REC_SEQ | INTEGER | Yes | — | RDB$4926 | — |
| 5 | VRBL_DATE | DATE | No | — | RDB$4927 | — |
| 6 | VRBL_EMP_NO | VARCHAR(16) | No | — | RDB$4928 | NONE/NONE |
| 9 | VRBL_WORK_ACC | VARCHAR(2) | No | — | RDB$4931 | NONE/NONE |
| 10 | VRBL_ACT_TIME | NUMERIC(8,2) | No | — | RDB$4932 | — |
| 11 | VRBL_STD_TIME | NUMERIC(8,2) | No | — | RDB$4933 | — |
| 12 | VRBL_DESC | VARCHAR(60) | No | — | RDB$4934 | NONE/NONE |
| 13 | VRBL_LABOR_RATE | NUMERIC(8,2) | No | — | RDB$4935 | — |
| 14 | VRBL_WHY_MADE | VARCHAR(2) | No | — | RDB$4936 | NONE/NONE |
| 15 | VRBL_PMNT_CODE | VARCHAR(4) | No | — | RDB$4937 | NONE/NONE |
| 16 | VRBL_JOB_CODE | VARCHAR(4) | No | — | RDB$4938 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| VREPAIRLABOR_KEY | VRBL_RN_PREF, VRBL_RN_NUM, VRBL_RN_SUF, VRBL_REC_SEQ | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R286 | VREPAIRLABOR.(VRBL_RN_PREF, VRBL_RN_NUM, VRBL_RN_SUF) | VREPAIRHDR.(VRH_RN_PREF, VRH_RN_NUM, VRH_RN_SUF) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R287 | VREPAIRLABOR.(VRBL_WORK_ACC) | VRWORKACCCODE.(VRWAC_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
