# HAZ

Integration and reference. Haz records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (HAZ_ID, HAZ_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | HAZ_ID | VARCHAR(6) | Yes | — | RDB$2860 | NONE/NONE |
| 2 | HAZ_SEQ | SMALLINT | Yes | — | RDB$2861 | — |
| 3 | HAZ_DESC | VARCHAR(36) | No | — | RDB$2862 | NONE/NONE |
| 4 | HAZ_IMO_CLASS_NBR | NUMERIC(2,0) | No | — | RDB$2863 | — |
| 5 | HAZ_IMO_DIV_NBR | NUMERIC(2,0) | No | — | RDB$2864 | — |
| 6 | HAZ_EXCEPTIONS | NUMERIC(7,3) | No | — | RDB$2865 | — |
| 7 | HAZ_SPECIAL_REQUEST | NUMERIC(7,3) | No | — | RDB$2866 | — |
| 8 | HAZ_GUIDE_NBR | NUMERIC(2,0) | No | — | RDB$2867 | — |
| 9 | HAZ_PACKAGE_GROUP | VARCHAR(3) | No | — | RDB$2868 | NONE/NONE |
| 10 | HAZ_EXPLOSIVE_GROUP | VARCHAR(1) | No | — | RDB$2869 | NONE/NONE |
| 11 | HAZ_SUB_HAZ_CLASS_1 | NUMERIC(3,1) | No | — | RDB$2870 | — |
| 12 | HAZ_SUB_HAZ_CLASS_2 | NUMERIC(3,1) | No | — | RDB$2871 | — |
| 13 | HAZ_SUB_HAZ_CLASS_3 | NUMERIC(3,1) | No | — | RDB$2872 | — |
| 14 | HAZ_DESC_REC_NUM | BIGINT | Yes | — | RDB$2873 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| HAZ_INFO | HAZ_DESC | No | Yes | ASC | — |
| HAZ_KEY | HAZ_ID, HAZ_SEQ | Yes | Yes | ASC | — |
| HAZ_REC_NUM_KEY | HAZ_DESC_REC_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R138 | HAZ.(HAZ_IMO_CLASS_NBR, HAZ_IMO_DIV_NBR) | HAZ_CLASS_DESC.(HAZC_CLASS, HAZC_DIVISION) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R139 | HAZ_DESC_DATA.(HAZD_REC_NUM) | HAZ.(HAZ_DESC_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`HAZ_BEFORE_UPDATE`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
