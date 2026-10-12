# PRICE_TB

Pricing and tariffs. Price Tb records; grouped by naming and column context, business interpretation unconfirmed.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (PTB_REC_NUM). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | PTB_ACCOUNT | VARCHAR(12) | Yes | — | RDB$1993 | NONE/NONE |
| 2 | PTB_EFF_DATE | DATE | Yes | — | RDB$1994 | — |
| 3 | PTB_EXP_DATE | DATE | Yes | — | RDB$1995 | — |
| 4 | PTB_REC_NUM | BIGINT | Yes | — | RDB$1996 | — |
| 5 | PTB_PALLET | VARCHAR(6) | No | — | RDB$1997 | NONE/NONE |
| 6 | PTB_PALLET_RULE | CHAR(1) | No | — | RDB$1998 | NONE/NONE |
| 7 | PTB_AF_PU_TAR | VARCHAR(6) | No | — | RDB$1999 | NONE/NONE |
| 8 | PTB_AF_PUD_TAR | VARCHAR(6) | No | — | RDB$2000 | NONE/NONE |
| 9 | PTB_AF_LH | VARCHAR(6) | No | — | RDB$2001 | NONE/NONE |
| 10 | PTB_AF_DOD_TAR | VARCHAR(6) | No | — | RDB$2002 | NONE/NONE |
| 11 | PTB_AF_DLV | VARCHAR(6) | No | — | RDB$2003 | NONE/NONE |
| 12 | PTB_AF_DOR_DOR | VARCHAR(6) | No | — | RDB$2004 | NONE/NONE |
| 13 | PTB_AF_AIR_PIVOT | CHAR(1) | No | — | RDB$2005 | NONE/NONE |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| PTB_CUST | PTB_ACCOUNT, PTB_EFF_DATE, PTB_EXP_DATE | No | Yes | ASC | — |
| PTB_KEY | PTB_REC_NUM | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R253 | PRICE_TB.(PTB_ACCOUNT) | ACCOUNT.(AC_CUST) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R256 | PTB_INTRA.(PTBI_REC_NUM) | PRICE_TB.(PTB_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R257 | PTB_TAR_DISC.(PTBTD_REC_NUM) | PRICE_TB.(PTB_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R258 | PTB_TAR_PRIO.(PTBT_REC_NUM) | PRICE_TB.(PTB_REC_NUM) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

None.

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
