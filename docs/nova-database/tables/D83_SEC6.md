# D83_SEC6

Pricing and tariffs. D83 Sec6 records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (D83_S6_LOW_ZIP, D83_S6_BASE_LOW_ZIP). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | D83_S6_LOW_ZIP | CHAR(5) | Yes | — | RDB$2662 | NONE/NONE |
| 2 | D83_S6_HIGH_ZIP | CHAR(2) | No | — | RDB$2663 | NONE/NONE |
| 3 | D83_S6_BASE_LOW_ZIP | CHAR(5) | Yes | — | RDB$2664 | NONE/NONE |
| 4 | D83_S6_BASE_HIGH_ZIP | CHAR(2) | No | — | RDB$2665 | NONE/NONE |
| 5 | D83_S6_FACTOR | NUMERIC(3,0) | Yes | — | RDB$2666 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| D83_SEC6_KEY | D83_S6_LOW_ZIP, D83_S6_BASE_LOW_ZIP | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
