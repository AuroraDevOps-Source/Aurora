# DSCONTREV

Drivers and settlement. Dscontrev records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DSCR_CONT_ID, DSCR_SEQ, DSCR_TYPE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DSCR_CONT_ID | VARCHAR(12) | Yes | — | RDB$4136 | NONE/NONE |
| 2 | DSCR_SEQ | INTEGER | Yes | — | RDB$4137 | — |
| 3 | DSCR_WGT_TO | INTEGER | No | — | RDB$4138 | — |
| 4 | DSCR_MIN_DOL | NUMERIC(16,2) | No | — | RDB$4139 | — |
| 5 | DSCR_MAX_DOL | NUMERIC(16,2) | No | — | RDB$4140 | — |
| 6 | DSCR_PRIME_AMT | NUMERIC(16,4) | No | — | RDB$4141 | — |
| 7 | DSCR_ALT_AMT | NUMERIC(16,4) | No | — | RDB$4142 | — |
| 8 | DSCR_AMT_TYP | VARCHAR(1) | No | — | RDB$4143 | NONE/NONE |
| 9 | DSCR_TYPE | CHAR(1) | Yes | — | RDB$4144 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| DSCONTREV_KEY | DSCR_CONT_ID, DSCR_SEQ, DSCR_TYPE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R086 | DSCONTREV.(DSCR_CONT_ID) | DSCONTRACT.(DSC_CONT_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
