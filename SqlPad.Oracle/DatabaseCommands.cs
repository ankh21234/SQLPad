﻿using System;
using System.Linq;

namespace SqlPad.Oracle
{
	internal static class DatabaseCommands
	{
		public const string BuiltInFunctionMetadataCommandText =
@"SELECT
    OWNER,
    PACKAGE_NAME,
    NVL(SQL_FUNCTION_METADATA.FUNCTION_NAME, PROCEDURES.FUNCTION_NAME) FUNCTION_NAME,
	NVL(OVERLOAD, 0) OVERLOAD,
    NVL(ANALYTIC, PROCEDURES.AGGREGATE) ANALYTIC,
    NVL(SQL_FUNCTION_METADATA.AGGREGATE, PROCEDURES.AGGREGATE) AGGREGATE,
    NVL(PIPELINED, 'NO') PIPELINED,
    NVL(OFFLOADABLE, 'NO') OFFLOADABLE,
    NVL(PARALLEL, 'NO') PARALLEL,
    NVL(DETERMINISTIC, 'NO') DETERMINISTIC,
    MINARGS,
    MAXARGS,
    NVL(AUTHID, 'CURRENT_USER') AUTHID,
    NVL(DISP_TYPE, 'NORMAL') DISP_TYPE
FROM
    (SELECT DISTINCT
		OWNER,
        OBJECT_NAME PACKAGE_NAME,
        PROCEDURE_NAME FUNCTION_NAME,
		OVERLOAD,
        AGGREGATE,
        PIPELINED,
        PARALLEL,
        DETERMINISTIC,
        AUTHID
    FROM
        ALL_PROCEDURES
    WHERE
        OWNER = 'SYS' AND OBJECT_NAME = 'STANDARD' AND PROCEDURE_NAME NOT LIKE '%SYS$%' AND
        (ALL_PROCEDURES.OBJECT_TYPE = 'FUNCTION' OR (ALL_PROCEDURES.OBJECT_TYPE = 'PACKAGE' AND ALL_PROCEDURES.PROCEDURE_NAME IS NOT NULL))
		AND EXISTS
			(SELECT
				NULL
			FROM
				ALL_ARGUMENTS
			WHERE
				ALL_PROCEDURES.OBJECT_ID = ALL_ARGUMENTS.OBJECT_ID AND ALL_PROCEDURES.PROCEDURE_NAME = ALL_ARGUMENTS.OBJECT_NAME AND NVL(ALL_ARGUMENTS.OVERLOAD, 0) = NVL(ALL_PROCEDURES.OVERLOAD, 0) AND
				POSITION = 0 AND ARGUMENT_NAME IS NULL
			)
	) PROCEDURES
FULL JOIN
    (SELECT
        NAME FUNCTION_NAME,
        NVL(MAX(NULLIF(ANALYTIC, 'NO')), 'NO') ANALYTIC,
        NVL(MAX(NULLIF(AGGREGATE, 'NO')), 'NO') AGGREGATE,
        OFFLOADABLE,
        MIN(MINARGS) MINARGS,
        MAX(MAXARGS) MAXARGS,
        DISP_TYPE
    FROM
        V$SQLFN_METADATA
    WHERE
        DISP_TYPE NOT IN ('REL-OP', 'ARITHMATIC')
    GROUP BY
        NAME,
        OFFLOADABLE,
        DISP_TYPE) SQL_FUNCTION_METADATA
ON PROCEDURES.FUNCTION_NAME = SQL_FUNCTION_METADATA.FUNCTION_NAME
ORDER BY
    FUNCTION_NAME";

		public const string BuiltInFunctionParameterMetadataCommandText =
@"SELECT
	OWNER,
    PACKAGE_NAME,
	OBJECT_NAME FUNCTION_NAME,
	NVL(OVERLOAD, 0) OVERLOAD,
	ARGUMENT_NAME,
	POSITION,
	DATA_TYPE,
	DEFAULTED,
	IN_OUT
FROM
	ALL_ARGUMENTS
WHERE
	OWNER = 'SYS'
	AND PACKAGE_NAME = 'STANDARD'
	AND OBJECT_NAME NOT LIKE '%SYS$%'
	AND DATA_TYPE IS NOT NULL
ORDER BY
    POSITION";

