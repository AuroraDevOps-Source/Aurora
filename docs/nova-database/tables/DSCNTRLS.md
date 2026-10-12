# DSCNTRLS

Drivers and settlement. Dscntrls records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DSCNTRLS). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DSCNTRLS | VARCHAR(8) | Yes | — | RDB$4216 | NONE/NONE |
| 2 | DSC_DAYS_BACK | INTEGER | No | — | RDB$4217 | — |
| 3 | DSC_START_DT | DATE | No | — | RDB$4218 | — |
| 4 | DSC_END_DT | DATE | No | — | RDB$4219 | — |
| 5 | DSC_ADV_PAY_TYPE | VARCHAR(4) | No | — | RDB$4220 | NONE/NONE |
| 6 | DSC_PAYAMT_KW | VARCHAR(8) | No | — | RDB$5148 | NONE/NONE |
| 7 | DSC_PAYPCT_KW | VARCHAR(8) | No | — | RDB$5149 | NONE/NONE |
| 8 | DSC_NO_PAY_KW | VARCHAR(8) | No | — | RDB$5799 | NONE/NONE |
| 9 | DSC_RECUR_ALWAYS_TAKE | VARCHAR(1) | No | — | RDB$5802 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| RDB$PRIMARY2 | DSCNTRLS | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
