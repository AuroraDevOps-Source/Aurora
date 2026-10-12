# COMOD_TARIFF_RATE

Pricing and tariffs. Comod Tariff Rate records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTR_TARIFF_ID, CTR_KEY_1, CTR_KEY_2, CTR_KEY_3, CTR_KEY_4, CTR_KW, CTR_EFF_DATE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTR_TARIFF_ID | VARCHAR(6) | Yes | — | RDB$3541 | NONE/NONE |
| 2 | CTR_KEY_1 | VARCHAR(40) | Yes | — | RDB$3542 | NONE/NONE |
| 3 | CTR_KEY_2 | VARCHAR(40) | Yes | — | RDB$3543 | NONE/NONE |
| 4 | CTR_KEY_3 | VARCHAR(40) | Yes | — | RDB$3544 | NONE/NONE |
| 5 | CTR_KEY_4 | VARCHAR(40) | Yes | — | RDB$3545 | NONE/NONE |
| 6 | CTR_KW | VARCHAR(8) | Yes | — | RDB$3546 | NONE/NONE |
| 7 | CTR_EFF_DATE | DATE | Yes | — | RDB$3547 | — |
| 8 | CTR_EXP_DATE | DATE | Yes | — | RDB$3548 | — |
| 9 | CTR_RATE | NUMERIC(10,5) | No | — | RDB$3549 | — |
| 10 | CTR_COMPUTE | VARCHAR(1) | No | — | RDB$3550 | NONE/NONE |
| 11 | CTR_TABLE_ID | VARCHAR(8) | No | — | RDB$3551 | NONE/NONE |
| 12 | CTR_MIN | NUMERIC(6,2) | No | — | RDB$3552 | — |
| 13 | CTR_MAX | NUMERIC(6,2) | No | — | RDB$3553 | — |
| 14 | CTR_PAY_REC_NUM | BIGINT | No | — | RDB$3554 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_COMOD_TARIFF_RATE_KEY | CTR_TARIFF_ID, CTR_KEY_1, CTR_KEY_2, CTR_KEY_3, CTR_KEY_4, CTR_KW, CTR_EFF_DATE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R043 | COMOD_PAY_DATA.(CTP_REC_NUM) | COMOD_TARIFF_RATE.(CTR_PAY_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  Referenced fields have no exact active unique index; multiple matches are possible. |
| R046 | COMOD_TARIFF_RATE.(CTR_TARIFF_ID) | COMOD_TARIFF_CONTROL.(CT_TARIFF_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
