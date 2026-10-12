# VRWORKACCCODE

Maintenance and purchasing. Vrworkacccode records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (VRWAC_CODE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | VRWAC_CODE | VARCHAR(2) | Yes | — | RDB$4987 | NONE/NONE |
| 2 | VRWAC_DESCRIPTION | VARCHAR(100) | Yes | — | RDB$4988 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_VRWORKACCCODE | VRWAC_CODE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R287 | VREPAIRLABOR.(VRBL_WORK_ACC) | VRWORKACCCODE.(VRWAC_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R289 | VREPAIROUTSIDE.(VRBO_WORK_ACC) | VRWORKACCCODE.(VRWAC_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
