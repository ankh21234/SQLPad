﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using SqlPad.FindReplace;
using Timer = System.Timers.Timer;

namespace SqlPad
{
	[DebuggerDisplay("OutputViewer (Title={Title})")]
	public partial class OutputViewer : IDisposable
	{
		private const int MaxHistoryEntrySize = 8192;

		private readonly Timer _timerExecutionMonitor = new Timer(100);
		private readonly Stopwatch _stopWatch = new Stopwatch();
		private readonly StringBuilder _databaseOutputBuilder = new StringBuilder();
		private readonly ObservableCollection<object[]> _resultRows = new ObservableCollection<object[]>();
		private readonly SessionExecutionStatisticsCollection _sessionExecutionStatistics = new SessionExecutionStatisticsCollection();
		private readonly ObservableCollection<CompilationError> _compilationErrors = new ObservableCollection<CompilationError>();
		private readonly StatusInfoModel _statusInfo = new StatusInfoModel();
		private readonly DatabaseProviderConfiguration _providerConfiguration;
		private readonly DocumentPage _documentPage;
		private readonly IConnectionAdapter _connectionAdapter;
		private readonly IStatementValidator _validator;

		private bool _isRunning;
		private bool _isSelectingCells;
		private bool _hasExecutionResult;
		private object _previousSelectedTab;
		private StatementExecutionResult _executionResult;
		private CancellationTokenSource _statementExecutionCancellationTokenSource;

		public event EventHandler<CompilationErrorArgs> CompilationError;

		public IExecutionPlanViewer ExecutionPlanViewer { get; private set; }
		
		public ITraceViewer TraceViewer { get; private set; }

		private bool IsCancellationRequested
		{
			get
			{
				var cancellationTokenSource = _statementExecutionCancellationTokenSource;
				return cancellationTokenSource != null && cancellationTokenSource.IsCancellationRequested;
			}
		}

		public bool KeepDatabaseOutputHistory { get; set; }

		public StatusInfoModel StatusInfo
		{
			get { return _statusInfo; }
		}

		public bool IsBusy
		{
			get { return _isRunning; }
			private set
			{
				_isRunning = value;
				_documentPage.NotifyExecutionEvent();
			}
		}

		public IReadOnlyList<object[]> ResultRowItems { get { return _resultRows; } }

		public IReadOnlyList<SessionExecutionStatisticsRecord> SessionExecutionStatistics { get { return _sessionExecutionStatistics; } }

		public IReadOnlyList<CompilationError> CompilationErrors { get { return _compilationErrors; } }

		public OutputViewer(DocumentPage documentPage)
		{
			InitializeComponent();
			
			_timerExecutionMonitor.Elapsed += delegate { Dispatcher.Invoke(() => UpdateTimerMessage(_stopWatch.Elapsed, IsCancellationRequested)); };

			Application.Current.Deactivated += ApplicationDeactivatedHandler;

			SetUpSessionExecutionStatisticsView();

			Initialize();

			_documentPage = documentPage;

			_providerConfiguration = WorkDocumentCollection.GetProviderConfiguration(_documentPage.CurrentConnection.ProviderName);

			_connectionAdapter = _documentPage.DatabaseModel.CreateConnectionAdapter();

			_validator = _documentPage.InfrastructureFactory.CreateStatementValidator();

			ExecutionPlanViewer = _documentPage.InfrastructureFactory.CreateExecutionPlanViewer(_documentPage.DatabaseModel);
			TabExecutionPlan.Content = ExecutionPlanViewer.Control;

			TraceViewer = _documentPage.InfrastructureFactory.CreateTraceViewer(_connectionAdapter);
			TabTrace.Content = TraceViewer.Control;
		}

		private void SetUpSessionExecutionStatisticsView()
		{
			ApplySessionExecutionStatisticsFilter();
			SetUpSessionExecutionStatisticsSorting();
		}

