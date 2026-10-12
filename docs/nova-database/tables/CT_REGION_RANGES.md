# CT_REGION_RANGES

Locations and routing. Ct Region Ranges records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: **None declared**. No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTRR_REGION_ID | VARCHAR(4) | Yes | — | D_REGION_ID | NONE/NONE |
| 2 | CTRR_LOW_ZIP | VARCHAR(9) | No | — | D_ZIP | NONE/NONE |
| 3 | CTRR_HIGH_ZIP | VARCHAR(9) | No | — | D_ZIP | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CTRR_REGION_KEY | CTRR_REGION_ID | No | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R051 | CT_REGION_RANGES.(CTRR_REGION_ID) | CT_REGION_IDS.(CTR_REGION_ID) | High | Trigger CTR_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
