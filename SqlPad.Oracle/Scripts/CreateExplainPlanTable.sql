﻿CREATE GLOBAL TEMPORARY TABLE EXPLAIN_PLAN
(
	STATEMENT_ID VARCHAR2(30), 
	PLAN_ID NUMBER, 
	TIMESTAMP DATE, 
	REMARKS VARCHAR2(4000), 
	OPERATION VARCHAR2(30), 
	OPTIONS VARCHAR2(255), 
	OBJECT_NODE VARCHAR2(128), 
	OBJECT_OWNER VARCHAR2(30), 
	OBJECT_NAME VARCHAR2(30), 
	OBJECT_ALIAS VARCHAR2(65), 
	OBJECT_INSTANCE INTEGER, 
	OBJECT_TYPE VARCHAR2(30), 
	OPTIMIZER VARCHAR2(255), 
	SEARCH_COLUMNS NUMBER, 
	ID INTEGER, 
	PARENT_ID INTEGER, 
	DEPTH INTEGER, 
	POSITION INTEGER, 
	COST INTEGER, 
	CARDINALITY INTEGER, 
	BYTES INTEGER, 
	OTHER_TAG VARCHAR2(255), 
	PARTITION_START VARCHAR2(255), 
	PARTITION_STOP VARCHAR2(255), 
	PARTITION_ID INTEGER, 
	OTHER LONG, 
	DISTRIBUTION VARCHAR2(30), 
	CPU_COST INTEGER, 
	IO_COST INTEGER, 
	TEMP_SPACE INTEGER, 
	ACCESS_PREDICATES VARCHAR2(4000), 
	FILTER_PREDICATES VARCHAR2(4000), 
	PROJECTION VARCHAR2(4000), 
	TIME INTEGER, 
	QBLOCK_NAME VARCHAR2(30), 
	OTHER_XML CLOB
)
ON COMMIT PRESERVE ROWS

CREATE OR REPLACE PUBLIC SYNONYM EXPLAIN_PLAN FOR EXPLAIN_PLAN