		private void ApplySessionExecutionStatisticsFilter()
		{
			var view = CollectionViewSource.GetDefaultView(_sessionExecutionStatistics);
			view.Filter = ShowAllSessionExecutionStatistics
				? (Predicate<object>)null
				: o => ((SessionExecutionStatisticsRecord)o).Value != 0;
		}

		private void SetUpSessionExecutionStatisticsSorting()
		{
			var view = CollectionViewSource.GetDefaultView(_sessionExecutionStatistics);
			view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
		}

		public void DisplayResult(StatementExecutionResult executionResult)
		{
			_executionResult = executionResult;
			_hasExecutionResult = true;
			ResultGrid.Columns.Clear();

			foreach (var columnHeader in _executionResult.ColumnHeaders)
			{
				var columnTemplate = CreateResultGridTextColumnTemplate(columnHeader);
				ResultGrid.Columns.Add(columnTemplate);
			}

			ResultGrid.HeadersVisibility = DataGridHeadersVisibility.Column;

			_statusInfo.ResultGridAvailable = true;
			_resultRows.Clear();

			AppendRows(executionResult.InitialResultSet);
		}

		private async void CellHyperlinkClickHandler(object sender, RoutedEventArgs e)
		{
			var cell = (DataGridCell)sender;
			var currentRow = (object[])((Hyperlink)e.OriginalSource).DataContext;
			var referenceConstraintExecutionModel = ((ColumnHeader)cell.Column.Header).FetchReferenceDataExecutionModel;
			var columnIndex = ResultGrid.Columns.IndexOf(cell.Column);
			referenceConstraintExecutionModel.BindVariables[0].Value = currentRow[columnIndex];

			await _connectionAdapter.ExecuteStatementAsync(referenceConstraintExecutionModel, CancellationToken.None);
		}

		private DataGridBoundColumn CreateResultGridTextColumnTemplate(ColumnHeader columnHeader)
		{
			var hyperlinkColumnTemplate = columnHeader.FetchReferenceDataExecutionModel == null
				? null
				: new DataGridHyperlinkColumn();

			var columnTemplate = CreateDataGridTextColumnTemplate(columnHeader, hyperlinkColumnTemplate);
			if (hyperlinkColumnTemplate != null)
			{
				columnTemplate.CellStyle = columnTemplate.CellStyle == null
					? (Style)Resources["CellStyleHyperLink"]
					: (Style)Resources["CellStyleHyperLinkRightAlign"];
			}

			return columnTemplate;
		}

		internal static DataGridBoundColumn CreateDataGridTextColumnTemplate(ColumnHeader columnHeader, DataGridBoundColumn columnTemplate = null)
		{
			columnTemplate = columnTemplate ?? new DataGridTextColumn();
			columnTemplate.Header = columnHeader;
			
			columnTemplate.Binding = new Binding(String.Format("[{0}]", columnHeader.ColumnIndex)) { Converter = CellValueConverter.Instance };
			columnTemplate.EditingElementStyle = (Style)Application.Current.Resources["CellTextBoxStyleReadOnly"];

			if (columnHeader.DataType.In(typeof(Decimal), typeof(Int16), typeof(Int32), typeof(Int64), typeof(Byte)))
			{
				columnTemplate.HeaderStyle = (Style)Application.Current.Resources["HeaderStyleRightAlign"];
				columnTemplate.CellStyle = (Style)Application.Current.Resources["CellStyleRightAlign"];
			}

			return columnTemplate;
		}

		internal static void ShowLargeValueEditor(DataGrid dataGrid)
		{
			var currentRow = (object[])dataGrid.CurrentItem;
			if (currentRow == null || dataGrid.CurrentColumn == null)
				return;

			var columnIndex = dataGrid.Columns.IndexOf(dataGrid.CurrentColumn);
			var cellValue = currentRow[columnIndex];
			var largeValue = cellValue as ILargeValue;
			if (largeValue != null)
			{
				new LargeValueEditor(((ColumnHeader)dataGrid.CurrentColumn.Header).Name, largeValue) { Owner = Window.GetWindow(dataGrid) }.ShowDialog();
			}
		}

