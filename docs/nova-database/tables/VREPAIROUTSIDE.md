# VREPAIROUTSIDE

Maintenance and purchasing. Vrepairoutside records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (VRBO_RN_PREF, VRBO_RN_NUM, VRBO_RN_SUF, VRBO_REC_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | VRBO_RN_PREF | VARCHAR(1) | Yes | — | RDB$4939 | NONE/NONE |
| 2 | VRBO_RN_NUM | INTEGER | Yes | — | RDB$4940 | — |
| 3 | VRBO_RN_SUF | VARCHAR(2) | Yes | — | RDB$4941 | NONE/NONE |
| 4 | VRBO_REC_SEQ | INTEGER | Yes | — | RDB$4942 | — |
| 5 | VRBO_VENDOR | VARCHAR(8) | No | — | RDB$4943 | NONE/NONE |
| 6 | VRBO_INV_ID | VARCHAR(18) | No | — | RDB$4944 | NONE/NONE |
| 7 | VRBO_DATE | DATE | No | — | RDB$4945 | — |
| 10 | VRBO_PART | VARCHAR(3) | No | — | RDB$4948 | NONE/NONE |
| 11 | VRBO_WORK_ACC | VARCHAR(2) | No | — | RDB$4949 | NONE/NONE |
| 12 | VRBO_PART_COST | NUMERIC(11,2) | No | — | RDB$4950 | — |
| 13 | VRBO_LABOR_COST | NUMERIC(11,2) | No | — | RDB$4951 | — |
| 14 | VRBO_DESC | VARCHAR(128) | No | — | RDB$4952 | NONE/NONE |
| 15 | VRBO_CO | VARCHAR(2) | No | — | RDB$6480 | NONE/NONE |
| 16 | VRBO_INV_DATE | DATE | No | — | RDB$6596 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| VREPAIROUTSIDE_KEY | VRBO_RN_PREF, VRBO_RN_NUM, VRBO_RN_SUF, VRBO_REC_SEQ | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R288 | VREPAIROUTSIDE.(VRBO_RN_PREF, VRBO_RN_NUM, VRBO_RN_SUF) | VREPAIRHDR.(VRH_RN_PREF, VRH_RN_NUM, VRH_RN_SUF) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R289 | VREPAIROUTSIDE.(VRBO_WORK_ACC) | VRWORKACCCODE.(VRWAC_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
