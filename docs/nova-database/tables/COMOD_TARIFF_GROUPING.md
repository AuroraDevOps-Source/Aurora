# COMOD_TARIFF_GROUPING

Pricing and tariffs. Comod Tariff Grouping records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTG_TARIFF_ID, CTG_POINT). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTG_TARIFF_ID | VARCHAR(6) | Yes | — | RDB$3483 | NONE/NONE |
| 2 | CTG_POINT | VARCHAR(38) | Yes | — | RDB$3484 | NONE/NONE |
| 3 | CTG_EQUATE | VARCHAR(38) | No | — | RDB$3485 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_COMOD_TARIFF_GROUPING_KEY | CTG_TARIFF_ID, CTG_POINT | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R045 | COMOD_TARIFF_GROUPING.(CTG_TARIFF_ID) | COMOD_TARIFF_CONTROL.(CT_TARIFF_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
