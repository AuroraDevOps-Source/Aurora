# COMOD_TARIFF_CONTROL

Pricing and tariffs. Comod Tariff Control records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CT_TARIFF_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CT_TARIFF_ID | VARCHAR(6) | Yes | — | RDB$3486 | NONE/NONE |
| 2 | CT_KEY_1 | VARCHAR(4) | Yes | — | RDB$3487 | NONE/NONE |
| 3 | CT_KEY_2 | VARCHAR(4) | Yes | — | RDB$3488 | NONE/NONE |
| 4 | CT_KEY_3 | VARCHAR(4) | Yes | — | RDB$3489 | NONE/NONE |
| 5 | CT_KEY_4 | VARCHAR(4) | Yes | — | RDB$3490 | NONE/NONE |
| 6 | CT_GROUPING | VARCHAR(1) | No | — | RDB$3491 | NONE/NONE |
| 7 | CT_GROUP_TYPE | VARCHAR(4) | No | — | RDB$3492 | NONE/NONE |
| 8 | CT_MAXWGT_RULE | VARCHAR(1) | No | — | RDB$3493 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_COMOD_TARIFF_CONTROL_KEY | CT_TARIFF_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R045 | COMOD_TARIFF_GROUPING.(CTG_TARIFF_ID) | COMOD_TARIFF_CONTROL.(CT_TARIFF_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R046 | COMOD_TARIFF_RATE.(CTR_TARIFF_ID) | COMOD_TARIFF_CONTROL.(CT_TARIFF_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R047 | COMOD_TARIFF_TABLE.(CTT_TARIFF_ID) | COMOD_TARIFF_CONTROL.(CT_TARIFF_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
