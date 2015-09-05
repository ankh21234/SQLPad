﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;

namespace SqlPad
{
	public partial class DebuggerViewer
	{
		private readonly Dictionary<string, DebuggerTabItem> _viewers = new Dictionary<string, DebuggerTabItem>();

		private OutputViewer _outputViewer;
		private IDebuggerSession _debuggerSession;
		private bool _isInitialized;

		public IList<StackTraceItem> StackTrace { get; } = new ObservableCollection<StackTraceItem>();

		public IList<WatchItem> WatchItems { get; } = new ObservableCollection<WatchItem>();

		public DebuggerViewer()
		{
			InitializeComponent();
		}

		public void Initialize(OutputViewer outputViewer, IDebuggerSession debuggerSession)
		{
			_outputViewer = outputViewer;
			_debuggerSession = debuggerSession;

			foreach (var tabItem in TabSourceViewer.Items.Cast<DebuggerTabItem>().ToArray())
			{
				TabSourceViewer.RemoveTabItemWithoutBindingError(tabItem);
			}

			_viewers.Clear();

			WatchItems.Clear();
			foreach (var watchItem in _outputViewer.DocumentPage.WorkDocument.WatchItems)
			{
				WatchItems.Add(new WatchItem { Name = watchItem });
			}

			if (!_isInitialized)
			{
				TabDebuggerOptions.SelectedIndex = _outputViewer.DocumentPage.WorkDocument.DebuggerViewDefaultTabIndex;
				_isInitialized = true;
			}

			_debuggerSession.Attached += delegate { Dispatcher.Invoke(DebuggerAttachedHandler); };
		}

		private void DebuggerAttachedHandler()
		{
			DisplaySourceAsync(CancellationToken.None);
			EnsureWatchItem();
		}

		private void EnsureWatchItem()
		{
			if (WatchItems.All(i => !String.IsNullOrWhiteSpace(i.Name)))
			{
				WatchItems.Add(new WatchItem());
			}
		}

		public async void DisplaySourceAsync(CancellationToken cancellationToken)
		{
			StackTrace.Clear();
			StackTrace.AddRange(_debuggerSession.StackTrace);

			var activeStackItem = _debuggerSession.StackTrace.Last();
			DebuggerTabItem debuggerView;
			if (!_viewers.TryGetValue(activeStackItem.Header, out debuggerView))
			{
				debuggerView = new DebuggerTabItem(activeStackItem.Header);
				debuggerView.CodeViewer.Initialize(_outputViewer.DocumentPage.InfrastructureFactory, _outputViewer.ConnectionAdapter.DatabaseModel);

				await debuggerView.CodeViewer.LoadAsync(activeStackItem.ProgramText, cancellationToken);

				_viewers.Add(activeStackItem.Header, debuggerView);
				TabSourceViewer.Items.Add(debuggerView);
			}

			TabSourceViewer.SelectedItem = debuggerView;

			HighlightStackTraceLines();
		}

		private void HighlightStackTraceLines()
		{
			var inactiveItems = _debuggerSession.StackTrace
				.Take(_debuggerSession.StackTrace.Count - 1)
				.ToLookup(i => i.Header, i => i.Line);

			var activeStackItem = _debuggerSession.StackTrace.Last();

			var activeItemHighlighted = false;
			foreach (var group in inactiveItems)
			{
				var debuggerView = _viewers[group.Key];
				var activeLineNumber = String.Equals(activeStackItem.Header, group.Key) ? activeStackItem.Line : (int?)null;
				activeItemHighlighted |= activeLineNumber.HasValue;
				debuggerView.HighlightStackTraceLines(activeLineNumber, group);
			}

			if (!activeItemHighlighted)
			{
				_viewers[activeStackItem.Header].HighlightStackTraceLines(activeStackItem.Line, Enumerable.Empty<int>());
			}
		}

		private void MouseButtonDownHandler(object sender, MouseButtonEventArgs args)
		{
			if (args.ClickCount != 2)
			{
				return;
			}

			var currentItem = (StackTraceItem)StackTraceItems.SelectedItem;
			var debuggerViewer = _viewers[currentItem.Header];
			TabSourceViewer.SelectedItem = debuggerViewer;
			debuggerViewer.CodeViewer.Editor.ScrollToLine(currentItem.Line);
		}

		private async void CellEditEndingHandler(object sender, DataGridCellEditEndingEventArgs args)
		{
			if (args.EditAction == DataGridEditAction.Cancel)
			{
				return;
			}

			var watchItem = (WatchItem)args.Row.DataContext;
			if (Equals(args.Column, ColumnDebugVariableName))
			{
				_outputViewer.DocumentPage.WorkDocument.WatchItems = WatchItems.Select(i => i.Name).ToArray();

				if (String.IsNullOrWhiteSpace(watchItem.Name))
				{
					watchItem.Value = null;
					return;
				}
			}
			else
			{
				await _debuggerSession.SetValue(watchItem.Name, watchItem.Value.ToString(), CancellationToken.None);
			}

			try
			{
				watchItem.Value = await _debuggerSession.GetValue(watchItem.Name, CancellationToken.None);
			}
			catch (Exception exception)
			{
				Messages.ShowError(exception.Message);
			}
		}

		public async void RefreshWatchItemsAsync(CancellationToken token)
		{
			foreach (var watchItem in WatchItems.Where(i => !String.IsNullOrWhiteSpace(i.Name)))
			{
				watchItem.Value = await _debuggerSession.GetValue(watchItem.Name, token);
			}
		}

		private void DebuggerOptionsSelectionChangedHandler(object sender, SelectionChangedEventArgs e)
		{
			_outputViewer.DocumentPage.WorkDocument.DebuggerViewDefaultTabIndex = ((TabControl)sender).SelectedIndex;
		}

		private void WatchItemGridPreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (WatchItemGrid.CurrentItem != null && e.Key == Key.Delete && Keyboard.Modifiers != ModifierKeys.Alt && Keyboard.Modifiers != ModifierKeys.Control && Keyboard.Modifiers != ModifierKeys.Windows)
			{
				WatchItems.Remove((WatchItem)WatchItemGrid.CurrentItem);
			}
		}
	}

	public class WatchItem : ModelBase
	{
		private object _value;
		public string Name { get; set; }

		public object Value
		{
			get { return _value; }
			set { UpdateValueAndRaisePropertyChanged(ref _value, value); }
		}
	}
}