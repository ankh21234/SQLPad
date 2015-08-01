﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Win32;
using SqlPad.Oracle.DataDictionary;
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Client;
#else
using Oracle.DataAccess.Client;
#endif
using SqlPad.Oracle.ExecutionPlan;
using SqlPad.Oracle.ModelDataProviders;
using SqlPad.Oracle.ToolTips;
using Timer = System.Timers.Timer;

namespace SqlPad.Oracle.DatabaseConnection
{
	public class OracleDatabaseModel : OracleDatabaseModelBase
	{
		internal const int InitialLongFetchSize = 131072;
		private const string ModuleNameSqlPadDatabaseModelBase = "Database model";
		private const string OracleDataAccessRegistryPath = @"Software\Oracle\ODP.NET";
		private const string OracleDataAccessComponents = "Oracle Data Access Components";

		private readonly Timer _refreshTimer = new Timer();
		private readonly HashSet<OracleConnectionAdapter> _connectionAdapters = new HashSet<OracleConnectionAdapter>();
		private readonly string _connectionStringName;
		private readonly OracleConnectionStringBuilder _oracleConnectionString;
		private bool _isInitialized;
		private bool _isDisposed;
		private bool _isRefreshing;
		private bool _cacheLoaded;
		private Task _backgroundTask;
		private readonly CancellationTokenSource _backgroundTaskCancellationTokenSource = new CancellationTokenSource();
		private ILookup<OracleProgramIdentifier, OracleProgramMetadata> _allFunctionMetadata = Enumerable.Empty<OracleProgramMetadata>().ToLookup(m => m.Identifier);
		private ILookup<OracleObjectIdentifier, OracleReferenceConstraint> _uniqueConstraintReferringReferenceConstraints = Enumerable.Empty<OracleReferenceConstraint>().ToLookup(m => m.FullyQualifiedName);
		private readonly ConnectionStringSettings _connectionString;
		private readonly string _moduleName;
		private HashSet<string> _schemas = new HashSet<string>();
		private IReadOnlyDictionary<string, OracleSchema> _allSchemas = new Dictionary<string, OracleSchema>();
		private string _currentSchema;
		private readonly OracleDataDictionaryMapper _dataDictionaryMapper;
		private OracleDataDictionary _dataDictionary = OracleDataDictionary.EmptyDictionary;

		//private readonly OracleCustomTypeGenerator _customTypeGenerator;

		private static readonly Dictionary<string, OracleDataDictionary> CachedDataDictionaries = new Dictionary<string, OracleDataDictionary>();
		private static readonly Dictionary<string, OracleDatabaseModel> DatabaseModels = new Dictionary<string, OracleDatabaseModel>();
		private static readonly Dictionary<string, DatabaseProperty> DatabaseProperties = new Dictionary<string, DatabaseProperty>();
		private static readonly HashSet<string> ActiveDataModelRefresh = new HashSet<string>();
		private static readonly Dictionary<string, List<RefreshModel>> WaitingDataModelRefresh = new Dictionary<string, List<RefreshModel>>();

		public string ConnectionIdentifier { get; }

		private OracleDatabaseModel(ConnectionStringSettings connectionString, string identifier)
		{
			_connectionString = connectionString;
			ConnectionIdentifier = identifier;
			_moduleName = $"{ModuleNameSqlPadDatabaseModelBase}/{identifier}";
			_oracleConnectionString = new OracleConnectionStringBuilder(connectionString.ConnectionString);
			_currentSchema = _oracleConnectionString.UserID;
			_connectionStringName = $"{_oracleConnectionString.DataSource}_{_currentSchema}";

			lock (ActiveDataModelRefresh)
			{
				if (!WaitingDataModelRefresh.ContainsKey(_connectionStringName))
				{
					WaitingDataModelRefresh[_connectionStringName] = new List<RefreshModel>();
				}
			}

			_dataDictionaryMapper = new OracleDataDictionaryMapper(this);

			InternalRefreshStarted += InternalRefreshStartedHandler;
			InternalRefreshCompleted += InternalRefreshCompletedHandler;

			//_customTypeGenerator = OracleCustomTypeGenerator.GetCustomTypeGenerator(connectionString.Name);
		}

		private static event EventHandler InternalRefreshStarted = delegate { };
		private static event EventHandler InternalRefreshCompleted = delegate { };

		private void InternalRefreshStartedHandler(object sender, EventArgs eventArgs)
		{
			var refreshedModel = GetDatabaseModelIfDifferentCompatibleInstance(sender);
			if (refreshedModel == null)
			{
				return;
			}

			_isRefreshing = true;
			RaiseEvent(RefreshStarted);
		}

