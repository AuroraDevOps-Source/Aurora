# CA_HISTORY

Accounts and carriers. Ca History records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CAH_ACCT_ID, CAH_ORDER_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 5 | CAH_SHIP_DATE | DATE | No | — | RDB$5433 | — |
| 6 | CAH_ORIGIN_CITY | VARCHAR(22) | No | — | RDB$5434 | NONE/NONE |
| 7 | CAH_ORIGIN_STATE | CHAR(2) | No | — | RDB$5435 | NONE/NONE |
| 8 | CAH_DEST_CITY | VARCHAR(22) | No | — | RDB$5436 | NONE/NONE |
| 9 | CAH_DEST_STATE | CHAR(2) | No | — | RDB$5437 | NONE/NONE |
| 10 | CAH_TTL_REVENUE | DECIMAL(9,2) | No | — | RDB$5438 | — |
| 11 | CAH_BROKER_AMT | DECIMAL(9,2) | No | — | RDB$5439 | — |
| 12 | CAH_NUM_STOPS | SMALLINT | No | — | RDB$5440 | — |
| 13 | CAH_DIRECT_MILES | INTEGER | No | — | RDB$5441 | — |
| 14 | CAH_OFF_ROUTE_MILES | INTEGER | No | — | RDB$5442 | — |
| 15 | CAH_ACCT_ID | VARCHAR(12) | Yes | — | D_ACCT_ID | NONE/NONE |
| 16 | CAH_ORDER_ID | INTEGER | Yes | — | RDB$5539 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CAH_ORIG_DEST_KEY | CAH_ORIGIN_STATE, CAH_ORIGIN_CITY, CAH_DEST_STATE, CAH_DEST_CITY, CAH_SHIP_DATE | No | Yes | ASC | — |
| CAH_SHIP_DATE_KEY | CAH_SHIP_DATE | No | Yes | ASC | — |
| PK_CA_HISTORY | CAH_ACCT_ID, CAH_ORDER_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R031 | CA_HISTORY.(CAH_ACCT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R032 | CA_HISTORY.(CAH_ORDER_ID) | OPS_HEADER.(PB_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R033 | CA_HISTORY.(CAH_ORDER_ID, CAH_ACCT_ID) | OPS_AGENTS.(OPA_ORDER_ID, OPA_AGENT_ID) | High | Trigger OPS_AGENTS_AFTER_DELETE explicitly deletes matching child rows Delete cleanup is not insert/update referential validation. Referenced fields have no exact active unique index; multiple matches are possible. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
