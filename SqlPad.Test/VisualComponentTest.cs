﻿using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NUnit.Framework;
using Shouldly;
using SqlPad.Commands;
using SqlPad.DataExport;

namespace SqlPad.Test
{
	[TestFixture]
	public class VisualComponentTest
	{
		private App _app;

		[TestFixtureSetUp]
		public void FixtureSetup()
		{
			_app = new App();
			_app.InitializeComponent();
		}

		[Test(Description = @""), STAThread]
		public void RealApplicationTest()
		{
			VisualTestRunner.RunTest("SqlPad.Test.VisualComponentTest, SqlPad.Test", "TestBasicSqlPadBehavior");
		}

		private static SqlTextEditor Editor => DocumentPage.Editor;

		private static DocumentPage DocumentPage
		{
			get
			{
				var mainWindow = (MainWindow)Application.Current.MainWindow;
				return (DocumentPage)((TabItem)mainWindow.DocumentTabControl.SelectedItem).Content;
			}
		}

		private static void TestBasicSqlPadBehavior(VisualTestContext context)
		{
			try
			{
				AssertBasicSqlPadBehavior();
			}
			finally
			{
				Editor.IsModified = false;
			}
		}

		private static void AssertBasicSqlPadBehavior()
		{
			const string statementText =
@"SELECT
	:BIND_VARIABLE, UNDEFINED, DUMMY AMBIGUOUS,
	SQLPAD_FUNCTION('Invalid parameter 1', 'Invalid parameter 2')
FROM DUAL, DUAL D;
SELECT *;

SELECT 1 FROM DUAL UNION ALL SELECT 2 FROM DUAL UNION ALL SELECT 3, 4 FROM DUAL;

SELECT T.*, :BIND_VARIABLE, :BIND_VARIABLE FROM T@HQ_PDB;

SELECT
	CAST(NULL AS NUMBER(39)),
	CAST(NULL AS NUMBER(0)),
	CAST(NULL AS NUMBER(38, -85)),
	CAST(NULL AS NUMBER(38, 128)),
	CAST(NULL AS RAW(2001)),
	CAST(NULL AS RAW(0)),
	CAST(NULL AS VARCHAR2(32768)),
	CAST(NULL AS NVARCHAR2(2001)),
	CAST(NULL AS FLOAT(127)),
	CAST(NULL AS TIMESTAMP(10)),
	CAST(NULL AS INTERVAL YEAR(10) TO MONTH),
	CAST(NULL AS INTERVAL DAY(10) TO SECOND(10)),
	CAST(NULL AS UROWID(4001)),
	CAST(NULL AS CHAR(2001)),
	CAST(NULL AS NCHAR(1001))
FROM
	DUAL;

SELECT
	CUME_DIST(1, 1) WITHIN GROUP (ORDER BY NULL),
	RANK(1) WITHIN GROUP (ORDER BY NULL),
	DENSE_RANK(1) WITHIN GROUP (ORDER BY NULL),
	PERCENTILE_CONT(0) WITHIN GROUP (ORDER BY NULL),
	PERCENTILE_DISC(0) WITHIN GROUP (ORDER BY NULL),
	COALESCE(NULL, NULL, 0),
	SYSTIMESTAMP(9),
	SYSTIMESTAMP,
	LOCALTIMESTAMP(9),
	LOCALTIMESTAMP,
	CURRENT_TIMESTAMP(9),
	CURRENT_TIMESTAMP,
	CASE WHEN LNNVL(1 <> 1) THEN 1 END,
	EXTRACT(DAY FROM SYSDATE)
FROM
	TABLE(DBMS_XPLAN.DISPLAY_CURSOR(NULL, NULL, 'ALLSTATS LAST ADVANCED')) T1, TABLE(SYS.ODCIRAWLIST(HEXTORAW('ABCDEF'), HEXTORAW('A12345'), HEXTORAW('F98765'))) T2
WHERE
	LNNVL(1 <> 1);";

			Editor.Document.Insert(0, statementText);
			Editor.CaretOffset = 50;

			VisualTestRunner.Wait(0.1);

			Editor.CaretOffset = 111;

			GenericCommands.FindUsages.Execute(null, Editor.TextArea);
			GenericCommands.ExecuteDatabaseCommandWithActualExecutionPlan.Execute(null, Editor.TextArea);
			GenericCommands.ListContextActions.Execute(null, Editor.TextArea);
			GenericCommands.ListCodeGenerationItems.Execute(null, Editor.TextArea);

			VisualTestRunner.Wait(0.2);

			DocumentPage.BindVariables.Count.ShouldBe(1);
			DocumentPage.Schemas.Count.ShouldBeGreaterThan(0);
			DocumentPage.IsDirty.ShouldBe(true);
			DocumentPage.DatabaseModel.ShouldNotBe(null);
			DocumentPage.ConnectionStatus.ShouldBe(ConnectionStatus.Connected);
			DocumentPage.CurrentConnection.ShouldNotBe(null);
			DocumentPage.ConnectionErrorMessage.ShouldBe(String.Empty);
			//DocumentPage.CurrentSchema.Length.ShouldBeGreaterThan(0);

			DocumentPage.OutputViewers.Count.ShouldBe(1);
			var outputViewer = DocumentPage.OutputViewers[0];
			outputViewer.DatabaseOutput.ShouldBe($"Test database output{Environment.NewLine}");
			outputViewer.CompilationErrors.Count.ShouldBe(1);
			outputViewer.ExecutionLog.Length.ShouldBe(87);
			outputViewer.ExecutionLog.ShouldContain("Statement executed successfully");
			outputViewer.IsBusy.ShouldBe(false);
			outputViewer.HasActiveTransaction.ShouldBe(false);
			outputViewer.SessionExecutionStatistics.Count.ShouldBeGreaterThan(0);
			outputViewer.ActiveResultViewer.ShouldNotBe(null);

			var statusInfo = outputViewer.StatusInfo;
			statusInfo.ResultGridAvailable.ShouldBe(true);

			TestPairCharacterInsertion();

			TestPairCharacterInsertionWhenNextCharacterIsThePairCharacter();

			TestPairCharacterInsertionAfterAlreadyEnteredPair();

			TestPairCharacterInsertionBeforeExistingText();

			TestParenthesisCharacterInsertionWithinExistingParenthesis();

			TestPairCharacterDeletionWithCursorBetweenEmptyPair();

			VisualTestRunner.Wait(0.2);
		}

