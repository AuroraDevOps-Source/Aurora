# CT_COUNTRIES

Locations and routing. Ct Countries records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTC_CODE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTC_CODE | VARCHAR(4) | Yes | — | D_COUNTRY_CODE | NONE/NONE |
| 2 | CTC_COUNTRY_NAME | VARCHAR(50) | No | — | D_DESC | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CT_COUNTRIES_PRIMARY | CTC_CODE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R002 | ACCOUNT.(AC_COUNTRY_CD) | CT_COUNTRIES.(CTC_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R055 | CT_STATE.(CTST_COUNTRY) | CT_COUNTRIES.(CTC_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
