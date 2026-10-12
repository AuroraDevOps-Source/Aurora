# CNTR_TRACK_BOLS

Containers and warehouse. Cntr Track Bols records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTB_CARRIER_BOL). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTB_CARRIER_BOL | VARCHAR(80) | Yes | — | D_CONTROL_NUMBER | NONE/NONE |
| 2 | CTB_REGISTERED_ID | BIGINT | No | — | D_SYSID | — |
| 3 | CTB_REGISTERED_TIME | TIMESTAMP | No | — | RDB$6175 | — |
| 5 | CTB_REGISTERED_MSG | VARCHAR(255) | No | — | RDB$6177 | NONE/NONE |
| 6 | CTB_POLLING_ENABLED | VARCHAR(1) | No | — | D_YESNO | NONE/NONE |
| 7 | CTB_LAST_POLL_TIME | TIMESTAMP | No | — | RDB$6178 | — |
| 8 | CTB_LAST_POLL_ID | BIGINT | No | — | D_SYSID | — |
| 9 | CTB_LAST_POLL_STATUS | VARCHAR(10) | No | — | RDB$6179 | NONE/NONE |
| 10 | CTB_LAST_POLL_MSG | VARCHAR(255) | No | — | RDB$6180 | NONE/NONE |
| 11 | CTB_REGISTERED_STATUS | VARCHAR(10) | No | — | RDB$6370 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_CNTR_TRACK_BOLS | CTB_CARRIER_BOL | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R041 | CNTR_TRACK_CONTAINER.(CTC_BOL) | CNTR_TRACK_BOLS.(CTB_CARRIER_BOL) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
