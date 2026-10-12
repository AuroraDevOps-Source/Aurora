# COMOD_TARIFF_TABLE

Pricing and tariffs. Comod Tariff Table records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTT_TARIFF_ID, CTT_TABLE_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTT_TARIFF_ID | VARCHAR(6) | Yes | — | RDB$3508 | NONE/NONE |
| 2 | CTT_TABLE_ID | VARCHAR(8) | Yes | — | RDB$3509 | NONE/NONE |
| 3 | CTT_TABLE_TYPE | VARCHAR(1) | No | — | RDB$3510 | NONE/NONE |
| 4 | CTT_TABLE_REC_NUM | BIGINT | No | — | RDB$3511 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_COMOD_TARIFF_TABLE_KEY | CTT_TARIFF_ID, CTT_TABLE_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R044 | COMOD_TABLE_DATA.(CTD_REC_NUM) | COMOD_TARIFF_TABLE.(CTT_TABLE_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  Referenced fields have no exact active unique index; multiple matches are possible. |
| R047 | COMOD_TARIFF_TABLE.(CTT_TARIFF_ID) | COMOD_TARIFF_CONTROL.(CT_TARIFF_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`COMOD_TARIFF_TABLE_BEFORE`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