		public void Cancel()
		{
			var cancellationTokenSource = _statementExecutionCancellationTokenSource;
			if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
			{
				cancellationTokenSource.Cancel();
			}
		}

		private void Initialize()
		{
			_hasExecutionResult = false;

			_resultRows.Clear();
			_compilationErrors.Clear();
			_sessionExecutionStatistics.Clear();
			TabCompilationErrors.Visibility = Visibility.Collapsed;
			TabStatistics.Visibility = Visibility.Collapsed;
			TabExecutionPlan.Visibility = Visibility.Collapsed;

			_statusInfo.ResultGridAvailable = false;
			_statusInfo.MoreRowsAvailable = false;
			_statusInfo.DdlStatementExecutedSuccessfully = false;
			_statusInfo.AffectedRowCount = -1;
			_statusInfo.SelectedRowIndex = 0;
			
			WriteDatabaseOutput(String.Empty);

			LastStatementText = String.Empty;

			ResultGrid.HeadersVisibility = DataGridHeadersVisibility.None;

			_previousSelectedTab = TabControlResult.SelectedItem;

			SelectDefaultTabIfNeeded();
		}

		private void SelectDefaultTabIfNeeded()
		{
			if (!IsTabAlwaysVisible(TabControlResult.SelectedItem))
			{
				TabControlResult.SelectedItem = TabResultSet;
			}
		}

		private bool IsPreviousTabAlwaysVisible
		{
			get { return _previousSelectedTab != null && IsTabAlwaysVisible(_previousSelectedTab); }
		}
		
		private void SelectPreviousTab()
		{
			if (_previousSelectedTab != null)
			{
				TabControlResult.SelectedItem = _previousSelectedTab;
			}
		}

		private void ShowExecutionPlan()
		{
			TabExecutionPlan.Visibility = Visibility.Visible;
			TabControlResult.SelectedItem = TabExecutionPlan;
		}

		private void ShowCompilationErrors()
		{
			TabCompilationErrors.Visibility = Visibility.Visible;
			TabControlResult.SelectedItem = TabCompilationErrors;
		}

		public Task ExecuteExplainPlanAsync(StatementExecutionModel executionModel)
		{
			return ExecuteUsingCancellationToken(() => ExecuteExplainPlanAsyncInternal(executionModel));
		}

		private async Task ExecuteExplainPlanAsyncInternal(StatementExecutionModel executionModel)
		{
			SelectDefaultTabIfNeeded();

			TabStatistics.Visibility = Visibility.Collapsed;

			var actionResult = await SafeTimedActionAsync(() => ExecutionPlanViewer.ExplainAsync(executionModel, _statementExecutionCancellationTokenSource.Token));

			if (_statementExecutionCancellationTokenSource.Token.IsCancellationRequested)
			{
				NotifyExecutionCanceled();
			}
			else
			{
				UpdateTimerMessage(actionResult.Elapsed, false);

				if (actionResult.IsSuccessful)
				{
					ShowExecutionPlan();
				}
				else
				{
					Messages.ShowError(actionResult.Exception.Message);
				}
			}
		}

		public Task ExecuteDatabaseCommandAsync(StatementExecutionModel executionModel)
		{
			return ExecuteUsingCancellationToken(() => ExecuteDatabaseCommandAsyncInternal(executionModel));
		}

