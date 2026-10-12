# AC_AGENT_CONTRACTS

Accounts and carriers. Ac Agent Contracts records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (ICAC_CONTRACT_ID, ICAC_SEQ_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | ICAC_CONTRACT_ID | VARCHAR(12) | Yes | — | RDB$2648 | NONE/NONE |
| 2 | ICAC_KEYWORD | VARCHAR(8) | No | — | RDB$2649 | NONE/NONE |
| 3 | ICAC_WORK_CODE | CHAR(2) | No | — | RDB$2650 | NONE/NONE |
| 4 | ICAC_SEQ_NUM | SMALLINT | Yes | — | RDB$2651 | — |
| 5 | ICAC_COMPUTE_TYPE | CHAR(1) | No | — | RDB$2652 | NONE/NONE |
| 6 | ICAC_PERCENT | NUMERIC(6,4) | No | — | RDB$2653 | — |
| 7 | ICAC_AMOUNT | NUMERIC(9,2) | No | — | RDB$2654 | — |
| 8 | ICAC_MIN | NUMERIC(9,2) | No | — | RDB$6392 | — |
| 9 | ICAC_MAX | NUMERIC(9,2) | No | — | RDB$6393 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| ICAC_CONTRACT_KEY | ICAC_CONTRACT_ID, ICAC_SEQ_NUM | Yes | Yes | ASC | — |
| ICAC_CONTRACT_X | ICAC_CONTRACT_ID, ICAC_KEYWORD, ICAC_WORK_CODE, ICAC_SEQ_NUM | No | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R013 | AC_AGENT_CONTRACTS.(ICAC_CONTRACT_ID) | AC_AGENT.(ICA_CONTRACT_ID) | Low | Shared contract identifier; AC_AGENT has no unique contract key Could be a shared contract definition rather than a child owned by one agent. Referenced fields have no exact active unique index; multiple matches are possible. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
