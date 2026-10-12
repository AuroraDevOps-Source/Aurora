# DAT_EQUIPMENT

Integration and reference. Dat Equipment records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DE_EQ_TYPE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DE_EQ_TYPE | BIGINT | Yes | — | RDB$5818 | — |
| 2 | DE_DESC | VARCHAR(50) | Yes | — | D_DESC | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_DAT_EQUIPMENT | DE_EQ_TYPE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R062 | DAT_QUEUE.(DQ_EQUIPMENT_TYPE) | DAT_EQUIPMENT.(DE_EQ_TYPE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
