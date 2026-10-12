# OPS_EDI_RESEND

Orders and manifests. Ops Edi Resend records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (OPER_ORDER_ID, OPER_TYPE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | OPER_ORDER_ID | INTEGER | Yes | — | RDB$5359 | — |
| 2 | OPER_TYPE | VARCHAR(4) | Yes | — | RDB$5360 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| OPS_EDI_RESEND_PRIMARY | OPER_ORDER_ID, OPER_TYPE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R172 | OPS_EDI_RESEND.(OPER_ORDER_ID) | OPS_HEADER.(PB_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`OPS_EDI_RESEND_AFTER_MOD`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