		public const string UserFunctionMetadataCommandText =
	@"SELECT
    OWNER,
    PACKAGE_NAME,
    FUNCTION_NAME,
	NVL(OVERLOAD, 0) OVERLOAD,
    AGGREGATE ANALYTIC,
    AGGREGATE,
    PIPELINED,
    'NO' OFFLOADABLE,
    PARALLEL,
    DETERMINISTIC,
    NULL MINARGS,
    NULL MAXARGS,
    AUTHID,
    'NORMAL' DISP_TYPE
FROM
    (SELECT DISTINCT
        OWNER,
        CASE WHEN OBJECT_TYPE = 'PACKAGE' THEN OBJECT_NAME END PACKAGE_NAME,
        CASE WHEN OBJECT_TYPE = 'FUNCTION' THEN OBJECT_NAME ELSE PROCEDURE_NAME END FUNCTION_NAME,
		OVERLOAD,
        AGGREGATE,
        PIPELINED,
        PARALLEL,
        DETERMINISTIC,
        AUTHID
    FROM
        ALL_PROCEDURES
    WHERE
        NOT (OWNER = 'SYS' AND OBJECT_NAME = 'STANDARD') AND
        (ALL_PROCEDURES.OBJECT_TYPE = 'FUNCTION' OR (ALL_PROCEDURES.OBJECT_TYPE = 'PACKAGE' AND ALL_PROCEDURES.PROCEDURE_NAME IS NOT NULL))
	AND EXISTS
        (SELECT
            NULL
        FROM
            ALL_ARGUMENTS
        WHERE
            ALL_PROCEDURES.OBJECT_ID = OBJECT_ID AND NVL(ALL_PROCEDURES.PROCEDURE_NAME, ALL_PROCEDURES.OBJECT_NAME) = OBJECT_NAME AND NVL(OVERLOAD, 0) = NVL(ALL_PROCEDURES.OVERLOAD, 0) AND
            POSITION = 0 AND ARGUMENT_NAME IS NULL
        )
	)
ORDER BY
	OWNER,
    PACKAGE_NAME,
    FUNCTION_NAME";

		public const string UserFunctionParameterMetadataCommandText =
@"SELECT
    OWNER,
    PACKAGE_NAME,
    OBJECT_NAME FUNCTION_NAME,
    NVL(OVERLOAD, 0) OVERLOAD,
    ARGUMENT_NAME,
    POSITION,
    DATA_TYPE,
    DEFAULTED,
    IN_OUT
FROM
    ALL_ARGUMENTS
WHERE
    NOT (OWNER = 'SYS' AND OBJECT_NAME = 'STANDARD')
ORDER BY
    OWNER,
    PACKAGE_NAME,
    POSITION";

		public static readonly string SelectTypesCommandText =
			String.Format("SELECT OWNER, TYPE_NAME, TYPECODE, PREDEFINED, INCOMPLETE, FINAL, INSTANTIABLE, SUPERTYPE_OWNER, SUPERTYPE_NAME FROM SYS.ALL_TYPES WHERE TYPECODE IN ({0})",
				ToInValueList(OracleTypeBase.ObjectType, OracleTypeBase.CollectionType, OracleTypeBase.XmlType));

		public static readonly string SelectAllObjectsCommandText =
			String.Format("SELECT OWNER, OBJECT_NAME, SUBOBJECT_NAME, OBJECT_ID, DATA_OBJECT_ID, OBJECT_TYPE, CREATED, LAST_DDL_TIME, STATUS, TEMPORARY/*, EDITIONABLE, EDITION_NAME*/ FROM SYS.ALL_OBJECTS WHERE OBJECT_TYPE IN ({0})",
				ToInValueList(OracleSchemaObjectType.Synonym, OracleSchemaObjectType.View, OracleSchemaObjectType.Table, OracleSchemaObjectType.Sequence, OracleSchemaObjectType.Function, OracleSchemaObjectType.Package, OracleSchemaObjectType.Type));

		public const string SelectTablesCommandText =
@"SELECT OWNER, TABLE_NAME, TABLESPACE_NAME, CLUSTER_NAME, STATUS, LOGGING, NUM_ROWS, BLOCKS, AVG_ROW_LEN, DEGREE, CACHE, SAMPLE_SIZE, LAST_ANALYZED, TEMPORARY, NESTED, ROW_MOVEMENT, COMPRESS_FOR,
CASE
	WHEN TEMPORARY = 'N' AND TABLESPACE_NAME IS NULL AND PARTITIONED = 'NO' AND IOT_TYPE IS NULL AND PCT_FREE = 0 THEN 'External'
	WHEN IOT_TYPE = 'IOT' THEN 'Index'
	ELSE 'Heap'
END ORGANIZATION
FROM SYS.ALL_TABLES";

