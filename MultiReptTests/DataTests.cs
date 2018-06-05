using Microsoft.VisualStudio.TestTools.UnitTesting;
using MultiRept;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiRept.Tests
{
	[TestClass()]
	public class ReplaceParameterTests
	{
		[TestMethod()]
		public void ReplaceParameterTest1()
		{
			var v = new ReplaceParameter("abc", "bef", FindMode.Plain);
			Assert.IsTrue(v.IsPlainMode);
			Assert.IsFalse(v.IsWordMode);
			Assert.IsFalse(v.IsRegexMode);
			Assert.AreEqual("abc", v.ReplaceFrom);
			Assert.AreEqual("bef", v.ReplaceTo);
		}

		[TestMethod()]
		public void ReplaceParameterTest2()
		{
			var v = new ReplaceParameter("abc", "bef", FindMode.Word);
			Assert.IsFalse(v.IsPlainMode);
			Assert.IsTrue(v.IsWordMode);
			Assert.IsFalse(v.IsRegexMode);
			Assert.AreEqual("abc", v.ReplaceFrom);
			Assert.IsTrue(v.ReplaceFromPattern.IsMatch(".abc."));
			Assert.IsTrue(v.ReplaceFromPattern.IsMatch("abc."));
			Assert.IsTrue(v.ReplaceFromPattern.IsMatch(".abc"));
			Assert.AreEqual("abc", v.ReplaceFromPattern.Match(".abc.").Groups[0].Value);
		}

		[TestMethod()]
		public void ReplaceParameterTest3()
		{
			var v = new ReplaceParameter("abc", "\\ab$1\\l$2b\\u${ab}\\U$abc\\L$20\\U${a23b}", FindMode.Regex);
			Assert.IsFalse(v.IsPlainMode);
			Assert.IsFalse(v.IsWordMode);
			Assert.IsTrue(v.IsRegexMode);
			Assert.AreEqual("abc", v.ReplaceFrom);

			var patternList = v.ReplaceToPattern;
			Assert.AreEqual(ReplaceToType.Plain, patternList[0].Type);
			Assert.AreEqual("\\ab", patternList[0].Label);

			Assert.AreEqual(ReplaceToType.GroupIndex, patternList[1].Type);
			Assert.AreEqual(ChangeCase.None, patternList[1].Change);
			Assert.AreEqual(1, patternList[1].Index);

			Assert.AreEqual(ReplaceToType.GroupIndex, patternList[2].Type);
			Assert.AreEqual(ChangeCase.LowerHead, patternList[2].Change);
			Assert.AreEqual(2, patternList[2].Index);

			Assert.AreEqual(ReplaceToType.Plain, patternList[3].Type);
			Assert.AreEqual("b", patternList[3].Label);

			Assert.AreEqual(ReplaceToType.GroupLabel, patternList[4].Type);
			Assert.AreEqual(ChangeCase.UpperHead, patternList[4].Change);
			Assert.AreEqual("ab", patternList[4].Label);

			Assert.AreEqual(ReplaceToType.Plain, patternList[5].Type);
			Assert.AreEqual("\\U$abc", patternList[5].Label);

			Assert.AreEqual(ReplaceToType.GroupIndex, patternList[6].Type);
			Assert.AreEqual(ChangeCase.LowerAll, patternList[6].Change);
			Assert.AreEqual(20, patternList[6].Index);

			Assert.AreEqual(ReplaceToType.GroupLabel, patternList[7].Type);
			Assert.AreEqual(ChangeCase.UpperAll, patternList[7].Change);
			Assert.AreEqual("a23b", patternList[7].Label);
		}
	}
}