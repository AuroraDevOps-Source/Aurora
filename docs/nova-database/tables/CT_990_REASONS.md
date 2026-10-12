# CT_990_REASONS

Codes and configuration. Ct 990 Reasons records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CT9R_CODE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CT9R_CODE | VARCHAR(3) | Yes | — | RDB$3675 | NONE/NONE |
| 2 | CT9R_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_CT_990_REASONS | CT9R_CODE | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
