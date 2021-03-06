﻿using System;
using System.Windows;
using System.Windows.Controls;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipViewColumn : IToolTip
	{
		public event EventHandler Pin;

		public ToolTipViewColumn(ColumnDetailsModel dataModel)
		{
			InitializeComponent();

			DataContext = dataModel;
		}

		public Control Control => this;

	    public FrameworkElement InnerContent => this;
	}
}
