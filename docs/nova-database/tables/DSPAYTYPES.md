# DSPAYTYPES

Drivers and settlement. Dspaytypes records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DST_TYPE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DST_TYPE | VARCHAR(4) | Yes | — | RDB$3996 | NONE/NONE |
| 2 | DST_DESC | VARCHAR(80) | No | — | RDB$3997 | NONE/NONE |
| 3 | DST_CO | VARCHAR(2) | No | — | RDB$3998 | NONE/NONE |
| 4 | DST_GLID | VARCHAR(10) | No | — | RDB$3999 | NONE/NONE |
| 5 | DST_GLDIV | VARCHAR(4) | No | — | RDB$4000 | NONE/NONE |
| 6 | DST_GLDEPT | VARCHAR(6) | No | — | RDB$4001 | NONE/NONE |
| 7 | DST_PAY_DED | VARCHAR(1) | No | — | RDB$4002 | NONE/NONE |
| 8 | DST_TAX_NTAX | VARCHAR(1) | No | — | RDB$4003 | NONE/NONE |
| 9 | DST_FRT_ASC | VARCHAR(1) | No | — | RDB$4004 | NONE/NONE |
| 10 | DST_ONLY_ONCE_PER_PAY | CHAR(1) | No | — | RDB$6804 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| DSPAYTYPES_PRIMARY_KEY | DST_TYPE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R082 | DRVSETL.(DS_TYPE) | DSPAYTYPES.(DST_TYPE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R083 | DSADJUST.(DSJ_DSTYPE) | DSPAYTYPES.(DST_TYPE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R088 | DSPRELIM.(DP_TYPE) | DSPAYTYPES.(DST_TYPE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R170 | OPS_DRVPAY.(ODP_PAYCODE) | DSPAYTYPES.(DST_TYPE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
