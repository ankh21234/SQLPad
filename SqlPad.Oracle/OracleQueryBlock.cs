using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleQueryBlock (Alias={Alias}; Type={Type}; RootNode={RootNode}; Columns={Columns.Count})")]
	public class OracleQueryBlock
	{
		private OracleObjectReference _selfObjectReference;

		public OracleQueryBlock()
		{
			ObjectReferences = new List<OracleObjectReference>();
			Columns = new List<OracleSelectListColumn>();
			TypeReferences = new List<OracleTypeReference>();
			AccessibleQueryBlocks = new List<OracleQueryBlock>();
			ColumnReferences = new List<OracleColumnReference>();
			FunctionReferences = new List<OracleProgramReference>();
		}

		public OracleObjectReference SelfObjectReference
		{
			get { return _selfObjectReference ?? BuildSelfObjectReference(); }
		}

		private OracleObjectReference BuildSelfObjectReference()
		{
			_selfObjectReference = new OracleObjectReference
			                       {
									   AliasNode = AliasNode,
									   Owner = this,
									   Type = TableReferenceType.InlineView
			                       };

			_selfObjectReference.QueryBlocks.Add(this);

			return _selfObjectReference;
		}

		public string Alias { get { return AliasNode == null ? null : AliasNode.Token.Value; } }

		public string NormalizedAlias { get { return Alias.ToQuotedIdentifier(); } }

		public StatementDescriptionNode AliasNode { get; set; }

		public QueryBlockType Type { get; set; }
		
		public bool HasAsteriskClause { get; set; }

		public StatementDescriptionNode RootNode { get; set; }

		public StatementDescriptionNode WhereClause { get; set; }

		public StatementDescriptionNode SelectList { get; set; }

		public bool HasDistinctResultSet { get; set; }

		public bool ContainsSchemaQualifiers
		{
			get
			{
				return ObjectReferences.Select(o => o.OwnerNode)
					.Concat(AllColumnReferences.Select(c => c.OwnerNode))
					.Concat(AllFunctionReferences.Select(f => f.OwnerNode))
					.Any(n => n != null);
			}
		}

		public StatementDescriptionNode GroupByClause { get; set; }

		public StatementDescriptionNode HavingClause { get; set; }
		
		public StatementDescriptionNode OrderByClause { get; set; }
		
		public OracleStatement Statement { get; set; }

		public ICollection<OracleObjectReference> ObjectReferences { get; private set; }

		public ICollection<OracleSelectListColumn> Columns { get; private set; }
		
		public ICollection<OracleTypeReference> TypeReferences { get; private set; }

		public ICollection<OracleColumnReference> ColumnReferences { get; private set; }
		
		public ICollection<OracleProgramReference> FunctionReferences { get; private set; }

		public IEnumerable<OracleProgramReference> AllFunctionReferences { get { return Columns.SelectMany(c => c.FunctionReferences).Concat(FunctionReferences); } }

		public IEnumerable<OracleColumnReference> AllColumnReferences { get { return Columns.SelectMany(c => c.ColumnReferences).Concat(ColumnReferences); } }

		public ICollection<OracleQueryBlock> AccessibleQueryBlocks { get; private set; }

		public OracleQueryBlock FollowingConcatenatedQueryBlock { get; set; }
		
		public OracleQueryBlock PrecedingConcatenatedQueryBlock { get; set; }

		public OracleQueryBlock ParentCorrelatedQueryBlock { get; set; }

		public IEnumerable<OracleQueryBlock> AllFollowingConcatenatedQueryBlocks
		{
			get
			{
				var concatenatedQueryBlock = FollowingConcatenatedQueryBlock;
				while (concatenatedQueryBlock != null)
				{
					yield return concatenatedQueryBlock;

					concatenatedQueryBlock = concatenatedQueryBlock.FollowingConcatenatedQueryBlock;
				}
			}
		}
	}
}
