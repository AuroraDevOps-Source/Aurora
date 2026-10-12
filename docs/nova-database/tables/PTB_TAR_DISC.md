# PTB_TAR_DISC

Pricing and tariffs. Ptb Tar Disc records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PTBTD_REC_NUM, PTBTD_SEQ_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PTBTD_REC_NUM | BIGINT | Yes | — | RDB$2017 | — |
| 2 | PTBTD_SEQ_NUM | SMALLINT | Yes | — | RDB$2018 | — |
| 3 | PTBTD_TAR | VARCHAR(6) | No | — | RDB$2019 | NONE/NONE |
| 4 | PTBTD_PERC | NUMERIC(6,4) | No | — | RDB$2020 | — |
| 5 | PTBTM_PERC | NUMERIC(6,4) | No | — | RDB$2021 | — |
| 6 | PTBTTL_PERC | NUMERIC(6,4) | No | — | RDB$2022 | — |
| 7 | PTBTD_MIN | NUMERIC(9,2) | No | — | RDB$2023 | — |
| 8 | PTBTD_RULE | CHAR(1) | No | — | RDB$2024 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PTBTD_KEY | PTBTD_REC_NUM, PTBTD_SEQ_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R257 | PTB_TAR_DISC.(PTBTD_REC_NUM) | PRICE_TB.(PTB_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
