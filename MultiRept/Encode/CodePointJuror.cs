using System;
using System.IO;
using System.Collections.Concurrent;
using System.Reflection;
using NumberStyles = System.Globalization.NumberStyles;

namespace EncodeDetector.Extends
{

	delegate int b2judge(byte b1, byte b2);
	delegate int b3judge(byte b1, byte b2, byte b3);

	static class CodePointJuror
	{
		public enum Type
		{
			UTF8, EUCJP, SJIS
		}

		private const int DRIFT = 2;

		private const int ELEMIDX_RANK = 0;
		private const int ELEMIDX_UTF8 = 1;
		private const int ELEMIDX_SJIS = 2;
		private const int ELEMIDX_EUCJP = 3;

		private static ConcurrentDictionary<int, int> utf8Juror = new ConcurrentDictionary<int, int>();
		private static ConcurrentDictionary<int, int> sjisJuror = new ConcurrentDictionary<int, int>();
		private static ConcurrentDictionary<int, int> eucjpJuror = new ConcurrentDictionary<int, int>();

		public static readonly int MAXPOINT;

		static CodePointJuror()
		{
			MAXPOINT = DRIFT;

			var assm = Assembly.GetExecutingAssembly();
			//このアセンブリの全てのリソース名を取得する
			string[] resources = assm.GetManifestResourceNames();
			string targetResource = null;
			foreach (var resource in resources)
			{
				if (resource.EndsWith(".CodePointJuror.txt"))
				{
					if (targetResource == null)
					{
						targetResource = resource;
					}
					else
					{
						targetResource = targetResource.Length < resource.Length ? targetResource : resource;
					}
				}
			}

			if (targetResource != null)
			{
				using (var stream = assm.GetManifestResourceStream(targetResource))
				using (var reader = new StreamReader(stream))
				{
					string line;
					while ((line = reader.ReadLine()) != null)
					{
						if (line[0] == '#') continue;

						var elms = line.Split(',');
						var rank = Int32.Parse(elms[ELEMIDX_RANK]) + DRIFT;
						utf8Juror[Int32.Parse(elms[ELEMIDX_UTF8], NumberStyles.HexNumber)] = rank;
						sjisJuror[Int32.Parse(elms[ELEMIDX_SJIS], NumberStyles.HexNumber)] = rank;
						eucjpJuror[Int32.Parse(elms[ELEMIDX_EUCJP], NumberStyles.HexNumber)] = rank;

						MAXPOINT = Math.Max(MAXPOINT, rank + DRIFT);
					}
				}
			}
		}

		private static int concat(byte b1, byte b2)
		{
			return (b1 << 8) | b2;
		}

		private static int concat(byte b1, byte b2, byte b3)
		{
			return (b1 << 16) | (b2 << 8) | b3;
		}

		public static b2judge judge2(Type type)
		{
			switch (type)
			{
				case Type.UTF8:
					return (b1, b2) => { var cd = concat(b1, b2); var map = utf8Juror; int pnt; return map.TryGetValue(cd, out pnt) ? pnt : DRIFT; };

				case Type.SJIS:
					return (b1, b2) => { var cd = concat(b1, b2); var map = sjisJuror; int pnt; return map.TryGetValue(cd, out pnt) ? pnt : DRIFT; };

				case Type.EUCJP:
					return (b1, b2) => { var cd = concat(b1, b2); var map = eucjpJuror; int pnt; return map.TryGetValue(cd, out pnt) ? pnt : DRIFT; };

				default:
					throw new InvalidDataException("Unknown encoding type: " + type);
			}
		}

		public static b3judge judge3(Type type)
		{
			switch (type)
			{
				case Type.UTF8:
					return (b1, b2, b3) => { var cd = concat(b1, b2, b3); var map = utf8Juror; int pnt; return map.TryGetValue(cd, out pnt) ? pnt : DRIFT; };

				case Type.SJIS:
					return (b1, b2, b3) => { var cd = concat(b1, b2, b3); var map = sjisJuror; int pnt; return map.TryGetValue(cd, out pnt) ? pnt : DRIFT; };

				case Type.EUCJP:
					return (b1, b2, b3) => { var cd = concat(b1, b2, b3); var map = eucjpJuror; int pnt; return map.TryGetValue(cd, out pnt) ? pnt : DRIFT; };

				default:
					throw new InvalidDataException("Unknown encoding type: " + type);
			}
		}
	}
}