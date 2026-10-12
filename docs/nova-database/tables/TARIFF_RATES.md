# TARIFF_RATES

Pricing and tariffs. Tariff Rates records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (TR_TARIFF_ID, TR_EFF_DT, TR_RATE_BASE, TR_CLASS). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | TR_TARIFF_ID | VARCHAR(6) | Yes | — | RDB$3159 | NONE/NONE |
| 2 | TR_EFF_DT | DATE | Yes | — | RDB$3160 | — |
| 3 | TR_RATE_BASE | INTEGER | Yes | — | RDB$3161 | — |
| 4 | TR_CLASS | VARCHAR(4) | Yes | — | RDB$3162 | NONE/NONE |
| 5 | TR_MIN_1 | NUMERIC(6,2) | No | — | RDB$3163 | — |
| 6 | TR_MIN_2 | NUMERIC(6,2) | No | — | RDB$3164 | — |
| 7 | TR_MIN_3 | NUMERIC(6,2) | No | — | RDB$3165 | — |
| 8 | TR_MIN_4 | NUMERIC(6,2) | No | — | RDB$3166 | — |
| 9 | TR_MIN_5 | NUMERIC(6,2) | No | — | RDB$3167 | — |
| 10 | TR_MIN_6 | NUMERIC(6,2) | No | — | RDB$3168 | — |
| 11 | TR_MAX_1 | NUMERIC(6,2) | No | — | RDB$3169 | — |
| 12 | TR_MAX_2 | NUMERIC(6,2) | No | — | RDB$3170 | — |
| 13 | TR_MAX_3 | NUMERIC(6,2) | No | — | RDB$3171 | — |
| 14 | TR_L5C | NUMERIC(9,4) | No | — | RDB$3172 | — |
| 15 | TR_5C | NUMERIC(9,4) | No | — | RDB$3173 | — |
| 16 | TR_1M | NUMERIC(9,4) | No | — | RDB$3174 | — |
| 17 | TR_2M | NUMERIC(9,4) | No | — | RDB$3175 | — |
| 18 | TR_5M | NUMERIC(9,4) | No | — | RDB$3176 | — |
| 19 | TR_10M | NUMERIC(9,4) | No | — | RDB$3177 | — |
| 20 | TR_20M | NUMERIC(9,4) | No | — | RDB$3178 | — |
| 21 | TR_30M | NUMERIC(9,4) | No | — | RDB$3179 | — |
| 22 | TR_40M | NUMERIC(9,4) | No | — | RDB$3180 | — |
| 23 | TR_50M | NUMERIC(9,4) | No | — | RDB$3181 | — |
| 24 | TR_99M | NUMERIC(9,4) | No | — | RDB$3182 | — |
| 25 | TR_12RATE | NUMERIC(9,4) | No | — | RDB$5598 | — |
| 26 | TR_13RATE | NUMERIC(9,4) | No | — | RDB$5599 | — |
| 27 | TR_14RATE | NUMERIC(9,4) | No | — | RDB$5600 | — |
| 28 | TR_15RATE | NUMERIC(9,4) | No | — | RDB$5601 | — |
| 29 | TR_16RATE | NUMERIC(9,4) | No | — | RDB$5602 | — |
| 30 | TR_17RATE | NUMERIC(9,4) | No | — | RDB$5603 | — |
| 31 | TR_18RATE | NUMERIC(9,4) | No | — | RDB$5604 | — |
| 32 | TR_19RATE | NUMERIC(9,4) | No | — | RDB$5605 | — |
| 33 | TR_20RATE | NUMERIC(9,4) | No | — | RDB$5606 | — |
| 34 | TR_WGT_QUAL1 | VARCHAR(1) | No | — | RDB$4598 | NONE/NONE |
| 35 | TR_WGT_QUAL2 | VARCHAR(1) | No | — | RDB$4599 | NONE/NONE |
| 36 | TR_WGT_QUAL3 | VARCHAR(1) | No | — | RDB$4600 | NONE/NONE |
| 37 | TR_WGT_QUAL4 | VARCHAR(1) | No | — | RDB$4601 | NONE/NONE |
| 38 | TR_WGT_QUAL5 | VARCHAR(1) | No | — | RDB$4602 | NONE/NONE |
| 39 | TR_WGT_QUAL6 | VARCHAR(1) | No | — | RDB$4603 | NONE/NONE |
| 40 | TR_WGT_QUAL7 | VARCHAR(1) | No | — | RDB$4604 | NONE/NONE |
| 41 | TR_WGT_QUAL8 | VARCHAR(1) | No | — | RDB$4605 | NONE/NONE |
| 42 | TR_WGT_QUAL9 | VARCHAR(1) | No | — | RDB$4606 | NONE/NONE |
| 43 | TR_WGT_QUAL10 | VARCHAR(1) | No | — | RDB$4607 | NONE/NONE |
| 44 | TR_WGT_QUAL11 | VARCHAR(1) | No | — | RDB$4608 | NONE/NONE |
| 45 | TR_WGT_QUAL12 | VARCHAR(1) | No | — | RDB$4609 | NONE/NONE |
| 46 | TR_WGT_QUAL13 | VARCHAR(1) | No | — | RDB$5607 | NONE/NONE |
| 47 | TR_WGT_QUAL14 | VARCHAR(1) | No | — | RDB$5608 | NONE/NONE |
| 48 | TR_WGT_QUAL15 | VARCHAR(1) | No | — | RDB$5609 | NONE/NONE |
| 49 | TR_WGT_QUAL16 | VARCHAR(1) | No | — | RDB$5610 | NONE/NONE |
| 50 | TR_WGT_QUAL17 | VARCHAR(1) | No | — | RDB$5611 | NONE/NONE |
| 51 | TR_WGT_QUAL18 | VARCHAR(1) | No | — | RDB$5612 | NONE/NONE |
| 52 | TR_WGT_QUAL19 | VARCHAR(1) | No | — | RDB$5613 | NONE/NONE |
| 53 | TR_WGT_QUAL20 | VARCHAR(1) | No | — | RDB$5614 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_TARIFF_RATES_KEY | TR_TARIFF_ID, TR_EFF_DT, TR_RATE_BASE, TR_CLASS | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R265 | TARIFF_RATES.(TR_TARIFF_ID, TR_EFF_DT) | TARIFF_CONTROL.(TC_TARIFF_ID, TC_EFF_DT) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
