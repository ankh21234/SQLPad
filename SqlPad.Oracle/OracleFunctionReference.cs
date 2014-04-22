using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	public abstract class OracleReference
	{
		protected OracleReference()
		{
			ObjectNodeObjectReferences = new HashSet<OracleObjectReference>();
		}

		public OracleObjectIdentifier FullyQualifiedObjectName
		{
			get { return OracleObjectIdentifier.Create(OwnerNode, ObjectNode, null); }
		}

		public abstract string Name { get; }

		public string NormalizedName { get { return Name.ToQuotedIdentifier(); } }

		public string ObjectName { get { return ObjectNode == null ? null : ObjectNode.Token.Value; } }

		public string ObjectNormalizedName { get { return ObjectNode == null ? null : ObjectName.ToQuotedIdentifier(); } }

		public OracleQueryBlock Owner { get; set; }

		public StatementDescriptionNode OwnerNode { get; set; }

		public StatementDescriptionNode ObjectNode { get; set; }

		public ICollection<OracleObjectReference> ObjectNodeObjectReferences { get; set; }
	}

	[DebuggerDisplay("OracleFunctionReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Object={ObjectNode == null ? null : ObjectNode.Token.Value}; Function={FunctionIdentifierNode.Token.Value})")]
	public class OracleFunctionReference : OracleReference
	{
		public OracleFunctionReference()
		{
			ParameterNodes = new HashSet<StatementDescriptionNode>();
		}

		public override string Name { get { return FunctionIdentifierNode.Token.Value; } }

		public StatementDescriptionNode FunctionIdentifierNode { get; set; }
		
		public ICollection<StatementDescriptionNode> ParameterNodes { get; set; }

		public OracleSqlFunctionMetadata FunctionMetadata { get; set; }
		
		public StatementDescriptionNode RootNode { get; set; }
		
		public bool HasAnalyticClause { get; set; }
	}
}
