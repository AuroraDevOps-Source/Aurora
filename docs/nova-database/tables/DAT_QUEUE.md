# DAT_QUEUE

Integration and reference. Dat Queue records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DQ_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DQ_ACTION | INTEGER | Yes | — | RDB$6070 | — |
| 2 | DQ_DAT_ALARM_ID | VARCHAR(8) | No | — | RDB$6071 | NONE/NONE |
| 3 | DQ_DAT_ASSET_ID | VARCHAR(8) | No | — | RDB$6072 | NONE/NONE |
| 4 | DQ_DAT_LOGIN_ID | VARCHAR(16) | No | — | RDB$6073 | NONE/NONE |
| 5 | DQ_EQUIPMENT_TYPE | BIGINT | No | — | RDB$6074 | — |
| 6 | DQ_FIRST_POST | TIMESTAMP | No | — | RDB$6075 | — |
| 7 | DQ_ID | BIGINT | Yes | — | D_SYSID | — |
| 8 | DQ_LAST_POST | TIMESTAMP | No | — | RDB$6076 | — |
| 9 | DQ_NOVA_ID | BIGINT | No | — | D_SYSID | — |
| 10 | DQ_NOVA_UID | VARCHAR(8) | No | — | RDB$6077 | NONE/NONE |
| 11 | DQ_ORDER_COMMENTS | BLOB SUB_TYPE 0 | No | — | RDB$6078 | — |
| 12 | DQ_POST_FAULT_CODE | INTEGER | No | — | RDB$6079 | — |
| 13 | DQ_POST_FAULT_NAME | VARCHAR(50) | No | — | RDB$6080 | NONE/NONE |
| 14 | DQ_POST_STATUS | INTEGER | No | — | RDB$6081 | — |
| 15 | DQ_PROGRAM_ID | VARCHAR(255) | No | — | RDB$6082 | NONE/NONE |
| 16 | DQ_RATE | NUMERIC(16,2) | No | — | D_BILL_AMOUNT | — |
| 17 | DQ_RESULT | VARCHAR(255) | No | — | RDB$6083 | NONE/NONE |
| 18 | DQ_TIMESTAMP | TIMESTAMP | Yes | — | RDB$6084 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_DAT_QUEUE_1 | DQ_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R062 | DAT_QUEUE.(DQ_EQUIPMENT_TYPE) | DAT_EQUIPMENT.(DE_EQ_TYPE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R063 | DAT_QUEUE.(DQ_NOVA_UID) | USERS.(U_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`DAT_QUEUE_DELETE_EVENT`, `DAT_QUEUE_INSERT_EVENT`, `DAT_QUEUE_UPDATE_EVENT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
