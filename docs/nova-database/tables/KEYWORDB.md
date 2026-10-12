# KEYWORDB

Pricing and tariffs. Charge/rating keyword rules and accounting attributes.

[Back to inventory](../TABLES.md) · [Relationship map](../RELATIONSHIPS.md)

## Keys

Primary key: (KW_ID). No foreign keys declared.

## Columns

| # | Column | SQL type | Not null | Default | Domain | Character set / collation |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | KW_ID | VARCHAR(8) | Yes | — | RDB$1304 | NONE/NONE |
| 2 | KW_PROCESS_NUM | NUMERIC(3,0) | No | — | RDB$1305 | — |
| 3 | KW_PIECES_OPT | VARCHAR(1) | No | — | RDB$1306 | NONE/NONE |
| 4 | KW_DESC_OPT | VARCHAR(1) | No | — | RDB$1307 | NONE/NONE |
| 5 | KW_WEIGHT_OPT | VARCHAR(1) | No | — | RDB$1308 | NONE/NONE |
| 6 | KW_RATE_OPT | VARCHAR(1) | No | — | RDB$1309 | NONE/NONE |
| 7 | KW_AMOUNT_OPT | VARCHAR(1) | No | — | RDB$1310 | NONE/NONE |
| 8 | KW_RATE_TYPE | VARCHAR(1) | No | — | RDB$1311 | NONE/NONE |
| 9 | KW_DEFAULT_SIGN | VARCHAR(1) | No | — | RDB$1312 | NONE/NONE |
| 10 | KW_REPL_DESC | VARCHAR(1) | No | — | RDB$1313 | NONE/NONE |
| 11 | KW_EXTERN_ID | VARCHAR(32) | No | — | RDB$1314 | NONE/NONE |
| 12 | KW_DEL_CD | VARCHAR(1) | No | — | RDB$1315 | NONE/NONE |
| 13 | KW_EXTEND_RATE | VARCHAR(1) | No | — | RDB$1316 | NONE/NONE |
| 14 | KW_GL_CODE | VARCHAR(8) | No | — | RDB$1317 | NONE/NONE |
| 15 | KW_STD_DESC | VARCHAR(36) | No | — | RDB$1318 | NONE/NONE |
| 16 | KW_RATE_QUAL | VARCHAR(4) | No | — | RDB$1319 | NONE/NONE |
| 17 | KW_RATE_CL | VARCHAR(6) | No | — | RDB$1320 | NONE/NONE |
| 18 | KW_CHG_QUAL | VARCHAR(4) | No | — | RDB$1321 | NONE/NONE |
| 19 | KW_HAZARD | VARCHAR(1) | No | — | RDB$1322 | NONE/NONE |
| 20 | KW_CUST_PRT | VARCHAR(1) | No | — | RDB$1323 | NONE/NONE |
| 21 | KW_OPT_TOT | VARCHAR(1) | No | — | RDB$1324 | NONE/NONE |
| 22 | KW_TOT_PIECES | VARCHAR(1) | No | — | RDB$1325 | NONE/NONE |
| 23 | KW_TOT_WEIGHT | VARCHAR(1) | No | — | RDB$1326 | NONE/NONE |
| 24 | KW_TOT_AMT | VARCHAR(1) | No | — | RDB$1327 | NONE/NONE |
| 25 | KW_IL_SPLIT | VARCHAR(1) | No | — | RDB$1328 | NONE/NONE |
| 26 | KW_FSC | VARCHAR(1) | No | — | RDB$1329 | NONE/NONE |
| 27 | KW_DRIVER_PAY | VARCHAR(1) | No | — | RDB$1330 | NONE/NONE |
| 28 | KW_FRT_REV | VARCHAR(1) | No | — | RDB$1331 | NONE/NONE |
| 29 | KW_ICC_REV | VARCHAR(1) | No | — | RDB$1332 | NONE/NONE |
| 30 | KW_ASSC_REV | VARCHAR(1) | No | — | RDB$1333 | NONE/NONE |
| 31 | KW_DISCOUNT | VARCHAR(1) | No | — | RDB$1334 | NONE/NONE |
| 32 | KW_EFF_DATE | DATE | No | — | RDB$1335 | — |
| 33 | KW_COMMODITY | VARCHAR(1) | No | — | RDB$1336 | NONE/NONE |
| 34 | KW_QTY_TYPE | VARCHAR(1) | No | — | RDB$1337 | NONE/NONE |
| 35 | KW_CC | VARCHAR(2) | No | — | RDB$1338 | NONE/NONE |
| 36 | KW_DIV_DR | VARCHAR(4) | No | — | RDB$1339 | NONE/NONE |
| 37 | KW_DEPT_DR | VARCHAR(6) | No | — | RDB$1340 | NONE/NONE |
| 38 | KW_GL_ACCT_DR | VARCHAR(10) | No | — | RDB$1341 | NONE/NONE |
| 39 | KW_DIV_CR | VARCHAR(4) | No | — | RDB$1342 | NONE/NONE |
| 40 | KW_DEPT_CR | VARCHAR(6) | No | — | RDB$1343 | NONE/NONE |
| 41 | KW_GL_ACCT_CR | VARCHAR(10) | No | — | RDB$1344 | NONE/NONE |
| 42 | KW_FORCE_STD_DESC | VARCHAR(1) | No | — | RDB$1345 | NONE/NONE |
| 43 | KW_RATE_AMT | NUMERIC(18,4) | No | — | RDB$1346 | — |
| 44 | KW_MIN_AMT | NUMERIC(18,2) | No | — | RDB$1347 | — |
| 45 | KW_MAX_AMT | NUMERIC(18,2) | No | — | RDB$1348 | — |
| 46 | KW_SHOW_ON_WEB | VARCHAR(1) | No | — | RDB$5382 | NONE/NONE |
| 47 | KW_PASSTHRU_REV | VARCHAR(1) | No | — | RDB$5562 | NONE/NONE |
| 48 | KW_SHOW_ON_DISPATCH | VARCHAR(1) | No | — | RDB$6110 | NONE/NONE |
| 49 | KW_REV_ALLOCATION_IND | VARCHAR(1) | No | — | RDB$6415 | NONE/NONE |
| 50 | KW_SORT_KEY | VARCHAR(8) | No | — | RDB$6477 | NONE/NONE |
| 51 | KW_DR_PRINT | VARCHAR(1) | No | — | RDB$6860 | NONE/NONE |
| 52 | KW_EXCESS_VALUE | NUMERIC(18,2) | No | — | RDB$7230 | — |

## Indexes

| Index | Columns (in order) | Unique | Active | Direction | Expression / predicate |
| --- | --- | --- | --- | --- | --- |
| KW_KEY | KW_ID | Yes | Yes | ASC | — |

## Relationships

| ID | Child columns | Parent columns | Confidence | Evidence / caution |
| --- | --- | --- | --- | --- |
| R049 | CT_COMMODITY.(CTCD_KEYWORD) | KEYWORDB.(KW_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R166 | OPS_AG_LINES.(OPAL_KEYWORD) | KEYWORDB.(KW_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R203 | OPS_LINE_ITEMS.(OPL_KEYWORD) | KEYWORDB.(KW_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R241 | PRICE_ASC_DATA.(PASCD_KW) | KEYWORDB.(KW_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |
| R251 | PRICE_SPECIAL_DATA.(PSD_KEYWORD) | KEYWORDB.(KW_ID) | Medium | Column roles and key structure; inferred, not data-validated.  |

## Trigger evidence

`KEYWORD_CHANGED`

Full metadata, defaults, descriptions and trigger source are retained in [metadata](../metadata/).