		private void InternalRefreshCompletedHandler(object sender, EventArgs eventArgs)
		{
			var refreshedModel = GetDatabaseModelIfDifferentCompatibleInstance(sender);
			if (refreshedModel == null)
			{
				return;
			}

			_dataDictionary = refreshedModel._dataDictionary;
			_allFunctionMetadata = refreshedModel._allFunctionMetadata;
			_uniqueConstraintReferringReferenceConstraints = refreshedModel._uniqueConstraintReferringReferenceConstraints;
			_allSchemas = refreshedModel._allSchemas;
			_schemas = refreshedModel._schemas;

			Trace.WriteLine($"{DateTime.Now} - Metadata for '{_connectionStringName}/{ConnectionIdentifier}' has been retrieved from the cache. ");

			lock (ActiveDataModelRefresh)
			{
				List<RefreshModel> refreshModels;
				if (WaitingDataModelRefresh.TryGetValue(_connectionStringName, out refreshModels))
				{
					var modelIndex = refreshModels.FindIndex(m => m.DatabaseModel == this);
					if (modelIndex != -1)
					{
						refreshModels[modelIndex].TaskCompletionSource.SetResult(_dataDictionary);
						refreshModels.RemoveAt(modelIndex);
					}
				}
			}

			_isRefreshing = false;
			RaiseEvent(RefreshCompleted);
		}

		private OracleDatabaseModel GetDatabaseModelIfDifferentCompatibleInstance(object sender)
		{
			var refreshedModel = (OracleDatabaseModel)sender;
			return sender == this || !String.Equals(ConnectionString.ConnectionString, refreshedModel.ConnectionString.ConnectionString)
				? null
				: refreshedModel;
		}

		private void RefreshTimerElapsedHandler(object sender, ElapsedEventArgs elapsedEventArgs)
		{
			SetRefreshTimerInterval();
			RefreshIfNeeded();
		}

		private void SetRefreshTimerInterval()
		{
			_refreshTimer.Interval = ConfigurationProvider.Configuration.DataModel.DataModelRefreshPeriod * 60000;
			Trace.WriteLine($"Data model refresh timer set: {ConfigurationProvider.Configuration.DataModel.DataModelRefreshPeriod} minute(s). ");
		}

		public static void ValidateConfiguration()
		{
			string odacVersion = null;
			var odacRegistryKey = Registry.LocalMachine.OpenSubKey(OracleDataAccessRegistryPath);
			if (odacRegistryKey != null)
			{
				odacVersion = odacRegistryKey.GetSubKeyNames().OrderByDescending(n => n).FirstOrDefault();
			}

			var traceMessage = odacVersion == null
				? $"{OracleDataAccessComponents} registry entry was not found. "
			    : $"{OracleDataAccessComponents} version {odacVersion} found. ";

			Trace.WriteLine(traceMessage);

			Trace.WriteLine($"{OracleDataAccessComponents} assembly version {typeof (OracleConnection).Assembly.FullName}");
		}

		public static OracleDatabaseModel GetDatabaseModel(ConnectionStringSettings connectionString, string identifier = null)
		{
			OracleDatabaseModel databaseModel;
			lock (DatabaseModels)
			{
				if (DatabaseModels.TryGetValue(connectionString.ConnectionString, out databaseModel))
				{
					databaseModel = databaseModel.Clone(identifier);
				}
				else
				{
					DatabaseModels[connectionString.ConnectionString] = databaseModel = new OracleDatabaseModel(connectionString, identifier);
				}
			}

			return databaseModel;
		}

		private void ExecuteActionAsync(Action action)
		{
			if (_isRefreshing)
				return;

			_backgroundTask = Task.Factory.StartNew(action);
		}

		public override ILookup<OracleProgramIdentifier, OracleProgramMetadata> AllFunctionMetadata => _allFunctionMetadata;

		public override ILookup<OracleObjectIdentifier, OracleReferenceConstraint> UniqueConstraintReferringReferenceConstraints => _uniqueConstraintReferringReferenceConstraints;

		protected override ILookup<OracleProgramIdentifier, OracleProgramMetadata> NonSchemaBuiltInFunctionMetadata => _dataDictionary.NonSchemaFunctionMetadata;

		protected override ILookup<OracleProgramIdentifier, OracleProgramMetadata> BuiltInPackageFunctionMetadata => _dataDictionary.BuiltInPackageFunctionMetadata;

