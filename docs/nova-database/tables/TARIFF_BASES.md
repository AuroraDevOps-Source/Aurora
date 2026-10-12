# TARIFF_BASES

Pricing and tariffs. Tariff Bases records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (TB_TARIFF_ID, TB_EFF_DT, TB_ORIGIN, TB_DEST). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | TB_TARIFF_ID | VARCHAR(6) | Yes | — | RDB$2900 | NONE/NONE |
| 2 | TB_EFF_DT | DATE | Yes | — | RDB$2901 | — |
| 3 | TB_ORIGIN | VARCHAR(38) | Yes | — | RDB$2902 | NONE/NONE |
| 4 | TB_DEST | VARCHAR(38) | Yes | — | RDB$2903 | NONE/NONE |
| 5 | TB_RATE_BASE | NUMERIC(8,0) | Yes | — | RDB$2904 | — |
| 6 | TB_BI_DIR | VARCHAR(1) | No | — | RDB$2905 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_TARIFF_BASES_KEY | TB_TARIFF_ID, TB_EFF_DT, TB_ORIGIN, TB_DEST | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R261 | TARIFF_BASES.(TB_TARIFF_ID, TB_EFF_DT) | TARIFF_CONTROL.(TC_TARIFF_ID, TC_EFF_DT) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
