# CT_RAND_MCNALLY

Codes and configuration. Ct Rand Mcnally records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTRM_KEY). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTRM_KEY | VARCHAR(10) | Yes | — | RDB$4056 | NONE/NONE |
| 2 | CTRM_COMPANY_CODE | VARCHAR(8) | No | — | RDB$4057 | NONE/NONE |
| 3 | CTRM_USER_NAME | VARCHAR(40) | No | — | RDB$4058 | NONE/NONE |
| 4 | CTRM_PASSWORD | VARCHAR(50) | No | — | RDB$4059 | NONE/NONE |
| 5 | CTRM_DATA_STORE | VARCHAR(50) | No | — | RDB$4060 | NONE/NONE |
| 6 | CTRM_REQUEST_ID | BIGINT | No | — | RDB$4061 | — |
| 7 | CTRM_LAST_DRV_STATUS_DATE | TIMESTAMP | No | — | RDB$4062 | — |
| 8 | CTRM_POLL_DRV_STATUS | SMALLINT | No | — | RDB$4063 | — |
| 9 | CTRM_POLL_MESSAGES | SMALLINT | No | — | RDB$4064 | — |
| 10 | CTRM_LAST_MSG_INDEX | BIGINT | No | — | RDB$4101 | — |
| 11 | CTRM_INBOUND_MSG_BATCH_SIZE | INTEGER | No | — | RDB$4102 | — |
| 12 | CTRM_AUTO_ADD_EQUIP | VARCHAR(1) | No | — | RDB$4103 | NONE/NONE |
| 13 | CTRM_POLL_HOS_TOTALS | SMALLINT | No | — | RDB$4134 | — |
| 14 | CTRM_LAST_HOS_TOTALS_DATE | TIMESTAMP | No | — | RDB$4135 | — |
| 15 | CTRM_LAST_HOS_LOGS_INDEX | BIGINT | No | — | RDB$4228 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_CT_RAND_MCNALLY | CTRM_KEY | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