		public override ConnectionStringSettings ConnectionString => _connectionString;

		public override bool IsInitialized => _isInitialized;

		public override bool IsMetadataAvailable => _dataDictionary != OracleDataDictionary.EmptyDictionary;

		public override bool HasDbaPrivilege => String.Equals(_oracleConnectionString.DBAPrivilege.ToUpperInvariant(), "SYSDBA");

		public override string CurrentSchema
		{
			get { return _currentSchema; }
			set
			{
				_currentSchema = value;

				foreach (var connectionAdapter in _connectionAdapters)
				{
					connectionAdapter.SwitchCurrentSchema();
				}
			}
		}

		public override string DatabaseDomainName => DatabaseProperties[_connectionString.ConnectionString].DomainName;

		public override ICollection<string> Schemas => _schemas;

		public override IReadOnlyDictionary<string, OracleSchema> AllSchemas => _allSchemas;

		public override IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects => _dataDictionary.AllObjects;

		public override IDictionary<OracleObjectIdentifier, OracleDatabaseLink> DatabaseLinks => _dataDictionary.DatabaseLinks;

		public override IReadOnlyCollection<string> CharacterSets => _dataDictionary.CharacterSets;

		public override IDictionary<int, string> StatisticsKeys => _dataDictionary.StatisticsKeys;

		public override IDictionary<string, string> SystemParameters => _dataDictionary.SystemParameters;

		public override Version Version => DatabaseProperties[_connectionString.ConnectionString].Version;

		public override void RefreshIfNeeded()
		{
			if (IsRefreshNeeded)
			{
				Refresh();
			}
		}

		public override bool IsFresh => !IsRefreshNeeded;

	    private bool IsRefreshNeeded => DataDictionaryValidityTimestamp < DateTime.Now;

	    private DateTime DataDictionaryValidityTimestamp => _dataDictionary.Timestamp.AddMinutes(ConfigurationProvider.Configuration.DataModel.DataModelRefreshPeriod);

	    public override ILookup<string, string> ContextData => _dataDictionaryMapper.GetContextData();

	    public override Task Refresh(bool force = false)
		{
			lock (ActiveDataModelRefresh)
			{
				if (ActiveDataModelRefresh.Contains(_connectionStringName))
				{
					var taskCompletionSource = new TaskCompletionSource<OracleDataDictionary>();
					WaitingDataModelRefresh[_connectionStringName].Add(new RefreshModel { DatabaseModel = this, TaskCompletionSource = taskCompletionSource });

					Trace.WriteLine($"{DateTime.Now} - Cache for '{_connectionStringName}' is being loaded by other requester. Waiting until operation finishes. ");

					RaiseEvent(RefreshStarted);
					return taskCompletionSource.Task;
				}

				ActiveDataModelRefresh.Add(_connectionStringName);
			}

			ExecuteActionAsync(() => LoadSchemaObjectMetadata(force));

			return _backgroundTask;
		}

		public override IConnectionAdapter CreateConnectionAdapter()
		{
			var connectionAdapter = new OracleConnectionAdapter(this);
			_connectionAdapters.Add(connectionAdapter);
			return connectionAdapter;
		}

		internal void RemoveConnectionAdapter(OracleConnectionAdapter connectionAdapter)
		{
			_connectionAdapters.Remove(connectionAdapter);
		}

		public async override Task<ExecutionPlanItemCollection> ExplainPlanAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken)
		{
			if (String.IsNullOrEmpty(OracleConfiguration.Configuration.ExecutionPlan.TargetTable.Name))
			{
				throw new InvalidOperationException("OracleConfiguration/ExecutionPlan/TargetTable[Name] is missing. ");
			}

			var planKey = Convert.ToString(executionModel.StatementText.GetHashCode());
			var targetTableIdentifier = OracleObjectIdentifier.Create(OracleConfiguration.Configuration.ExecutionPlan.TargetTable.Schema, OracleConfiguration.Configuration.ExecutionPlan.TargetTable.Name);
			var explainPlanDataProvider = new ExplainPlanDataProvider(executionModel.StatementText, planKey, targetTableIdentifier);

			await UpdateModelAsync(cancellationToken, false, explainPlanDataProvider.CreateExplainPlanUpdater, explainPlanDataProvider.LoadExplainPlanUpdater);

			return explainPlanDataProvider.ItemCollection;
		}

