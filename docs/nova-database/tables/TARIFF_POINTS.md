# TARIFF_POINTS

Pricing and tariffs. Tariff Points records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (TP_TARIFF_ID, TP_EFF_DT, TP_ZIP_POINT). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | TP_TARIFF_ID | VARCHAR(6) | Yes | — | RDB$3106 | NONE/NONE |
| 2 | TP_EFF_DT | DATE | Yes | — | RDB$3107 | — |
| 3 | TP_ZIP_POINT | VARCHAR(38) | Yes | — | RDB$3108 | NONE/NONE |
| 4 | TP_EQU_POINT | VARCHAR(38) | No | — | RDB$3109 | NONE/NONE |
| 5 | TP_FACTOR | INTEGER | No | — | RDB$3110 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_TARIFF_POINTS_KEY | TP_TARIFF_ID, TP_EFF_DT, TP_ZIP_POINT | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R263 | TARIFF_POINTS.(TP_TARIFF_ID, TP_EFF_DT) | TARIFF_CONTROL.(TC_TARIFF_ID, TC_EFF_DT) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
