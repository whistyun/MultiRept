using Microsoft.VisualStudio.TestTools.UnitTesting;
using MultiRept;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MultiRept.Tests
{
	[TestClass()]
	public class FileStoreTests
	{
		FileStore store = new FileStore();

		[TestMethod()]
		public void NewActTest()
		{
			FileStore store = new FileStore();
			Assert.IsFalse(store.HasStore);
			store.NewAct();
			Assert.IsTrue(store.HasStore);
		}

		[TestMethod()]
		public void DeleteActTest()
		{
			FileStore store = new FileStore();
			Assert.IsFalse(store.HasStore);
			store.NewAct();
			store.DeleteAct();
			Assert.IsFalse(store.HasStore);
		}

		[TestMethod()]
		public void InsertTest()
		{
			var targetFilePath = System.IO.Path.GetFullPath("DBTests.txt");

			FileStore store = new FileStore();
			PrivateObject po = new PrivateObject(store);

			store.NewAct();
			Assert.IsTrue(store.HasStore);
			Assert.AreEqual(0, store.SelectFileInfos().Count);
			store.Insert(targetFilePath, "hogehoge", (ThreadStart)delegate () { });
			Assert.AreEqual(1, store.SelectFileInfos().Count);
			store.Insert(targetFilePath, "fugafuga", (ThreadStart)delegate () { });
			Assert.AreEqual(2, store.SelectFileInfos().Count);
			try
			{
				store.Insert(targetFilePath, "fuga", (ThreadStart)delegate ()
				{
					throw new InvalidCastException();
				});
			}
			catch (Exception e)
			{
				Assert.IsTrue(e is InvalidCastException);
			}
			Assert.AreEqual(2, store.SelectFileInfos().Count);

			store.NewAct();
			Assert.AreEqual(0, store.SelectFileInfos().Count);
			store.Insert(targetFilePath, "hogehoge", (ThreadStart)delegate () { });
			Assert.AreEqual(1, store.SelectFileInfos().Count);

			store.DeleteAct();
			Assert.AreEqual(2, store.SelectFileInfos().Count);
		}
	}
}