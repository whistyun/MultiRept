using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using EncodeDetector;

namespace MultiRept
{
	public class ReplaceLogicParam
	{
		private string filePattern;
		private Regex[] filePatternRegexes;

		/// <summary>処理を行う対象のディレクトリ</summary>
		public string RootDir { set; get; }
		/// <summary>処理対象のファイル名パターン</summary>
		public string FilePattern
		{
			set
			{
				filePattern = value;
				// ファイルパターンを(,)で区切って正規表現のパターンに変換
				filePatternRegexes = (from ptn in filePattern.Split(',')
											 select new Regex("^" + Util.Wild2Regex(ptn) + "$")).ToArray();
			}
			get
			{
				return filePattern;
			}

		}
		/// <summary>隠しフォルダ/ファイルは除外する</summary>
		public bool IgnoreHide { set; get; }
		/// <summary>ファイルを読み込む際のエンコード</summary>
		public Encoding Encoding { set; get; }
		/// <summary>置換キーワード一覧</summary>
		public List<ReplaceParameter> Keywords { set; get; }

		public Regex[] FilePatternRegexes
		{
			get
			{
				return filePatternRegexes;
			}
		}
	}

	/// <summary>
	/// 特定のファイルに対して変更箇所のみを知るためのインターフェース
	/// </summary>
	public interface IReplaceAbstractListener
	{
		void Begin(string filePath);
		void Inform(int lineAt, Encoding encoding, string line);
		void End();
		void ErrorEnd(string message);
	}

	/// <summary>
	/// 特定のファイルに対してどのような置換処理が行われたか詳細に知るためのインターフェース
	/// </summary>
	public interface IReplaceDetailListener
	{
		void Begin(string filePath);
		void AddPlain(string plainText);
		void AddDiff(string original, string changed);
		void NewLine();
		void End();
		void ErrorEnd(string message);
	}

	public delegate void Begin(string filePath);
	public delegate void Inform(int lineAt, Encoding encoding, string line);
	public delegate void End();
	public delegate void ErrorEnd(string message);
	public class ReplaceLogic
	{
		private FileStore db;

		/// <summary>特定のファイルに対して置換処理を開始したことを通知します</summary>
		public event Begin Begin;

		/// <summary>特定のファイルに対してどの行にたいして置換が行われたか通知します</summary>
		public event Inform Inform;

		/// <summary>置換処理が正常終了したことを通知します</summary>
		public event End End;

		/// <summary>置換処理が異常終了したことを通知します</summary>
		public event ErrorEnd ErrorEnd;

		public event Action<string> AddPlain;
		public event Action<string, string> AddDiff;
		public event Action NewLine;

		public ReplaceLogic(FileStore db)
		{
			this.db = db;
		}

		public void AddListener(IReplaceAbstractListener listener)
		{
			Begin += listener.Begin;
			Inform += listener.Inform;
			End += listener.End;
			ErrorEnd += listener.ErrorEnd;
		}

		public void AddListener(IReplaceDetailListener listener)
		{
			Begin += listener.Begin;
			AddPlain += listener.AddPlain;
			AddDiff += listener.AddDiff;
			NewLine += listener.NewLine;
			End += listener.End;
			ErrorEnd += listener.ErrorEnd;
		}

