﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlPad.Oracle
{
	public class OracleStatementValidator : IStatementValidator
	{
		public IValidationModel BuildValidationModel(string sqlText, StatementBase statement, IDatabaseModel databaseModel)
		{
			var oracleDatabaseModel = (OracleDatabaseModelBase)databaseModel;
			var semanticModel = new OracleStatementSemanticModel(sqlText, (OracleStatement)statement, oracleDatabaseModel);

			var validationModel = new OracleValidationModel { SemanticModel = semanticModel };

			foreach (var tableReference in semanticModel.QueryBlocks.SelectMany(qb => qb.ObjectReferences).Where(tr => tr.Type != TableReferenceType.InlineView))
			{
				if (tableReference.Type == TableReferenceType.CommonTableExpression)
				{
					validationModel.ObjectNodeValidity[tableReference.ObjectNode] = new NodeValidationData { IsRecognized = true };
					continue;
				}

				if (tableReference.OwnerNode != null)
				{
					validationModel.ObjectNodeValidity[tableReference.OwnerNode] = new NodeValidationData { IsRecognized = tableReference.SearchResult.SchemaFound };
				}

				validationModel.ObjectNodeValidity[tableReference.ObjectNode] = new NodeValidationData { IsRecognized = tableReference.SearchResult.SchemaObject != null };
			}

			foreach (var queryBlock in semanticModel.QueryBlocks)
			{
				foreach (var column in queryBlock.Columns.Where(c => c.ExplicitDefinition))
				{
					ResolveColumnNodeValidities(validationModel, column, column.ColumnReferences);
				}

				ResolveColumnNodeValidities(validationModel, null, queryBlock.ColumnReferences);

				foreach (var functionReference in queryBlock.AllFunctionReferences)
				{
					var metadataFound = functionReference.Metadata != null;
					var semanticError = SemanticError.None;
					var isRecognized = false;
					if (metadataFound)
					{
						isRecognized = true;
						if (functionReference.ParameterListNode != null)
						{
							var maximumParameterCount = functionReference.Metadata.MinimumArguments > 0 && functionReference.Metadata.MaximumArguments == 0
								? Int32.MaxValue
								: functionReference.Metadata.MaximumArguments;

							// TODO: Handle optional parameters
							if ((functionReference.ParameterNodes.Count < functionReference.Metadata.MinimumArguments) ||
							    (functionReference.ParameterNodes.Count > maximumParameterCount))
							{
								validationModel.ProgramNodeValidity[functionReference.ParameterListNode] = new ProgramValidationData(SemanticError.InvalidParameterCount) { IsRecognized = true };
							}
						}
						else if (functionReference.Metadata.MinimumArguments > 0)
						{
							semanticError = SemanticError.InvalidParameterCount;
						}
						else if (functionReference.Metadata.DisplayType == OracleFunctionMetadata.DisplayTypeParenthesis)
						{
							semanticError = SemanticError.MissingParenthesis;
						}

						if (functionReference.AnalyticClauseNode != null && !functionReference.Metadata.IsAnalytic)
						{
							validationModel.ProgramNodeValidity[functionReference.AnalyticClauseNode] = new ProgramValidationData(SemanticError.AnalyticClauseNotSupported) { IsRecognized = true, Node = functionReference.AnalyticClauseNode };
						}
					}
					
					if (functionReference.ObjectNode != null)
					{
						var packageSemanticError = functionReference.SchemaObject == null || functionReference.SchemaObject.IsValid
							? SemanticError.None
							: SemanticError.ObjectStatusInvalid;

						validationModel.ProgramNodeValidity[functionReference.ObjectNode] = new ProgramValidationData(packageSemanticError) { IsRecognized = functionReference.SchemaObject != null, Node = functionReference.ObjectNode };
					}

					if (semanticError == SemanticError.None && isRecognized && !functionReference.Metadata.IsPackageFunction && functionReference.SchemaObject != null && !functionReference.SchemaObject.IsValid)
					{
						semanticError = SemanticError.ObjectStatusInvalid;
					}

					validationModel.ProgramNodeValidity[functionReference.FunctionIdentifierNode] = new ProgramValidationData(semanticError) { IsRecognized = isRecognized, Node = functionReference.FunctionIdentifierNode };
				}

				foreach (var typeReference in queryBlock.TypeReferences)
				{
					validationModel.ProgramNodeValidity[typeReference.ObjectNode] = new ProgramValidationData { IsRecognized = true, Node = typeReference.ObjectNode };
				}
			}

			var invalidIdentifiers = semanticModel.Statement.AllTerminals
				.Select(GetInvalidIdentifierValidationData)
				.Where(nv => nv != null);

			foreach (var nodeValidity in invalidIdentifiers)
			{
				validationModel.IdentifierNodeValidity[nodeValidity.Node] = nodeValidity;
			}

			return validationModel;
		}

		private INodeValidationData GetInvalidIdentifierValidationData(StatementDescriptionNode node)
		{
			if (!node.Id.IsIdentifierOrAlias())
				return null;

			var trimmedIdentifier = node.Token.Value.Trim('"');

			var errorMessage = String.Empty;
			if (node.Id == OracleGrammarDescription.Terminals.BindVariableIdentifier && trimmedIdentifier == node.Token.Value)
			{
				int bindVariableNumberIdentifier;
				if (Int32.TryParse(trimmedIdentifier.Substring(0, trimmedIdentifier.Length > 5 ? 5 : trimmedIdentifier.Length), out bindVariableNumberIdentifier) && bindVariableNumberIdentifier > 65535)
				{
					errorMessage = "Numeric bind variable identifier must be between 0 and 65535. ";
				}
			}

			if (String.IsNullOrEmpty(errorMessage) && trimmedIdentifier.Length > 0 && trimmedIdentifier.Length <= 30)
			{
				return null;
			}

			if (String.IsNullOrEmpty(errorMessage))
			{
				errorMessage = "Identifier length must be between one and 30 characters excluding quotes. ";
			}

			return new InvalidIdentifierNodeValidationData(errorMessage) { IsRecognized = true, Node = node };
		}

		private void ResolveColumnNodeValidities(OracleValidationModel validationModel, OracleSelectListColumn column, IEnumerable<OracleColumnReference> columnReferences)
		{
			foreach (var columnReference in columnReferences)
			{
				// Schema
				if (columnReference.OwnerNode != null)
					validationModel.ObjectNodeValidity[columnReference.OwnerNode] =
						new NodeValidationData(columnReference.ObjectNodeObjectReferences)
						{
							IsRecognized = columnReference.ObjectNodeObjectReferences.Count > 0,
							Node = columnReference.OwnerNode
						};

				// Object
				if (columnReference.ObjectNode != null)
					validationModel.ObjectNodeValidity[columnReference.ObjectNode] =
						new NodeValidationData(columnReference.ObjectNodeObjectReferences)
						{
							IsRecognized = columnReference.ObjectNodeObjectReferences.Count > 0,
							Node = columnReference.ObjectNode
						};

				// Column
				validationModel.ColumnNodeValidity[columnReference.ColumnNode] =
					new ColumnNodeValidationData(columnReference)
					{
						IsRecognized = column != null && column.IsAsterisk || columnReference.ColumnNodeObjectReferences.Count > 0,
						Node = columnReference.ColumnNode
					};
			}
		}
	}

	public class OracleValidationModel : IValidationModel
	{
		private readonly Dictionary<StatementDescriptionNode, INodeValidationData> _objectNodeValidity = new Dictionary<StatementDescriptionNode, INodeValidationData>();
		private readonly Dictionary<StatementDescriptionNode, INodeValidationData> _columnNodeValidity = new Dictionary<StatementDescriptionNode, INodeValidationData>();
		private readonly Dictionary<StatementDescriptionNode, INodeValidationData> _programNodeValidity = new Dictionary<StatementDescriptionNode, INodeValidationData>();
		private readonly Dictionary<StatementDescriptionNode, INodeValidationData> _identifierNodeValidity = new Dictionary<StatementDescriptionNode, INodeValidationData>();

		public OracleStatementSemanticModel SemanticModel { get; set; }

		public StatementBase Statement { get { return SemanticModel.Statement; } }

		public IDictionary<StatementDescriptionNode, INodeValidationData> ObjectNodeValidity { get { return _objectNodeValidity; } }

		public IDictionary<StatementDescriptionNode, INodeValidationData> ColumnNodeValidity { get { return _columnNodeValidity; } }

		public IDictionary<StatementDescriptionNode, INodeValidationData> ProgramNodeValidity { get { return _programNodeValidity; } }

		public IDictionary<StatementDescriptionNode, INodeValidationData> IdentifierNodeValidity { get { return _identifierNodeValidity; } }

		public IEnumerable<KeyValuePair<StatementDescriptionNode, INodeValidationData>> GetNodesWithSemanticErrors()
		{
			return ColumnNodeValidity
				.Concat(ObjectNodeValidity)
				.Concat(ProgramNodeValidity)
				.Concat(IdentifierNodeValidity)
				.Where(nv => nv.Value.SemanticError != SemanticError.None)
				.Select(nv => new KeyValuePair<StatementDescriptionNode, INodeValidationData>(nv.Key, nv.Value));
		}
	}

	public class NodeValidationData : INodeValidationData
	{
		private readonly HashSet<OracleObjectReference> _objectReferences;

		public NodeValidationData(OracleObjectReference objectReference) : this(Enumerable.Repeat(objectReference, 1))
		{
		}

		public NodeValidationData(IEnumerable<OracleObjectReference> objectReferences = null)
		{
			_objectReferences = new HashSet<OracleObjectReference>(objectReferences ?? Enumerable.Empty<OracleObjectReference>());
		}

		public bool IsRecognized { get; set; }

		public virtual SemanticError SemanticError
		{
			get { return _objectReferences.Count >= 2 ? SemanticError.AmbiguousReference : SemanticError.None; }
		}

		public ICollection<OracleObjectReference> ObjectReferences { get { return _objectReferences; } }

		public ICollection<string> ObjectNames
		{
			get
			{
				return _objectReferences.Select(t => t.FullyQualifiedName.ToString())
					.Where(n => !String.IsNullOrEmpty(n))
					.OrderByDescending(n => n)
					.ToArray();
			}
		}

		public StatementDescriptionNode Node { get; set; }

		public virtual string ToolTipText
		{
			get
			{
				return SemanticError == SemanticError.None
					? Node.Type == NodeType.NonTerminal
						? null
						: Node.Id
					: FormatToolTipWithObjectNames();
			}
		}

		private string FormatToolTipWithObjectNames()
		{
			var objectNames = ObjectNames;
			return String.Format("{0}{1}", SemanticError.ToToolTipText(), objectNames.Count == 0 ? null : String.Format(" ({0})", String.Join(", ", ObjectNames)));
		}
	}

	public class ProgramValidationData : NodeValidationData
	{
		private readonly SemanticError _semanticError;

		public ProgramValidationData(SemanticError semanticError = SemanticError.None)
		{
			_semanticError = semanticError;
		}

		public override SemanticError SemanticError { get { return _semanticError; } }

		public override string ToolTipText
		{
			get { return _semanticError.ToToolTipText(); }
		}
	}

	public class ColumnNodeValidationData : NodeValidationData
	{
		private readonly OracleColumnReference _columnReference;
		private readonly string[] _ambiguousColumnNames;

		public ColumnNodeValidationData(OracleColumnReference columnReference)
			: base(columnReference.ColumnNodeObjectReferences)
		{
			if (columnReference == null)
			{
				throw new ArgumentNullException("columnReference");
			}
			
			_columnReference = columnReference;

			if (_columnReference.SelectListColumn != null && _columnReference.SelectListColumn.IsAsterisk)
			{
				_ambiguousColumnNames = _columnReference.Owner
					.Columns.Where(c => !c.ExplicitDefinition)
					.SelectMany(c => c.ColumnReferences)
					.Where(c => c.ColumnNodeColumnReferences.Count > 1 && ObjectReferencesEqual(_columnReference, c))
					.SelectMany(c => c.ColumnNodeColumnReferences)
					.Where(c => !String.IsNullOrEmpty(c.Name))
					.Select(c => c.Name.ToSimpleIdentifier())
					.Distinct()
					.ToArray();
			}
			else
			{
				_ambiguousColumnNames = new string[0];
			}
		}

		private static bool ObjectReferencesEqual(OracleReference asteriskColumnReference, OracleColumnReference implicitColumnReference)
		{
			return asteriskColumnReference.ObjectNodeObjectReferences.Count != 1 || implicitColumnReference.ColumnNodeObjectReferences.Count != 1 ||
				   asteriskColumnReference.ObjectNodeObjectReferences.First() == implicitColumnReference.ColumnNodeObjectReferences.First();
		}

		public ICollection<OracleColumn> ColumnNodeColumnReferences { get { return _columnReference.ColumnNodeColumnReferences; } }

		public override SemanticError SemanticError
		{
			get
			{
				return _ambiguousColumnNames.Length > 0 || ColumnNodeColumnReferences.Count >= 2
					? SemanticError.AmbiguousReference
					: base.SemanticError;
			}
		}

		public override string ToolTipText
		{
			get
			{
				var additionalInformation = _ambiguousColumnNames.Length > 0
					? String.Format(" ({0})", String.Join(", ", _ambiguousColumnNames))
					: String.Empty;

				return _ambiguousColumnNames.Length > 0 && ObjectReferences.Count <= 1
					? SemanticError.AmbiguousReference.ToToolTipText() + additionalInformation
					: base.ToolTipText;
			}
		}
	}

	public class InvalidIdentifierNodeValidationData : NodeValidationData
	{
		private readonly string _toolTipText;

		public InvalidIdentifierNodeValidationData(string toolTipText)
		{
			_toolTipText = toolTipText;
		}

 		public override SemanticError SemanticError { get { return SemanticError.InvalidIdentifier; } }

		public override string ToolTipText
		{
			get { return _toolTipText; }
		}
	}
}
