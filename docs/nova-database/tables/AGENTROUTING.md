# AGENTROUTING

Accounts and carriers. Agentrouting records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (AGR_TYPE, AGR_FIELD1, AGR_FIELD2). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | AGR_TYPE | VARCHAR(2) | Yes | — | RDB$6940 | NONE/NONE |
| 2 | AGR_FIELD1 | VARCHAR(12) | Yes | — | RDB$6941 | NONE/NONE |
| 3 | AGR_FIELD2 | VARCHAR(12) | Yes | — | RDB$6942 | NONE/NONE |
| 4 | AGR_AGENT1_ID | VARCHAR(12) | No | — | RDB$6943 | NONE/NONE |
| 5 | AGR_AGENT1_XDOCK | VARCHAR(1) | No | — | RDB$6944 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| AGENTROUTING_KEY | AGR_TYPE, AGR_FIELD1, AGR_FIELD2 | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R025 | AGENTROUTING.(AGR_AGENT1_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
