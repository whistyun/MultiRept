using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.RegularExpressions;
using IOPath = System.IO.Path;
using FileInfo = System.IO.FileInfo;

namespace MultiRept
{
	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class MainWindow : Window
	{
		private int actNo = 0;

		private DB db = new DB();

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
		/// 置換実施ボタン押下時の処理
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private async void ReplaceButton_Click(object sender, RoutedEventArgs e)
		{
			var directoryPath = directoryTextBox.Text;
			var filePattern = filePatternTextBox.Text;
			var keywordComponents = replaceKeyList.Children.OfType<OneTask>().ToList();

			// チェック：ディレクトリは未入力でないか？
			if (directoryPath == "")
			{
				MessageBox.Show("フォルダを選択してください", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
				directoryTextBox.Focus();
				return;
			}

			// チェック：ディレクトリは存在するか？
			if (!Directory.Exists(directoryPath))
			{
				MessageBox.Show("指定されたフォルダは存在しません", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
				directoryTextBox.Focus();
				return;
			}

			// チェック：ファイルパターンが(わざわざ)未入力にされていないか
			if (filePattern.Trim() == "")
			{
				MessageBox.Show("ファイルパターンが未入力です", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
				filePatternTextBox.Focus();
				return;
			}

			// チェック：置換キーワードが未入力でないか
			var emptyKeywordsExists = keywordComponents.Where(elem => elem.ReplaceFrom == "").Count() != 0;
			if (emptyKeywordsExists)
			{
				MessageBox.Show("置換キーワードに未入力なものが含まれます", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			// 置換キーワードパラメタ
			var keywords = (from c in keywordComponents select c.GetParameter()).ToList();

			// 文字コード設定
			var encoding =
				 Utf8RadioButton.IsChecked.Value ? new UTF8Encoding(false) :
				 EucJpRadioButton.IsChecked.Value ? Encoding.GetEncoding(20932) :
				 Encoding.GetEncoding(932);

			// ファイルパターンを(,)で区切って正規表現のパターンに変換
			var filePatterns = (from ptn in filePattern.Split(',')
									  select new Regex("^" + Util.Wild2Regex(ptn) + "$")).ToArray();

			actNo++;

			/*置換処理開始*/
			ReplaceButton.IsEnabled = false;
			CancelButton.IsEnabled = false;
			Progress.Maximum = Int32.MaxValue;
			Progress.Minimum = 0;
			Progress.Value = 0;

			// 置換処理
			var progress = new Progress<int>(SetProgress);
			await Task.Run(() => DoReplace(directoryPath, filePatterns, encoding, keywords, progress));

			/*置換処理完了*/
			ReplaceButton.IsEnabled = true;
			CancelButton.IsEnabled = actNo > 0;
			MessageBox.Show("置換処理が完了しました", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		/// <summary>
		/// 指定のディレクトリを再帰的に検索し、
		/// 置換処理を行います。
		/// </summary>
		/// <param name="directoryPath">処理を行う対象のディレクトリ</param>
		/// <param name="filePatterns">処理対象のファイル名パターン</param>
		/// <param name="encoding">ファイルを読み込む際のエンコード</param>
		/// <param name="keywords">置換キーワード一覧</param>
		/// <param name="informer">進捗状況通知先(0～Int32.MaxValue)</param>
		private void DoReplace(string directoryPath, Regex[] filePatterns, Encoding encoding, List<ReplaceParameter> keywords, IProgress<int> informer)
		{

			var targets = new List<string>();

			var directories = new Stack<String>();
			directories.Push(directoryPath);
			while (directories.Count > 0)
			{
				var directory = directories.Pop();
				foreach (var newFile in Directory.GetFiles(directory))
				{
					foreach (var filePtnRegex in filePatterns)
					{
						if (filePtnRegex.IsMatch(newFile))
						{
							//処理対象
							targets.Add(newFile);
							break;
						}
					}
				}

				// ディレクトリを取得し、スタックに詰める
				foreach (var newDirectory in Directory.GetDirectories(directory))
				{
					directories.Push(newDirectory);
				}
			}

			long index = 1;
			long total = targets.Count;

			if (total == 0) { return; }

			foreach (var target in targets)
			{
				TryReplace(target, encoding, keywords);
				informer.Report((int)(Int32.MaxValue * index / total));
				++index;
			}
		}

		/// <summary>
		/// 置換処理
		/// </summary>
		/// <param name="filepath"></param>
		/// <param name="keyword"></param>
		private void TryReplace(string filepath, Encoding encoding, List<ReplaceParameter> keywords)
		{

			var replaceOcc = false;
			// 出力先
			var output = IOPath.GetTempFileName();

			using (var reader = new StreamReader(filepath, encoding, false, 1024 * 10))
			using (var writer = new StreamWriter(output, false, encoding, 1024 * 10))
			{

				string lnCd;
				string line;
				StringBuilder newLine = new StringBuilder();
				while ((line = reader.ReadLine(out lnCd)) != null)
				{

					int startAt = 0;

					do
					{
						//置換対象キーワードを含んでいるか？
						var hitKeyAndMatch =
							 // 全てのキーワードについて検索
							 keywords.Select(keyword => Tuple.Create(keyword, keyword.ReplaceFromPattern.Match(line, startAt)))
							 .Where(regres => regres.Item2.Success)
							 // 最初にヒットしたものを対象とする
							 .OrderBy(regres => regres.Item2.Index)
							 .FirstOrDefault();

						if (hitKeyAndMatch != null)
						{
							var hitKey = hitKeyAndMatch.Item1;
							var match = hitKeyAndMatch.Item2;

							replaceOcc = true;

							// ヒット位置より前の文字をそのままコピー
							newLine.Append(line.Substring(0, match.Index));

							// ヒット位置の文字を変更
							foreach (ExtendReplaceTo rep in hitKey.ReplaceToPattern)
							{
								if (rep.Type == ReplaceToType.Plain)
								{
									newLine.Append(rep.Label);
								}
								else
								{
									Group group = rep.Type == ReplaceToType.GroupIndex ?
										 match.Groups[rep.Index] :
										 match.Groups[rep.Label];

									string value = group.Value;

									switch (rep.Change)
									{
										case ChangeCase.LowerHead:
											value = Char.ToLower(value[0]) + value.Substring(1);
											break;
										case ChangeCase.LowerAll:
											value = value.ToLower();
											break;
										case ChangeCase.UpperHead:
											value = Char.ToUpper(value[0]) + value.Substring(1);
											break;
										case ChangeCase.UpperAll:
											value = value.ToUpper();
											break;
									}
									newLine.Append(value);
								}
							}

							// ヒット位置より後の文字をそのままコピー
							newLine.Append(line.Substring(match.Index + match.Length));


							line = newLine.ToString();
							startAt = match.Index + match.Length;
							newLine.Clear();

						}
						else
						{
							// どのパターンもヒットしていないなら打ち止め、次の行へ
							break;
						}

					} while (startAt < line.Length);

					writer.Write(line);
					if (lnCd != null) writer.Write(lnCd);
				}
			}

			if (replaceOcc)
			{
				//置換が行われた

				// 置換前のファイルの退避(DBへ)
				ReplacedFile key = new ReplacedFile();
				key.ActNo = actNo;
				key.FilePath = filepath;
				key.ReplacedFileHash = Util.MakeHash(output);
				db.Insert(key, new FileInfo(filepath));

				// ファイル置換
				File.Delete(filepath);
				File.Move(output, filepath);
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
			var progress = new Progress<int>(SetProgress);
			var result = await Task.Run(() => DoCancel(progress));

			/*置換処理完了*/
			if (result)
			{
				db.DeleteActNo(actNo);
				MessageBox.Show("置換処理を取り消しました", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
				actNo--;
			}
			else
			{
				MessageBox.Show("置換処理の取り消しを取り消しました", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
			}

			ReplaceButton.IsEnabled = true;
			CancelButton.IsEnabled = actNo > 0;
		}

		private bool DoCancel(IProgress<int> informer)
		{

			var fileinfos = db.SelectFileInfos(actNo);

			long fileinfoLen = fileinfos.Count * 2;
			long fileinfoIdx = fileinfoLen;

			if (fileinfoLen == 0) { return true; }

			// ハッシュ値チェック
			foreach (var fileinfo in fileinfos)
			{
				--fileinfoIdx;

				string nowHash = Util.MakeHash(fileinfo.FilePath);
				string storeHash = fileinfo.ReplacedFileHash;

				if (nowHash != storeHash)
				{
					fileinfoIdx = fileinfoLen / 2;
					var result = MessageBox.Show(
						 "置換後にファイルの変更が行われているようです。\r\n本当に取り消しますか？",
						 "確認",
						 MessageBoxButton.YesNo);

					if (result == MessageBoxResult.Yes)
					{
						break;
					}
					else
					{
						return false;
					}
				}

				informer.Report((int)(Int32.MaxValue * fileinfoIdx / fileinfoLen));
			}

			informer.Report((int)(Int32.MaxValue * fileinfoIdx / fileinfoLen));

			// 置換開始
			foreach (var fileinfo in fileinfos)
			{
				--fileinfoIdx;
				db.Select(fileinfo.Id, new FileInfo(fileinfo.FilePath));
				informer.Report((int)(Int32.MaxValue * fileinfoIdx / fileinfoLen));
			}

			return true;
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
