# TARIFF_MASTER

Pricing and tariffs. Tariff Master records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (TM_TARIFF_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | TM_TARIFF_ID | VARCHAR(6) | Yes | — | RDB$3521 | NONE/NONE |
| 2 | TM_TARIFF_TYPE | VARCHAR(1) | Yes | — | RDB$3522 | NONE/NONE |
| 3 | TM_CLASS_EFF_DT | DATE | No | — | RDB$3523 | — |
| 4 | TM_CLASS_CONFERENCE | VARCHAR(3) | No | — | RDB$3524 | NONE/NONE |
| 5 | TM_CREATED_DATE | DATE | No | — | RDB$3525 | — |
| 6 | TM_DESCRIPTION | VARCHAR(250) | No | — | RDB$3540 | NONE/NONE |
| 7 | TM_NO_DISCOUNT | CHAR(1) | No | — | RDB$5793 | NONE/NONE |
| 8 | TM_NO_FSC | CHAR(1) | No | — | RDB$7123 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_TARIFF_MASTER_KEY | TM_TARIFF_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R262 | TARIFF_CONTROL.(TC_TARIFF_ID) | TARIFF_MASTER.(TM_TARIFF_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
