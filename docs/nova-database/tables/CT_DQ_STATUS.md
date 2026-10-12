# CT_DQ_STATUS

Codes and configuration. Ct Dq Status records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTDS_CODE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTDS_CODE | VARCHAR(5) | Yes | — | RDB$2677 | NONE/NONE |
| 2 | CTDS_DESC | VARCHAR(250) | No | — | RDB$2678 | NONE/NONE |
| 3 | CTDS_BGND_COLOR | INTEGER | No | — | RDB$2679 | — |
| 4 | CTDS_TEXT_COLOR | INTEGER | No | — | RDB$2680 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CTDS_PRIM_KEY | CTDS_CODE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R076 | DRIVER_QUAL.(DQ_STATUS) | CT_DQ_STATUS.(CTDS_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
