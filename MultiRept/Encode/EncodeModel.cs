using System;
using System.Text;

using EncodeDetector.Extends;

namespace EncodeDetector.Model
{
	/// <summary>
	/// バイナリのエンコード判定用クラス
	/// </summary>
	public abstract class EncodeModel
	{

		/// <summary>
		/// Encodingインスタンス
		/// </summary>
		public abstract Encoding Encoding { get; }

		/// <summary>
		/// BOM付きのEncodingインスタンス
		/// </summary>
		/// <exception cref="InvalidOperationException">
		/// BOMを持たない場合
		/// </exception>
		public virtual Encoding EncodingWithBom
		{
			get
			{
				throw new InvalidOperationException();
			}
		}

		/// <summary>
		/// BOMを示すbyte配列。BOMがない場合
		/// </summary>
		public virtual byte[] Bom
		{
			get { return null; }
		}

		/// <summary>
		/// 引数で指定されたバイト配列が、このインスタンスのEncodingに
		/// 適合しているか判定します。
		/// </summary>
		/// <param name="buffer">検査対象のバイト配列</param>
		/// <param name="index">検査の開始位置</param>
		/// <param name="endIndex">検査の終了位置（含まない）</param>
		/// <returns>適合具合のスコア値。まったく適合しない場合は負の値を返す。</returns>
		public abstract int Check(byte[] buffer, ref int index, int endIndex);
	}

	/*
	public class Ascii : EncodeModel
	{
		public override Encoding Encoding
		{
			get
			{
				return Encoding.GetEncoding(20127);
			}
		}

		public override int Check(byte[] buffer, ref int index, int endIndex)
		{
			do
			{
				byte bf = buffer[index];
				if (bf < 0x00 || 0x7F < bf)
				{
					return -1;
				}

			} while (++index < endIndex);

			return 0;
		}
	}
	*/

	public class Sjis : EncodeModel
	{
		private b2judge b2j = CodePointJuror.judge2(CodePointJuror.Type.SJIS);

		public override Encoding Encoding
		{
			get
			{
				return Encoding.GetEncoding(932);
			}
		}

		public override int Check(byte[] buffer, ref int index, int endIndex)
		{
			// [0x00-0x7F]
			// [0xA1-0xDF]
			// [0x81-0x9F or 0xE0-0xFC][0x40-0x7E or 0x80-0xFC]

			int score = 0;
			do
			{
				byte b1 = buffer[index];

				if (b1.IsAsciiText())
				{
					// ascii
					continue;
				}
				else if (b1.Between(0xA1, 0xDF))
				{
					//JIS X 0201
					score += 1;
				}
				else if (b1.Between(0x81, 0x9F) || b1.Between(0xE0, 0xFC))
				{
					byte b2;
					if (buffer.Next(ref index, out b2))
					{
						if (b2.Between(0x40, 0x7E) || b2.Between(0x80, 0xFC))
						{
							score += b2j(b1, b2);
							continue;
						}
						else return -1;
					}
					else break;
				}
				else return -1;

			} while (++index < endIndex);

			return score;
		}
	}

	public class Utf8 : EncodeModel
	{
		private b2judge b2j = CodePointJuror.judge2(CodePointJuror.Type.UTF8);
		private b3judge b3j = CodePointJuror.judge3(CodePointJuror.Type.UTF8);

		public override Encoding Encoding
		{
			get
			{
				return new UTF8Encoding(false);
			}
		}
		public override Encoding EncodingWithBom
		{
			get
			{
				return new UTF8Encoding(true);
			}
		}


		public override byte[] Bom
		{
			get
			{
				return new byte[] { 0xEF, 0xBB, 0xBF };
			}
		}

		public override int Check(byte[] buffer, ref int index, int endIndex)
		{
			// [0x00-0x7F]
			// [0xC0-0xDF][0x80-0xBF]
			// [0xE0-0xEF][0x80-0xBF][0x80-0xBF]
			// [0xF0-0xF7][0x80-0xBF][0x80-0xBF][0x80-0xBF]

			int score = 0;
			do
			{
				byte b1 = buffer[index];
				if (b1.IsAsciiText())
				{
					continue;
				}
				else if (b1.Between(0xC0, 0xDF))
				{
					byte b2;
					if (buffer.Next(ref index, out b2))
					{
						if (b2.Between(0x80, 0xBF))
						{
							score += b2j(b1, b2);
							continue;
						}
						else return -1;
					}
					else break;
				}
				else if (b1.Between(0xE0, 0xEF))
				{
					byte b2;
					byte b3;
					if (buffer.Next(ref index, out b2, out b3))
					{
						if (b2.Between(0x80, 0xBF) && b3.Between(0x80, 0xBF))
						{
							score += b3j(b1, b2, b3);
							continue;
						}
						else return -1;
					}
					else break;

				}
				else if (b1.Between(0xF0, 0xF7))
				{
					byte b2;
					byte b3;
					byte b4;
					if (buffer.Next(ref index, out b2, out b3, out b4))
					{
						if (b2.Between(0x80, 0xBF) && b3.Between(0x80, 0xBF) && b4.Between(0x80, 0xBF))
						{
							score += CodePointJuror.MAXPOINT + 1;
							continue;
						}
						else return -1;
					}
					else break;
				}

			} while (++index < endIndex);

			return score;
		}
	}

	public class EucJP : EncodeModel
	{
		private b2judge b2j = CodePointJuror.judge2(CodePointJuror.Type.EUCJP);

		public override Encoding Encoding
		{
			get
			{
				return Encoding.GetEncoding(51932);
			}
		}

		public override int Check(byte[] buffer, ref int index, int endIndex)
		{
			// [0x00-0x7F]
			// [0xA1-0xF3][0xA1-0xFE]
			// [0x8E][0xA1-0xDF]
			// [0x8F][0xA1-0xFE][0xA1-0xFE]

			// 。。。[0xA1-0xF3][0xA1-0xFE]について、本来は
			// [0xA1,0xA4-0xF3][0xA1-0xFE]
			// [0xA2][0xA1-0xAE, 0xAA-0xAF, 0xC0, 0xC1, 0xCA-0xCF, 0xD0, 0xDC-0xDF, 0xE0-0xEA, 0xF2-0xF9, 0xFE]
			// [0xA3][0xB0-0xB9, 0xC1-0xCF, 0xD0-0xDA, 0xE1-0xEF, 0xF0-0xFA]
			// [0xF4][0xA1-0xA6]

			int score = 0;
			do
			{
				byte b1 = buffer[index];
				byte b2;
				byte b3;

				if (b1.IsAsciiText())
				{
					continue;
				}
				else if (b1.Between(0xA1, 0xF3))
				{
					if (buffer.Next(ref index, out b2))
					{
						if (b2.Between(0xA1, 0xFE))
						{
							score += b2j(b1, b2);
							continue;
						}
						else return -1;
					}
					else break;
				}
				else if (b1 == 0x8E)
				{
					if (buffer.Next(ref index, out b2))
					{
						if (b2.Between(0xA1, 0xDF))
						{
							score += b2j(b1, b2);
							continue;
						}
						else return -1;
					}
					else break;
				}
				else if (b1 == 0x8F)
				{
					if (buffer.Next(ref index, out b2, out b3))
					{
						if (b2.Between(0xA1, 0xFE) && b3.Between(0xA1, 0xFE))
						{
							score += CodePointJuror.MAXPOINT + 1;
							continue;
						}
						else return -1;
					}
					else break;
				}
				else return -1;

			} while (++index < endIndex);

			return score;
		}
	}
}