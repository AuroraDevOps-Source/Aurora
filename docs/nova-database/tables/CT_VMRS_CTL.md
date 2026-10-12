# CT_VMRS_CTL

Codes and configuration. Ct Vmrs Ctl records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (CTVC_KEY). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CTVC_KEY | VARCHAR(10) | Yes | — | RDB$5007 | NONE/NONE |
| 2 | CTVC_COMP_LABOR_RATE | NUMERIC(10,2) | No | — | RDB$5008 | — |
| 3 | CTVC_OUTSIDE_LABOR_RATE | NUMERIC(10,2) | No | — | RDB$5009 | — |
| 4 | CTVC_RO_BILL_LABOR_KW | VARCHAR(8) | No | — | RDB$5010 | NONE/NONE |
| 5 | CTVC_RO_BILL_PARTS_KW | VARCHAR(8) | No | — | RDB$5011 | NONE/NONE |
| 6 | CTVC_COMP_AR_KW | VARCHAR(8) | No | — | RDB$5012 | NONE/NONE |
| 7 | CTVC_OUT_AR_KW | VARCHAR(8) | No | — | RDB$5013 | NONE/NONE |
| 8 | CTVC_DFLT_PO_SHIPID | VARCHAR(12) | No | — | RDB$5014 | NONE/NONE |
| 9 | CTVC_DFLT_PO_BILLTOID | VARCHAR(12) | No | — | RDB$5015 | NONE/NONE |
| 10 | CTVC_PO_AR_KW | VARCHAR(8) | No | — | RDB$5016 | NONE/NONE |
| 11 | CTVC_SUPV_EMAIL | VARCHAR(150) | No | — | D_EMAIL | NONE/NONE |
| 12 | CTVC_RO_BILL_OUTSIDE_KW | VARCHAR(8) | No | — | RDB$5618 | NONE/NONE |
| 13 | CTVC_RO_BILL_TAX_KW | VARCHAR(8) | No | — | RDB$5622 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| CT_VMRS_CTL_KEY | CTVC_KEY | Yes | Yes | ASC | — |

## Relationships

No relationship established in this mapping. This does not prove the table is independent.

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
