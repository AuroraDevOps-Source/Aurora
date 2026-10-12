# DSCONTRACT

Drivers and settlement. Dscontract records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DSC_CONT_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DSC_CONT_ID | VARCHAR(12) | Yes | — | RDB$4005 | NONE/NONE |
| 2 | DSC_ADD_DT | TIMESTAMP | No | — | RDB$4006 | — |
| 3 | DSC_ADD_USER | VARCHAR(12) | No | — | RDB$4007 | NONE/NONE |
| 4 | DSC_CHG_DT | TIMESTAMP | No | — | RDB$4008 | — |
| 5 | DSC_CHG_USER | VARCHAR(12) | No | — | RDB$4009 | NONE/NONE |
| 6 | DSC_DESC | VARCHAR(256) | No | — | RDB$4010 | NONE/NONE |
| 7 | DSC_STATUS | VARCHAR(1) | No | — | RDB$4050 | NONE/NONE |
| 8 | DSC_TYPE | CHAR(1) | No | — | RDB$4104 | NONE/NONE |
| 9 | DSC_HOURLY | NUMERIC(8,2) | No | — | RDB$7186 | — |
| 10 | DSC_PER_DLV | NUMERIC(8,2) | No | — | RDB$7187 | — |
| 11 | DSC_ADDL_DLV_SVC | NUMERIC(8,2) | No | — | RDB$7188 | — |
| 12 | DSC_ATTEND_DAY | NUMERIC(8,2) | No | — | RDB$7189 | — |
| 13 | DSC_ATTEND_WEEK | NUMERIC(8,2) | No | — | RDB$7190 | — |
| 14 | DSC_MIN_WEEK | NUMERIC(8,2) | No | — | RDB$7191 | — |
| 15 | DSC_DAILY | NUMERIC(8,2) | No | — | RDB$7192 | — |
| 16 | DSC_PER_MI | NUMERIC(8,2) | No | — | RDB$7193 | — |
| 17 | DSC_PER_MILES_OVER | NUMERIC(8,2) | No | — | RDB$7194 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| DSCONTRACT_KEY | DSC_CONT_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R066 | DRIVER_HEADER.(DH_CONTRACT) | DSCONTRACT.(DSC_CONT_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R084 | DSCONTKW.(DSCK_CONT_ID) | DSCONTRACT.(DSC_CONT_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R085 | DSCONTMI.(DSCM_CONT_ID) | DSCONTRACT.(DSC_CONT_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R086 | DSCONTREV.(DSCR_CONT_ID) | DSCONTRACT.(DSC_CONT_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R087 | DSCONTSTAT.(DSCS_CONT_ID) | DSCONTRACT.(DSC_CONT_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