		public const string SelectAllSchemasCommandText = "SELECT USERNAME FROM SYS.ALL_USERS";

		public const string SelectSynonymTargetsCommandText = "SELECT OWNER, SYNONYM_NAME, TABLE_OWNER, TABLE_NAME FROM SYS.ALL_SYNONYMS";

		public const string SelectTableColumnsCommandText = "SELECT OWNER, TABLE_NAME, COLUMN_NAME, DATA_TYPE, DATA_TYPE_OWNER, DATA_LENGTH, CHAR_LENGTH, DATA_PRECISION, DATA_SCALE, CHAR_USED, NULLABLE, COLUMN_ID, NUM_DISTINCT, LOW_VALUE, HIGH_VALUE, NUM_NULLS, NUM_BUCKETS, LAST_ANALYZED, SAMPLE_SIZE, AVG_COL_LEN, HISTOGRAM FROM SYS.ALL_TAB_COLUMNS ORDER BY OWNER, TABLE_NAME, COLUMN_ID";

		public const string SelectConstraintsCommandText = "SELECT OWNER, CONSTRAINT_NAME, CONSTRAINT_TYPE, TABLE_NAME, SEARCH_CONDITION, R_OWNER, R_CONSTRAINT_NAME, DELETE_RULE, STATUS, DEFERRABLE, VALIDATED, RELY, INDEX_OWNER, INDEX_NAME FROM SYS.ALL_CONSTRAINTS WHERE CONSTRAINT_TYPE IN ('C', 'R', 'P', 'U')";

		public const string SelectConstraintColumnsCommandText = "SELECT OWNER, CONSTRAINT_NAME, COLUMN_NAME, POSITION FROM SYS.ALL_CONS_COLUMNS ORDER BY OWNER, CONSTRAINT_NAME, POSITION";

		public const string SelectSequencesCommandText = "SELECT SEQUENCE_OWNER, SEQUENCE_NAME, MIN_VALUE, MAX_VALUE, INCREMENT_BY, CYCLE_FLAG, ORDER_FLAG, CACHE_SIZE, LAST_NUMBER FROM SYS.ALL_SEQUENCES";

		public const string SelectTypeAttributesCommandText = "SELECT OWNER, TYPE_NAME, ATTR_NAME, ATTR_TYPE_MOD, ATTR_TYPE_OWNER, ATTR_TYPE_NAME, LENGTH, PRECISION, SCALE, ATTR_NO, CHAR_USED FROM SYS.ALL_TYPE_ATTRS ORDER BY OWNER, TYPE_NAME, ATTR_NO";

		public const string GetObjectScriptCommand = "SELECT SYS.DBMS_METADATA.GET_DDL(OBJECT_TYPE => :OBJECT_TYPE, NAME => :NAME, SCHEMA => :SCHEMA) SCRIPT FROM SYS.DUAL";

		public const string SelectDatabaseLinksCommandText = "SELECT OWNER, DB_LINK, USERNAME, HOST, CREATED FROM ALL_DB_LINKS";

		public const string GetColumnStatisticsCommand = "SELECT NUM_DISTINCT, LOW_VALUE, HIGH_VALUE, DENSITY, NUM_NULLS, NUM_BUCKETS, LAST_ANALYZED, SAMPLE_SIZE, AVG_COL_LEN, INITCAP(HISTOGRAM) HISTOGRAM FROM ALL_TAB_COL_STATISTICS WHERE OWNER = :OWNER AND TABLE_NAME = :TABLE_NAME AND COLUMN_NAME = :COLUMN_NAME";

		public const string GetColumnHistogramCommand = "SELECT ENDPOINT_NUMBER FROM ALL_TAB_HISTOGRAMS WHERE OWNER = :OWNER AND TABLE_NAME = :TABLE_NAME AND COLUMN_NAME = :COLUMN_NAME ORDER BY ENDPOINT_NUMBER";

