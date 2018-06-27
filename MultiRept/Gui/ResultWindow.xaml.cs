using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.IO;
using System.Xml;

namespace MultiRept.Gui
{
	/// <summary>
	/// ResultWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class ResultWindow : Window
	{
		/// <summary>相対パス用</summary>
		private int folderPathLength;

		/// <summary>置換結果一覧</summary>
		private List<ResultData> resultList;
		private Dictionary<string, Tuple<string, string>> detailDic;

		public ResultWindow()
		{
			resultList = new List<ResultData>();
			detailDic = new Dictionary<string, Tuple<string, string>>();
			InitializeComponent();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			dataGrid.ItemsSource = resultList;
		}

		private void Row_DoubleClick(object sender, MouseButtonEventArgs e)
		{
			DataGridRow row = sender as DataGridRow;
			ResultData data = row.Item as ResultData;

			if (detailDic.ContainsKey(data.FilePath))
			{
				//存在する場合は、Diff用のウィンドウを開く
				var fileSet = detailDic[data.FilePath];

				var window = new DupleViewWindow();
				window.Load(data.FilePath, fileSet.Item1, fileSet.Item2);
				window.Show();
			}
		}

		/// <summary>
		/// 処理対象のフォルダパス
		/// </summary>
		public string Folder
		{
			set
			{
				FolderLabel.Content = value;

				// 末尾のパス区切り文字は消す
				var lastChr = value[value.Length - 1];
				folderPathLength = (lastChr == '\\' || lastChr == '/') ? value.Length : value.Length + 1;
			}
			get { return (string)FolderLabel.Content; }
		}

		/// <summary>
		/// 処理対象のファイルパターン
		/// </summary>
		public string FilePattern
		{
			set { FileLabel.Content = value; }
			get { return (string)FileLabel.Content; }
		}

		DetailListener cache;
		public DetailListener DetailInformer
		{
			get
			{
				if (cache == null)
				{
					cache = new DetailListener(folderPathLength, detailDic);
				}
				return cache;
			}
		}

		SummaryListener cache2;
		public SummaryListener SummaryInformer
		{
			get
			{
				if (cache2 == null)
				{
					cache2 = new SummaryListener(folderPathLength, resultList);
				}
				return cache2;
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			foreach (var tuple in detailDic.Values)
			{
				try
				{
					File.Delete(tuple.Item1);
					File.Delete(tuple.Item2);
				}
				catch (Exception) { }
			}
		}
	}

	public class SummaryListener : IReplaceAbstractListener
	{
		/// <summary>相対パス用</summary>
		private int folderPathLength;
		/// <summary>置換結果一覧</summary>
		private List<ResultData> resultList;

		/// <summary>ファイルパス(相対パス)</summary>
		private string fileRelativePath;
		/// <summary>取り戻し用の置換結果一覧index</summary>
		private int resultListStartIndex;

		public SummaryListener(int folderPathLength, List<ResultData> outList)
		{
			this.folderPathLength = folderPathLength;
			this.resultList = outList;
		}

		public void Begin(string filepath)
		{
			fileRelativePath = filepath.Substring(folderPathLength);
			resultListStartIndex = resultList.Count;
		}

		public void End()
		{
		}

		public void ErrorEnd(string message)
		{
			if (resultListStartIndex != -1 && resultList.Count > resultListStartIndex)
			{
				resultList.RemoveRange(resultListStartIndex, resultList.Count - resultListStartIndex);
				resultListStartIndex = -1;
			}

			resultList.Add(new ResultData
			{
				FilePath = fileRelativePath,
				Contents = "<エラー> " + message.Trim(),
				IsError = true
			});
		}

		public void Inform(int line, Encoding encoding, string contents)
		{
			this.Inform(line, Util.GetMyDisplayName(encoding), contents);
		}

		public void Inform(int line, string encodingName, string contents)
		{
			resultList.Add(new ResultData
			{
				FilePath = fileRelativePath,
				LineNo = line,
				EncodingName = encodingName,
				Contents = contents,
				IsError = false
			});
		}
	}

	public class DetailListener : IReplaceDetailListener
	{
		private int prefixCutter;
		private Dictionary<string, Tuple<string, string>> detailDic;

		private bool containsDiff;

		private bool lineStart;
		private TextWriter originalWriter;
		private TextWriter changedWriter;

		public String TargetFilePath { private set; get; }
		public String OriginalTempFile { private set; get; }
		public String ChangeTempFile { private set; get; }

		public DetailListener(int prefixCutter, Dictionary<string, Tuple<string, string>> detailDic)
		{
			this.prefixCutter = prefixCutter;
			this.detailDic = detailDic;
		}

		public void Begin(string filePath)
		{
			TargetFilePath = filePath.Substring(prefixCutter);

			containsDiff = false;
			lineStart = true;

			OriginalTempFile = System.IO.Path.GetTempFileName();
			originalWriter = new StreamWriter(OriginalTempFile, false, new UTF8Encoding(false), 1024 * 20);
			originalWriter.Write("<StackPanel Orientation='Vertical'");
			originalWriter.Write(" xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'");
			originalWriter.Write(" xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'");
			originalWriter.Write(" xmlns:d='http://schemas.microsoft.com/expression/blend/2008'");
			originalWriter.Write(" xmlns:mc='http://schemas.openxmlformats.org/markup-compatibility/2006'");
			originalWriter.Write(">\n");

			ChangeTempFile = System.IO.Path.GetTempFileName();
			changedWriter = new StreamWriter(ChangeTempFile, false, new UTF8Encoding(false), 1024 * 20);
			changedWriter.Write("<StackPanel Orientation='Vertical'");
			changedWriter.Write(" xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'");
			changedWriter.Write(" xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'");
			changedWriter.Write(" xmlns:d='http://schemas.microsoft.com/expression/blend/2008'");
			changedWriter.Write(" xmlns:mc='http://schemas.openxmlformats.org/markup-compatibility/2006'");
			changedWriter.Write(">\n");
		}

		private string EscapeXml(string text)
		{
			return text
				.Replace("<", "&lt;")
				.Replace(">", "&gt;")
				.Replace("&", "&amp;")
				.Replace("\"", "&quot;")
				.Replace("'", "&apos;")
				.Replace(" ", "&#x20;")
				.Replace("\t", "&#x9;")
				;
		}

		public void AddPlain(string plainText)
		{
			TryStart();

			var text = EscapeXml(plainText);
			originalWriter.Write(text);
			changedWriter.Write(text);
		}

		public void AddDiff(string original, string changed)
		{
			TryStart();

			containsDiff = true;

			var orgText = EscapeXml(original);
			originalWriter.Write("<Run Background='#F6CED8'>");
			originalWriter.Write(orgText);
			originalWriter.Write("</Run>");

			var chgText = EscapeXml(changed);
			changedWriter.Write("<Run Background='#A9F5A9'>");
			changedWriter.Write(chgText);
			changedWriter.Write("</Run>");
		}

		public void NewLine()
		{
			if (lineStart)
			{
				originalWriter.Write("<TextBlock></TextBlock>\n");
				changedWriter.Write("<TextBlock></TextBlock>\n");
			}
			else
			{
				lineStart = true;
				originalWriter.Write("</TextBlock>\n");
				changedWriter.Write("</TextBlock>\n");
			}
		}

		public void End()
		{
			if (!lineStart)
			{
				originalWriter.Write("</TextBlock>\n");
				changedWriter.Write("</TextBlock>\n");
			}

			originalWriter.Write("</StackPanel>");
			changedWriter.Write("</StackPanel>");
			originalWriter.Close();
			changedWriter.Close();

			if (containsDiff)
			{
				detailDic.Add(
					TargetFilePath,
					Tuple.Create(OriginalTempFile, ChangeTempFile));
			}
			else
			{
				File.Delete(OriginalTempFile);
				File.Delete(ChangeTempFile);
			}
		}

		public void ErrorEnd(string message)
		{
			originalWriter.Close();
			changedWriter.Close();

			File.Delete(OriginalTempFile);
			File.Delete(ChangeTempFile);
		}

		private void TryStart()
		{
			if (lineStart)
			{
				lineStart = false;
				originalWriter.Write("<TextBlock TextWrapping='NoWrap'>");
				changedWriter.Write("<TextBlock TextWrapping='NoWrap'>");
			}
		}
	}


	public class ResultData
	{
		public string FilePath { set; get; }
		public int LineNo { set; get; }
		public string EncodingName { set; get; }
		public string Contents { set; get; }
		public bool IsError { set; get; }
	}
}
