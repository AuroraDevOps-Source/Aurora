# EQUIP_DESC

Equipment and fuel. Equip Desc records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (ED_ID, ED_DESC_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | ED_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | ED_DESC_ID | BIGINT | Yes | — | D_SYSID | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| EQUIP_DESC_PRIMARY | ED_ID, ED_DESC_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R099 | EQUIP_DESC.(ED_DESC_ID) | CT_DESC.(CTD_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R100 | EQUIP_DESC.(ED_ID) | EQUIP_HEADER.(EH_ID) | High | Trigger EQUIP_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
