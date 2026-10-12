# AC_AGENT_SRV_POINTS

Accounts and carriers. Ac Agent Srv Points records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (ICAS_REC_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | ICAS_AGENT_ID | VARCHAR(12) | Yes | — | RDB$2535 | NONE/NONE |
| 2 | ICAS_SERVICE_POINT | VARCHAR(6) | No | — | RDB$2536 | NONE/NONE |
| 3 | ICAS_WORK_CODE | VARCHAR(2) | No | — | RDB$2537 | NONE/NONE |
| 4 | ICAS_REC_NUM | NUMERIC(18,0) | Yes | — | RDB$2538 | — |
| 5 | ICAS_COMPUTE_TYPE | CHAR(1) | No | — | RDB$2539 | NONE/NONE |
| 6 | ICAS_AMOUNT | NUMERIC(9,2) | No | — | RDB$2540 | — |
| 7 | ICAS_PERCENT | NUMERIC(6,4) | No | — | RDB$2541 | — |
| 8 | ICAS_TARIFF | VARCHAR(6) | No | — | RDB$2542 | NONE/NONE |
| 9 | ICAS_MINIMUM | NUMERIC(9,2) | No | — | RDB$2543 | — |
| 10 | ICAS_MAXIMUM | NUMERIC(9,2) | No | — | RDB$2544 | — |
| 11 | ICAS_CONTRACT | VARCHAR(12) | No | — | RDB$2545 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| ICAS_SRV_PNT | ICAS_AGENT_ID, ICAS_SERVICE_POINT, ICAS_WORK_CODE | No | Yes | ASC | — |
| ICA_SRV_KEY | ICAS_REC_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R015 | AC_AGENT_SRV_POINTS.(ICAS_AGENT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
