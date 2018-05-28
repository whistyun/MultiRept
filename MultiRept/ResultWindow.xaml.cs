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

namespace MultiRept
{
	/// <summary>
	/// ResultWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class ResultWindow : Window
	{
		public ResultWindow()
		{
			ResultList = new List<ResultData>();
			InitializeComponent();
		}

		string folderPath;

		public string Folder
		{
			set
			{
				FolderLabel.Content = value;

				folderPath = value;
				if (folderPath[folderPath.Length - 1] == '\\' || folderPath[folderPath.Length - 1] == '/')
				{
					folderPath = folderPath.Substring(0, folderPath.Length - 1);
				}
			}
			get { return (string)FolderLabel.Content; }
		}

		public string FilePattern
		{
			set { FileLabel.Content = value; }
			get { return (string)FileLabel.Content; }
		}

		public List<ResultData> ResultList
		{
			set; get;
		}

		private int resultListStartIndex;


		public void StartLog()
		{
			resultListStartIndex = ResultList.Count;
		}

		public void AddError(string filePath, string message)
		{
			if (resultListStartIndex != -1 && ResultList.Count > resultListStartIndex)
			{
				ResultList.RemoveRange(resultListStartIndex, ResultList.Count - resultListStartIndex);
				resultListStartIndex = -1;
			}

			var relPath = "." + filePath.Substring(folderPath.Length);
			ResultList.Add(new ResultData
			{
				FilePath = relPath,
				Contents = "<エラー> " + message.Trim(),
				IsError = true
			});
		}

		public void Add(string filePath, int line, string encodingName, string contents)
		{
			var relPath = "." + filePath.Substring(folderPath.Length);
			ResultList.Add(new ResultData
			{
				FilePath = relPath,
				LineNo = line,
				EncodingName = encodingName,
				Contents = contents,
				IsError = false
			});
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			dataGrid.ItemsSource = ResultList;
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
