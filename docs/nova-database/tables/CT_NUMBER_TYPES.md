# CT_NUMBER_TYPES

Codes and configuration. Ct Number Types records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTN_TYPE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTN_TYPE | VARCHAR(4) | Yes | — | D_DESC_CODE | NONE/NONE |
| 2 | CTN_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_CT_NUMBER_TYPES | CTN_TYPE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R213 | OPS_NUMBERS.(OPN_TYPE) | CT_NUMBER_TYPES.(CTN_TYPE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
