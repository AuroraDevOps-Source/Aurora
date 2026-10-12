# CODFEE

Pricing and tariffs. Codfee records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CODFEE_TABLE_ID, CODFEE_EFF_DATE, CODFEE_SEQ). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CODFEE_TABLE_ID | VARCHAR(6) | Yes | — | RDB$3210 | NONE/NONE |
| 2 | CODFEE_EFF_DATE | DATE | Yes | — | RDB$3211 | — |
| 3 | CODFEE_EXP_DATE | DATE | Yes | — | RDB$3212 | — |
| 4 | CODFEE_SEQ | NUMERIC(5,0) | Yes | — | RDB$3213 | — |
| 5 | CODFEE_AMOUNT | NUMERIC(9,2) | No | — | RDB$3214 | — |
| 6 | CODFEE_TYPE | VARCHAR(1) | No | — | RDB$3215 | NONE/NONE |
| 7 | CODFEE_FEE | NUMERIC(9,2) | No | — | RDB$3216 | — |
| 8 | CODFEE_PERC | NUMERIC(6,4) | No | — | RDB$3217 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CODFEE_KEY | CODFEE_TABLE_ID, CODFEE_EFF_DATE, CODFEE_SEQ | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
