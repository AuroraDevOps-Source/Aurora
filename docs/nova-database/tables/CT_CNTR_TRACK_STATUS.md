# CT_CNTR_TRACK_STATUS

Codes and configuration. Ct Cntr Track Status records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CCTS_CODE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CCTS_CODE | VARCHAR(4) | Yes | — | RDB$6202 | NONE/NONE |
| 2 | CCTS_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_CT_CNTR_TRACK_STATUS | CCTS_CODE | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
