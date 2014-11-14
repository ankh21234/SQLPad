﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;

namespace SqlPad
{
	public interface IDatabaseModel : IDisposable
	{
		ConnectionStringSettings ConnectionString { get; }

		string CurrentSchema { get; set; }

		bool IsInitialized { get; }

		ICollection<string> Schemas { get; }

		bool CanFetch { get; }

		bool IsExecuting { get; }

		bool IsModelFresh { get; }

		void RefreshIfNeeded();

		Task Initialize();

		Task Refresh(bool force);

		event EventHandler Initialized;

		event EventHandler<DatabaseModelConnectionErrorArgs> InitializationFailed;

		event EventHandler<DatabaseModelConnectionErrorArgs> Disconnected;

		event EventHandler RefreshStarted;

		event EventHandler RefreshFinished;

		StatementExecutionResult ExecuteStatement(StatementExecutionModel executionModel);

		Task<StatementExecutionResult> ExecuteStatementAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken);

		Task<ExplainPlanResult> ExplainPlanAsync(string statement, CancellationToken cancellationToken);

		Task<ICollection<SessionExecutionStatisticsRecord>> GetExecutionStatisticsAsync(CancellationToken cancellationToken);

		Task<string> GetActualExecutionPlanAsync(CancellationToken cancellationToken);

		IEnumerable<object[]> FetchRecords(int rowCount);

		bool HasActiveTransaction { get; }

		void CommitTransaction();

		void RollbackTransaction();
	}

	public class ColumnHeader
	{
		public int ColumnIndex { get; set; }

		public string Name { get; set; }

		public string DatabaseDataType { get; set; }

		public Type DataType { get; set; }

		public IColumnValueConverter ValueConverter { get; set; }
	}

	public interface IColumnValueConverter
	{
		object ConvertToCellValue(object rawValue);
	}

	public class DatabaseModelConnectionErrorArgs : EventArgs
	{
		public Exception Exception { get; private set; }

		public DatabaseModelConnectionErrorArgs(Exception exception)
		{
			if (exception == null)
			{
				throw new ArgumentNullException("exception");
			}
			
			Exception = exception;
		}
	}
}