		public async override Task<string> GetObjectScriptAsync(OracleSchemaObject schemaObject, CancellationToken cancellationToken, bool suppressUserCancellationException = true)
		{
			var scriptDataProvider = new ObjectScriptDataProvider(schemaObject);
			await UpdateModelAsync(cancellationToken, false, scriptDataProvider);
			return scriptDataProvider.ScriptText;
		}

		public async override Task UpdatePartitionDetailsAsync(PartitionDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var partitionDataProvider = new PartitionDataProvider(dataModel, Version);
			var spaceAllocationDataProvider = new TableSpaceAllocationDataProvider(dataModel, dataModel.Owner, dataModel.Name);
			await UpdateModelAsync(cancellationToken, true, partitionDataProvider.PartitionDetailDataProvider, partitionDataProvider.SubPartitionDetailDataProvider, spaceAllocationDataProvider);
		}

		public async override Task UpdateSubPartitionDetailsAsync(SubPartitionDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var partitionDataProvider = new PartitionDataProvider(dataModel, Version);
			var spaceAllocationDataProvider = new TableSpaceAllocationDataProvider(dataModel, dataModel.Owner, dataModel.Name);
			await UpdateModelAsync(cancellationToken, true, partitionDataProvider.SubPartitionDetailDataProvider, spaceAllocationDataProvider);
		}

		public async override Task UpdateTableDetailsAsync(OracleObjectIdentifier objectIdentifier, TableDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var tableDetailDataProvider = new TableDetailDataProvider(dataModel, objectIdentifier);
			var spaceAllocationDataProvider = new TableSpaceAllocationDataProvider(dataModel, objectIdentifier, String.Empty);
			var tableCommentDataProvider = new CommentDataProvider(dataModel, objectIdentifier, null);
			var tableInMemorySpaceAllocationDataProvider = new TableInMemorySpaceAllocationDataProvider(dataModel, objectIdentifier, Version);
			var indexDetailDataProvider = new IndexDetailDataProvider(dataModel, objectIdentifier, null);
			var indexColumnDataProvider = new IndexColumnDataProvider(dataModel, objectIdentifier, null);
			var partitionDataProvider = new PartitionDataProvider(dataModel, objectIdentifier, Version);
			await UpdateModelAsync(cancellationToken, true, tableDetailDataProvider, tableCommentDataProvider, spaceAllocationDataProvider, tableInMemorySpaceAllocationDataProvider, indexDetailDataProvider, indexColumnDataProvider, partitionDataProvider.PartitionDetailDataProvider, partitionDataProvider.SubPartitionDetailDataProvider);
		}

		public async override Task UpdateViewDetailsAsync(OracleObjectIdentifier objectIdentifier, ViewDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var viewCommentDataProvider = new CommentDataProvider(dataModel, objectIdentifier, null);
			var columnConstraintDataProvider = new ConstraintDataProvider(dataModel, objectIdentifier, null);
			await UpdateModelAsync(cancellationToken, true, viewCommentDataProvider, columnConstraintDataProvider);
		}

		public async override Task UpdateColumnDetailsAsync(OracleObjectIdentifier objectIdentifier, string columnName, ColumnDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var columnDetailDataProvider = new ColumnDetailDataProvider(dataModel, objectIdentifier, columnName);
			var columnCommentDataProvider = new CommentDataProvider(dataModel, objectIdentifier, columnName);
			var columnConstraintDataProvider = new ConstraintDataProvider(dataModel, objectIdentifier, columnName);
			var columnIndexesDataProvider = new IndexDetailDataProvider(dataModel, objectIdentifier, columnName);
			var indexColumnDataProvider = new IndexColumnDataProvider(dataModel, objectIdentifier, columnName);
			var detailHistogramDataProvider = new ColumnDetailHistogramDataProvider(dataModel, objectIdentifier, columnName);
			var columnInMemoryDetailsDataProvider = new ColumnDetailInMemoryDataProvider(dataModel, objectIdentifier, columnName, Version);
			await UpdateModelAsync(cancellationToken, true, columnDetailDataProvider, columnCommentDataProvider, columnConstraintDataProvider, columnIndexesDataProvider, indexColumnDataProvider, detailHistogramDataProvider, columnInMemoryDetailsDataProvider);
		}

		public async override Task UpdateUserDetailsAsync(OracleSchemaModel dataModel, CancellationToken cancellationToken)
		{
			var userDetailDataProvider = new UserDataProvider(dataModel);
			await UpdateModelAsync(cancellationToken, true, userDetailDataProvider);
		}

