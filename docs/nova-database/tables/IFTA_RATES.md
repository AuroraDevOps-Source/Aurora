# IFTA_RATES

Equipment and fuel. Ifta Rates records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (IFTA_STATE, IFTA_EFF_DATE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | IFTA_STATE | VARCHAR(2) | Yes | — | RDB$6866 | NONE/NONE |
| 2 | IFTA_EFF_DATE | DATE | Yes | — | RDB$6867 | — |
| 3 | IFTA_EXP_DATE | DATE | No | — | RDB$6868 | — |
| 4 | IFTA_DIESEL | NUMERIC(16,4) | No | — | RDB$6869 | — |
| 5 | IFTA_GASOLINE | NUMERIC(16,4) | No | — | RDB$6870 | — |
| 6 | IFTA_BLEND | NUMERIC(16,4) | No | — | RDB$6871 | — |
| 7 | IFTA_PROPANE | NUMERIC(16,4) | No | — | RDB$6872 | — |
| 8 | IFTA_LNG | NUMERIC(16,4) | No | — | RDB$6873 | — |
| 9 | IFTA_CNG | NUMERIC(16,4) | No | — | RDB$6874 | — |
| 10 | IFTA_HYDROGEN | NUMERIC(16,4) | No | — | RDB$6875 | — |
| 11 | IFTA_ELECTRICITY | NUMERIC(16,4) | No | — | RDB$6876 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| IFTA_KEY | IFTA_STATE, IFTA_EFF_DATE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R140 | IFTA_RATES.(IFTA_STATE) | CT_STATE.(CTST_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
