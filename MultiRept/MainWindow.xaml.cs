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

namespace MultiRept
{
	delegate void FileMove(string src, string dist);
	delegate void DialogAlert(string message);
	delegate MessageBoxResult DialogConfirm(string message);

	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class MainWindow : Window
	{
		private ResultWindow logWindow;
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
		/// 置換実施ボタン押下時の処理
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private async void ReplaceButton_Click(object sender, RoutedEventArgs e)
		{
			var directoryPath = directoryTextBox.Text;
			var filePattern = filePatternTextBox.Text;
			var keywordComponents = replaceKeyList.Children.OfType<OneTask>().ToList();

			string errorMessage = null;

			// チェック：ディレクトリは未入力でないか？
			if (directoryPath == "")
			{
				string message = "フォルダを選択してください";
				errorMessage = errorMessage ?? message;

				directoryTextBox.ErrorMessage = message;
			}
			else
			{
				directoryTextBox.HasError = false;
			}

			// チェック：ディレクトリは存在するか？
			if (!Directory.Exists(directoryPath))
			{
				string message = "指定されたフォルダは存在しません";
				errorMessage = errorMessage ?? message;

				directoryTextBox.ErrorMessage = message;
			}
			else
			{
				directoryTextBox.HasError = false;
			}

			// チェック：ファイルパターンが(わざわざ)未入力にされていないか
			if (filePattern.Trim() == "")
			{
				string message = "ファイルパターンが未入力です";
				errorMessage = errorMessage ?? message;

				filePatternTextBox.ErrorMessage = message;
			}
			else
			{
				filePatternTextBox.HasError = false;
			}

			// チェック：置換キーワードが未入力でないか
			var emptyKeywordsExists = keywordComponents.Where(elem => elem.CheckError()).Count() != 0;
			if (emptyKeywordsExists)
			{
				errorMessage = errorMessage ?? "置換キーワードに不正な入力があります";
			}

			if (errorMessage != null)
			{
				MessageBox.Show(this, errorMessage, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			// 置換キーワードパラメタ
			var keywords = (from c in keywordComponents select c.GetParameter()).ToList();

			// 文字コード設定
			var encoding =
				 Utf8RadioButton.IsChecked.Value ? new UTF8Encoding(false) :
				 EucJpRadioButton.IsChecked.Value ? Encoding.GetEncoding(51932) :
				 SjisRadioButton.IsChecked.Value ? Encoding.GetEncoding(932) :
				 null;

			// ファイルパターンを(,)で区切って正規表現のパターンに変換
			var filePatterns = (from ptn in filePattern.Split(',')
									  select new Regex("^" + Util.Wild2Regex(ptn) + "$")).ToArray();

			var ignoreHide = IgnoreHideFile.IsChecked.Value;

			/*置換処理開始*/
			ReplaceButton.IsEnabled = false;
			CancelButton.IsEnabled = false;
			Progress.Maximum = Int32.MaxValue;
			Progress.Minimum = 0;
			Progress.Value = 0;

			logWindow = new ResultWindow();
			logWindow.Folder = directoryPath;
			logWindow.FilePattern = filePattern;

			// 置換処理
			db.NewAct();
			var progress = new Progress<int>(SetProgress);
			await Task.Run(() => DoReplace(directoryPath, filePatterns, ignoreHide, encoding, keywords, progress));

			/*置換処理完了*/
			MessageBox.Show(this, "置換処理が完了しました", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
			ReplaceButton.IsEnabled = true;
			CancelButton.IsEnabled = true;

			logWindow.Show();
		}

		/// <summary>
		/// 指定のディレクトリを再帰的に検索し、
		/// 置換処理を行います。
		/// </summary>
		/// <param name="directoryPath">処理を行う対象のディレクトリ</param>
		/// <param name="filePatterns">処理対象のファイル名パターン</param>
		/// <param name="ignoreHide">隠しフォルダ/ファイルは除外する</param>
		/// <param name="encoding">ファイルを読み込む際のエンコード</param>
		/// <param name="keywords">置換キーワード一覧</param>
		/// <param name="informer">進捗状況通知先(0～Int32.MaxValue)</param>
		private void DoReplace(string directoryPath, Regex[] filePatterns, bool ignoreHide, Encoding encoding, List<ReplaceParameter> keywords, IProgress<int> informer)
		{

			var targets = new List<string>();

			var directories = new Stack<String>();
			directories.Push(directoryPath);
			while (directories.Count > 0)
			{
				var directory = directories.Pop();
				foreach (var newFile in Directory.GetFiles(directory))
				{
					if (ignoreHide)
					{
						var fileinfo = new FileInfo(newFile);
						if (fileinfo.Attributes.HasFlag(FileAttributes.Hidden)) continue;
					}

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
					if (ignoreHide)
					{
						var dirInfo = new DirectoryInfo(newDirectory);
						if (dirInfo.Attributes.HasFlag(FileAttributes.Hidden)) continue;
					}

					directories.Push(newDirectory);
				}
			}

			long index = 1;
			long total = targets.Count;

			if (total == 0) { return; }

			foreach (var target in targets)
			{
				try
				{
					StartLog();
					TryReplace(target, encoding, keywords);
				}
				catch (IOException ioe)
				{
					OutErrLog(target, ioe.Message);
				}
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

			if (encoding == null)
			{
				var detector = new CodeDetector();
				encoding = detector.Check(new FileInfo(filepath));
			}

			if (encoding != null)
			{
				using (var reader = new StreamReader(filepath, encoding, false, 1024 * 20))
				using (var writer = new StreamWriter(output, false, encoding, 1024 * 20))
				{

					int lineCnt = 0;
					string lnCd;
					string line;
					StringBuilder newLine = new StringBuilder();
					while ((line = reader.ReadLine(out lnCd)) != null)
					{
						lineCnt++;
						bool findMatch = false;
						int startAt = 0;

						do
						{
							//置換対象キーワードを含んでいるか？
							var hitKeyAndMatch =
								 // 全てのキーワードについて検索
								 keywords.Select(keyword => Tuple.Create(
									 keyword,
									 keyword.ReplaceFromPattern.Matches(line, startAt)
										.Cast<Match>().Where(m => m.Success && m.Length != 0)
										.OrderBy(m => m.Index)
										.FirstOrDefault()
								 ))
								 .Where(regres => regres.Item2 != null && regres.Item2.Success && regres.Item2.Length != 0)
								 // 最初にヒットしたものを対象とする
								 .OrderBy(regres => regres.Item2.Index)
								 .FirstOrDefault();

							if (hitKeyAndMatch != null)
							{
								findMatch = true;

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
								startAt = newLine.Length;
								newLine.Append(line.Substring(match.Index + match.Length));
								line = newLine.ToString();
								newLine.Clear();
							}
							else
							{
								// どのパターンもヒットしていないなら打ち止め、次の行へ
								break;
							}

						} while (startAt < line.Length);

						if (findMatch)
						{
							OutLog(filepath, lineCnt, encoding, line);
						}


						writer.Write(line);
						if (lnCd != null) writer.Write(lnCd);
					}
				}

				if (replaceOcc)
				{
					// 置換前のファイルの退避(DBへ)
					db.Insert(
						filepath,
						Util.MakeHash(output),
						(FileMove)delegate (string src, string dist)
						{
							// ファイル置換
							File.Delete(dist);
							File.Move(src, dist);
						},
						output,
						filepath);
				}
			}
		}

		private void StartLog()
		{
			logWindow.StartLog();
		}

		private void OutErrLog(string filePath, string message)
		{
			logWindow.AddError(filePath, message);
		}
		private void OutLog(string filePath, int lineNo, Encoding encoding, string contents)
		{
			string encodingName;
			if (encoding is UTF8Encoding)
			{
				var utf8 = encoding as UTF8Encoding;
				encodingName = utf8.GetPreamble().Length == 0 ? "UTF-8" : "UTF-8(BOM)";
			}
			else switch (encoding.CodePage)
				{
					case 20932:
					case 51932:
						encodingName = "EUC-JP";
						break;
					case 932:
						encodingName = "SJIS";
						break;
					default:
						encodingName = encoding.WebName;
						break;

				}


			logWindow.Add(filePath, lineNo, encodingName, contents);
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

		private bool DoCancel(IProgress<int> informer)
		{

			var fileinfos = db.SelectFileInfos();

			long fileinfoLen = fileinfos.Count * 2;
			long fileinfoIdx = fileinfoLen;

			if (fileinfoLen == 0) { return true; }

			var userChangedfile = new List<string>();
			var userLockedfile = new List<string>();

			// ハッシュ値チェック
			foreach (var fileinfo in fileinfos)
			{
				--fileinfoIdx;

				try
				{
					string nowHash = Util.MakeHash(fileinfo.FilePath);
					string storeHash = fileinfo.ReplacedFileHash;
					if (nowHash != storeHash)
					{
						userChangedfile.Add(fileinfo.FilePath);
					}
				}
				catch (FileNotFoundException)
				{
					userChangedfile.Add(fileinfo.FilePath);
				}
				catch (IOException)
				{
					userLockedfile.Add(fileinfo.FilePath);
				}



				if (userLockedfile.Count != 0)
				{
					var lockedFiles = new StringBuilder();
					for (int i = 0; i < Math.Min(10, userLockedfile.Count); ++i)
					{
						lockedFiles.Append("・").Append(userLockedfile[i]).Append("\r\n");
					}
					if (10 < userLockedfile.Count)
					{
						lockedFiles.Append("...");
					}

					fileinfoIdx = fileinfoLen;

					Dispatcher.Invoke((DialogAlert)delegate (string msg)
					{
						MessageBox.Show(this, msg, "エラー", MessageBoxButton.OK);
					},
						"取消対象のファイルを別のプログラムが開いているため、\r\n" +
						"取消操作が開始できません。\r\n" + lockedFiles.ToString()
					);

					return false;
				}

				if (userChangedfile.Count != 0)
				{
					var changedFiles = new StringBuilder();
					for (int i = 0; i < Math.Min(10, userChangedfile.Count); ++i)
					{
						changedFiles.Append("・").Append(userChangedfile[i]).Append("\r\n");
					}
					if (10 < userChangedfile.Count)
					{
						changedFiles.Append("...");
					}

					fileinfoIdx = fileinfoLen / 2;

					var result = (MessageBoxResult)Dispatcher.Invoke((DialogConfirm)delegate (string msg)
					{
						return MessageBox.Show(this, msg, "確認", MessageBoxButton.YesNo);
					},
						"置換後にファイルの変更が行われているようです。\r\n" +
						"本当に取り消しますか？\r\n" + changedFiles.ToString()
					);

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
				do
				{
					try
					{
						db.Select(fileinfo);
						break;
					}
					catch (IOException)
					{
						Dispatcher.Invoke((DialogAlert)delegate (string msg)
						{
							MessageBox.Show(this, msg, "エラー", MessageBoxButton.OK);
						},
							"下記のファイルを変更できませんでした。\r\n" +
							"他のプログラムで開いていいないか確認してださい。\r\n" +
							"(OKボタンで変更を再開します)\r\n" +
							fileinfo.FilePath
						);
					}
				} while (true);


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
