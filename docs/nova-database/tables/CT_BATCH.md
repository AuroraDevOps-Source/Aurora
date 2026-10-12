# CT_BATCH

Codes and configuration. Ct Batch records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTB_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTB_ID | VARCHAR(20) | Yes | — | RDB$4766 | NONE/NONE |
| 2 | CTB_LAST_MILEAGE_SEQ | BIGINT | No | — | RDB$4767 | — |
| 3 | CTB_LAST_LOADED_EMPTY_SEQ | BIGINT | No | — | RDB$5064 | — |
| 4 | CTB_LAST_EQUIP_COUNT_DATE | DATE | No | — | RDB$5065 | — |
| 5 | CTB_LAST_REVENUE_DATE | DATE | No | — | RDB$5066 | — |
| 6 | CTB_LAST_ORDER_COUNT_DATE | DATE | No | — | RDB$5067 | — |
| 7 | CTB_LAST_OFF_RTE_MILES_DATE | DATE | No | — | RDB$5081 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_CT_BATCH | CTB_ID | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
