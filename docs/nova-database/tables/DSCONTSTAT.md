# DSCONTSTAT

Drivers and settlement. Dscontstat records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DSCS_CONT_ID, DSCS_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DSCS_CONT_ID | VARCHAR(12) | Yes | — | RDB$5528 | NONE/NONE |
| 2 | DSCS_SEQ | INTEGER | Yes | — | RDB$5529 | — |
| 3 | DSCS_STATCODE | INTEGER | No | — | RDB$5530 | — |
| 4 | DSCS_MIN_DOL | NUMERIC(16,2) | No | — | RDB$5531 | — |
| 5 | DSCS_MAX_DOL | NUMERIC(16,2) | No | — | RDB$5532 | — |
| 6 | DSCS_PRIME_AMT | NUMERIC(16,4) | No | — | RDB$5533 | — |
| 7 | DSCS_ALT_AMT | NUMERIC(16,4) | No | — | RDB$5534 | — |
| 8 | DSCS_AMT_TYP | VARCHAR(1) | No | — | RDB$5535 | NONE/NONE |
| 9 | DSCS_REL_IND | VARCHAR(1) | No | — | RDB$5536 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| DSCONTSTAT_KEY | DSCS_CONT_ID, DSCS_SEQ | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R087 | DSCONTSTAT.(DSCS_CONT_ID) | DSCONTRACT.(DSC_CONT_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
