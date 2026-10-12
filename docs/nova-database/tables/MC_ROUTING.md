# MC_ROUTING

Integration and reference. Mc Routing records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (MCR_SYSTEM_TYPE, MCR_FORM_NUMBER, MCR_FORM_TYPE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | MCR_SYSTEM_TYPE | VARCHAR(1) | Yes | — | RDB$4578 | NONE/NONE |
| 2 | MCR_FORM_NUMBER | BIGINT | Yes | — | RDB$4579 | — |
| 3 | MCR_FORM_TYPE | VARCHAR(1) | Yes | — | RDB$4580 | NONE/NONE |
| 4 | MCR_QUEUE | VARCHAR(25) | No | — | D_QUEUE_ID | NONE/NONE |
| 5 | MCR_ERROR_QUEUE | VARCHAR(25) | No | — | D_QUEUE_ID | NONE/NONE |
| 6 | MCR_TRIGGER | VARCHAR(25) | No | — | RDB$4581 | NONE/NONE |
| 7 | MCR_TRIGGER_OPTIONS | VARCHAR(255) | No | — | RDB$4582 | NONE/NONE |
| 8 | MCR_ERROR_FORM_NUMBER | BIGINT | No | — | RDB$4583 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_MC_ROUTING | MCR_SYSTEM_TYPE, MCR_FORM_NUMBER, MCR_FORM_TYPE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R153 | MC_ROUTING.(MCR_ERROR_QUEUE) | MC_QUEUES.(MCQ_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R154 | MC_ROUTING.(MCR_QUEUE) | MC_QUEUES.(MCQ_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
