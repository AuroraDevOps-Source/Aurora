# DSADJUST

Drivers and settlement. Dsadjust records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DSJ_DRIV, DSJ_DSTYPE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DSJ_DRIV | BIGINT | Yes | — | RDB$4157 | — |
| 2 | DSJ_DSTYPE | VARCHAR(4) | Yes | — | RDB$4158 | NONE/NONE |
| 3 | DSJ_STATUS | VARCHAR(2) | No | — | RDB$4159 | NONE/NONE |
| 4 | DSJ_FREQ | VARCHAR(2) | No | — | RDB$4160 | NONE/NONE |
| 5 | DSJ_AMT_TYP | VARCHAR(1) | No | — | RDB$4161 | NONE/NONE |
| 6 | DSJ_AMT | NUMERIC(16,4) | No | — | RDB$4162 | — |
| 7 | DSJ_LAST_DT | DATE | No | — | RDB$4163 | — |
| 8 | DSJ_LIMIT | NUMERIC(16,2) | No | — | RDB$4164 | — |
| 9 | DSJ_TOT_TO_DT | NUMERIC(16,2) | No | — | RDB$4165 | — |
| 10 | DSJ_BASIS | VARCHAR(1) | No | — | RDB$4166 | NONE/NONE |
| 11 | DSJ_START_DT | DATE | No | — | RDB$6805 | — |
| 12 | DSJ_END_DT | DATE | No | — | RDB$6806 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| DSADJUST_PRIM_KEY | DSJ_DRIV, DSJ_DSTYPE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R083 | DSADJUST.(DSJ_DSTYPE) | DSPAYTYPES.(DST_TYPE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
