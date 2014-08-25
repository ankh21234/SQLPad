using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	internal static class CodeCompletionSearchHelper
	{
		private static readonly HashSet<OracleFunctionIdentifier> SpecificCodeCompletionFunctionIdentifiers =
				new HashSet<OracleFunctionIdentifier>
			{
				OracleDatabaseModelBase.IdentifierBuiltInFunctionRound,
				OracleDatabaseModelBase.IdentifierBuiltInFunctionToChar,
				OracleDatabaseModelBase.IdentifierBuiltInFunctionTrunc,
				OracleDatabaseModelBase.IdentifierBuiltInFunctionSysContext
			};

		public static IEnumerable<ICodeCompletionItem> ResolveSpecificFunctionParameterCodeCompletionItems(StatementGrammarNode currentNode, IEnumerable<OracleCodeCompletionFunctionOverload> functionOverloads)
		{
			var completionItems = new List<ICodeCompletionItem>();
			var specificFunctionOverloads = functionOverloads.Where(m => SpecificCodeCompletionFunctionIdentifiers.Contains(m.FunctionMetadata.Identifier)).ToArray();
			if (specificFunctionOverloads.Length == 0 || currentNode.Id != Terminals.StringLiteral)
			{
				return completionItems;
			}

			var truncFunctionOverload = specificFunctionOverloads
				.FirstOrDefault(o => o.CurrentParameterIndex == 1 && o.FunctionMetadata.Identifier.In(OracleDatabaseModelBase.IdentifierBuiltInFunctionTrunc, OracleDatabaseModelBase.IdentifierBuiltInFunctionRound) &&
				                      o.FunctionMetadata.Parameters[o.CurrentParameterIndex + 1].DataType == "VARCHAR2");
			
			if (truncFunctionOverload != null)
			{
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "CC", "CC - One greater than the first two digits of a four-digit year"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "YYYY", "YYYY - Year (rounds up on July 1)"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "YEAR", "YEAR - Year (rounds up on July 1)"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "I", "I - ISO Year"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "IYYY", "IYYY - ISO Year"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "Q", "Q - Quarter (rounds up on the sixteenth day of the second month of the quarter)"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "MONTH", "MONTH - Month (rounds up on the sixteenth day)"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "MON", "MON - Month (rounds up on the sixteenth day)"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "MM", "MM - Month (rounds up on the sixteenth day)"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "WW", "WW - Same day of the week as the first day of the year"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "IW", "IW - Same day of the week as the first day of the ISO year"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "W", "W - Same day of the week as the first day of the month"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "D", "D - Starting day of the week"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "DAY", "DAY - Starting day of the week"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "HH", "HH - Hour"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "MI", "MI - Minute"));
			}

			var toCharFunctionOverload = specificFunctionOverloads
				.FirstOrDefault(o => o.CurrentParameterIndex == 2 && o.FunctionMetadata.Identifier == OracleDatabaseModelBase.IdentifierBuiltInFunctionToChar &&
				                      o.FunctionMetadata.Parameters[o.CurrentParameterIndex + 1].DataType == "VARCHAR2");
			if (toCharFunctionOverload != null)
			{
				const string itemText = "NLS_NUMERIC_CHARACTERS = ''<decimal separator><group separator>'' NLS_CURRENCY = ''currency_symbol'' NLS_ISO_CURRENCY = <territory>";
				const string itemDescription = "NLS_NUMERIC_CHARACTERS = '<decimal separator><group separator>' NLS_CURRENCY = 'currency_symbol' NLS_ISO_CURRENCY = <territory>";
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, itemText, itemDescription));
			}

			var sysContextFunctionOverload = specificFunctionOverloads
				.FirstOrDefault(o => o.CurrentParameterIndex == 1 && o.FunctionMetadata.Identifier == OracleDatabaseModelBase.IdentifierBuiltInFunctionSysContext &&
				                     o.FunctionMetadata.Parameters[o.CurrentParameterIndex + 1].DataType == "VARCHAR2");
			if (sysContextFunctionOverload != null)
			{
				var firstParameter = sysContextFunctionOverload.ProgramReference.ParameterNodes[0];
				if (firstParameter.ChildNodes.Count == 1 && firstParameter.ChildNodes[0].Id == Terminals.StringLiteral && firstParameter.ChildNodes[0].Token.Value.ToUpperInvariant() == "'USERENV'")
				{
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "ACTION", "ACTION - Identifies the position in the module (application name) and is set through the DBMS_APPLICATION_INFO package or OCI. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "AUDITED_CURSORID", "AUDITED_CURSORID - Returns the cursor ID of the SQL that triggered the audit. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "AUTHENTICATED_IDENTITY", "AUTHENTICATED_IDENTITY - Returns the identity used in authentication. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "AUTHENTICATION_DATA", "AUTHENTICATION_DATA - Data being used to authenticate the login user. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "AUTHENTICATION_METHOD", "AUTHENTICATION_METHOD - Returns the method of authentication. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "BG_JOB_ID", "BG_JOB_ID - Job ID of the current session if it was established by an Oracle Database background process. Null if the session was not established by a background process. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "CLIENT_IDENTIFIER", "CLIENT_IDENTIFIER - Returns an identifier that is set by the application through the DBMS_SESSION.SET_IDENTIFIER procedure. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "CLIENT_INFO", "CLIENT_INFO - Returns up to 64 bytes of user session information that can be stored by an application using the DBMS_APPLICATION_INFO package. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "CURRENT_BIND", "CURRENT_BIND - The bind variables for fine-grained auditing. You can specify this attribute only inside the event handler for the fine-grained auditing feature. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "CURRENT_EDITION_ID", "CURRENT_EDITION_ID - The identifier of the current edition. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "CURRENT_EDITION_NAME", "CURRENT_EDITION_NAME - The name of the current edition. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "CURRENT_SCHEMA", "CURRENT_SCHEMA - The name of the currently active default schema. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "CURRENT_SCHEMAID", "CURRENT_SCHEMAID - Identifier of the currently active default schema. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "CURRENT_SQL", "CURRENT_SQL - CURRENT_SQL returns the first 4K bytes of the current SQL that triggered the fine-grained auditing event. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "CURRENT_SQL<n>", "CURRENT_SQL<n> - The CURRENT_SQLn attributes return subsequent 4K-byte increments, where n can be an integer from 1 to 7, inclusive. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "CURRENT_SQL_LENGTH", "CURRENT_SQL_LENGTH - The length of the current SQL statement that triggers fine-grained audit or row-level security (RLS) policy functions or event handlers. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "CURRENT_USER", "CURRENT_USER - The name of the database user whose privileges are currently active. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "CURRENT_USERID", "CURRENT_USERID - The identifier of the database user whose privileges are currently active. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "DATABASE_ROLE", "DATABASE_ROLE - The database role using the SYS_CONTEXT function with the USERENV namespace. The role is one of the following: PRIMARY, PHYSICAL STANDBY, LOGICAL STANDBY, SNAPSHOT STANDBY. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "DB_DOMAIN", "DB_DOMAIN - Domain of the database as specified in the DB_DOMAIN initialization parameter. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "DB_NAME", "DB_NAME - Name of the database as specified in the DB_NAME initialization parameter. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "DB_UNIQUE_NAME", "DB_UNIQUE_NAME - Name of the database as specified in the DB_UNIQUE_NAME initialization parameter. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "DBLINK_INFO", "DBLINK_INFO - Returns the source of a database link session. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "ENTRYID", "DBLINK_INFO - The current audit entry number. The audit entryid sequence is shared between fine-grained audit records and regular audit records. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "ENTERPRISE_IDENTITY", "ENTERPRISE_IDENTITY - Returns the user's enterprise-wide identity. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "FG_JOB_ID", "FG_JOB_ID - Job ID of the current session if it was established by a client foreground process. NULL if the session was not established by a foreground process. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "GLOBAL_CONTEXT_MEMORY", "GLOBAL_CONTEXT_MEMORY - Returns the number being used in the System Global Area by the globally accessed context. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "GLOBAL_UID", "GLOBAL_UID - Returns the global user ID from Oracle Internet Directory for Enterprise User Security (EUS) logins; returns null for all other logins. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "HOST", "HOST - Name of the host machine from which the client has connected. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "IDENTIFICATION_TYPE", "IDENTIFICATION_TYPE - Returns the way the user's schema was created in the database. Specifically, it reflects the IDENTIFIED clause in the CREATE/ALTER USER syntax. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "INSTANCE", "INSTANCE - The instance identification number of the current instance. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "INSTANCE_NAME", "INSTANCE_NAME - The name of the instance. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "IP_ADDRESS", "IP_ADDRESS - IP address of the machine from which the client is connected. If the client and server are on the same machine and the connection uses IPv6 addressing, then ::1 is returned. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "ISDBA", "ISDBA - Returns TRUE if the user has been authenticated as having DBA privileges either through the operating system or through a password file. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "LANG", "LANG - The abbreviated name for the language, a shorter form than the existing 'LANGUAGE' parameter. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "LANGUAGE", "LANGUAGE - The language and territory currently used by your session, along with the database character set. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "MODULE", "MODULE - The application name (module) set through the DBMS_APPLICATION_INFO package or OCI. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "NETWORK_PROTOCOL", "NETWORK_PROTOCOL - Network protocol being used for communication, as specified in the 'PROTOCOL=protocol' portion of the connect string. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "NLS_CALENDAR", "NLS_CALENDAR - The current calendar of the current session. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "NLS_CURRENCY", "NLS_CURRENCY - The currency of the current session. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "NLS_DATE_FORMAT", "NLS_DATE_FORMAT - The date format for the session. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "NLS_DATE_LANGUAGE", "NLS_DATE_LANGUAGE - The language used for expressing dates. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "NLS_SORT", "NLS_SORT - BINARY or the linguistic sort basis. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "NLS_TERRITORY", "NLS_TERRITORY - The territory of the current session. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "OS_USER", "OS_USER - Operating system user name of the client process that initiated the database session. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "POLICY_INVOKER", "POLICY_INVOKER - The invoker of row-level security (RLS) policy functions. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "PROXY_ENTERPRISE_IDENTITY", "PROXY_ENTERPRISE_IDENTITY - Returns the Oracle Internet Directory DN when the proxy user is an enterprise user. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "PROXY_USER", "PROXY_USER - Name of the database user who opened the current session on behalf of SESSION_USER. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "PROXY_USERID", "PROXY_USERID - Identifier of the database user who opened the current session on behalf of SESSION_USER. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "SERVER_HOST", "SERVER_HOST - The host name of the machine on which the instance is running. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "SERVICE_NAME", "SERVICE_NAME - The name of the service to which a given session is connected. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "SESSION_EDITION_ID", "SESSION_EDITION_ID - The identifier of the session edition. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "SESSION_EDITION_NAME", "SESSION_EDITION_NAME - The name of the session edition. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "SESSION_USER", "SESSION_USER - The name of the database user at logon. For enterprise users, returns the schema. For other users, returns the database user name. This value remains the same throughout the duration of the session. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "SESSION_USERID", "SESSION_USERID - The identifier of the database user at logon. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "SESSIONID", "SESSIONID - The auditing session identifier. You cannot use this attribute in distributed SQL statements. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "SID", "SID - The session ID. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "STATEMENTID", "STATEMENTID - The auditing statement identifier. STATEMENTID represents the number of SQL statements audited in a given session. "));
					completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "TERMINAL", "TERMINAL - The operating system identifier for the client of the current session. "));
				}
			}

			return completionItems;
		}

		private static OracleCodeCompletionItem BuildRoundOrTruncFormatParameter(StatementGrammarNode node, string parameterValue, string description)
		{
			return
				new OracleCodeCompletionItem
				{
					Category = OracleCodeCompletionCategory.FunctionParameter,
					Name = description,
					StatementNode = node,
					Text = String.Format("'{0}'", parameterValue)
				};
		}

		public static bool IsMatch(string fullyQualifiedName, string inputPhrase)
		{
			inputPhrase = inputPhrase == null ? null : inputPhrase.Trim('"');
			return String.IsNullOrWhiteSpace(inputPhrase) ||
			       ResolveSearchPhrases(inputPhrase).All(p => fullyQualifiedName.ToUpperInvariant().Contains(p));
		}

		private static IEnumerable<string> ResolveSearchPhrases(string inputPhrase)
		{
			var builder = new StringBuilder();
			var containsSmallLetter = false;
			foreach (var character in inputPhrase)
			{
				if (containsSmallLetter && Char.IsUpper(character) && builder.Length > 0)
				{
					yield return builder.ToString().ToUpperInvariant();
					containsSmallLetter = false;
					builder.Clear();
				}
				else if (Char.IsLower(character))
				{
					containsSmallLetter = true;
				}

				builder.Append(character);
			}

			if (builder.Length > 0)
			{
				yield return builder.ToString().ToUpperInvariant();
			}
		}
	}
}
