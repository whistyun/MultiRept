﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MultiRept
{
	public class Util
	{
		/// <summary>
		/// ワイルドカードでの入力を正規表現の形式に変換します。
		/// </summary>
		/// <param name="ptn">ワイルドカード</param>
		/// <returns>正規表現のパターン</returns>
		public static string Wild2Regex(string ptn)
		{
			var builder = new StringBuilder();
			foreach (char c in ptn)
			{
				switch (c)
				{
					case '?':
						//?は任意の1文字を示す正規表現(.)に変換
						builder.Append('.');
						break;

					case '*':
						//*は0文字以上の任意の文字列を示す正規表現(.*)に変換
						builder.Append(".*");
						break;

					default:
						//上記以外はエスケープする
						builder.Append(Regex.Escape(c.ToString()));
						break;
				}
			}
			return builder.ToString();
		}

		/// <summary>
		/// ファイルの内容からハッシュ(SHA512)を計算します
		/// </summary>
		/// <param name="filepath"></param>
		/// <returns></returns>
		public static string MakeHash(string filepath)
		{
			var sha512 = new SHA512Managed();
			using (var stream = new FileStream(filepath, FileMode.Open))
			{
				var hash = sha512.ComputeHash(stream);
				return BitConverter.ToString(hash);
			}
		}

		/// <summary>
		/// 指定されたフォルダを削除します。
		/// フォルダ内にファイルやフォルダがある場合は、
		/// それらを含めて削除します。
		/// </summary>
		/// <param name="path"></param>
		public static void DeleteDir(string path)
		{
			foreach (var sub in Directory.GetFiles(path))
			{
				File.Delete(sub);
			}

			foreach (var sub in Directory.GetDirectories(path))
			{
				DeleteDir(sub);
			}

			Directory.Delete(path);
		}

		public static string GetMyDisplayName(Encoding encoding)
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

			return encodingName;
		}
	}

	public static class StreamReaderEnhance
	{
		public static string ReadLine(this TextReader reader, out string lineBreak)
		{
			StringBuilder builder = new StringBuilder();
			int cd;

			while ((cd = reader.Read()) != -1)
			{
				if (cd == '\r')
				{
					if (reader.Peek() == '\n')
					{
						reader.Read();
						lineBreak = "\r\n";
					}
					else
					{
						lineBreak = "\r";
					}
					return builder.ToString();
				}
				else if (cd == '\n')
				{
					lineBreak = "\n";
					return builder.ToString();
				}
				else
				{
					builder.Append((char)cd);
				}
			}

			lineBreak = null;

			return builder.Length == 0 ? null : builder.ToString();
		}
	}
}
