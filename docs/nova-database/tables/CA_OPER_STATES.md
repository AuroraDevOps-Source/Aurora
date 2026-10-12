# CA_OPER_STATES

Accounts and carriers. Ca Oper States records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CAOS_ACCT_ID, CAOS_STATE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 2 | CAOS_STATE | CHAR(2) | Yes | — | RDB$5444 | NONE/NONE |
| 3 | CAOS_ACCT_ID | VARCHAR(8) | Yes | — | RDB$5475 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CAOS_STATE_KEY | CAOS_STATE | No | Yes | ASC | — |
| PK_CA_OPER_STATES | CAOS_ACCT_ID, CAOS_STATE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R035 | CA_OPER_STATES.(CAOS_ACCT_ID) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R036 | CA_OPER_STATES.(CAOS_STATE) | CT_STATE.(CTST_CODE) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
