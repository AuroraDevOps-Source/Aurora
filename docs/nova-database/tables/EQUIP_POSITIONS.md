# EQUIP_POSITIONS

Equipment and fuel. Equip Positions records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (EP_ID, EP_DATE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | EP_ID | BIGINT | Yes | — | RDB$4065 | — |
| 2 | EP_DATE | TIMESTAMP | Yes | — | RDB$4066 | — |
| 3 | EP_LATITUDE | DOUBLE PRECISION | No | — | RDB$4067 | — |
| 4 | EP_NS | CHAR(1) | No | — | RDB$4068 | NONE/NONE |
| 5 | EP_LONGITUDE | DOUBLE PRECISION | No | — | RDB$4069 | — |
| 6 | EP_EW | CHAR(1) | No | — | RDB$4070 | NONE/NONE |
| 7 | EP_CITY | VARCHAR(35) | No | — | D_CITY | NONE/NONE |
| 8 | EP_STATE | VARCHAR(2) | No | — | D_STATE | NONE/NONE |
| 9 | EP_DISTANCE | DOUBLE PRECISION | No | — | RDB$4071 | — |
| 10 | EP_BEARING | DOUBLE PRECISION | No | — | RDB$4072 | — |
| 11 | EP_DIRECTION | VARCHAR(2) | No | — | RDB$4073 | NONE/NONE |
| 12 | EP_CARGO_INFO | BLOB SUB_TYPE 1 | No | — | RDB$6936 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| EP_PRIMARY_KEY | EP_ID, EP_DATE | Yes | Yes | ASC | — |
| EP_REVERSE_KEY | EP_ID, EP_DATE | No | Yes | DESC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R119 | EQUIP_POSITIONS.(EP_ID) | EQUIP_HEADER.(EH_ID) | High | Trigger EQUIP_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |

## Trigger evidence

`EP_AFTER_INSERT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