		private async Task ExecuteDatabaseCommandAsyncInternal(StatementExecutionModel executionModel)
		{
			Initialize();

			_connectionAdapter.EnableDatabaseOutput = EnableDatabaseOutput;

			var executionHistoryRecord = new StatementExecutionHistoryEntry { ExecutedAt = DateTime.Now };

			Task<StatementExecutionResult> innerTask = null;
			var actionResult = await SafeTimedActionAsync(() => innerTask = _connectionAdapter.ExecuteStatementAsync(executionModel, _statementExecutionCancellationTokenSource.Token));

			if (!actionResult.IsSuccessful)
			{
				Messages.ShowError(actionResult.Exception.Message);
				return;
			}

			LastStatementText = executionHistoryRecord.StatementText = executionModel.StatementText;

			if (executionHistoryRecord.StatementText.Length <= MaxHistoryEntrySize)
			{
				_providerConfiguration.AddStatementExecution(executionHistoryRecord);
			}
			else
			{
				Trace.WriteLine(String.Format("Executes statement not stored in the execution history. The maximum allowed size is {0} characters while the statement has {1} characters.", MaxHistoryEntrySize, executionHistoryRecord.StatementText.Length));
			}

			var executionResult = innerTask.Result;
			if (!executionResult.ExecutedSuccessfully)
			{
				NotifyExecutionCanceled();
				return;
			}

			TransactionControlVisibity = _connectionAdapter.HasActiveTransaction ? Visibility.Visible : Visibility.Collapsed;

			UpdateTimerMessage(actionResult.Elapsed, false);
			
			WriteDatabaseOutput(executionResult.DatabaseOutput);

			if (executionResult.Statement.GatherExecutionStatistics)
			{
				await ExecutionPlanViewer.ShowActualAsync(_connectionAdapter, _statementExecutionCancellationTokenSource.Token);
				TabStatistics.Visibility = Visibility.Visible;
				TabExecutionPlan.Visibility = Visibility.Visible;
				_sessionExecutionStatistics.MergeWith(await _connectionAdapter.GetExecutionStatisticsAsync(_statementExecutionCancellationTokenSource.Token));
				SelectPreviousTab();
			}
			else if (IsPreviousTabAlwaysVisible)
			{
				SelectPreviousTab();
			}

			if (executionResult.CompilationErrors.Count > 0)
			{
				var lineOffset = _documentPage.Editor.GetLineNumberByOffset(executionModel.Statement.SourcePosition.IndexStart);
				foreach (var error in executionResult.CompilationErrors)
				{
					error.Line += lineOffset;
					_compilationErrors.Add(error);
				}

				ShowCompilationErrors();
			}

			if (executionResult.ColumnHeaders.Count == 0)
			{
				if (executionResult.AffectedRowCount == -1)
				{
					_statusInfo.DdlStatementExecutedSuccessfully = true;
				}
				else
				{
					_statusInfo.AffectedRowCount = executionResult.AffectedRowCount;
				}

				return;
			}

			//await _validator.ApplyReferenceConstraintsAsync(executionResult, _documentPage.DatabaseModel, _statementExecutionCancellationTokenSource.Token);

			DisplayResult(executionResult);
		}

		private void NotifyExecutionCanceled()
		{
			_statusInfo.ExecutionTimerMessage = "Canceled";
		}

		private async Task ExecuteUsingCancellationToken(Func<Task> function)
		{
			IsBusy = true;

			using (_statementExecutionCancellationTokenSource = new CancellationTokenSource())
			{
				await function();

				_statementExecutionCancellationTokenSource = null;
			}

			IsBusy = false;
		}

		private void TabControlResultGiveFeedbackHandler(object sender, GiveFeedbackEventArgs e)
		{
			e.Handled = true;
		}

		private void CanExportDataHandler(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = ResultGrid.Items.Count > 0;
		}

		private void CanGenerateCSharpQueryClassHandler(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = ResultGrid.Columns.Count > 0;
		}

		private void ExportDataFileHandler(object sender, ExecutedRoutedEventArgs args)
		{
			var dataExporter = (IDataExporter)args.Parameter;
			var dialog = new SaveFileDialog { Filter = dataExporter.FileNameFilter, OverwritePrompt = true };
			if (dialog.ShowDialog() != true)
			{
				return;
			}

			App.SafeActionWithUserError(() => dataExporter.ExportToFile(dialog.FileName, ResultGrid, _documentPage.InfrastructureFactory.DataExportConverter));
		}

