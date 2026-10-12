# CT_PEOPLENET

Codes and configuration. Ct Peoplenet records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTP_KEY). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTP_KEY | VARCHAR(10) | Yes | — | RDB$5129 | NONE/NONE |
| 2 | CTP_COMPANY_CODE | VARCHAR(25) | No | — | RDB$5130 | NONE/NONE |
| 3 | CTP_PASSWORD | VARCHAR(25) | No | — | RDB$5131 | NONE/NONE |
| 4 | CTP_AUTO_ADD_EQUIP | VARCHAR(1) | No | — | RDB$5132 | NONE/NONE |
| 5 | CTP_URL | VARCHAR(255) | No | — | RDB$5140 | NONE/NONE |
| 6 | CTP_HTTP_TIMEOUT | INTEGER | No | — | RDB$5141 | — |
| 7 | CTP_HTTP_LOG | VARCHAR(1) | No | — | RDB$5142 | NONE/NONE |
| 8 | CTP_POLL_MESSAGES | SMALLINT | No | — | RDB$5143 | — |
| 9 | CTP_LAST_MSG_PKT_ID | BIGINT | No | — | RDB$5150 | — |
| 10 | CTP_LAST_LOCN_HIST_ID | BIGINT | No | — | RDB$5151 | — |
| 11 | CTP_LAST_MSG_STATUS_PKT_ID | BIGINT | No | — | RDB$5318 | — |
| 12 | CTP_POLL_WORKFLOW | SMALLINT | No | — | RDB$5373 | — |
| 13 | CTP_LAST_WORKFLOW_PKT_ID | BIGINT | No | — | RDB$5374 | — |
| 14 | CTP_APPROACHING_RADIUS | NUMERIC(5,2) | No | — | RDB$5375 | — |
| 15 | CTP_ARRIVED_RADIUS | NUMERIC(5,2) | No | — | RDB$5376 | — |
| 16 | CTP_DEPARTED_RADIUS | NUMERIC(5,2) | No | — | RDB$5377 | — |
| 17 | CTP_APPROACHING_WARNING_TIME | INTEGER | No | — | RDB$5378 | — |
| 18 | CTP_DEPARTED_WARNING_TIME | INTEGER | No | — | RDB$5379 | — |
| 19 | CTP_WORKFLOW_ONBOARD_FORM | BIGINT | No | — | RDB$5380 | — |
| 20 | CTP_WORKFLOW_DELIVERED_FORM | INTEGER | No | — | RDB$5381 | — |
| 21 | CTP_POLL_HOS_TOTALS | SMALLINT | No | — | RDB$5507 | — |
| 22 | CTP_POLL_HOS_LOGS | SMALLINT | No | — | RDB$5508 | — |
| 23 | CTP_LAST_HOS_LOG_ID | BIGINT | No | — | RDB$5509 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_CT_PEOPLENET | CTP_KEY | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