		public async override Task<IReadOnlyList<string>> GetRemoteTableColumnsAsync(string databaseLink, OracleObjectIdentifier schemaObject, CancellationToken cancellationToken)
		{
			var remoteTableColumnDataProvider = new RemoteTableColumnDataProvider(databaseLink, schemaObject);
			await UpdateModelAsync(cancellationToken, false, remoteTableColumnDataProvider);
			return remoteTableColumnDataProvider.Columns;
		}

		internal async Task UpdateModelAsync(CancellationToken cancellationToken, bool suppressException, params IModelDataProvider[] updaters)
		{
			using (var connection = new OracleConnection(_connectionString.ConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.BindByName = true;

					OracleTransaction transaction = null;

					try
					{
						foreach (var updater in updaters)
						{
							command.Parameters.Clear();
							command.CommandText = String.Empty;
							command.CommandType = CommandType.Text;
							updater.InitializeCommand(command);

							try
							{
								if (updater.IsValid)
								{
									if (await connection.EnsureConnectionOpen(cancellationToken))
									{
										connection.ModuleName = _moduleName;
										connection.ActionName = "Model data provider";

										using (var setSchemaCommand = connection.CreateCommand())
										{
											await setSchemaCommand.SetSchema(_currentSchema, cancellationToken);
										}

										transaction = connection.BeginTransaction();
									}

									if (updater.HasScalarResult)
									{
										var result = await command.ExecuteScalarAsynchronous(cancellationToken);
										updater.MapScalarValue(result);
									}
									else
									{
										using (var reader = await command.ExecuteReaderAsynchronous(CommandBehavior.Default, cancellationToken))
										{
											updater.MapReaderData(reader);
										}
									}
								}
							}
							catch (OracleException exception)
							{
								if (exception.Number == (int)OracleErrorCode.UserInvokedCancellation)
								{
									break;
								}

								throw;
							}
						}
					}
					catch (Exception e)
					{
						Trace.WriteLine($"Update model failed: {e}");

						if (!suppressException)
						{
							throw;
						}
					}
					finally
					{
						if (transaction != null)
						{
							await transaction.RollbackAsynchronous();
							transaction.Dispose();
						}
					}
				}
			}
		}

		public override event EventHandler Initialized;
		
		public override event EventHandler<DatabaseModelConnectionErrorArgs> InitializationFailed;

		public override event EventHandler<DatabaseModelConnectionErrorArgs> Disconnected;
		
		public override event EventHandler RefreshStarted;

		public override event EventHandler RefreshCompleted;

		public override void Dispose()
		{
			_isDisposed = true;

			_refreshTimer.Stop();
			_refreshTimer.Dispose();

			if (_backgroundTask != null)
			{
				if (_isRefreshing)
				{
					_isRefreshing = false;
					RaiseEvent(RefreshCompleted);
				}

				if (_backgroundTask.Status == TaskStatus.Running)
				{
					_backgroundTaskCancellationTokenSource.Cancel();
				}
				else
				{
					_backgroundTask.Dispose();
				}
			}

			Initialized = null;
			Disconnected = null;
			InitializationFailed = null;
			RefreshStarted = null;
			RefreshCompleted = null;

			foreach (var adapter in _connectionAdapters.ToArray())
			{
				adapter.Dispose();
			}

			lock (DatabaseModels)
			{
				if (DatabaseModels.ContainsValue(this))
				{
					DatabaseModels.Remove(_connectionString.ConnectionString);
				}
			}

			lock (ActiveDataModelRefresh)
			{
				List<RefreshModel> refreshModels;
				if (WaitingDataModelRefresh.TryGetValue(_connectionStringName, out refreshModels))
				{
					refreshModels.RemoveAll(m => m.DatabaseModel == this);
				}
			}
		}

		private OracleDatabaseModel Clone(string modulePrefix)
		{
			var clone =
				new OracleDatabaseModel(ConnectionString, modulePrefix)
				{
					_currentSchema = _currentSchema,
					_dataDictionary = _dataDictionary,
					_allFunctionMetadata = _allFunctionMetadata,
					_uniqueConstraintReferringReferenceConstraints = _uniqueConstraintReferringReferenceConstraints
				};

			return clone;
		}

		internal void Disconnect(OracleException exception)
		{
			OracleSchemaResolver.Unregister(this);

			if (!_isInitialized)
			{
				return;
			}

			_isInitialized = false;

		    Disconnected?.Invoke(this, new DatabaseModelConnectionErrorArgs(exception));
		}