		private void ExportDataClipboardHandler(object sender, ExecutedRoutedEventArgs args)
		{
			var dataExporter = (IDataExporter)args.Parameter;

			App.SafeActionWithUserError(() => dataExporter.ExportToClipboard(ResultGrid, _documentPage.InfrastructureFactory.DataExportConverter));
		}

		private const string ExportClassTemplate =
@"using System;
using System.Data;

public class Query
{{
	private IDbConnection _connection;

	private const string CommandText =
@""{0}"";
{1}	
	private IEnumerable<ResultRow> Execute()
	{{
		using (var command = _connection.CreateCommand())
		{{
			command.CommandText = CommandText;
			{2}			_connection.Open();

			using (var reader = command.ExecuteReader())
			{{
				while (reader.Read())
				{{
					var row =
						new ResultRow
						{{
{3}
						}};

					yield return row;
				}}
			}}

			_connection.Close();
		}}
	}}

	private static T GetReaderValue<T>(object value)
	{{
		return value == DBNull.Value
			? default(T)
			: (T)value;
	}}
}}
";

		private void GenerateCSharpQuery(object sender, ExecutedRoutedEventArgs args)
		{
			var dialog = new SaveFileDialog { Filter = "C# files (*.cs)|*.cs|All files (*.*)|*", OverwritePrompt = true };
			if (dialog.ShowDialog() != true)
			{
				return;
			}

			var columnMapBuilder = new StringBuilder();
			var resultRowPropertyBuilder = new StringBuilder();
			var bindVariableBuilder = new StringBuilder();
			var parameterBuilder = new StringBuilder();

			if (_executionResult.Statement.BindVariables.Count > 0)
			{
				bindVariableBuilder.AppendLine();
				parameterBuilder.AppendLine();
				
				foreach (var bindVariable in _executionResult.Statement.BindVariables)
				{
					bindVariableBuilder.Append("\tpublic ");
					bindVariableBuilder.Append(bindVariable.InputType);
					bindVariableBuilder.Append(" ");
					bindVariableBuilder.Append(bindVariable.Name);
					bindVariableBuilder.AppendLine(" { get; set; }");

					var parameterName = String.Format("parameter{0}", bindVariable.Name);
					parameterBuilder.Append("\t\t\tvar ");
					parameterBuilder.Append(parameterName);
					parameterBuilder.AppendLine(" = command.CreateParameter();");
					parameterBuilder.Append("\t\t\t");
					parameterBuilder.Append(parameterName);
					parameterBuilder.Append(".Value = ");
					parameterBuilder.Append(bindVariable.Name);
					parameterBuilder.AppendLine(";");
					parameterBuilder.Append("\t\t\tcommand.Parameters.Add(");
					parameterBuilder.Append(parameterName);
					parameterBuilder.AppendLine(");");
					parameterBuilder.AppendLine();
				}
			}

			var index = 0;
			foreach (var column in _executionResult.ColumnHeaders)
			{
				index++;

				var dataTypeName = String.Equals(column.DataType.Namespace, "System")
					? column.DataType.Name
					: column.DataType.FullName;

				if (column.DataType.IsValueType)
				{
					dataTypeName = String.Format("{0}?", dataTypeName);
				}

				columnMapBuilder.Append("\t\t\t\t\t\t\t");
				columnMapBuilder.Append(column.Name);
				columnMapBuilder.Append(" = GetReaderValue<");
				columnMapBuilder.Append(dataTypeName);
				columnMapBuilder.Append(">(reader[\"");
				columnMapBuilder.Append(column.Name);
				columnMapBuilder.Append("\"])");

				if (index < ResultGrid.Columns.Count)
				{
					columnMapBuilder.AppendLine(",");
				}

				resultRowPropertyBuilder.Append("\tpublic ");
				resultRowPropertyBuilder.Append(dataTypeName);
				resultRowPropertyBuilder.Append(" ");
				resultRowPropertyBuilder.Append(column.Name);
				resultRowPropertyBuilder.AppendLine(" { get; set; }");
			}

			var statementText = _executionResult.Statement.StatementText.Replace("\"", "\"\"");
			var queryClass = String.Format(ExportClassTemplate, statementText, bindVariableBuilder, parameterBuilder, columnMapBuilder);

			using (var writer = File.CreateText(dialog.FileName))
			{
				writer.WriteLine(queryClass);
				writer.WriteLine("public class ResultRow");
				writer.WriteLine("{");
				writer.Write(resultRowPropertyBuilder);
				writer.WriteLine("}");
			}
		}

