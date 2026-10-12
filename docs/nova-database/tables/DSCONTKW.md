# DSCONTKW

Drivers and settlement. Dscontkw records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DSCK_CONT_ID, DSCK_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DSCK_CONT_ID | VARCHAR(12) | Yes | — | RDB$4041 | NONE/NONE |
| 2 | DSCK_SEQ | INTEGER | Yes | — | RDB$4042 | — |
| 3 | DSCK_KW | VARCHAR(8) | No | — | RDB$4043 | NONE/NONE |
| 4 | DSCK_MIN_DOL | NUMERIC(16,2) | No | — | RDB$4044 | — |
| 5 | DSCK_MAX_DOL | NUMERIC(16,2) | No | — | RDB$4045 | — |
| 6 | DSCK_PRIME_AMT | NUMERIC(16,4) | No | — | RDB$4046 | — |
| 7 | DSCK_ALT_AMT | NUMERIC(16,4) | No | — | RDB$4047 | — |
| 8 | DSCK_AMT_TYP | VARCHAR(1) | No | — | RDB$4048 | NONE/NONE |
| 9 | DSCK_STATUS | VARCHAR(1) | No | — | RDB$4049 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| DSCONTKW_KEY | DSCK_CONT_ID, DSCK_SEQ | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R084 | DSCONTKW.(DSCK_CONT_ID) | DSCONTRACT.(DSC_CONT_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