		private void EnsureDatabaseProperties()
		{
			DatabaseProperty property;
			if (DatabaseProperties.TryGetValue(_connectionString.ConnectionString, out property))
			{
				return;
			}

			using (var connection = new OracleConnection(_connectionString.ConnectionString))
			{
				connection.Open();

				var versionString = connection.ServerVersion.Remove(connection.ServerVersion.LastIndexOf('.'));

				DatabaseProperties[_connectionString.ConnectionString] =
					new DatabaseProperty
					{
						DomainName = String.Equals(connection.DatabaseDomainName, "null") ? null : connection.DatabaseDomainName,
						Version = Version.Parse(versionString)
					};
			}
		}

		internal IEnumerable<T> ExecuteReader<T>(Func<string> getCommandTextFunction, Func<OracleDataReader, T> formatFunction)
		{
			using (var connection = new OracleConnection(_connectionString.ConnectionString))
			{
				connection.Open();

				using (var command = connection.CreateCommand())
				{
					command.CommandText = getCommandTextFunction();
					command.BindByName = true;
					command.InitialLONGFetchSize = OracleDataDictionaryMapper.LongFetchSize;

					connection.ModuleName = _moduleName;
					connection.ActionName = "Fetch data dictionary metadata";

					using (var task = command.ExecuteReaderAsynchronous(CommandBehavior.CloseConnection, _backgroundTaskCancellationTokenSource.Token))
					{
						try
						{
							task.Wait();
						}
						catch (AggregateException)
						{
							if (task.IsCanceled)
							{
								yield break;
							}
							
							throw;
						}

						using (var reader = task.Result)
						{
							while (reader.Read())
							{
								yield return formatFunction(reader);
							}
						}
					}
				}
			}
		}

		private void RemoveActiveRefreshTask()
		{
			lock (ActiveDataModelRefresh)
			{
				RaiseEvent(InternalRefreshCompleted);
				ActiveDataModelRefresh.Remove(_connectionStringName);
			}
		}

		private void LoadSchemaObjectMetadata(bool force)
		{
			TryLoadSchemaObjectMetadataFromCache();

			var isRefreshDone = !IsRefreshNeeded && !force;
			if (isRefreshDone)
			{
				Trace.WriteLine($"{DateTime.Now} - Cache for '{_connectionStringName}' is valid until {DataDictionaryValidityTimestamp}. ");
				return;
			}

			var reason = force ? "has been forced to refresh" : (_dataDictionary.Timestamp > DateTime.MinValue ? "has expired" : "does not exist or is corrupted");
			Trace.WriteLine($"{DateTime.Now} - Cache for '{_connectionStringName}' {reason}. Cache refresh started. ");

			_isRefreshing = true;
			RaiseEvent(InternalRefreshStarted);
			RaiseEvent(RefreshStarted);

			var isRefreshSuccessful = RefreshSchemaObjectMetadata();

			RemoveActiveRefreshTask();

			if (isRefreshSuccessful)
			{
				try
				{
					MetadataCache.StoreDatabaseModelCache(_connectionStringName, stream => _dataDictionary.Serialize(stream));
				}
				catch (Exception e)
				{
					Trace.WriteLine($"Storing metadata cache failed: {e}");
				}
			}

			_isRefreshing = false;
			RaiseEvent(RefreshCompleted);
		}