		private void ResultGridMouseDoubleClickHandler(object sender, MouseButtonEventArgs e)
		{
			ShowLargeValueEditor(ResultGrid);
		}

		private void ResultGridSelectedCellsChangedHandler(object sender, SelectedCellsChangedEventArgs e)
		{
			if (_isSelectingCells)
			{
				return;
			}

			_statusInfo.SelectedRowIndex = ResultGrid.CurrentCell.Item == null
				? 0
				: ResultGrid.Items.IndexOf(ResultGrid.CurrentCell.Item) + 1;

			CalculateSelectedCellStatistics();
		}

		private void CalculateSelectedCellStatistics()
		{
			if (ResultGrid.SelectedCells.Count <= 1)
			{
				SelectedCellInfoVisibility = Visibility.Collapsed;
				return;
			}

			var sum = 0m;
			var min = Decimal.MaxValue;
			var max = Decimal.MinValue;
			var count = 0;
			var hasOnlyNumericValues = true;
			foreach (var selectedCell in ResultGrid.SelectedCells)
			{
				var cellValue = ((object[])selectedCell.Item)[selectedCell.Column.DisplayIndex];
				var stringValue = cellValue.ToString();
				if (String.IsNullOrEmpty(stringValue))
				{
					continue;
				}

				if (hasOnlyNumericValues)
				{
					try
					{
						var numericValue = Convert.ToDecimal(stringValue, CultureInfo.CurrentCulture);
						sum += numericValue;

						if (numericValue > max)
						{
							max = numericValue;
						}

						if (numericValue < min)
						{
							min = numericValue;
						}
					}
					catch
					{
						hasOnlyNumericValues = false;
					}
				}

				count++;
			}

			SelectedCellValueCount = count;

			if (count > 0)
			{
				SelectedCellSum = sum;
				SelectedCellMin = min;
				SelectedCellMax = max;
				SelectedCellAverage = sum / count;

				SelectedCellNumericInfoVisibility = hasOnlyNumericValues ? Visibility.Visible : Visibility.Collapsed;
			}
			else
			{
				SelectedCellNumericInfoVisibility = Visibility.Collapsed;
			}

			SelectedCellInfoVisibility = Visibility.Visible;
		}

		private void ColumnHeaderMouseClickHandler(object sender, RoutedEventArgs e)
		{
			var header = e.OriginalSource as DataGridColumnHeader;
			if (header == null)
			{
				return;
			}

			if (Keyboard.Modifiers != ModifierKeys.Shift)
			{
				ResultGrid.SelectedCells.Clear();
			}

			_isSelectingCells = true;

			var cells = ResultGrid.Items.Cast<object[]>()
				.Select(r => new DataGridCellInfo(r, header.Column));

			foreach (var cell in cells)
			{
				if (!ResultGrid.SelectedCells.Contains(cell))
				{
					ResultGrid.SelectedCells.Add(cell);
				}
			}

			_isSelectingCells = false;

			_statusInfo.SelectedRowIndex = ResultGrid.SelectedCells.Count;

			CalculateSelectedCellStatistics();

			ResultGrid.Focus();
		}

		private bool IsTabAlwaysVisible(object tabItem)
		{
			return TabControlResult.Items.IndexOf(tabItem).In(0, 2);
		}

		private async void ResultGridScrollChangedHandler(object sender, ScrollChangedEventArgs e)
		{
			if (e.VerticalOffset + e.ViewportHeight != e.ExtentHeight)
			{
				return;
			}

			if (!CanFetchNextRows())
			{
				return;
			}

			await ExecuteUsingCancellationToken(FetchNextRows);
		}

