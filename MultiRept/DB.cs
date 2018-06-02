using System;
using System.Collections.Generic;
using System.Data.Linq;
using System.Data.Linq.Mapping;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using System.Diagnostics;
using System.Runtime.Serialization.Json;
using System.Reflection;

namespace MultiRept
{
	public class FileStore : IDisposable
	{
		DirectoryInfo root;

		int actNo;
		FileInfo actFile;
		DirectoryInfo actDir;
		StreamWriter actFileWriter;

		List<ReplacedFile> replaceList;

		public FileStore(string prefix = "MultiRept")
		{
			var pid = Process.GetCurrentProcess().Id;

			do
			{
				var stamp = DateTime.UtcNow.ToString("yyyyMMdd.HHmmss.fff");
				var folderName = Path.Combine(Path.GetTempPath(), prefix + pid + "_" + stamp);

				if (!Directory.Exists(folderName))
				{
					root = Directory.CreateDirectory(folderName);
					break;
				}
			} while (true);
		}

		public void Dispose()
		{
			if (this.actFileWriter != null)
			{
				actFileWriter.Close();
			}

			foreach (var file in root.GetFiles())
			{
				file.Delete();
			}

			foreach (var dir in root.GetDirectories())
			{
				Util.DeleteDir(dir.FullName);
			}

			actNo = 0;
			actDir = null;
			replaceList = null;
		}

		public void NewAct()
		{
			if (this.actFileWriter != null)
			{
				actFileWriter.Close();
			}

			this.actNo = actNo + 1;

			this.actDir = root.CreateSubdirectory(actNo.ToString());
			this.actFile = new FileInfo(Path.Combine(root.FullName, actNo.ToString() + ".info"));

			this.replaceList = new List<ReplacedFile>();

			this.actFileWriter = new StreamWriter(
				actFile.OpenWrite(),
				new UTF8Encoding(),
				1024 * 5);
		}

		public void DeleteAct()
		{
			if (this.actFileWriter != null)
			{
				actFileWriter.Close();
			}

			var actDir = Path.Combine(root.FullName, actNo.ToString());
			var actInfo = Path.Combine(root.FullName, actNo.ToString() + ".info");
			Util.DeleteDir(actDir);
			File.Delete(actInfo);

			this.actNo = actNo - 1;

			if (HasStore)
			{
				this.actDir = new DirectoryInfo(Path.Combine(root.FullName, actNo.ToString()));
				this.actFile = new FileInfo(Path.Combine(root.FullName, actNo.ToString() + ".info"));

				var replaceListTxt = File.ReadAllText(this.actFile.FullName, new UTF8Encoding());
				if (replaceListTxt.Length != 0)
				{
					this.replaceList = replaceListTxt.Split('\n').Select(s => ReplacedFile.ValueOf(s)).ToList();
				}
				else
				{
					this.replaceList = new List<ReplacedFile>();
				}

				this.actFileWriter = new StreamWriter(
					new FileStream(this.actFile.FullName, FileMode.Append),
					new UTF8Encoding(),
					1024 * 5);
			}
		}

		public bool HasStore
		{
			get { return actNo > 0; }
		}

		public void Insert(string filepath, string hash, Delegate invoker, params object[] param)
		{
			var key = new ReplacedFile()
			{
				ActNo = actNo,
				Id = replaceList.Count,
				FilePath = filepath,
				ReplacedFileHash = hash
			};
			var entityPath = Path.Combine(actDir.FullName, key.Id.ToString());

			replaceList.Add(key);
			File.Copy(filepath, entityPath);

			try
			{
				invoker.DynamicInvoke(param);
				if (replaceList.Count > 1)
				{
					actFileWriter.Write('\n');
				}
				actFileWriter.Write(key.ToString());
				actFileWriter.Flush();
			}
			catch (TargetInvocationException e)
			{
				replaceList.RemoveAt(replaceList.Count - 1);
				File.Delete(entityPath);
				throw e.InnerException;
			}
			catch (Exception e)
			{
				replaceList.RemoveAt(replaceList.Count - 1);
				File.Delete(entityPath);
				throw e;
			}

		}

		public List<ReplacedFile> SelectFileInfos()
		{
			return new List<ReplacedFile>(replaceList);
		}

		public FileInfo Select(ReplacedFile id, FileInfo outTo = null)
		{
			var output = outTo ?? new FileInfo(id.FilePath);

			var actDir = Path.Combine(root.FullName, id.ActNo.ToString());
			var targetFile = Path.Combine(actDir, id.Id.ToString());
			File.Delete(output.FullName);
			File.Copy(targetFile, output.FullName);

			return output;
		}
	}

	public class ReplacedFile
	{
		public int Id { set; get; }

		public int ActNo { set; get; }

		public string FilePath { set; get; }

		public string ReplacedFileHash { set; get; }

		public static ReplacedFile ValueOf(String text)
		{
			int swt = 0;
			int id = -1;
			int actNo = -1;
			string filepath = null;
			string hash = null;

			var buffer = new StringBuilder();
			var escape = false;
			foreach (var ch in text)
			{
				if (escape)
				{
					escape = false;
					buffer.Append(ch);
				}
				else if (ch == '\\')
				{
					escape = true;
				}
				else if (ch == ',')
				{
					switch (swt++)
					{
						case 0:
							id = Int32.Parse(buffer.ToString());
							break;
						case 1:
							actNo = Int32.Parse(buffer.ToString());
							break;
						case 2:
							filepath = buffer.ToString();
							break;
						case 3:
							hash = buffer.ToString();
							break;
						default:
							throw new InvalidDataException();
					}
					buffer.Clear();
				}
				else
				{
					buffer.Append(ch);
				}
			}

			if (buffer.Length > 0)
			{
				switch (swt++)
				{
					case 0:
						id = Int32.Parse(buffer.ToString());
						break;
					case 1:
						actNo = Int32.Parse(buffer.ToString());
						break;
					case 2:
						filepath = buffer.ToString();
						break;
					case 3:
						hash = buffer.ToString();
						break;
					default:
						throw new InvalidDataException();
				}
			}

			if (hash == null)
			{
				throw new InvalidDataException();
			}

			return new ReplacedFile()
			{
				Id = id,
				ActNo = actNo,
				FilePath = filepath,
				ReplacedFileHash = hash
			};
		}

		public override string ToString()
		{
			return
				Id + "," +
				ActNo + "," +
				FilePath.Replace("\\", "\\\\").Replace(",", "\\,") + "," +
				ReplacedFileHash.Replace("\\", "\\\\").Replace(",", "\\,");
		}
	}
}
