# WEBHOOK_QUEUE

Orders and manifests. Order webhook queue; related header enqueue triggers are inactive.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (WHQ_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | WHQ_ID | BIGINT | Yes | — | RDB$7239 | — |
| 2 | WHQ_ORDER_ID | INTEGER | Yes | — | RDB$7240 | — |
| 3 | WHQ_CREATED_AT | TIMESTAMP | Yes | DEFAULT CURRENT_TIMESTAMP | RDB$7241 | — |
| 4 | WHQ_PROCESSED_AT | TIMESTAMP | No | — | RDB$7242 | — |
| 5 | WHQ_STATUS | VARCHAR(20) | Yes | DEFAULT 'PENDING' | RDB$7243 | NONE/NONE |
| 6 | WHQ_ERROR_MSG | VARCHAR(1000) | No | — | RDB$7244 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| IDX_WEBHOOK_QUEUE_ORDER | WHQ_ORDER_ID | No | Yes | ASC | — |
| IDX_WEBHOOK_QUEUE_STATUS | WHQ_STATUS | No | Yes | ASC | — |
| RDB$PRIMARY4 | WHQ_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R292 | WEBHOOK_QUEUE.(WHQ_ORDER_ID) | OPS_HEADER.(PB_ID) | High | Inactive triggers TRG_OPS_HEADER_WH_AI and TRG_OPS_HEADER_WH_AU map NEW.PB_ID to WHQ_ORDER_ID Mapping is explicit; both webhook triggers are inactive in this snapshot. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
