# DRIV_WORKFLOW_HIST

Drivers and settlement. Driv Workflow Hist records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DWH_DRV_ID, DWH_TIMESTAMP). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DWH_DRV_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | DWH_TIMESTAMP | TIMESTAMP | Yes | — | RDB$6946 | — |
| 3 | DWH_TYPE | VARCHAR(2) | No | — | RDB$6947 | NONE/NONE |
| 4 | DWH_DETAILS | BLOB SUB_TYPE 1 | No | — | RDB$6948 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_DRIV_WORKFLOW_HIST | DWH_DRV_ID, DWH_TIMESTAMP | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R081 | DRIV_WORKFLOW_HIST.(DWH_DRV_ID) | EQUIP_HEADER.(EH_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
