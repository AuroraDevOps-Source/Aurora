# ZIPSERVICE

Locations and routing. Zipservice records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (ZS_ORIGZIP, ZS_DESTZIP). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | ZS_ORIGZIP | VARCHAR(6) | Yes | — | RDB$3711 | NONE/NONE |
| 2 | ZS_DESTZIP | VARCHAR(6) | Yes | — | RDB$3712 | NONE/NONE |
| 3 | ZS_SERVDAYS | INTEGER | No | — | RDB$3713 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| ZIPSERVICE_DEST_KEY | ZS_DESTZIP, ZS_ORIGZIP | Yes | Yes | ASC | — |
| ZIPSERVICE_PRIM_KEY | ZS_ORIGZIP, ZS_DESTZIP | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
