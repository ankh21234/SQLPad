﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Utils;

namespace SqlPad.Bookmarks
{
	public class IconMargin : AbstractMargin
	{
		private readonly TextEditor _textEditor;

		private readonly List<BreakpointMarker> _markers = new List<BreakpointMarker>();
		private readonly List<BreakpointMarker> _visibleMarkers = new List<BreakpointMarker>();

		public SqlDocumentRepository DocumentRepository { get; set; }

		protected override int VisualChildrenCount
		{
			get { return _visibleMarkers.Count; }
		}

		public IconMargin(TextEditor textEditor)
		{
			_textEditor = textEditor;
		}

		public void RemoveBreakpoint(BreakpointMarker breakpoint)
		{
			_markers.Remove(breakpoint);
			_visibleMarkers.Remove(breakpoint);

			RemoveVisualChild(breakpoint);
			InvalidateMeasure();
		}

		public void AddBreakpoint(BreakpointMarker breakpoint)
		{
			_markers.Add(breakpoint);

			InvalidateMeasure();
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			return new Size(14, 0);
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			var pixelSize = PixelSnapHelpers.GetPixelSize(this);
			var textView = TextView;

			_visibleMarkers.Clear();
			
			foreach (var marker in _markers)
			{
				RemoveVisualChild(marker);

				var visualLine = textView.GetVisualLine(marker.Line.LineNumber);
				if (visualLine == null)
				{
					continue;
				}

				_visibleMarkers.Add(marker);
				AddVisualChild(marker);

				var topLeft = new Point(0, visualLine.VisualTop - textView.VerticalOffset);
				marker.Arrange(new Rect(PixelSnapHelpers.Round(topLeft, pixelSize), marker.DesiredSize));
			}
			
			return base.ArrangeOverride(finalSize);
		}

		protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
		{
			if (oldTextView != null)
			{
				oldTextView.VisualLinesChanged -= TextViewVisualLinesChangedHandler;
			}
			
			base.OnTextViewChanged(oldTextView, newTextView);
			
			if (newTextView != null)
			{
				newTextView.VisualLinesChanged += TextViewVisualLinesChangedHandler;
			}
			
			InvalidateVisual();
		}

		private void TextViewVisualLinesChangedHandler(object sender, EventArgs eventArgs)
		{
			InvalidateVisual();
		}

		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			e.Handled = true;

			var visualPosition = e.GetPosition(_textEditor);
			var position = _textEditor.GetPositionFromPoint(visualPosition);
			if (position == null)
			{
				return;
			}

			var offset = _textEditor.Document.GetOffset(position.Value.Line, position.Value.Column);
			var documentLine = _textEditor.Document.GetLineByOffset(offset);

			var breakpoint = new BreakpointMarker { Line = documentLine };
			
			AddBreakpoint(breakpoint);
		}

		protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
		{
			return new PointHitTestResult(this, hitTestParameters.HitPoint);
		}

		protected override Visual GetVisualChild(int index)
		{
			return _visibleMarkers[index];
		}
	}

	public sealed class BreakpointMarker : UIElement
	{
		private const double BreakpointRadius = 6;
		
		private static readonly Size BreakpointSize = new Size(2 * BreakpointRadius, 2 * BreakpointRadius);
		private static readonly Pen EdgePen = new Pen(Brushes.Black, 1.0) { StartLineCap = PenLineCap.Square, EndLineCap = PenLineCap.Square };

		static BreakpointMarker()
		{
			EdgePen.Freeze();
		}

		public DocumentLine Line { get; set; }

		protected override Size MeasureCore(Size availableSize)
		{
			return BreakpointSize;
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			drawingContext.DrawEllipse(Brushes.Red, EdgePen, new Point(BreakpointSize.Width / 2, BreakpointSize.Height / 2), BreakpointRadius, BreakpointRadius);
		}

		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			e.Handled = true;

			var bookmarkMargin = (IconMargin)VisualParent;
			bookmarkMargin.RemoveBreakpoint(this);
		}
	}
}