		private async void FetchAllRowsHandler(object sender, ExecutedRoutedEventArgs args)
		{
			await ExecuteUsingCancellationToken(FetchAllRows);
		}

		private async Task FetchAllRows()
		{
			while (_connectionAdapter.CanFetch && !IsCancellationRequested)
			{
				await FetchNextRows();
			}
		}

		private void ErrorListMouseDoubleClickHandler(object sender, MouseButtonEventArgs e)
		{
			var errorUnderCursor = (CompilationError)((DataGrid)sender).CurrentItem;
			if (errorUnderCursor == null || CompilationError == null)
			{
				return;
			}

			CompilationError(this, new CompilationErrorArgs(errorUnderCursor));
		}

		private void ResultGridBeginningEditHandler(object sender, DataGridBeginningEditEventArgs e)
		{
			var textCompositionArgs = e.EditingEventArgs as TextCompositionEventArgs;
			if (textCompositionArgs != null)
			{
				e.Cancel = true;
			}
		}

		private void CanFetchAllRowsHandler(object sender, CanExecuteRoutedEventArgs canExecuteRoutedEventArgs)
		{
			canExecuteRoutedEventArgs.CanExecute = CanFetchNextRows();
			canExecuteRoutedEventArgs.ContinueRouting = canExecuteRoutedEventArgs.CanExecute;
		}

		private bool CanFetchNextRows()
		{
			return _hasExecutionResult && !IsBusy && _connectionAdapter.CanFetch && !_connectionAdapter.IsExecuting;
		}

		private async Task FetchNextRows()
		{
			Task<IReadOnlyList<object[]>> innerTask = null;
			var batchSize = StatementExecutionModel.DefaultRowBatchSize - _resultRows.Count % StatementExecutionModel.DefaultRowBatchSize;
			var exception = await App.SafeActionAsync(() => innerTask = _connectionAdapter.FetchRecordsAsync(batchSize, _statementExecutionCancellationTokenSource.Token));

			if (exception != null)
			{
				Messages.ShowError(exception.Message);
			}
			else
			{
				AppendRows(innerTask.Result);

				if (_executionResult.Statement.GatherExecutionStatistics)
				{
					_sessionExecutionStatistics.MergeWith(await _connectionAdapter.GetExecutionStatisticsAsync(_statementExecutionCancellationTokenSource.Token));
				}
			}
		}

		private void AppendRows(IEnumerable<object[]> rows)
		{
			_resultRows.AddRange(rows);
			_statusInfo.MoreRowsAvailable = _connectionAdapter.CanFetch;
		}

		private async Task<ActionResult> SafeTimedActionAsync(Func<Task> action)
		{
			var actionResult = new ActionResult();

			_stopWatch.Restart();
			_timerExecutionMonitor.Start();

			actionResult.Exception = await App.SafeActionAsync(action);
			actionResult.Elapsed = _stopWatch.Elapsed;

			_timerExecutionMonitor.Stop();
			_stopWatch.Stop();

			return actionResult;
		}

		public void Dispose()
		{
			Application.Current.Deactivated -= ApplicationDeactivatedHandler;
			_timerExecutionMonitor.Stop();
			_timerExecutionMonitor.Dispose();
			_connectionAdapter.Dispose();
		}

		private void ButtonCommitTransactionClickHandler(object sender, RoutedEventArgs e)
		{
			App.SafeActionWithUserError(() =>
			{
				_connectionAdapter.CommitTransaction();
				SetValue(TransactionControlVisibityProperty, Visibility.Collapsed);
			});

			_documentPage.Editor.Focus();
		}

