# ISAPI_QUEUE

Integration and reference. Isapi Queue records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (IQ_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | IQ_ID | BIGINT | Yes | — | RDB$6950 | — |
| 2 | IQ_REQ_TIME | TIMESTAMP | No | — | RDB$6951 | — |
| 3 | IQ_REQ_TYPE | VARCHAR(12) | No | — | RDB$6952 | NONE/NONE |
| 4 | IQ_REQ_CONTENT | BLOB SUB_TYPE 1 | No | — | RDB$6953 | NONE/NONE |
| 5 | IQ_REQ_CONTENT_TYPE | VARCHAR(80) | No | — | RDB$6954 | NONE/NONE |
| 6 | IQ_REQ_QUERY | VARCHAR(255) | No | — | RDB$6955 | NONE/NONE |
| 7 | IQ_RESP_CONTENT | BLOB SUB_TYPE 1 | No | — | RDB$6956 | NONE/NONE |
| 8 | IQ_RESP_CONTENT_TYPE | VARCHAR(80) | No | — | RDB$6957 | NONE/NONE |
| 9 | IQ_RESP_CODE | INTEGER | No | — | RDB$6958 | — |
| 10 | IQ_RESP_MSG | VARCHAR(80) | No | — | RDB$6959 | NONE/NONE |
| 11 | IQ_DATA_PROVIDER | VARCHAR(80) | No | — | RDB$6960 | NONE/NONE |
| 12 | IQ_PROCESSED_TIME | TIMESTAMP | No | — | RDB$6961 | — |
| 13 | IQ_REQUEST_KEY | VARCHAR(34) | No | — | RDB$6963 | NONE/NONE |
| 14 | IQ_ORDER_NUMBER | BIGINT | No | — | RDB$7162 | — |
| 15 | IQ_EXT_REF | VARCHAR(200) | No | — | RDB$7163 | NONE/NONE |
| 16 | IQ_PROCESSED_MESSAGE | VARCHAR(255) | No | — | RDB$7164 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| IDX_ISAPI_QUEUE_PROCESSED | IQ_PROCESSED_TIME | No | Yes | ASC | — |
| PK_ISAPI_QUEUE | IQ_ID | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

`ISAPI_QUEUE_AFTER_INSERT`, `ISAPI_QUEUE_BEFORE_INSERT`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
