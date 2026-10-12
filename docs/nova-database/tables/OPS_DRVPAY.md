# OPS_DRVPAY

Orders and manifests. Ops Drvpay records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (ODP_ORDER_ID, ODP_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | ODP_ORDER_ID | INTEGER | Yes | — | RDB$5631 | — |
| 2 | ODP_SEQ | SMALLINT | Yes | — | RDB$5632 | — |
| 3 | ODP_DRVID | BIGINT | No | — | RDB$5633 | — |
| 4 | ODP_DRVTYPE | VARCHAR(5) | No | — | RDB$5634 | NONE/NONE |
| 5 | ODP_PAYCODE | VARCHAR(4) | No | — | RDB$5635 | NONE/NONE |
| 6 | ODP_DESC | VARCHAR(80) | No | — | RDB$5636 | NONE/NONE |
| 7 | ODP_AMT | NUMERIC(16,2) | No | — | RDB$5637 | — |
| 8 | ODP_PCT | NUMERIC(16,4) | No | — | RDB$5638 | — |
| 9 | ODP_CONTRACT | VARCHAR(12) | No | — | RDB$5639 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_OPS_DRVPAY | ODP_ORDER_ID, ODP_SEQ | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R168 | OPS_DRVPAY.(ODP_DRVID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R169 | OPS_DRVPAY.(ODP_ORDER_ID) | OPS_HEADER.(PB_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R170 | OPS_DRVPAY.(ODP_PAYCODE) | DSPAYTYPES.(DST_TYPE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`OPS_DRVPAY_AFTER_MOD`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
