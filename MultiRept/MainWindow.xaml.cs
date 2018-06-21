using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Text.RegularExpressions;
using IOPath = System.IO.Path;
using FileInfo = System.IO.FileInfo;
using EncodeDetector;
using MultiRept.Gui;

namespace MultiRept
{
	delegate void DialogAlert(string message);
	delegate MessageBoxResult DialogConfirm(string message);

	public partial class MainWindow : Window
	{
		private FileStore db = new FileStore();

		public MainWindow()
		{
			InitializeComponent();
		}

		private void AddButton_Click(object sender, RoutedEventArgs e)
		{
			replaceKeyList.Children.Add(new OneTask());
		}

		private void RemoveButton_Click(object sender, RoutedEventArgs e)
		{
			if (replaceKeyList.Children.Count > 1)
			{
				replaceKeyList.Children.RemoveAt(replaceKeyList.Children.Count - 1);
			}
		}

		/// <summary>
		/// 置換実施ボタンのチェック処理
		/// </summary>
		/// <returns>true:チェックエラー無し / false:チェックエラー有り</returns>
		private bool ValidateReplaceButton()
		{
			string errorMessage = null;
			directoryTextBox.HasError = false;
			filePatternTextBox.HasError = false;

			var directoryPath = directoryTextBox.Text;
			// チェック：ディレクトリは未入力でないか？
			if (directoryPath == "")
			{
				string message = "フォルダを選択してください";
				errorMessage = errorMessage ?? message;

				directoryTextBox.ErrorMessage = message;
			}
			// チェック：ディレクトリは存在するか？
			else if (!Directory.Exists(directoryPath))
			{
				string message = "指定されたフォルダは存在しません";
				errorMessage = errorMessage ?? message;

				directoryTextBox.ErrorMessage = message;
			}

			var filePattern = filePatternTextBox.Text;
			// チェック：ファイルパターンが(わざわざ)未入力にされていないか
			if (filePattern.Trim() == "")
			{
				string message = "ファイルパターンが未入力です";
				errorMessage = errorMessage ?? message;

				filePatternTextBox.ErrorMessage = message;
			}

			var keywordComponents = replaceKeyList.Children.OfType<OneTask>().ToList();
			var emptyKeywordsExists = keywordComponents.Where(elem => elem.CheckError()).Count() != 0;
			// チェック：置換キーワードが未入力でないか
			if (emptyKeywordsExists)
			{
				errorMessage = errorMessage ?? "置換キーワードに不正な入力があります";
			}


			// チェックエラーがある場合はメッセージを表示して終了
			if (errorMessage != null)
			{
				MessageBox.Show(this, errorMessage, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}
			else
			{
				return true;
			}
		}

		/// <summary>
		/// 置換実施ボタン押下時の処理
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private async void ReplaceButton_Click(object sender, RoutedEventArgs e)
		{
			//チェック処理を通過する場合のみ処理する
			if (ValidateReplaceButton())
			{
				/*置換処理開始*/
				ReplaceButton.IsEnabled = false;
				CancelButton.IsEnabled = false;
				Progress.Maximum = Int32.MaxValue;
				Progress.Minimum = 0;
				Progress.Value = 0;

				//　画面項目からパラメタ取得
				var param = new ReplaceLogicParam()
				{
					RootDir = directoryTextBox.Text,
					FilePattern = filePatternTextBox.Text,
					IgnoreHide = IgnoreHideFile.IsChecked.Value,

					Encoding = Utf8RadioButton.IsChecked.Value ? new UTF8Encoding(false) :
									EucJpRadioButton.IsChecked.Value ? Encoding.GetEncoding(51932) :
									SjisRadioButton.IsChecked.Value ? Encoding.GetEncoding(932) :
									null,

					Keywords = replaceKeyList.Children.OfType<OneTask>().ToList().Select(c => c.GetParameter()).ToList()
				};

				var logWindow = new ResultWindow();
				logWindow.Folder = param.RootDir;
				logWindow.FilePattern = param.FilePattern;

				// 置換処理
				db.NewAct();

				var logic = new ReplaceLogic(db);
				logic.Begin += logWindow.Begin;
				logic.Inform += logWindow.Inform;
				logic.ErrorEnd += logWindow.EndError;

				var progress = new Progress<int>(SetProgress);
				await Task.Run(() => logic.Do(param, progress));

				/*置換処理完了*/
				MessageBox.Show(this, "置換処理が完了しました", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
				ReplaceButton.IsEnabled = true;
				CancelButton.IsEnabled = true;

				logWindow.Show();
			}
		}

		/// <summary>
		/// 置換取消ボタン押下時の処理
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private async void CancelButton_Click(object sender, RoutedEventArgs e)
		{

			/*置換処理開始*/
			ReplaceButton.IsEnabled = false;
			CancelButton.IsEnabled = false;
			Progress.Maximum = Int32.MaxValue;
			Progress.Minimum = 0;
			Progress.Value = Int32.MaxValue;

			// 置換処理
			var logic = new CancelLogic(db);
			logic.Error += CancelButtonError;
			logic.Confirm += CancelButtonConfirm;
			logic.InformUserInterrupt += CancelButtonInform;

			var progress = new Progress<int>(SetProgress);
			var result = await Task.Run(() => logic.Do(progress));

			/*置換処理完了*/
			if (result)
			{
				db.DeleteAct();
				MessageBox.Show(this, "置換処理を取り消しました", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			else
			{
				MessageBox.Show(this, "置換処理の取り消しを取り消しました", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
			}

			ReplaceButton.IsEnabled = true;
			CancelButton.IsEnabled = db.HasStore;
		}

		private bool CancelButtonError(List<string> filepathList)
		{
			var msg =
				"取消対象のファイルを別のプログラムが開いているため、\r\n" +
				"取消操作が開始できません。\r\n";

			foreach (var filepath in filepathList)
			{
				msg += "・" + Path.GetFileName(filepath) + "\r\n";
			}

			Action<string> showMessage = (m) => MessageBox.Show(this, m, "エラー", MessageBoxButton.OK);
			Dispatcher.Invoke(showMessage, msg);

			return false;
		}

		private bool CancelButtonConfirm(List<string> filepathList)
		{
			var msg =
				"置換後にファイルの変更が行われているようです。\r\n" +
				"本当に取り消しますか？\r\n";

			foreach (var filepath in filepathList)
			{
				msg += "・" + Path.GetFileName(filepath) + "\r\n";
			}

			Func<string, bool> showMessage = (m) => MessageBox.Show(this, m, "確認", MessageBoxButton.YesNo) == MessageBoxResult.Yes;

			return (bool)Dispatcher.Invoke(showMessage, msg);
		}

		private void CancelButtonInform(string filepath)
		{
			var msg =
				"下記のファイルを変更できませんでした。\r\n" +
				"他のプログラムで開いていいないか確認してださい。\r\n" +
				"(OKボタンで変更を再開します)\r\n" +
				filepath;

			Action<string> showMessage = (m) => MessageBox.Show(this, m, "エラー", MessageBoxButton.OK);
			Dispatcher.Invoke(showMessage, msg);
		}

		/// <summary>
		/// 参照ボタン押下時の処理
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Button_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new System.Windows.Forms.FolderBrowserDialog();
			dialog.SelectedPath = directoryTextBox.Text;
			System.Windows.Forms.DialogResult result = dialog.ShowDialog();
			if (result == System.Windows.Forms.DialogResult.OK)
			{
				directoryTextBox.Text = dialog.SelectedPath;
			}
		}

		/// <summary>
		/// 閉じられた時の処理
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Window_Closed(object sender, EventArgs e)
		{
			db.Dispose();
		}


		/// <summary>
		/// プログレスバー操作用
		/// </summary>
		/// <param name="progress"></param>
		private void SetProgress(int progress)
		{
			double newProgress = progress;
			newProgress = Math.Min(newProgress, Progress.Maximum);
			newProgress = Math.Max(newProgress, Progress.Minimum);
			Progress.Value = newProgress;
		}
	}
}
