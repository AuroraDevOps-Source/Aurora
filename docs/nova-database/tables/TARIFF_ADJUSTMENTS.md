# TARIFF_ADJUSTMENTS

Pricing and tariffs. Tariff Adjustments records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (TA_TARIFF_ID, TA_EFF_DT, TA_FROM, TA_TO, TA_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | TA_TARIFF_ID | VARCHAR(6) | Yes | — | RDB$3183 | NONE/NONE |
| 2 | TA_EFF_DT | DATE | Yes | — | RDB$3184 | — |
| 3 | TA_FROM | VARCHAR(6) | Yes | — | RDB$3185 | NONE/NONE |
| 4 | TA_TO | VARCHAR(6) | Yes | — | RDB$3186 | NONE/NONE |
| 5 | TA_SEQ | SMALLINT | Yes | — | RDB$3187 | — |
| 6 | TA_ADJ_TYPE | VARCHAR(1) | No | — | RDB$3188 | NONE/NONE |
| 7 | TA_MIN | NUMERIC(7,4) | No | — | RDB$3189 | — |
| 8 | TA_L5C | NUMERIC(7,4) | No | — | RDB$3190 | — |
| 9 | TA_5C | NUMERIC(7,4) | No | — | RDB$3191 | — |
| 10 | TA_1M | NUMERIC(7,4) | No | — | RDB$3192 | — |
| 11 | TA_2M | NUMERIC(7,4) | No | — | RDB$3193 | — |
| 12 | TA_5M | NUMERIC(7,4) | No | — | RDB$3194 | — |
| 13 | TA_10M | NUMERIC(7,4) | No | — | RDB$3195 | — |
| 14 | TA_20M | NUMERIC(7,4) | No | — | RDB$3196 | — |
| 15 | TA_30M | NUMERIC(7,4) | No | — | RDB$3197 | — |
| 16 | TA_40M | NUMERIC(7,4) | No | — | RDB$3198 | — |
| 17 | TA_50M | NUMERIC(7,4) | No | — | RDB$3199 | — |
| 18 | TA_99M | NUMERIC(7,4) | No | — | RDB$3200 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_TARIFF_ADJUST_KEY | TA_TARIFF_ID, TA_EFF_DT, TA_FROM, TA_TO, TA_SEQ | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R260 | TARIFF_ADJUSTMENTS.(TA_TARIFF_ID, TA_EFF_DT) | TARIFF_CONTROL.(TC_TARIFF_ID, TC_EFF_DT) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
