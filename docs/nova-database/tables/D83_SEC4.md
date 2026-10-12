# D83_SEC4

Pricing and tariffs. D83 Sec4 records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (D83_S4_LOW_RANGE_3, D83_S4_HIGH_RANGE_3, D83_S4_ZIP_5). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | D83_S4_LOW_RANGE_3 | CHAR(3) | Yes | — | RDB$2658 | NONE/NONE |
| 2 | D83_S4_HIGH_RANGE_3 | CHAR(3) | Yes | — | RDB$2659 | NONE/NONE |
| 3 | D83_S4_ZIP_5 | CHAR(5) | Yes | — | RDB$2660 | NONE/NONE |
| 4 | D83_S4_FACTOR | NUMERIC(6,0) | Yes | — | RDB$2661 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| D83_SEC4_KEY | D83_S4_LOW_RANGE_3, D83_S4_HIGH_RANGE_3, D83_S4_ZIP_5 | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
