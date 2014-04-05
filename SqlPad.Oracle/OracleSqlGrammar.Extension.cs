﻿using System.Diagnostics;

namespace SqlPad.Oracle
{
	public partial class SqlGrammar
	{
	}

	[DebuggerDisplay("SqlGrammarStartSymbol (Id={Id})")]
	public partial class SqlGrammarStartSymbol
	{

	}

	[DebuggerDisplay("SqlGrammarRuleSequence (Elements={Items.Length}, Comment={Comment})")]
	public partial class SqlGrammarRuleSequence
	{
	}

	[DebuggerDisplay("SqlGrammarTerminal (Id={Id}, Value={Value}, RegexValue={RegexValue})")]
	public partial class SqlGrammarTerminal
	{
		
	}

	[DebuggerDisplay("SqlGrammarRuleSequenceTerminal (Id={Id}, IsOptional={IsOptional})")]
	public partial class SqlGrammarRuleSequenceTerminal : ISqlGrammarRuleSequenceItem
	{
		public bool IsRequired
		{
			get { return !isOptionalFieldSpecified || (isOptionalFieldSpecified && !IsOptional); }
		}

		public NodeType Type { get { return NodeType.Terminal; } }
	}

	[DebuggerDisplay("SqlGrammarRuleSequenceNonTerminal (Id={Id}, IsOptional={IsOptional})")]
	public partial class SqlGrammarRuleSequenceNonTerminal : ISqlGrammarRuleSequenceItem
	{
		public bool IsRequired
		{
			get { return !isOptionalFieldSpecified || (isOptionalFieldSpecified && !IsOptional); }
		}

		public NodeType Type { get { return NodeType.NonTerminal; } }
	}

	public interface ISqlGrammarRuleSequenceItem
	{
		NodeType Type { get; }

		string Id { get; }

		bool IsRequired { get; }
	}
}
