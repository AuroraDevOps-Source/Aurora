# CT_POINTMILES

Locations and routing. Ct Pointmiles records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTPM_FROM_ZIP, CTPM_FROM_CITY, CTPM_FROM_ST, CTPM_FROM_ADR, CTPM_TO_ZIP, CTPM_TO_CITY, CTPM_TO_ST, CTPM_TO_ADR, CTPM_MI_TYPE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTPM_FROM_ZIP | VARCHAR(10) | Yes | — | RDB$6534 | NONE/NONE |
| 2 | CTPM_FROM_CITY | VARCHAR(32) | Yes | — | RDB$6535 | NONE/NONE |
| 3 | CTPM_FROM_ST | VARCHAR(2) | Yes | — | RDB$6536 | NONE/NONE |
| 4 | CTPM_FROM_ADR | VARCHAR(50) | Yes | — | RDB$6537 | NONE/NONE |
| 5 | CTPM_TO_ZIP | VARCHAR(10) | Yes | — | RDB$6538 | NONE/NONE |
| 6 | CTPM_TO_CITY | VARCHAR(32) | Yes | — | RDB$6539 | NONE/NONE |
| 7 | CTPM_TO_ST | VARCHAR(2) | Yes | — | RDB$6540 | NONE/NONE |
| 8 | CTPM_TO_ADR | VARCHAR(50) | Yes | — | RDB$6541 | NONE/NONE |
| 9 | CTPM_MI_TYPE | VARCHAR(2) | Yes | — | RDB$6542 | NONE/NONE |
| 10 | CTPM_MILES | NUMERIC(7,2) | No | — | RDB$6543 | — |
| 11 | CTPM_TRAVEL_MINUTES | BIGINT | No | — | RDB$6544 | — |
| 12 | CTPM_CREATE_DATE | DATE | No | — | RDB$6545 | — |
| 13 | CTPM_CREATE_SOURCE | VARCHAR(24) | No | — | RDB$6546 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CT_POINTMILES_KEY | CTPM_FROM_ZIP, CTPM_FROM_CITY, CTPM_FROM_ST, CTPM_FROM_ADR, CTPM_TO_ZIP, CTPM_TO_CITY, CTPM_TO_ST, CTPM_TO_ADR, CTPM_MI_TYPE | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