		public void Do(ReplaceLogicParam param, IProgress<int> informer)
		{
			//処理対象のファイル一覧
			var targets = new List<string>();

			//ディレクトリを見つけた場合は、一度スタックにつめる
			var directories = new Stack<String>();
			directories.Push(param.RootDir);

			//ディレクトリの中身を確認
			while (directories.Count > 0)
			{
				// ファイルを取得し、ファイル名が処理対象のパターンを満たすか確認
				var directory = directories.Pop();
				foreach (var newFile in Directory.GetFiles(directory))
				{
					// 隠しファイルは無視するか？
					if (param.IgnoreHide)
					{
						var fileinfo = new FileInfo(newFile);
						if (fileinfo.Attributes.HasFlag(FileAttributes.Hidden)) continue;
					}

					// パターンのチェック
					foreach (var filePtnRegex in param.FilePatternRegexes)
					{
						if (filePtnRegex.IsMatch(newFile))
						{
							// 処理対象
							targets.Add(newFile);
							break;
						}
					}
				}

				// ディレクトリを取得し、スタックに詰める
				foreach (var newDirectory in Directory.GetDirectories(directory))
				{
					// 隠しファイルは無視するか？
					if (param.IgnoreHide)
					{
						var dirInfo = new DirectoryInfo(newDirectory);
						if (dirInfo.Attributes.HasFlag(FileAttributes.Hidden)) continue;
					}

					// 一度スタックにつめる
					directories.Push(newDirectory);
				}
			}

			// ファイルを1件ずつ置換処理開始
			long index = 1;
			long total = targets.Count;
			foreach (var target in targets)
			{
				try
				{
					Begin?.Invoke(target);
					TryReplace(target, param.Encoding, param.Keywords);
					End?.Invoke();
				}
				catch (IOException ioe)
				{
					ErrorEnd?.Invoke(ioe.Message);
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
			var output = Path.GetTempFileName();

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
								var beforeText = line.Substring(0, match.Index);
								newLine.Append(beforeText);

								var bgnIdx = startAt;
								var endIdx = match.Index;
								if (bgnIdx != endIdx)
								{
									AddPlain?.Invoke(line.Substring(bgnIdx, endIdx - bgnIdx));
								}

								int newLineLength = newLine.Length;

								// ヒット位置の文字を置換後の文字に変更
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

								AddDiff?.Invoke(
									match.Groups[0].Value,
									newLine.ToString().Substring(newLineLength));

								// ヒット位置より後の文字をそのままコピーし、再検索
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

						// startAt < line.Lengthなら、置換されなかった文字があるはずなので、通知
						if (startAt < line.Length)
						{
							AddPlain?.Invoke(line.Substring(startAt));
						}

						// 置換処理が行われたことを通知
						if (findMatch)
						{
							Inform?.Invoke(lineCnt, encoding, line);
						}

						writer.Write(line);
						if (lnCd != null)
						{
							writer.Write(lnCd);
							NewLine?.Invoke();
						}
					}
				}

				if (replaceOcc)
				{
					// 置換前のファイルの退避(DBへ)
					db.Insert(
						filepath,
						Util.MakeHash(output),
						(Action<string, string>)delegate (string src, string dist)
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
	}

	public delegate bool FileListAlert(List<string> fileList);
	public class CancelLogic
	{
		private FileStore db;

		/// <summary>置換処理を開始するにあたって処理を開始できないことを通知します</summary>
		public event FileListAlert Error;

		/// <summary>置換処理を開始するにあたって処理を開始できないことを通知します</summary>
		public event FileListAlert Confirm;

		/// <summary>処理中の確認</summary>
		public event Action<string> InformUserInterrupt;

		public CancelLogic(FileStore db)
		{
			this.db = db;
		}

		public bool Do(IProgress<int> informer)
		{
			var fileinfos = db.SelectFileInfos();

			long fileinfoLen = fileinfos.Count * 2;
			long fileinfoIdx = fileinfoLen;
			if (fileinfoLen == 0) { return true; }

			var userChangedfileList = new List<string>();
			var userLockedfileList = new List<string>();

			// ハッシュ値を使用してファイル変更されていないか確認
			foreach (var fileinfo in fileinfos)
			{
				--fileinfoIdx;
				try
				{
					string nowHash = Util.MakeHash(fileinfo.FilePath);
					string storeHash = fileinfo.ReplacedFileHash;
					if (nowHash != storeHash)
					{
						userChangedfileList.Add(fileinfo.FilePath);
					}
				}
				catch (FileNotFoundException)
				{
					userChangedfileList.Add(fileinfo.FilePath);
				}
				catch (IOException)
				{
					userLockedfileList.Add(fileinfo.FilePath);
				}

				informer.Report((int)(Int32.MaxValue * fileinfoIdx / fileinfoLen));
			}

			// もし、書込みができないファイルがある場合はエラーとする
			if (userLockedfileList.Count != 0)
			{
				informer.Report(Int32.MaxValue);
				Error?.Invoke(userLockedfileList);
				return false;
			}

			// 変更がある場合は、確認の割り込みを行う
			if (userChangedfileList.Count != 0)
			{
				var rtnVal = Confirm?.Invoke(userChangedfileList);
				if (rtnVal.HasValue && !rtnVal.Value)
				{
					// キャンセル
					informer.Report(Int32.MaxValue);
					return false;
				}
			}

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
						// ユーザさんが処理中にロックをかけた
						InformUserInterrupt?.Invoke(fileinfo.FilePath);
					}
				} while (true);


				informer.Report((int)(Int32.MaxValue * fileinfoIdx / fileinfoLen));
			}

			return true;
		}
	}
}