		private bool RefreshSchemaObjectMetadata()
		{
			var lastRefresh = DateTime.Now;

			try
			{
				var stopwatch = Stopwatch.StartNew();
				UpdateSchemas(_dataDictionaryMapper.GetSchemaNames());
				Trace.WriteLine($"Fetch schema metadata finished in {stopwatch.Elapsed}. ");

				var allObjects = _dataDictionaryMapper.BuildDataDictionary();

				var userFunctions = _dataDictionaryMapper.GetUserFunctionMetadata().SelectMany(g => g).ToArray();
				var builtInFunctions = _dataDictionaryMapper.GetBuiltInFunctionMetadata().SelectMany(g => g).ToArray();
				_allFunctionMetadata = builtInFunctions
					.Concat(userFunctions.Where(f => f.Type == ProgramType.Function))
					.ToLookup(m => m.Identifier);

				stopwatch.Reset();

				var nonSchemaBuiltInFunctionMetadata = new List<OracleProgramMetadata>();

				foreach (var programMetadata in builtInFunctions.Concat(userFunctions))
				{
					if (String.IsNullOrEmpty(programMetadata.Identifier.Owner))
					{
						nonSchemaBuiltInFunctionMetadata.Add(programMetadata);
						continue;
					}

					OracleSchemaObject schemaObject;
					var packageIdentifier = OracleObjectIdentifier.Create(programMetadata.Identifier.Owner, programMetadata.Identifier.Package);
					if (allObjects.TryGetFirstValue(out schemaObject, packageIdentifier))
					{
						((OraclePackage)schemaObject).Functions.Add(programMetadata);
						programMetadata.Owner = schemaObject;
					}
					else
					{
						var programIdentifier = OracleObjectIdentifier.Create(programMetadata.Identifier.Owner, programMetadata.Identifier.Name);
						if (allObjects.TryGetFirstValue(out schemaObject, programIdentifier))
						{
							if (programMetadata.Type == ProgramType.Function)
							{
								((OracleFunction)schemaObject).Metadata = programMetadata;
							}
							else
							{
								((OracleProcedure)schemaObject).Metadata = programMetadata;
							}
							
							programMetadata.Owner = schemaObject;
						}
					}
				}

				Trace.WriteLine($"Function and procedure metadata schema object mapping finished in {stopwatch.Elapsed}. ");

				stopwatch.Reset();

				_uniqueConstraintReferringReferenceConstraints = BuildUniqueConstraintReferringReferenceConstraintLookup(allObjects.Values);

				var databaseLinks = _dataDictionaryMapper.GetDatabaseLinks();
				var characterSets = _dataDictionaryMapper.GetCharacterSets();
				var statisticsKeys = SafeFetchDictionary(_dataDictionaryMapper.GetStatisticsKeys, "OracleDataDictionaryMapper.GetStatisticsKeys failed: ");
				var systemParameters = SafeFetchDictionary(_dataDictionaryMapper.GetSystemParameters, "OracleDataDictionaryMapper.GetSystemParameters failed: ");

				Trace.WriteLine($"Unique constraint, database link, character sets, statistics keys and system parameter mapping finished in {stopwatch.Elapsed}. ");

				_dataDictionary = new OracleDataDictionary(allObjects, databaseLinks, nonSchemaBuiltInFunctionMetadata, characterSets, statisticsKeys, systemParameters, lastRefresh);

				Trace.WriteLine($"{DateTime.Now} - Data dictionary metadata cache has been initialized successfully. ");

				//_customTypeGenerator.GenerateCustomTypeAssembly(_dataDictionary);

				CachedDataDictionaries[_connectionStringName] = _dataDictionary;

				return true;
			}
			catch(Exception e)
			{
				Trace.WriteLine($"Oracle data dictionary refresh failed: {e}");
				return false;
			}
		}

		private Dictionary<TKey, TValue> SafeFetchDictionary<TKey, TValue>(Func<IEnumerable<KeyValuePair<TKey, TValue>>> fetchKeyValuePairFunction, string traceMessage)
		{
			try
			{
				return fetchKeyValuePairFunction().ToDictionary(k => k.Key, k => k.Value);
			}
			catch (Exception e)
			{
				Trace.WriteLine(traceMessage + e);
				return new Dictionary<TKey, TValue>();
			}
		}

		private void TryLoadSchemaObjectMetadataFromCache()
		{
			if (_cacheLoaded)
				return;

			Stream stream;
			OracleDataDictionary dataDictionary;
			if (CachedDataDictionaries.TryGetValue(_connectionStringName, out dataDictionary))
			{
				_dataDictionary = dataDictionary;
				BuildSupportLookups();
			}
			else if (MetadataCache.TryLoadDatabaseModelCache(_connectionStringName, out stream))
			{
				try
				{
					RaiseEvent(RefreshStarted);
					Trace.WriteLine($"{DateTime.Now} - Attempt to load metadata for '{_connectionStringName}/{ConnectionIdentifier}' from cache. ");
					var stopwatch = Stopwatch.StartNew();
					_dataDictionary = CachedDataDictionaries[_connectionStringName] = OracleDataDictionary.Deserialize(stream);
					Trace.WriteLine($"{DateTime.Now} - Metadata for '{_connectionStringName}/{ConnectionIdentifier}' loaded from cache in {stopwatch.Elapsed}");
					BuildSupportLookups();
					RemoveActiveRefreshTask();
				}
				catch (Exception e)
				{
					Trace.WriteLine($"Oracle data dictionary cache deserialization failed: {e}");
				}
				finally
				{
					stream.Dispose();
					RaiseEvent(RefreshCompleted);
				}
			}

			_cacheLoaded = true;
		}

