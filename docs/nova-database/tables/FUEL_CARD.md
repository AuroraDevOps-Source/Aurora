# FUEL_CARD

Equipment and fuel. Fuel Card records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (FC_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | FC_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | FC_CC | VARCHAR(2) | No | — | RDB$3876 | NONE/NONE |
| 3 | FC_ACCOUNT_ID | VARCHAR(32) | No | — | RDB$3877 | NONE/NONE |
| 4 | FC_CARD_ID | VARCHAR(32) | No | — | RDB$3878 | NONE/NONE |
| 5 | FC_DRIVER | BIGINT | No | — | D_SYSID | — |
| 6 | FC_POWER_UNIT | BIGINT | No | — | D_SYSID | — |
| 7 | FC_UNIT_TYPE | INTEGER | No | — | RDB$3879 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| FUEL_CARD_KEY | FC_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R133 | FUEL_CARD.(FC_DRIVER) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R134 | FUEL_CARD.(FC_POWER_UNIT) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R135 | FUEL_CARD.(FC_UNIT_TYPE) | CT_TYPE.(CTT_ID) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
