# CT_INS_TYPES

Codes and configuration. Ct Ins Types records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTI_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTI_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | CTI_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |
| 3 | CTI_TYPE | VARCHAR(1) | No | — | RDB$5486 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CT_INS_TYPES_PRIMARY | CTI_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R034 | CA_INSURANCE.(CAI_TYPE) | CT_INS_TYPES.(CTI_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
