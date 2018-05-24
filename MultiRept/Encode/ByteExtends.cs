using System;

namespace EncodeDetector.Extends
{
	public static class ByteExtends
	{
		/// <summary>
		/// 指定した範囲内の値か判定します。
		/// </summary>
		/// <param name="target">判定対象の値</param>
		/// <param name="from">下限(含む)</param>
		/// <param name="to">上限(含む)</param>
		/// <returns></returns>
		public static bool Between(this Byte target, byte from, byte to)
		{
			return from <= target && target <= to;
		}
		public static bool Between(this Int32 target, int from, int to)
		{
			return from <= target && target <= to;
		}

		public static bool IsAscii(this Byte bf)
		{
			return (0x00 <= bf && bf <= 0x7F);
		}

		public static bool IsAsciiText(this Byte bf)
		{
			return (0x20 <= bf && bf <= 0x7F) || (0x07 <= bf && bf <= 0x0d);
		}

		public static bool In(this Int32 target, params int[] tail)
		{
			foreach (int t in tail)
			{
				if (target == t) { return true; }
			}
			return false;
		}

		public static bool Next(this byte[] buff, ref int index, out byte b1)
		{
			if (index + 1 < buff.Length)
			{
				b1 = buff[++index];
				return true;
			}
			else
			{
				b1 = 0;
				return false;
			}
		}
		public static bool Next(this byte[] buff, ref int index, out byte b1, out byte b2)
		{
			if (index + 2 < buff.Length)
			{
				b1 = buff[++index];
				b2 = buff[++index];
				return true;
			}
			else
			{
				b1 = 0;
				b2 = 0;
				return false;
			}
		}

		public static bool Next16B(this byte[] buff, ref int index, out int s1)
		{
			if (index + 2 < buff.Length)
			{
				s1 = (buff[++index] << 8);
				s1 |= buff[++index];
				return true;
			}
			else
			{
				s1 = 0;
				return false;
			}
		}

		public static bool Next16L(this byte[] buff, ref int index, out int s1)
		{
			if (index + 2 < buff.Length)
			{
				s1 = buff[++index];
				s1 |= (buff[++index] << 8);
				return true;
			}
			else
			{
				s1 = 0;
				return false;
			}
		}

		public static bool Next(this byte[] buff, ref int index, out byte b1, out byte b2, out byte b3)
		{
			if (index + 3 < buff.Length)
			{
				b1 = buff[++index];
				b2 = buff[++index];
				b3 = buff[++index];
				return true;
			}
			else
			{
				b1 = 0;
				b2 = 0;
				b3 = 0;
				return false;
			}
		}
	}
}