		private static void TestPairCharacterInsertionBeforeExistingText()
		{
			Editor.Clear();
			EnterText("Existing text");
			Editor.CaretOffset = 0;

			EnterText("'");
			Editor.Text.ShouldBe("'Existing text");
			Editor.CaretOffset.ShouldBe(1);

			Editor.CaretOffset = 10;
			EnterText("\"");
			Editor.Text.ShouldBe("'Existing \"text");
			Editor.CaretOffset.ShouldBe(11);
		}

		private static void TestPairCharacterInsertion()
		{
			Editor.Clear();
			
			EnterText("(");
			Editor.Text.ShouldBe("()");
			Editor.CaretOffset.ShouldBe(1);

			Editor.CaretOffset = 2;
			EnterText("'");
			Editor.Text.ShouldBe("()''");
			Editor.CaretOffset.ShouldBe(3);

			Editor.CaretOffset = 4;
			EnterText("\"");
			Editor.Text.ShouldBe("()''\"\"");
			Editor.CaretOffset.ShouldBe(5);
		}

		private static void TestPairCharacterInsertionWhenNextCharacterIsThePairCharacter()
		{
			Editor.Clear();

			EnterText("()");
			Editor.CaretOffset = 1;
			EnterText(")");
			Editor.Text.ShouldBe("()");
			Editor.CaretOffset.ShouldBe(2);

			EnterText("''");
			Editor.CaretOffset = 3;
			EnterText("'");
			Editor.Text.ShouldBe("()''");
			Editor.CaretOffset.ShouldBe(4);

			EnterText("\"\"");
			Editor.CaretOffset = 5;
			EnterText("\"");
			Editor.Text.ShouldBe("()''\"\"");
			Editor.CaretOffset.ShouldBe(6);
		}

		private static void TestPairCharacterInsertionAfterAlreadyEnteredPair()
		{
			Editor.Clear();

			EnterText("''");
			Editor.CaretOffset = 2;
			EnterText("'");
			Editor.Text.ShouldBe("'''");
			Editor.CaretOffset.ShouldBe(3);

			Editor.Clear();

			EnterText("\"\"");
			Editor.CaretOffset = 2;
			EnterText("\"");
			Editor.Text.ShouldBe("\"\"\"");
			Editor.CaretOffset.ShouldBe(3);

			Editor.Clear();

			EnterText("()");
			Editor.CaretOffset = 2;
			EnterText("(");
			Editor.Text.ShouldBe("()()");
			Editor.CaretOffset.ShouldBe(3);
		}

		private static void TestParenthesisCharacterInsertionWithinExistingParenthesis()
		{
			Editor.Clear();

			EnterText("()");
			Editor.CaretOffset = 1;
			EnterText("(");
			Editor.Text.ShouldBe("(())");
			Editor.CaretOffset.ShouldBe(2);

			Editor.Clear();

			EnterText("(SELECT)");
			Editor.CaretOffset = 1;
			EnterText("(");
			Editor.Text.ShouldBe("((SELECT)");
			Editor.CaretOffset.ShouldBe(2);
		}

