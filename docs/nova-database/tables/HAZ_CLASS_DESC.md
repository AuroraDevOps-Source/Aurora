# HAZ_CLASS_DESC

Integration and reference. Haz Class Desc records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (HAZC_CLASS, HAZC_DIVISION). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | HAZC_CLASS | SMALLINT | Yes | — | RDB$3201 | — |
| 2 | HAZC_DIVISION | SMALLINT | Yes | — | RDB$3202 | — |
| 3 | HAZC_CLASS_DESC | VARCHAR(78) | No | — | RDB$3203 | NONE/NONE |
| 4 | HAZC_DIV_DESC1 | VARCHAR(78) | No | — | RDB$3204 | NONE/NONE |
| 5 | HAZC_DIV_DESC2 | VARCHAR(78) | No | — | RDB$3205 | NONE/NONE |
| 6 | HAZC_MATERIAL_CLASS | VARCHAR(78) | No | — | RDB$3206 | NONE/NONE |
| 7 | HAZC_LABEL | VARCHAR(78) | No | — | RDB$3207 | NONE/NONE |
| 8 | HAZC_PLACARD | VARCHAR(40) | No | — | RDB$3208 | NONE/NONE |
| 9 | HAZC_QTY | VARCHAR(30) | No | — | RDB$3209 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| HAZC_KEY | HAZC_CLASS, HAZC_DIVISION | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R138 | HAZ.(HAZ_IMO_CLASS_NBR, HAZ_IMO_DIV_NBR) | HAZ_CLASS_DESC.(HAZC_CLASS, HAZC_DIVISION) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
