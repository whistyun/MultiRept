using Microsoft.VisualStudio.TestTools.UnitTesting;
using MultiRept;
using System.IO;

namespace MultiRept.Tests
{
	[TestClass()]
	public class StreamReaderEnhanceTests
	{
		[TestMethod()]
		public void ReadLineTest()
		{
			var reader = new StringReader("a\r\nbc\n\nd\ref");

			string lb;
			string line;

			line=reader.ReadLine(out lb);
			Assert.AreEqual("a", line);
			Assert.AreEqual("\r\n", lb);

			line = reader.ReadLine(out lb);
			Assert.AreEqual("bc", line);
			Assert.AreEqual("\n", lb);

			line = reader.ReadLine(out lb);
			Assert.AreEqual("", line);
			Assert.AreEqual("\n", lb);

			line = reader.ReadLine(out lb);
			Assert.AreEqual("d", line);
			Assert.AreEqual("\r", lb);

			line = reader.ReadLine(out lb);
			Assert.AreEqual("ef", line);
			Assert.AreEqual(null, lb);
		}
	}
}