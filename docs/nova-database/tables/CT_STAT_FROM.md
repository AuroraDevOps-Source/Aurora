# CT_STAT_FROM

Codes and configuration. Ct Stat From records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTSF_ID, CTSF_FROM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTSF_ID | BIGINT | Yes | — | D_SYSID | — |
| 2 | CTSF_FROM | BIGINT | Yes | — | D_SYSID | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CTSF_PRIM_KEY | CTSF_ID, CTSF_FROM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R056 | CT_STAT_FROM.(CTSF_FROM) | CT_STAT.(CTS_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R057 | CT_STAT_FROM.(CTSF_ID) | CT_STAT.(CTS_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
