# TARIFF_COMMON_POINTS

Pricing and tariffs. Tariff Common Points records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (TPC_ZIP_POINT). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | TPC_ZIP_POINT | VARCHAR(38) | Yes | — | RDB$5842 | NONE/NONE |
| 2 | TPC_EQV_POINT | VARCHAR(38) | No | — | RDB$5843 | NONE/NONE |
| 3 | TPC_FACTOR | INTEGER | No | — | RDB$5844 | — |
| 4 | TPC_EQV_POINT2 | VARCHAR(38) | No | — | RDB$6162 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_TARIFF_COMMON_POINTS | TPC_ZIP_POINT | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
