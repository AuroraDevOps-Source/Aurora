# ITEM_WEIGHT

Pricing and tariffs. Item Weight records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (IW_ITEM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | IW_ITEM | VARCHAR(100) | Yes | — | RDB$7125 | NONE/NONE |
| 2 | IW_DESCRIPTION | VARCHAR(150) | Yes | — | RDB$7126 | NONE/NONE |
| 3 | IW_WEIGHT | INTEGER | No | — | RDB$7127 | — |
| 4 | IW_BGND_COLOR | INTEGER | No | — | RDB$7151 | — |
| 5 | IW_TEXT_COLOR | INTEGER | No | — | RDB$7152 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| RDB$PRIMARY3 | IW_ITEM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R202 | OPS_LINE_ITEMS.(OPL_ITEM_NUM) | ITEM_WEIGHT.(IW_ITEM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