		private static void TestPairCharacterDeletionWithCursorBetweenEmptyPair()
		{
			Editor.Clear();

			EnterText("()");
			Editor.CaretOffset = 1;

			PressBackspace();

			Editor.Text.ShouldBe(String.Empty);
			Editor.CaretOffset.ShouldBe(0);
		}

		private static void EnterText(string text)
		{
			Editor.TextArea.RaiseEvent(
				new TextCompositionEventArgs(
					InputManager.Current.PrimaryKeyboardDevice,
					new TextComposition(InputManager.Current, Editor.TextArea, text)) { RoutedEvent = TextCompositionManager.TextInputEvent }
				);
		}

		private static void PressBackspace()
		{
			var keyEventArgs =
				new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(Editor), 0, Key.Back)
				{
					RoutedEvent = Keyboard.PreviewKeyDownEvent
				};

			Editor.TextArea.RaiseEvent(keyEventArgs);

			keyEventArgs.RoutedEvent = Keyboard.KeyDownEvent;
			
			Editor.TextArea.RaiseEvent(keyEventArgs);
		}

		[Test, STAThread]
		public void TestCsvDataExporter()
		{
			var resultGrid = InitializeDataGrid();

			var result = GetExportContent(resultGrid, new CsvDataExporter());

			const string expectedResult = "\"DUMMY1\";\"DUMMY_WITH_UNDERSCORES\"\r\n\"Value \"\"1\"\" '2' <3>\";\"08/16/2014 22:25:34\"\r\n\"\"\"2.\"\"Value\";\"08/16/2014 00:00:00\"\r\n";
			result.ShouldBe(expectedResult);
		}

		[Test, STAThread]
		public void TestTsvDataExporter()
		{
			var resultGrid = InitializeDataGrid();

			var result = GetExportContent(resultGrid, new TsvDataExporter());

			const string expectedResult = "\"DUMMY1\"\t\"DUMMY_WITH_UNDERSCORES\"\r\n\"Value \"\"1\"\" '2' <3>\"\t\"08/16/2014 22:25:34\"\r\n\"\"\"2.\"\"Value\"\t\"08/16/2014 00:00:00\"\r\n";
			result.ShouldBe(expectedResult);
		}

		[Test, STAThread]
		public void TestJsonDataExporter()
		{
			var resultGrid = InitializeDataGrid();

			var result = GetExportContent(resultGrid, new JsonDataExporter());

			const string expectedResult = "[\r\n  {\r\n    \"DUMMY1\": \"Value \\\"1\\\" '2' <3>\",\r\n    \"DUMMY_WITH_UNDERSCORES\": \"08/16/2014 22:25:34\"\r\n  },\r\n  {\r\n    \"DUMMY1\": \"\\\"2.\\\"Value\",\r\n    \"DUMMY_WITH_UNDERSCORES\": \"08/16/2014 00:00:00\"\r\n  }\r\n]";
			result.ShouldBe(expectedResult);
		}

		[Test, STAThread]
		public void TestXmlDataExporter()
		{
			var resultGrid = InitializeDataGrid();

			var result = GetExportContent(resultGrid, new XmlDataExporter());

			const string expectedResult = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<data>\r\n  <row>\r\n    <DUMMY1>Value \"1\" '2' &lt;3&gt;</DUMMY1>\r\n    <DUMMY_WITH_UNDERSCORES>08/16/2014 22:25:34</DUMMY_WITH_UNDERSCORES>\r\n  </row>\r\n  <row>\r\n    <DUMMY1>\"2.\"Value</DUMMY1>\r\n    <DUMMY_WITH_UNDERSCORES>08/16/2014 00:00:00</DUMMY_WITH_UNDERSCORES>\r\n  </row>\r\n</data>";
			result.ShouldBe(expectedResult);
		}

		[Test, STAThread]
		public void TestSqlInsertDataExporter()
		{
			var resultGrid = InitializeDataGrid();

			var result = GetExportContent(resultGrid, new SqlInsertDataExporter());

			const string expectedResult = "INSERT INTO MY_TABLE (DUMMY1, DUMMY_WITH_UNDERSCORES) VALUES ('Value \"1\" ''2'' <3>', '08/16/2014 22:25:34');\r\nINSERT INTO MY_TABLE (DUMMY1, DUMMY_WITH_UNDERSCORES) VALUES ('\"2.\"Value', '08/16/2014 00:00:00');\r\n";
			result.ShouldBe(expectedResult);
		}

		[Test, STAThread]
		public void TestSqlUpdateDataExporter()
		{
			var resultGrid = InitializeDataGrid();

			var result = GetExportContent(resultGrid, new SqlUpdateDataExporter());

			const string expectedResult = "UPDATE MY_TABLE SET DUMMY1 = 'Value \"1\" ''2'' <3>', DUMMY_WITH_UNDERSCORES = '08/16/2014 22:25:34';\r\nUPDATE MY_TABLE SET DUMMY1 = '\"2.\"Value', DUMMY_WITH_UNDERSCORES = '08/16/2014 00:00:00';\r\n";
			result.ShouldBe(expectedResult);
		}

