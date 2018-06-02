using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MultiRept
{

	/// <summary>
	/// 置換内容を保持するためのクラス
	/// </summary>
	public class ReplaceParameter
	{
		/// <summary>
		/// 検索キーワード
		/// </summary>
		public String ReplaceFrom { private set; get; }

		/// <summary>
		/// 検索キーワードを正規表現でのパターンに変換したもの
		/// </summary>
		public Regex ReplaceFromPattern { private set; get; }

		/// <summary>
		/// 置換後の文字列
		/// </summary>
		public String ReplaceTo { private set; get; }
		public IList<ExtendReplaceTo> ReplaceToPattern { private set; get; }

		public FindMode Mode { private set; get; }

		/// <summary>
		/// 検索方法：テキスト
		/// </summary>
		public bool IsPlainMode { get { return Mode == FindMode.Plain; } }
		/// <summary>
		/// 検索方法：単語単位
		/// </summary>
		public bool IsWordMode { get { return Mode == FindMode.Word; } }
		/// <summary>
		/// 検索方法：正規表現
		/// </summary>
		public bool IsRegexMode { get { return Mode == FindMode.Regex; } }

		public ReplaceParameter(string replaceFrom, string replaceTo, FindMode mode)
		{
			this.ReplaceFrom = replaceFrom;
			this.ReplaceTo = replaceTo;
			this.Mode = mode;

			switch (mode)
			{
				case FindMode.Plain:
					string escapedText = Regex.Escape(this.ReplaceFrom);
					this.ReplaceFromPattern = new Regex(escapedText, RegexOptions.Compiled);
					this.ReplaceToPattern = new[] { ExtendReplaceTo.Plain(this.ReplaceTo) };
					break;


				case FindMode.Word:
					string escapedWord = Regex.Escape(this.ReplaceFrom);
					escapedText = "\\b" + escapedWord + "\\b";
					this.ReplaceFromPattern = new Regex(escapedText, RegexOptions.Compiled);
					this.ReplaceToPattern = new[] { ExtendReplaceTo.Plain(this.ReplaceTo) };
					break;

				case FindMode.Regex:
					this.ReplaceFromPattern = new Regex(this.ReplaceFrom, RegexOptions.Compiled);
					this.ReplaceToPattern = ExtendReplaceTo.Parse(this.ReplaceTo);
					break;
			}
		}
	}

	public enum FindMode
	{
		Plain,
		Word,
		Regex
	}

	public enum ReplaceToType
	{
		Plain,
		GroupIndex,
		GroupLabel
	}

	public enum ChangeCase
	{
		None,
		UpperHead,
		UpperAll,
		LowerHead,
		LowerAll
	}

	/// <summary>
	/// 置換後のテキストについて、正規表現での特殊な記述。
	/// (\L$1とか)を表示するためのクラス
	/// </summary>
	public class ExtendReplaceTo
	{
		public ReplaceToType Type { private set; get; }
		public ChangeCase Change { private set; get; }
		public int Index { private set; get; }
		public string Label { private set; get; }

		public static ExtendReplaceTo Plain(string text)
		{
			return new ExtendReplaceTo()
			{
				Type = ReplaceToType.Plain,
				Label = text
			};
		}

		public static ExtendReplaceTo Group(int index, ChangeCase change)
		{
			return new ExtendReplaceTo()
			{
				Type = ReplaceToType.GroupIndex,
				Index = index,
				Change = change
			};
		}
		public static ExtendReplaceTo Group(string label, ChangeCase change)
		{
			return new ExtendReplaceTo()
			{
				Type = ReplaceToType.GroupLabel,
				Label = label,
				Change = change
			};
		}

		public static List<ExtendReplaceTo> Parse(string replaceTo)
		{

			var repto = new List<ExtendReplaceTo>();
			var tgc = ChangeCase.None;
			var buff = new StringBuilder();
			var escape = false;

			for (int i = 0; i < replaceTo.Length; ++i)
			{
				char c = replaceTo[i];

				if (escape)
				{
					escape = false;
					buff.Append(c);

				}
				else
				{
					if (c == '\\')
					{
						// エスケープか、置換後テキストへの特殊な変換
						char c2 = CharAt(replaceTo, i + 1);
						char c3 = CharAt(replaceTo, i + 2);
						char c4 = CharAt(replaceTo, i + 3);

						if (Array.IndexOf(new[] { 'l', 'L', 'u', 'U' }, c2) >= 0
							&& c3 == '$'
							&& (c4 == '{' || ('0' <= c4 && c4 <= '9')))
						{
							// 置換後テキストへの特殊な変換

							switch (c2)
							{
								case 'l': tgc = ChangeCase.LowerHead; break;
								case 'L': tgc = ChangeCase.LowerAll; break;
								case 'u': tgc = ChangeCase.UpperHead; break;
								case 'U': tgc = ChangeCase.UpperAll; break;
							}

							i += 2;
							c = c3;

						}
						else
						{
							// エスケープ
							escape = true;
						}
					}

					if (c == '$')
					{
						// 置換後テキストへの変換

						// $後の文字は何か？
						char c2 = CharAt(replaceTo, i + 1);

						if (Char.IsDigit(c2))
						{
							// インデックス

							// 数字ではなくなるインデックスを探す
							int j = i + 1;
							do { c2 = CharAt(replaceTo, ++j); }
							while (Char.IsDigit(c2));

							// 置換後テキストが始まる前の固定文字
							if (buff.Length != 0)
							{
								repto.Add(ExtendReplaceTo.Plain(buff.ToString()));
								buff.Clear();
							}

							// 置換後テキストが始まる前の固定文字
							repto.Add(ExtendReplaceTo.Group(
								 Int32.Parse(replaceTo.Substring(i + 1, j - (i + 1))),
								 tgc));

							tgc = ChangeCase.None;

							i = j - 1;

						}
						else if (c2 == '{')
						{
							// ラベルかインデックス

							// '}'を探す
							int j = i + 1;
							do { c2 = CharAt(replaceTo, ++j); }
							while (c2 != '}');

							// 置換後テキストが始まる前の固定文字
							if (buff.Length != 0)
							{
								repto.Add(ExtendReplaceTo.Plain(buff.ToString()));
								buff.Clear();
							}

							// 置換後テキストが始まる前の固定文字
							var holder = replaceTo.Substring(i + 2, j - (i + 2));

							int index;
							if (Int32.TryParse(holder, out index))
							{
								// インデックス
								repto.Add(ExtendReplaceTo.Group(index, tgc));

							}
							else
							{
								// ラベル
								repto.Add(ExtendReplaceTo.Group(holder, tgc));
							}

							tgc = ChangeCase.None;

							i = j;

						}
						else
						{
							// $はただの文字として扱う
							buff.Append(c);
						}
					}
					else
					{
						buff.Append(c);
					}
				}
			}

			if (buff.Length != 0)
			{
				repto.Add(ExtendReplaceTo.Plain(buff.ToString()));
				buff.Clear();
			}

			return repto;
		}

		private static char CharAt(string text, int idx)
		{
			return idx < text.Length ? text[idx] : (char)0;
		}
	}
}
