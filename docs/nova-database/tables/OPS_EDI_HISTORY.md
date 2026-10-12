# OPS_EDI_HISTORY

Orders and manifests. Ops Edi History records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (OPEH_ORDER_ID, OPEH_SEQUENCE, OPEH_PARTNER_ID, OPEH_STATUS). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | OPEH_ORDER_ID | INTEGER | Yes | — | RDB$3676 | — |
| 2 | OPEH_SEQUENCE | BIGINT | Yes | — | RDB$3677 | — |
| 3 | OPEH_PARTNER_ID | VARCHAR(30) | Yes | — | RDB$3678 | NONE/NONE |
| 4 | OPEH_DATE | TIMESTAMP | No | — | RDB$3679 | — |
| 5 | OPEH_STATUS | VARCHAR(6) | Yes | — | RDB$3680 | NONE/NONE |
| 6 | OPEH_TIMESTAMP | VARCHAR(19) | No | — | RDB$3681 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_OPS_EDI_HISTORY | OPEH_ORDER_ID, OPEH_SEQUENCE, OPEH_PARTNER_ID, OPEH_STATUS | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R171 | OPS_EDI_HISTORY.(OPEH_ORDER_ID) | OPS_HEADER.(PB_ID) | High | Trigger OPS_HEADER_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. |

## Trigger evidence

`OPS_EDI_HISTORY_AFTER_MOD`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