		[Test, STAThread]
		public void TestHtmlDataExporter()
		{
			var resultGrid = InitializeDataGrid();

			var result = GetExportContent(resultGrid, new HtmlDataExporter());

			const string expectedResult = "<!DOCTYPE html><html><head><title></title></head><body><table border=\"1\" style=\"border-collapse:collapse\"><tr><th>DUMMY1</th><th>DUMMY_WITH_UNDERSCORES</th><tr><tr><td>Value \"1\" '2' &lt;3&gt;</td><td>08/16/2014 22:25:34</td><tr><tr><td>\"2.\"Value</td><td>08/16/2014 00:00:00</td><tr><table>";
			result.ShouldBe(expectedResult);
		}

		private string GetExportContent(DataGrid resultGrid, IDataExporter dataExporter)
		{
			var tempFileName = Path.GetTempFileName();
			var connectionConfiguration = ConfigurationProvider.GetConnectionCofiguration(ConfigurationProvider.ConnectionStrings[0].Name);
			CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
			dataExporter.ExportToFile(tempFileName, resultGrid, connectionConfiguration.InfrastructureFactory.DataExportConverter);

			var result = File.ReadAllText(tempFileName);
			File.Delete(tempFileName);
			return result;
		}

		private DataGrid InitializeDataGrid()
		{
			var columnHeaders =
				new[]
				{
					new ColumnHeader { ColumnIndex = 0, DatabaseDataType = "Varchar2", DataType = typeof (string), Name = "DUMMY1" },
					new ColumnHeader { ColumnIndex = 1, DatabaseDataType = "Date", DataType = typeof (DateTime), Name = "DUMMY_WITH_UNDERSCORES" },
					//new ColumnHeader { ColumnIndex = 2, DatabaseDataType = "Varchar2", DataType = typeof (string), Name = "\"'\\\"><?,.;:{}[]%$#@!~^&*()_+-§'''||(1/2*3+4-CASEWHEN1<=2OR2>=1THEN5ELSE6END)" }
				};

			var documentPage = new DocumentPage { CurrentConnection = ConfigurationProvider.ConnectionStrings[0] };
			documentPage.DatabaseModel.Dispose();
			
			var outputViewer = new OutputViewer(documentPage);

			var dataRows =
				new[]
				{
					new object[] {"Value \"1\" '2' <3>", new DateTime(2014, 8, 16, 22, 25, 34)},
					new object[] {"\"2.\"Value", new DateTime(2014, 8, 16)},
					//new object[] {"\"><?,.;:{}[]%$#@!~^&*()_+-§' ,5", new DateTime(2015, 5, 30) }
				};

			var resultViewer =
				new ResultViewer(outputViewer, null, new ResultInfo(null, null, ResultIdentifierType.UserDefined), columnHeaders)
				{
					ResultGrid = { ItemsSource = dataRows }
				};

			DataGridHelper.InitializeDataGridColumns(resultViewer.ResultGrid, columnHeaders, outputViewer.StatementValidator, outputViewer.ConnectionAdapter);

			return resultViewer.ResultGrid;
		}

		[Test, STAThread]
		public void TestLargeTextValueEditorInitialization()
		{
			var editor = new LargeValueEditor("Dummy", new TestLargeTextValue());
			var task = (Task) typeof (LargeValueEditor).GetMethod("SetEditorValue", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(editor, null);
			task.Wait();
			
			editor.TextEditor.Text.ShouldBe(TestLargeTextValue.TextValue);
			editor.TabText.Visibility.ShouldBe(Visibility.Visible);
			editor.TabRaw.Visibility.ShouldBe(Visibility.Collapsed);
		}

		private class TestLargeTextValue : ILargeTextValue
		{
			public const string TextValue = "</root>";

			public string DataTypeName => "CLOB";

		    public bool IsEditable => false;

		    public bool IsNull => false;

			public object RawValue
			{
				get { throw new NotImplementedException(); }
			}

			public string ToSqlLiteral()
			{
				return "TO_CLOB('</root>')";
			}

			public string ToXml()
			{
				return TextValue;
			}

			public string ToJson()
			{
				return "'</root>'";
			}

			public long Length => TextValue.Length;

		    public string Preview { get { throw new NotImplementedException(); } }

			public string Value => TextValue;

		    public void GetChunk(StringBuilder stringBuilder, int offset, int length)
			{
				throw new NotImplementedException();
			}

			public void Prefetch()
			{
				throw new NotImplementedException();
			}
		}
	}
}
