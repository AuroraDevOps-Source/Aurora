# CT_QUALCOMM

Codes and configuration. Ct Qualcomm records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTQ_KEY). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTQ_KEY | VARCHAR(10) | Yes | — | RDB$4246 | NONE/NONE |
| 2 | CTQ_COMPANY_CODE | VARCHAR(25) | No | — | RDB$4247 | NONE/NONE |
| 3 | CTQ_USER_NAME | VARCHAR(25) | No | — | RDB$4248 | NONE/NONE |
| 4 | CTQ_PASSWORD | VARCHAR(25) | No | — | RDB$4249 | NONE/NONE |
| 5 | CTQ_SUBSCRIBER_ID | BIGINT | No | — | RDB$4251 | — |
| 6 | CTQ_POLL_MESSAGES | SMALLINT | No | — | RDB$4250 | — |
| 7 | CTQ_LAST_MSG_ID | BIGINT | No | — | RDB$4253 | — |
| 8 | CTQ_AUTO_ADD_EQUIP | VARCHAR(1) | No | — | RDB$4262 | NONE/NONE |
| 9 | CTQ_HOS_USER_NAME | VARCHAR(25) | No | — | RDB$4267 | NONE/NONE |
| 10 | CTQ_HOS_PASSWORD | VARCHAR(25) | No | — | RDB$4268 | NONE/NONE |
| 11 | CTQ_POLL_HOS_TOTALS | SMALLINT | No | — | RDB$4269 | — |
| 12 | CTQ_POLL_HOS_LOGS | SMALLINT | No | — | RDB$4270 | — |
| 13 | CTQ_LAST_HOS_LOG_ID | BIGINT | No | — | RDB$4271 | — |
| 14 | CTQ_UPD_TRC_HUB_MILES | CHAR(1) | No | — | RDB$6621 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_CT_QUALCOMM | CTQ_KEY | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
