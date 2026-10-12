# CT_OMNITRACS_XRS

Codes and configuration. Ct Omnitracs Xrs records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTOX_KEY). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTOX_KEY | VARCHAR(10) | Yes | — | RDB$5493 | NONE/NONE |
| 2 | CTOX_COMPANY_CODE | VARCHAR(25) | No | — | RDB$5494 | NONE/NONE |
| 3 | CTOX_USER_NAME | VARCHAR(25) | No | — | RDB$5495 | NONE/NONE |
| 4 | CTOX_PASSWORD | VARCHAR(25) | No | — | RDB$5496 | NONE/NONE |
| 5 | CTOX_HTTP_TIMEOUT | INTEGER | No | — | RDB$5497 | — |
| 6 | CTOX_HTTP_LOG | VARCHAR(1) | No | — | RDB$5498 | NONE/NONE |
| 7 | CTOX_AUTO_ADD_EQUIP | VARCHAR(1) | No | — | RDB$5499 | NONE/NONE |
| 8 | CTOX_POLL_MESSAGES | SMALLINT | No | — | RDB$5500 | — |
| 9 | CTOX_LAST_MSG_STATUS_TIME | TIMESTAMP | No | — | RDB$5501 | — |
| 10 | CTOX_LAST_POSN_TIME | TIMESTAMP | No | — | RDB$5502 | — |
| 11 | CTOX_POLL_POSITIONS | INTEGER | No | — | RDB$5503 | — |
| 12 | CTOX_POLL_HOS_TOTALS | SMALLINT | No | — | RDB$5519 | — |
| 13 | CTOX_POLL_HOS_LOGS | SMALLINT | No | — | RDB$5520 | — |
| 14 | CTOX_LAST_HOS_TOTALS_TIME | TIMESTAMP | No | — | RDB$5521 | — |
| 16 | CTOX_LAST_HOS_LOGS_TIME | TIMESTAMP | No | — | RDB$5523 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_CT_OMNITRACS_XRS | CTOX_KEY | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
