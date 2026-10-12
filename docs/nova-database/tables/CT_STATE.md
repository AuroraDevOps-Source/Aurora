# CT_STATE

Locations and routing. Ct State records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTST_CODE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTST_CODE | VARCHAR(2) | Yes | — | D_STATE | NONE/NONE |
| 2 | CTST_NAME | VARCHAR(50) | Yes | — | D_DESC | NONE/NONE |
| 3 | CTST_LOW_ZIP | VARCHAR(3) | No | — | RDB$39 | NONE/NONE |
| 4 | CTST_HIGH_ZIP | VARCHAR(3) | No | — | RDB$40 | NONE/NONE |
| 5 | CTST_COUNTRY | VARCHAR(4) | No | — | D_COUNTRY_CODE | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CTSTATE_NAME_KEY | CTST_NAME | No | Yes | ASC | — |
| CTSTATE_PRIM_KEY | CTST_CODE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R008 | ACCOUNT.(AC_STATE) | CT_STATE.(CTST_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R036 | CA_OPER_STATES.(CAOS_STATE) | CT_STATE.(CTST_CODE) | Low | Column roles and key structure; inferred, not data-validated.  Storage type/scale differs; verify conversion and identifier semantics before joining. |
| R055 | CT_STATE.(CTST_COUNTRY) | CT_COUNTRIES.(CTC_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R140 | IFTA_RATES.(IFTA_STATE) | CT_STATE.(CTST_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R149 | LOCMAST.(LM_STATE) | CT_STATE.(CTST_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
