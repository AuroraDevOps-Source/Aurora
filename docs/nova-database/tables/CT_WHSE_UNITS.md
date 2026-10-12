# CT_WHSE_UNITS

Containers and warehouse. Ct Whse Units records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: **None declared**. No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTWHU_UNIT_NAME | VARCHAR(12) | Yes | — | RDB$6040 | NONE/NONE |
| 2 | CTWHU_DESC | VARCHAR(36) | No | — | RDB$6041 | NONE/NONE |
| 3 | CTWHU_WIDTH | SMALLINT | No | — | RDB$6042 | — |
| 4 | CTWHU_HEIGHT | SMALLINT | No | — | RDB$6043 | — |
| 5 | CTWHU_LENGTH | SMALLINT | No | — | RDB$6044 | — |
| 6 | CTWHU_LOC | VARCHAR(12) | No | — | RDB$6045 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| IDX_CT_WHSE_UNITS | CTWHU_UNIT_NAME | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R058 | CT_WHSE_UNITS.(CTWHU_LOC) | CT_WHSE_LOCS.(CTWHL_WHSE_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
