using System.IO;
using System.Linq;
using System.Collections.Generic;
using EncodeDetector.Model;
using System;
using System.Text;

namespace EncodeDetector
{
	public class CodeDetector
	{

		private int miniBomLen;
		private List<EncodeModel> encodeMaster = new List<EncodeModel>();

		public CodeDetector()
		{
			encodeMaster.Add(new Sjis());
			encodeMaster.Add(new EucJP());
			encodeMaster.Add(new Utf8());

			foreach (EncodeModel mode in encodeMaster)
			{
				miniBomLen = (mode.Bom ?? new byte[0]).Length;
			}
		}

		public Encoding Check(FileInfo finfo, long limit = long.MaxValue)
		{
			int LEN = Math.Max(1024 * 10, miniBomLen);
			// 候補のエンコード一覧
			var predicts = encodeMaster.Select(m => new ModeIndex(m)).ToList();

			// BOM判定用の先頭バイト
			byte[] topByte;

			int buffeEndIndex;

			byte[] buffer = new byte[LEN];
			// 最終的に読み込んだバイト数
			int totalBufferLength = 0;

			using (System.IO.FileStream fs = finfo.OpenRead())
			{
				// 初回の読み込み
				buffeEndIndex = fs.Read(buffer, 0, LEN);

				if (buffeEndIndex > 0)
				{
					// 先頭の数バイトはBOMチェックに使用する
					topByte = new byte[buffeEndIndex];
					Array.Copy(buffer, topByte, buffeEndIndex);

					while (buffeEndIndex > 0)
					{
						var bufferOffsetLimit = buffeEndIndex;
						var consumeIndex = Int32.MaxValue;

						for (int i = predicts.Count - 1; i >= 0; i--)
						{
							var predict = predicts[i];
							var model = predict.Model;
							var scoreAppend = model.Check(buffer, ref predict.Index, bufferOffsetLimit);

							if (scoreAppend < 0)
							{
								// 不採用
								predicts.RemoveAt(i);

								// 集約したら処理を打ち切る
								if (predicts.Count == 1)
								{
									return checkBom(predicts[0].Model, topByte);
								}
							}
							else
							{
								predict.Score += scoreAppend;
								consumeIndex = Math.Min(consumeIndex, predict.Index);
							}
						}

						// すでに消費済みの分は削除
						if (consumeIndex != 0 && consumeIndex != Int32.MaxValue)
						{
							Array.Copy(buffer, consumeIndex, buffer, 0, buffer.Length - consumeIndex);
							foreach (var predict in predicts)
							{
								predict.Index -= consumeIndex;
							}

							// 削除した分を読み込む
							buffeEndIndex -= consumeIndex;
							buffeEndIndex += fs.Read(buffer,
								buffeEndIndex,
								buffer.Length - buffeEndIndex);
						}

						totalBufferLength += consumeIndex;

						if (totalBufferLength >= limit) break;
					}

					//集約後、優先度・一致文字数でソート
					var encoding = (from a in predicts
										 orderby a.Priority, a.Score descending
										 select a.Model).First();

					return checkBom(encoding, topByte);
				}
				else
				{
					//ファイルが空
					return null;
				}

			}
		}

		private Encoding checkBom(EncodeModel model, byte[] topBytes)
		{
			var bom = model.Bom;
			if (bom == null
				|| bom.Length == 0
				|| bom.Length >= topBytes.Length)
			{
				return model.Encoding;
			}
			else
			{
				//BOMチェック
				for (int i = 0; i < bom.Length; ++i)
				{
					if (bom[i] != topBytes[i])
					{
						return model.Encoding;
					}
				}

				return model.EncodingWithBom;
			}
		}
	}

	internal class ModeIndex
	{
		public EncodeModel Model { private set; get; }
		public int Index;
		public int Priority { private set; get; }
		public int Score;

		public ModeIndex(EncodeModel model) : this(model, 1000)
		{
		}

		public ModeIndex(EncodeModel model, int priority)
		{
			Model = model;
			Index = 0;
			Score = 0;
		}
	}
}