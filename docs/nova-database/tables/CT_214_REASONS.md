# CT_214_REASONS

Codes and configuration. Ct 214 Reasons records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CT2R_CODE). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CT2R_CODE | VARCHAR(2) | Yes | — | RDB$3674 | NONE/NONE |
| 2 | CT2R_DESC | VARCHAR(50) | No | — | D_DESC | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PK_CT_214_REASONS | CT2R_CODE | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R182 | OPS_HEADER.(PB_EDI_REASON) | CT_214_REASONS.(CT2R_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R222 | OPS_STOP_REC.(OPS_EDI_REASON) | CT_214_REASONS.(CT2R_CODE) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
