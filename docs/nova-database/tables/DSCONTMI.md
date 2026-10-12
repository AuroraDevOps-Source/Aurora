# DSCONTMI

Drivers and settlement. Dscontmi records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DSCM_CONT_ID, DSCM_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DSCM_CONT_ID | VARCHAR(12) | Yes | — | RDB$4025 | NONE/NONE |
| 2 | DSCM_SEQ | INTEGER | Yes | — | RDB$4026 | — |
| 3 | DSCM_MILES_TO | INTEGER | No | — | RDB$4027 | — |
| 4 | DSCM_MIN_DOL | NUMERIC(16,2) | No | — | RDB$4028 | — |
| 5 | DSCM_MAX_DOL | NUMERIC(16,2) | No | — | RDB$4029 | — |
| 6 | DSCM_LOAD_AMT | NUMERIC(16,4) | No | — | RDB$4051 | — |
| 7 | DSCM_EMPTY_AMT | NUMERIC(16,4) | No | — | RDB$4052 | — |
| 8 | DSCM_BOBTAIL_AMT | NUMERIC(16,4) | No | — | RDB$4053 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| DSCONTMI_KEY | DSCM_CONT_ID, DSCM_SEQ | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R085 | DSCONTMI.(DSCM_CONT_ID) | DSCONTRACT.(DSC_CONT_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
