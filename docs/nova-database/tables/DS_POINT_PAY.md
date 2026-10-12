# DS_POINT_PAY

Drivers and settlement. Ds Point Pay records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (DSPP_ACCT_ID, DSPP_FROM_TYPE, DSPP_FROM_ID, DSPP_TO_TYPE, DSPP_TO_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | DSPP_ACCT_ID | VARCHAR(12) | Yes | — | RDB$6813 | NONE/NONE |
| 2 | DSPP_FROM_TYPE | VARCHAR(2) | Yes | — | RDB$4145 | NONE/NONE |
| 3 | DSPP_FROM_ID | VARCHAR(36) | Yes | — | RDB$4146 | NONE/NONE |
| 4 | DSPP_TO_TYPE | VARCHAR(2) | Yes | — | RDB$4147 | NONE/NONE |
| 5 | DSPP_TO_ID | VARCHAR(36) | Yes | — | RDB$4148 | NONE/NONE |
| 6 | DSPP_AMT1 | NUMERIC(12,4) | No | — | RDB$4149 | — |
| 7 | DSPP_AMT2 | NUMERIC(12,4) | No | — | RDB$4150 | — |
| 8 | DSPP_AMT3 | NUMERIC(12,4) | No | — | RDB$4151 | — |
| 9 | DSPP_AMT4 | NUMERIC(12,4) | No | — | RDB$4152 | — |
| 10 | DSPP_AMT5 | NUMERIC(12,4) | No | — | RDB$4153 | — |
| 11 | DSPP_AMT6 | NUMERIC(12,4) | No | — | RDB$4154 | — |
| 12 | DSPP_AMT7 | NUMERIC(12,4) | No | — | RDB$4155 | — |
| 13 | DSPP_AMT8 | NUMERIC(12,4) | No | — | RDB$4156 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_DS_POINT_PAY | DSPP_ACCT_ID, DSPP_FROM_TYPE, DSPP_FROM_ID, DSPP_TO_TYPE, DSPP_TO_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R094 | DS_POINT_PAY.(DSPP_ACCT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
