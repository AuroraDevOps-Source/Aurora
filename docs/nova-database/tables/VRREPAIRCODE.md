# VRREPAIRCODE

Maintenance and purchasing. Vrrepaircode records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (VRRC_CODE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | VRRC_CODE | VARCHAR(2) | Yes | — | RDB$4985 | NONE/NONE |
| 2 | VRRC_DESCRIPTION | VARCHAR(100) | Yes | — | RDB$4986 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_VRREPAIRCODE | VRRC_CODE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R283 | VREPAIRHDR.(VRH_REPAIR_CODE) | VRREPAIRCODE.(VRRC_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
