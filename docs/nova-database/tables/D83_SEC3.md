# D83_SEC3

Pricing and tariffs. D83 Sec3 records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (D83_S3_LOW, D83_S3_HIGH). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | D83_S3_LOW | CHAR(3) | Yes | — | RDB$2655 | NONE/NONE |
| 2 | D83_S3_HIGH | CHAR(3) | Yes | — | RDB$2656 | NONE/NONE |
| 3 | D83_S3_FACTOR | NUMERIC(6,0) | Yes | — | RDB$2657 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| D83_SEC3_KEY | D83_S3_LOW, D83_S3_HIGH | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
