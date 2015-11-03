using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.DataDictionary;
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Client;
#else
using Oracle.DataAccess.Client;
#endif
using SqlPad.Oracle.ExecutionPlan;

namespace SqlPad.Oracle.ModelDataProviders
{
	internal class ExplainPlanDataProvider
	{
		private readonly ExplainPlanModelInternal _dataMmodel;
		
		public IModelDataProvider CreateExplainPlanUpdater { get; private set; }
		
		public IModelDataProvider LoadExplainPlanUpdater { get; private set; }

		public ExecutionPlanItemCollection ItemCollection => _dataMmodel.ItemCollection;

	    public ExplainPlanDataProvider(string statementText, string planKey, OracleObjectIdentifier targetTableIdentifier)
		{
			_dataMmodel = new ExplainPlanModelInternal(statementText, planKey, targetTableIdentifier);
			CreateExplainPlanUpdater = new CreateExplainPlanDataProviderInternal(_dataMmodel);
			LoadExplainPlanUpdater = new LoadExplainPlanDataProviderInternal(_dataMmodel);
		}

		private class CreateExplainPlanDataProviderInternal : ModelDataProvider<ExplainPlanModelInternal>
		{
			public CreateExplainPlanDataProviderInternal(ExplainPlanModelInternal model) : base(model)
			{
			}

			public override void InitializeCommand(OracleCommand command)
			{
				command.CommandText = $"EXPLAIN PLAN SET STATEMENT_ID = '{DataModel.ExecutionPlanKey}' INTO {DataModel.TargetTableName} FOR\n{DataModel.StatementText}";
			}

			public override Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
			{
				return Task.FromResult(0);
			}
		}

		private class LoadExplainPlanDataProviderInternal : ModelDataProvider<ExplainPlanModelInternal>
		{
			private readonly ExecutionPlanBuilder _planBuilder = new ExecutionPlanBuilder();

			public LoadExplainPlanDataProviderInternal(ExplainPlanModelInternal model) : base(model)
			{
			}

			public override void InitializeCommand(OracleCommand command)
			{
				command.CommandText = String.Format(OracleDatabaseCommands.SelectExplainPlanCommandText, DataModel.TargetTableName);
				command.AddSimpleParameter("STATEMENT_ID", DataModel.ExecutionPlanKey);
			}

			public override async Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
			{
				DataModel.ItemCollection = await _planBuilder.Build(reader, cancellationToken);
			}
		}

		private class ExplainPlanModelInternal : ModelBase
		{
			public string StatementText { get; }
			
			public string ExecutionPlanKey { get; }
			
			public string TargetTableName { get; }

			public ExecutionPlanItemCollection ItemCollection { get; set; }
			
			public ExplainPlanModelInternal(string statementText, string executionPlanKey, OracleObjectIdentifier targetTableIdentifier)
			{
				StatementText = statementText;
				ExecutionPlanKey = executionPlanKey;
				TargetTableName = targetTableIdentifier.ToString();
			}
		}
	}

	internal class ExecutionPlanBuilder : ExecutionPlanBuilderBase<ExecutionPlanItemCollection, ExecutionPlanItem> { }

	internal abstract class ExecutionPlanBuilderBase<TCollection, TItem> where TCollection : ExecutionPlanItemCollectionBase<TItem>, new() where TItem : ExecutionPlanItem, new()
	{
		public async Task<TCollection> Build(OracleDataReader reader, CancellationToken cancellationToken)
		{
			var planItemCollection = InitializePlanItemCollection();

			while (await reader.ReadAsynchronous(cancellationToken))
			{
				var item = await CreatePlanItem(reader, cancellationToken);

				FillData(reader, item);

				planItemCollection.Add(item);
			}

			planItemCollection.Freeze();

			return planItemCollection;
		}

		protected virtual TCollection InitializePlanItemCollection()
		{
			return new TCollection();
		}

		protected virtual void FillData(IDataRecord reader, TItem item) { }

		private static async Task<TItem> CreatePlanItem(OracleDataReader reader, CancellationToken cancellationToken)
		{
			var time = OracleReaderValueConvert.ToInt32(reader["TIME"]);
			var otherData = OracleReaderValueConvert.ToString(await reader.GetValueAsynchronous(reader.GetOrdinal("OTHER_XML"), cancellationToken));

			return
				new TItem
				{
					Id = Convert.ToInt32(reader["ID"]),
					ParentId = OracleReaderValueConvert.ToInt32(reader["PARENT_ID"]),
					Depth = Convert.ToInt32(reader["DEPTH"]),
					Operation = (string)reader["OPERATION"],
					Options = OracleReaderValueConvert.ToString(reader["OPTIONS"]),
					Optimizer = OracleReaderValueConvert.ToString(reader["OPTIMIZER"]),
					ObjectOwner = OracleReaderValueConvert.ToString(reader["OBJECT_OWNER"]),
					ObjectName = OracleReaderValueConvert.ToString(reader["OBJECT_NAME"]),
					ObjectAlias = OracleReaderValueConvert.ToString(reader["OBJECT_ALIAS"]),
					ObjectType = OracleReaderValueConvert.ToString(reader["OBJECT_TYPE"]),
					Cost = OracleReaderValueConvert.ToInt64(reader["COST"]),
					Cardinality = OracleReaderValueConvert.ToInt64(reader["CARDINALITY"]),
					Bytes = OracleReaderValueConvert.ToInt64(reader["BYTES"]),
					PartitionStart = OracleReaderValueConvert.ToString(reader["PARTITION_START"]),
					PartitionStop = OracleReaderValueConvert.ToString(reader["PARTITION_STOP"]),
					Distribution = OracleReaderValueConvert.ToString(reader["DISTRIBUTION"]),
					CpuCost = OracleReaderValueConvert.ToInt64(reader["CPU_COST"]),
					IoCost = OracleReaderValueConvert.ToInt64(reader["IO_COST"]),
					TempSpace = OracleReaderValueConvert.ToInt64(reader["TEMP_SPACE"]),
					AccessPredicates = OracleReaderValueConvert.ToString(reader["ACCESS_PREDICATES"]),
					FilterPredicates = OracleReaderValueConvert.ToString(reader["FILTER_PREDICATES"]),
					Time = time.HasValue ? TimeSpan.FromSeconds(time.Value) : (TimeSpan?)null,
					QueryBlockName = OracleReaderValueConvert.ToString(reader["QBLOCK_NAME"]),
					Other = String.IsNullOrEmpty(otherData) ? null : XElement.Parse(otherData)
				};
		}
	}
}