		private void BuildSupportLookups()
		{
			try
			{
				var functionMetadata = _dataDictionary.AllObjects.Values
					.OfType<IFunctionCollection>()
					.SelectMany(o => o.Functions);

				_allFunctionMetadata = FilterFunctionsWithUnavailableMetadata(functionMetadata)
					.Concat(_dataDictionary.NonSchemaFunctionMetadata.SelectMany(g => g))
					.ToLookup(m => m.Identifier);

				_uniqueConstraintReferringReferenceConstraints = BuildUniqueConstraintReferringReferenceConstraintLookup(_dataDictionary.AllObjects.Values);
			}
			catch (Exception e)
			{
				_dataDictionary = OracleDataDictionary.EmptyDictionary;
				Trace.WriteLine($"All function metadata or unique constraint referring reference constraint lookup initialization from cache failed: {e}");
			}
		}

		private IEnumerable<OracleProgramMetadata> FilterFunctionsWithUnavailableMetadata(IEnumerable<OracleProgramMetadata> functions)
		{
			return functions.Where(m => m != null && m.Type != ProgramType.Procedure);
		} 

		private void RaiseEvent(EventHandler eventHandler)
		{
		    eventHandler?.Invoke(this, EventArgs.Empty);
		}

	    public override Task Initialize()
		{
			return Task.Factory.StartNew(InitializeInternal);
		}

		private void InitializeInternal()
		{
			try
			{
				ResolveSchemas();
			}
			catch(Exception e)
			{
				Trace.WriteLine($"Database model initialization failed: {e}");

				InitializationFailed?.Invoke(this, new DatabaseModelConnectionErrorArgs(e));

				return;
			}
			
			_isInitialized = true;
			
			RaiseEvent(Initialized);

			RefreshIfNeeded();

			if (_isDisposed)
			{
				return;
			}

			SetRefreshTimerInterval();
			_refreshTimer.Elapsed += RefreshTimerElapsedHandler;
			_refreshTimer.Start();
		}

		private void ResolveSchemas()
		{
			var schemas = OracleSchemaResolver.ResolveSchemas(this);
			UpdateSchemas(schemas);
		}

		private void UpdateSchemas(IEnumerable<OracleSchema> schemas)
		{
			var allSchemas = schemas.ToDictionary(s => s.Name);
			_schemas = new HashSet<string>(allSchemas.Values.Select(s => s.Name.Trim('"')));
			allSchemas.Add(SchemaPublic, OracleSchema.Public);
			_allSchemas = new ReadOnlyDictionary<string, OracleSchema>(allSchemas);
		}

		private struct RefreshModel
		{
			public TaskCompletionSource<OracleDataDictionary> TaskCompletionSource { get; set; }
			
			public OracleDatabaseModel DatabaseModel { get; set; }
		}

		private struct DatabaseProperty
		{
			public Version Version { get; set; }
			public string DomainName { get; set; }
		}

		private class OracleSchemaResolver
		{
			private static readonly Dictionary<string, OracleSchemaResolver> ActiveResolvers = new Dictionary<string, OracleSchemaResolver>();

			private readonly OracleDatabaseModel _databaseModel;
			private IReadOnlyCollection<OracleSchema> _schemas;

			private OracleSchemaResolver(OracleDatabaseModel databaseModel)
			{
				_databaseModel = databaseModel;
			}

			public static void Unregister(OracleDatabaseModel databaseModel)
			{
				lock (ActiveResolvers)
				{
					ActiveResolvers.Remove(databaseModel.ConnectionString.ConnectionString);
				}
			}

			public static IReadOnlyCollection<OracleSchema> ResolveSchemas(OracleDatabaseModel databaseModel)
			{
				OracleSchemaResolver resolver;
				lock (ActiveResolvers)
				{
					if (!ActiveResolvers.TryGetValue(databaseModel.ConnectionString.ConnectionString, out resolver))
					{
						resolver = new OracleSchemaResolver(databaseModel);
						ActiveResolvers.Add(databaseModel.ConnectionString.ConnectionString, resolver);
					}
				}

				resolver.ResolveSchemas();
				return resolver._schemas;
			}

			private void ResolveSchemas()
			{
				lock (this)
				{
					if (_schemas != null)
					{
						return;
					}

					_databaseModel.EnsureDatabaseProperties();
					_schemas = _databaseModel._dataDictionaryMapper.GetSchemaNames().ToList().AsReadOnly();
					Trace.WriteLine("Schema metadata loaded. ");
				}
			}
		}
	}

	internal enum OracleErrorCode
	{
		UserInvokedCancellation = 1013,
		NotConnectedToOracle = 3114,
		EndOfFileOnCommunicationChannel = 3113,
		SuccessWithCompilationError = 24344
	}
}