		private async void ButtonRollbackTransactionClickHandler(object sender, RoutedEventArgs e)
		{
			IsTransactionControlEnabled = false;

			IsBusy = true;

			var result = await SafeTimedActionAsync(_connectionAdapter.RollbackTransaction);
			UpdateTimerMessage(result.Elapsed, false);

			if (result.IsSuccessful)
			{
				TransactionControlVisibity = Visibility.Collapsed;
			}
			else
			{
				Messages.ShowError(result.Exception.Message);
			}

			IsTransactionControlEnabled = true;

			IsBusy = false;

			_documentPage.Editor.Focus();
		}

		private void WriteDatabaseOutput(string output)
		{
			if (!KeepDatabaseOutputHistory)
			{
				_databaseOutputBuilder.Clear();
			}

			if (!String.IsNullOrEmpty(output))
			{
				_databaseOutputBuilder.AppendLine(output);
			}

			DatabaseOutput = _databaseOutputBuilder.ToString();
		}

		private void UpdateTimerMessage(TimeSpan timeSpan, bool isCanceling)
		{
			string formattedValue;
			if (timeSpan.TotalMilliseconds < 1000)
			{
				formattedValue = String.Format("{0} {1}", (int)timeSpan.TotalMilliseconds, "ms");
			}
			else if (timeSpan.TotalMilliseconds < 60000)
			{
				formattedValue = String.Format("{0} {1}", Math.Round(timeSpan.TotalMilliseconds / 1000, 2), "s");
			}
			else
			{
				formattedValue = String.Format("{0:00}:{1:00}", (int)timeSpan.TotalMinutes, timeSpan.Seconds);
			}

			if (isCanceling)
			{
				formattedValue = String.Format("Canceling... {0}", formattedValue);
			}

			_statusInfo.ExecutionTimerMessage = formattedValue;
		}

		private struct ActionResult
		{
			public bool IsSuccessful { get { return Exception == null; } }

			public Exception Exception { get; set; }

			public TimeSpan Elapsed { get; set; }
		}

		/*private void SearchTextChangedHandler(object sender, TextChangedEventArgs e)
		{
			var searchedWords = TextSearchHelper.GetSearchedWords(SearchPhraseTextBox.Text);
			Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => ResultGrid.HighlightTextItems(TextSearchHelper.GetRegexPattern(searchedWords))));
		}*/

		private void DataGridTabHeaderMouseEnterHandler(object sender, MouseEventArgs e)
		{
			if (String.IsNullOrWhiteSpace(LastStatementText))
			{
				return;
			}
			
			DataGridTabHeaderPopupTextBox.FontFamily = _documentPage.Editor.FontFamily;
			DataGridTabHeaderPopupTextBox.FontSize = _documentPage.Editor.FontSize;
			DataGridTabHeaderPopup.IsOpen = true;
		}

		private void OutputViewerMouseMoveHandler(object sender, MouseEventArgs e)
		{
			if (!DataGridTabHeaderPopup.IsOpen)
			{
				return;
			}

			var position = e.GetPosition(DataGridTabHeaderPopup.Child);

			if (position.Y < 0 || position.Y > DataGridTabHeaderPopup.Child.RenderSize.Height + DataGridTabHeader.RenderSize.Height || position.X < 0 || position.X > DataGridTabHeaderPopup.Child.RenderSize.Width)
			{
				DataGridTabHeaderPopup.IsOpen = false;
			}
		}

		private void ApplicationDeactivatedHandler(object sender, EventArgs eventArgs)
		{
			DataGridTabHeaderPopup.IsOpen = false;
		}

		private void DataGridTabHeaderPopupMouseLeaveHandler(object sender, MouseEventArgs e)
		{
			DataGridTabHeaderPopup.IsOpen = false;
		}

		private void ButtonDebuggerContinueClickHandler(object sender, RoutedEventArgs e)
		{
		}

		private void ButtonDebuggerStepIntoClickHandler(object sender, RoutedEventArgs e)
		{
		}

		private void ButtonDebuggerStepOverClickHandler(object sender, RoutedEventArgs e)
		{
		}

		private void ButtonDebuggerAbortClickHandler(object sender, RoutedEventArgs e)
		{
		}
	}
}