		public const string GetTableDetailsCommand =
@"SELECT
	CASE 
        WHEN TABLESPACE_NAME IS NULL AND PARTITIONED = 'NO' AND IOT_TYPE IS NULL AND PCT_FREE = 0 THEN 'External'
        WHEN IOT_TYPE IS NOT NULL THEN 'Index'
        ELSE 'Heap'
    END ORGANIZATION,
	TEMPORARY,
	PARTITIONED,
	CLUSTER_NAME,
    BLOCKS,
    NUM_ROWS,
    AVG_ROW_LEN,
    CASE WHEN COMPRESSION = 'ENABLED' THEN INITCAP(COMPRESS_FOR) ELSE INITCAP(COMPRESSION) END COMPRESSION,
    LAST_ANALYZED
FROM
	ALL_TABLES
WHERE
	OWNER = :OWNER AND TABLE_NAME = :TABLE_NAME";

		public const string GetTableAllocatedBytesCommand =
@"SELECT
	SUM(BYTES) + SUM(CASE WHEN LOBS.SEGMENT_NAME IS NOT NULL THEN BYTES ELSE 0 END) ALLOCATED_BYTES
FROM
	DBA_SEGMENTS
	LEFT JOIN
	(
	    SELECT NVL(LOB_SEGMENT_NAME, LOB_INDEX_NAME) SEGMENT_NAME
	    FROM
	        (SELECT SEGMENT_NAME LOB_SEGMENT_NAME, INDEX_NAME LOB_INDEX_NAME FROM DBA_LOBS WHERE OWNER = :OWNER AND TABLE_NAME = :TABLE_NAME
	        UNION ALL
	        SELECT LOB_PARTITION_NAME LOB_SEGMENT_NAME, LOB_INDPART_NAME LOB_INDEX_NAME FROM DBA_LOB_PARTITIONS WHERE TABLE_OWNER = :OWNER AND TABLE_NAME = :TABLE_NAME
	        UNION ALL
	        SELECT LOB_SUBPARTITION_NAME LOB_SEGMENT_NAME, LOB_INDSUBPART_NAME LOB_INDEX_NAME FROM DBA_LOB_SUBPARTITIONS WHERE TABLE_OWNER = :OWNER AND TABLE_NAME = :TABLE_NAME)
	)
	LOBS ON DBA_SEGMENTS.SEGMENT_NAME = LOBS.SEGMENT_NAME
WHERE
	LOBS.SEGMENT_NAME IS NOT NULL OR
	(
		DBA_SEGMENTS.SEGMENT_TYPE IN ('TABLE', 'TABLE PARTITION', 'TABLE SUBPARTITION') AND
		DBA_SEGMENTS.OWNER = :OWNER AND
		DBA_SEGMENTS.SEGMENT_NAME = :TABLE_NAME
	)";

		public const string GetExecutionPlanIdentifiers = "SELECT NVL(SQL_ID, PREV_SQL_ID) SQL_ID, NVL(SQL_CHILD_NUMBER, PREV_CHILD_NUMBER) SQL_CHILD_NUMBER FROM V$SESSION WHERE V$SESSION.SID = :SID";
		public const string GetExecutionPlanText = "SELECT PLAN_TABLE_OUTPUT FROM TABLE(DBMS_XPLAN.DISPLAY_CURSOR(:SQL_ID, :CHILD_NUMBER, 'ALLSTATS LAST ADVANCED'))";
		public const string ExplainPlanBase = "SELECT OPERATION, OPTIONS, OBJECT_NODE, OBJECT_OWNER, OBJECT_NAME, OBJECT_ALIAS, OBJECT_INSTANCE, OBJECT_TYPE, OPTIMIZER, SEARCH_COLUMNS, POSITION, COST, CARDINALITY, BYTES, OTHER_TAG, PARTITION_START, PARTITION_STOP, PARTITION_ID, OTHER, DISTRIBUTION, CPU_COST, IO_COST, TEMP_SPACE, ACCESS_PREDICATES, FILTER_PREDICATES, PROJECTION, TIME, QBLOCK_NAME, OTHER_XML, REMARKS FROM {0} WHERE STATEMENT_ID = :STATEMENT_ID ORDER BY ID";
		public const string GetCharacterSets = "SELECT VALUE FROM V$NLS_VALID_VALUES WHERE parameter = 'CHARACTERSET' AND ISDEPRECATED = 'FALSE' ORDER BY VALUE";

		private static string ToInValueList(params string[] values)
		{
			return String.Join(", ", values.Select(t => String.Format("'{0}'", t)));
		}
	}
}