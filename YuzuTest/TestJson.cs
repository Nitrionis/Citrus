﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Yuzu;
using Yuzu.Json;
using YuzuTestAssembly;
using YuzuGen.YuzuTest;

namespace YuzuTest.Json
{
	[TestClass]
	public class TestJson
	{
		[TestMethod]
		public void TestSimple()
		{
			var js = new JsonSerializer();
			js.Options.AllowEmptyTypes = true;
			Assert.AreEqual("{\n}", js.ToString(new Empty()));

			var v1 = new Sample1 { X = 345, Y = "test" };
			js.JsonOptions.Indent = "";

			var result = js.ToString(v1);
			Assert.AreEqual("{\n\"X\":345,\n\"Y\":\"test\"\n}", result);
			Sample1 v2 = new Sample1();

			var jd = new JsonDeserializer();
			jd.FromString(v2, result);
			Assert.AreEqual(v1.X, v2.X);
			Assert.AreEqual(v1.Y, v2.Y);

			jd.FromString(v2, "{\"X\":999}");
			Assert.AreEqual(999, v2.X);
			Assert.AreEqual(v1.Y, v2.Y);

			v1.X = int.MaxValue;
			jd.FromString(v2, js.ToString(v1));
			Assert.AreEqual(v1.X, v2.X);

			v1.X = int.MinValue;
			jd.FromString(v2, js.ToString(v1));
			Assert.AreEqual(v1.X, v2.X);
		}

		[TestMethod]
		public void TestUnordered()
		{
			var jd = new JsonDeserializer();
			jd.JsonOptions.Unordered = true;
			var v = jd.FromString<Sample1>("{\n\"Y\":\"test\",\n\"X\":345\n}");
			Assert.AreEqual(345, v.X);
			Assert.AreEqual("test", v.Y);
		}

		[TestMethod]
		public void TestSimpleProps()
		{
			var js = new JsonSerializer();

			var v1 = new Sample2 { X = 345, Y = "test" };
			js.JsonOptions.Indent = "";

			var result = js.ToString(v1);
			Assert.AreEqual("{\n\"X\":345,\n\"Y\":\"test\"\n}", result);
			var v2 = new Sample2();

			var jd = new JsonDeserializer();
			jd.FromString(v2, result);
			Assert.AreEqual(v1.X, v2.X);
			Assert.AreEqual(v1.Y, v2.Y);

			jd.FromString(v2, "{\"X\":999}");
			Assert.AreEqual(999, v2.X);
			Assert.AreEqual(v1.Y, v2.Y);
		}

		[TestMethod]
		public void TestLong()
		{
			var js = new JsonSerializer();
			var v1 = new SampleLong { S = -1L << 33, U = 1UL << 33 };

			js.JsonOptions.Indent = "";
			var result = js.ToString(v1);
			Assert.AreEqual("{\n\"S\":-8589934592,\n\"U\":8589934592\n}", result);

			var v2 = new SampleLong();
			var jd = new JsonDeserializer();
			jd.FromString(v2, result);
			Assert.AreEqual(v1.S, v2.S);
			Assert.AreEqual(v1.U, v2.U);

			js.JsonOptions.Int64AsString = true;
			var result1 = js.ToString(v1);
			Assert.AreEqual("{\n\"S\":\"-8589934592\",\n\"U\":\"8589934592\"\n}", result1);
			var jd1 = new JsonDeserializer();
			jd1.JsonOptions.Int64AsString = true;
			jd1.FromString(v2, result1);
			Assert.AreEqual(v1.S, v2.S);
			Assert.AreEqual(v1.U, v2.U);

			js.JsonOptions.Int64AsString = false;
			v1.S = long.MinValue;
			v1.U = ulong.MaxValue;
			jd.FromString(v2, js.ToString(v1));
			Assert.AreEqual(v1.S, v2.S);
			Assert.AreEqual(v1.U, v2.U);
		}

		[TestMethod]
		public void TestSmallTypes()
		{
			var js = new JsonSerializer();
			var v1 = new SampleSmallTypes { Ch = 'A', Sh = -2000, USh = 2001, B = 198, Sb = -109 };

			js.JsonOptions.Indent = "";
			var result = js.ToString(v1);
			Assert.AreEqual("{\n\"B\":198,\n\"Ch\":\"A\",\n\"Sb\":-109,\n\"Sh\":-2000,\n\"USh\":2001\n}", result);

			var v2 = new SampleSmallTypes();
			var jd = new JsonDeserializer();
			jd.FromString(v2, result);
			Assert.AreEqual(v1.Ch, v2.Ch);
			Assert.AreEqual(v1.USh, v2.USh);
			Assert.AreEqual(v1.Sh, v2.Sh);
			Assert.AreEqual(v1.B, v2.B);
			Assert.AreEqual(v1.Sb, v2.Sb);

			v2 = (SampleSmallTypes)SampleSmallTypes_JsonDeserializer.Instance.FromString(result);
			Assert.AreEqual(v1.Ch, v2.Ch);
			Assert.AreEqual(v1.USh, v2.USh);
			Assert.AreEqual(v1.Sh, v2.Sh);
			Assert.AreEqual(v1.B, v2.B);
			Assert.AreEqual(v1.Sb, v2.Sb);

			XAssert.Throws<YuzuException>(() => jd.FromString(v2, result.Replace("A", "ABC")), "ABC");
			XAssert.Throws<OverflowException>(() => jd.FromString(v2, result.Replace("198", "298")));
			XAssert.Throws<OverflowException>(() => jd.FromString(v2, result.Replace("109", "209")));
			XAssert.Throws<OverflowException>(() => jd.FromString(v2, result.Replace("2000", "40000")));
			XAssert.Throws<OverflowException>(() => jd.FromString(v2, result.Replace("2001", "200000")));

			jd.FromString(v2, "{\n\"B\":255,\n\"Ch\":\"Z\",\n\"Sb\":-128,\n\"Sh\":-32768,\n\"USh\":32767\n}");
			Assert.AreEqual('Z', v2.Ch);
			Assert.AreEqual(32767, v2.USh);
			Assert.AreEqual(-32768, v2.Sh);
			Assert.AreEqual(255, v2.B);
			Assert.AreEqual(-128, v2.Sb);
		}

		[TestMethod]
		public void TestNested()
		{
			var js = new JsonSerializer();
			js.Options.TagMode = TagMode.Names;

			var v = new Sample3 {
				S1 = new Sample1 { X = 345, Y = "test" },
				F = 222,
				S2 = new Sample2 { X = -346, Y = "test1" },
			};
			js.JsonOptions.Indent = "";

			var result = js.ToString(v);
			Assert.AreEqual(
				"{\n\"S1\":" +
				"{\n\"X\":345,\n\"Y\":\"test\"\n},\n" +
				"\"F\":222,\n" +
				"\"S2\":" +
				"{\n\"X\":-346,\n\"Y\":\"test1\"\n}\n" +
				"}",
				result);

			var jd = new JsonDeserializer();
			jd.Options.TagMode = TagMode.Names;
			var w = new Sample3();
			jd.FromString(w, result);
			Assert.AreEqual(v.S1.X, w.S1.X);
			Assert.AreEqual(v.S1.Y, w.S1.Y);
			Assert.AreEqual(v.F, w.F);
			Assert.AreEqual(v.S2.X, w.S2.X);
			Assert.AreEqual(v.S2.Y, w.S2.Y);
		}

		[TestMethod]
		public void TestGenerated()
		{
			const string str =
				"{\n\"S1\":" +
				"{\n\"X\":345,\n\"Y\":\"test\"\n},\n" +
				"\"F\":222,\n" +
				"\"S2\":" +
				"{\n\"X\":-346,\n\"Y\":\"test1\"\n}\n" +
				"}";

			var jd = new Sample3_JsonDeserializer();
			var w = (Sample3)jd.FromString(str);
			Assert.AreEqual(345, w.S1.X);
			Assert.AreEqual("test", w.S1.Y);
			Assert.AreEqual(222, w.F);
			Assert.AreEqual(-346, w.S2.X);
			Assert.AreEqual("test1", w.S2.Y);

			var jdg = new JsonDeserializerGenerator();
			jdg.Assembly = GetType().Assembly;

			var w1 = new Sample1();
			jdg.FromString(w1, "{\"X\":88}");
			Assert.IsInstanceOfType(w1, typeof(Sample1));
			Assert.AreEqual(88, w1.X);

			var w2 = jdg.FromString("{\"class\":\"YuzuTest.Sample1, YuzuTest\",\"X\":99}");
			Assert.IsInstanceOfType(w2, typeof(Sample1));
			Assert.AreEqual(99, ((Sample1)w2).X);

			var w3 = new SampleMemberI();
			jdg.FromString(w3, "{\"class\":\"YuzuTest.SampleMemberI, YuzuTest\"}");
			Assert.AreEqual(71, ((SampleMemberI)w3).X);
		}

		[TestMethod]
		public void TestEnum()
		{
			var js = new JsonSerializer();

			var v = new Sample4 { E = SampleEnum.E3 };
			js.JsonOptions.Indent = "";

			var result1 = js.ToString(v);
			Assert.AreEqual("{\n\"E\":2\n}", result1);

			js.JsonOptions.EnumAsString = true;
			var result2 = js.ToString(v);
			Assert.AreEqual("{\n\"E\":\"E3\"\n}", result2);

			var jd = new JsonDeserializer();
			var w = new Sample4();
			jd.FromString(w, result1);
			Assert.AreEqual(SampleEnum.E3, w.E);

			w.E = SampleEnum.E1;
			jd.JsonOptions.EnumAsString = true;
			jd.FromString(w, result2);
			Assert.AreEqual(SampleEnum.E3, w.E);

			w = (Sample4)Sample4_JsonDeserializer.Instance.FromString(result2);
			Assert.AreEqual(SampleEnum.E3, w.E);
		}

		[TestMethod]
		public void TestBool()
		{
			var js = new JsonSerializer();

			var v = new SampleBool { B = true };
			js.JsonOptions.Indent = "";

			var result1 = js.ToString(v);
			Assert.AreEqual("{\n\"B\":true\n}", result1);

			var jd = new JsonDeserializer();
			var w = new SampleBool();
			jd.FromString(w, result1);
			Assert.AreEqual(true, w.B);

			w = (SampleBool)SampleBool_JsonDeserializer.Instance.FromString(result1);
			Assert.AreEqual(true, w.B);
		}

		[TestMethod]
		public void TestFloat()
		{
			var js = new JsonSerializer();
			js.Options.TagMode = TagMode.Names;

			var v = new SampleFloat { F = 1e-20f, D = -3.1415e100d };
			js.JsonOptions.Indent = "";

			var result1 = js.ToString(v);
			Assert.AreEqual("{\n\"F\":1E-20,\n\"D\":-3.1415E+100\n}", result1);

			var w = new SampleFloat();
			var jd = new JsonDeserializer();
			jd.Options.TagMode = TagMode.Names;
			jd.FromString(w, result1);
			Assert.AreEqual(v.F, w.F);
			Assert.AreEqual(v.D, w.D);
		}

		[TestMethod]
		public void TestDecimal()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";

			var v = new SampleDecimal { N = -12.34m };

			var jd = new JsonDeserializer();

			var result1 = js.ToString(v);
			Assert.AreEqual("{\n\"N\":-12.34\n}", result1);
			var w1 = new SampleDecimal();
			jd.FromString(w1, result1);
			Assert.AreEqual(v.N, w1.N);

			jd.JsonOptions.DecimalAsString = js.JsonOptions.DecimalAsString = true;
			var result2 = js.ToString(v);
			Assert.AreEqual("{\n\"N\":\"-12.34\"\n}", result2);
			var w2 = new SampleDecimal();
			jd.FromString(w2, result2);
			Assert.AreEqual(v.N, w2.N);

			var w3 = (SampleDecimal)SampleDecimal_JsonDeserializer.Instance.FromString(result1);
			Assert.AreEqual(v.N, w3.N);
		}

		[TestMethod]
		public void TestNullable()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.JsonOptions.FieldSeparator = "";
			var jd = new JsonDeserializer();

			var v1 = new SampleNullable { N = null };
			var result1 = js.ToString(v1);
			Assert.AreEqual("{\"N\":null}", result1);
			var w1 = jd.FromString<SampleNullable>(result1);
			Assert.AreEqual(v1.N, w1.N);
			var w1g = (SampleNullable)SampleNullable_JsonDeserializer.Instance.FromString(result1);
			Assert.AreEqual(v1.N, w1g.N);

			var v2 = new SampleNullable { N = 997 };
			var result2 = js.ToString(v2);
			Assert.AreEqual("{\"N\":997}", result2);
			var w2 = jd.FromString<SampleNullable>(result2);
			Assert.AreEqual(v2.N, w2.N);
			var w2g = (SampleNullable)SampleNullable_JsonDeserializer.Instance.FromString(result2);
			Assert.AreEqual(v2.N, w2g.N);
		}

		[TestMethod]
		public void TestMemberOrder()
		{
			var js = new JsonSerializer();
			js.Options.TagMode = TagMode.Names;
			js.JsonOptions.Indent = "";
			var result = js.ToString(new SampleMethodOrder());
			Assert.AreEqual("{\n\"F1\":0,\n\"P1\":0,\n\"F2\":0,\n\"P2\":0\n}", result);
		}

		[TestMethod]
		public void TestClassNames()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.JsonOptions.SaveRootClass = true;
			js.Options.TagMode = TagMode.Names;
			Assert.AreEqual(
				"{\n\"class\":\"YuzuTest.SampleBase, YuzuTest\",\n\"FBase\":0\n}", js.ToString(new SampleBase()));
			Assert.AreEqual(
				"{\n\"class\":\"YuzuTest.SampleDerivedA, YuzuTest\",\n\"FBase\":0,\n\"FA\":0\n}",
				js.ToString(new SampleDerivedA()));

			var jd = new JsonDeserializer();
			jd.Options.TagMode = TagMode.Names;
			var v = jd.FromString(
				"{\n\"class\":\"YuzuTest.SampleDerivedB, YuzuTest\",\n\"FBase\":3,\n\"FB\":7\n}");
			Assert.IsInstanceOfType(v, typeof(SampleDerivedB));
			var b = (SampleDerivedB)v;
			Assert.AreEqual(3, b.FBase);
			Assert.AreEqual(7, b.FB);
		}

		[TestMethod]
		public void TestList()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.Options.TagMode = TagMode.Names;
			var jd = new JsonDeserializer();
			jd.Options.TagMode = TagMode.Names;

			var v0 = new SampleList { E = new List<string> { "a", "b", "c" } };
			var result0 = js.ToString(v0);
			Assert.AreEqual("{\n\"E\":[\n\"a\",\n\"b\",\n\"c\"\n]\n}", result0);
			var w0 = new SampleList();
			jd.FromString(w0, result0);
			CollectionAssert.AreEqual(v0.E, w0.E);

			var v1 = new SampleTree { Value = 11, Children = new List<SampleTree>() };
			Assert.AreEqual("{\n\"Value\":11,\n\"Children\":[]\n}", js.ToString(v1));
			var w1 = new SampleTree();
			jd.FromString(w1, js.ToString(v1));
			Assert.AreEqual(0, w1.Children.Count);

			var v2 = new SampleTree {
				Value = 11,
				Children = new List<SampleTree> {
					new SampleTree {
						Value = 12,
						Children = new List<SampleTree>(),
					},
					new SampleTree {
						Value = 13,
					}
				}
			};
			var result2 = js.ToString(v2);
			Assert.AreEqual(
				"{\n\"Value\":11,\n\"Children\":[\n" +
				"{\n\"Value\":12,\n\"Children\":[]\n},\n" +
				"{\n\"Value\":13,\n\"Children\":null\n}\n" +
				"]\n}",
				result2);
			SampleTree w2 = new SampleTree();
			jd.FromString(w2, result2);
			Assert.AreEqual(v2.Value, w2.Value);
			Assert.AreEqual(v2.Children.Count, w2.Children.Count);
			Assert.AreEqual(v2.Children[0].Value, w2.Children[0].Value);
			Assert.AreEqual(v2.Children[1].Children, w2.Children[1].Children);
		}

		[TestMethod]
		public void TestCollection()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			var jd = new JsonDeserializer();

			var v0 = new SampleWithCollection();
			v0.A.Add(new SampleInterfaced { X = 9 });
			v0.B.Add(7);
			v0.B.Add(6);
			var result0 = js.ToString(v0);
			Assert.AreEqual(
				"{\n" +
					"\"A\":[\n{\n\"class\":\"YuzuTest.SampleInterfaced, YuzuTest\",\n\"X\":9\n}\n],\n" +
					"\"B\":[\n7,\n6\n]\n" +
				"}",
				result0);

			var w0 = new SampleWithCollection();
			jd.FromString(w0, result0);
			Assert.AreEqual(1, w0.A.Count);
			Assert.IsInstanceOfType(w0.A.First(), typeof(SampleInterfaced));
			Assert.AreEqual(9, w0.A.First().X);
			CollectionAssert.AreEqual(new int[] { 7, 6 }, w0.B.ToList());

			var w1 = (SampleWithCollection)SampleWithCollection_JsonDeserializer.Instance.FromString(result0);
			Assert.AreEqual(1, w1.A.Count);
			Assert.IsInstanceOfType(w1.A.First(), typeof(SampleInterfaced));
			Assert.AreEqual(9, w1.A.First().X);
			CollectionAssert.AreEqual(new int[] { 7, 6 }, w1.B.ToList());

			var v2 = new SampleCollection<int> { 2, 5, 4 };
			var result1 = js.ToString(v2);
			Assert.AreEqual("[\n2,\n5,\n4\n]", result1);
			var w2 = (SampleCollection<int>)SampleCollection_Int32_JsonDeserializer.Instance.FromString(result1);
			CollectionAssert.AreEqual(v2.ToList(), w2.ToList());
			var w2g = (SampleExplicitCollection<int>)
				SampleExplicitCollection_Int32_JsonDeserializer.Instance.FromString(result1);
			CollectionAssert.AreEqual(v2.ToList(), w2g.ToList());

			var v3 = new SampleConcreteCollection { 8, 3, 1 };
			var result3 = js.ToString(v3);
			Assert.AreEqual("[\n8,\n3,\n1\n]", result3);
			var w3 = new SampleConcreteCollection();
			jd.FromString(w3, result3);
			CollectionAssert.AreEqual(v3.ToList(), w3.ToList());
			var w3g = (SampleConcreteCollection)
				SampleConcreteCollection_JsonDeserializer.Instance.FromString(result3);
			CollectionAssert.AreEqual(v3.ToList(), w3g.ToList());
		}

		[TestMethod]
		public void TestTopLevelList()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			var jd = new JsonDeserializer();

			var v0 = new List<string> { "a", "b", "c" };
			var result0 = js.ToString(v0);
			Assert.AreEqual("[\n\"a\",\n\"b\",\n\"c\"\n]", result0);

			var w0 = new List<string>();
			jd.FromString(w0, result0);
			CollectionAssert.AreEqual(v0, w0);
			jd.FromString(w0, "[]");
			CollectionAssert.AreEqual(v0, w0);
			jd.FromString(w0, result0);
			CollectionAssert.AreEqual(new List<string> { "a", "b", "c", "a", "b", "c" }, w0);

			var v1 = new List<List<int>> { new List<int> { 1, 2 }, new List<int> { 3 } };
			var result1 = js.ToString(v1);
			Assert.AreEqual("[\n[\n1,\n2\n],\n[\n3\n]\n]", result1);
			List<List<int>> w1 = new List<List<int>>();
			YuzuGen.System.Collections.Generic.List_List_Int32_JsonDeserializer.Instance.FromString(w1, result1);
			Assert.AreEqual(v1.Count, w1.Count);
			CollectionAssert.AreEqual(v1[0], w1[0]);
			CollectionAssert.AreEqual(v1[1], w1[1]);
		}

		[TestMethod]
		public void TestTopLevelDict()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			var jd = new JsonDeserializer();

			var v0 = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
			var result0 = js.ToString(v0);
			Assert.AreEqual("{\n\"a\":1,\n\"b\":2\n}", result0);

			var w0 = new Dictionary<string, int>();
			jd.FromString(w0, result0);
			CollectionAssert.AreEqual(v0, w0);
			jd.FromString(w0, "{}");
			CollectionAssert.AreEqual(v0, w0);
			jd.FromString(w0, "{\"c\":3}");
			CollectionAssert.AreEqual(
				new Dictionary<string, int> { { "a", 1 }, { "b", 2 }, { "c", 3 } }, w0);
		}

		[TestMethod]
		public void TestDictionary()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.Options.TagMode = TagMode.Names;
			var jd = new JsonDeserializer();
			jd.Options.TagMode = TagMode.Names;

			var v0 = new SampleDict {
				Value = 3, Children = new Dictionary<string, SampleDict> {
				{ "a", new SampleDict { Value = 5, Children = new Dictionary<string, SampleDict>() } },
				{ "b", new SampleDict { Value = 7 } },
			}
			};
			var result0 = js.ToString(v0);
			Assert.AreEqual(
				"{\n\"Value\":3,\n\"Children\":{\n" +
				"\"a\":{\n\"Value\":5,\n\"Children\":{}\n},\n" +
				"\"b\":{\n\"Value\":7,\n\"Children\":null\n}\n" +
				"}\n}", result0);

			var w0 = new SampleDict();
			jd.FromString(w0, result0);
			Assert.AreEqual(v0.Value, w0.Value);
			Assert.AreEqual(v0.Children.Count, w0.Children.Count);
			Assert.AreEqual(v0.Children["a"].Value, w0.Children["a"].Value);

			var w1 = (SampleDict)SampleDict_JsonDeserializer.Instance.FromString(result0);
			Assert.AreEqual(v0.Value, w1.Value);
			Assert.AreEqual(v0.Children.Count, w1.Children.Count);
			Assert.AreEqual(v0.Children["a"].Value, w1.Children["a"].Value);
		}

		[TestMethod]
		public void TestDictionaryKeys()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.JsonOptions.FieldSeparator = "";

			var v0 = new SampleDictKeys {
				I =  new Dictionary<int, int> { { 5, 7 } },
				E =  new Dictionary<SampleEnum, int> { { SampleEnum.E2, 8 } },
				K =  new Dictionary<SampleKey, int> { { new SampleKey { V = 3 }, 9 } },
			};
			var result0 = js.ToString(v0);
			Assert.AreEqual(
				"{" +
				"\"E\":{\"E2\":8}," +
				"\"I\":{\"5\":7}," +
				"\"K\":{\"3!\":9}" +
				"}", result0);

			JsonDeserializer.RegisterKeyParser(
				typeof(SampleKey),
				s => new SampleKey { V = int.Parse(s.Substring(0, s.Length - 1)) });

			var jd = new JsonDeserializer();
			var w = new SampleDictKeys();
			jd.FromString(w, result0);
			Assert.AreEqual(1, w.I.Count);
			Assert.AreEqual(7, w.I[5]);
			Assert.AreEqual(1, w.E.Count);
			Assert.AreEqual(8, w.E[SampleEnum.E2]);
			Assert.AreEqual(1, w.K.Count);
			Assert.AreEqual(9, w.K[new SampleKey { V = 3 }]);

			w = (SampleDictKeys)SampleDictKeys_JsonDeserializer.Instance.FromString(result0);
			Assert.AreEqual(1, w.I.Count);
			Assert.AreEqual(7, w.I[5]);
			Assert.AreEqual(1, w.E.Count);
			Assert.AreEqual(8, w.E[SampleEnum.E2]);
			Assert.AreEqual(1, w.K.Count);
			Assert.AreEqual(9, w.K[new SampleKey { V = 3 }]);
		}

		[TestMethod]
		public void TestArray()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			var jd = new JsonDeserializer();

			var v0 = new SampleArray { A = new string[] { "a", "b", "c" } };
			var result0 = js.ToString(v0);
			Assert.AreEqual("{\n\"A\":[\n\"a\",\n\"b\",\n\"c\"\n]\n}", result0);
			var w0 = new SampleArray();
			jd.FromString(w0, result0);
			CollectionAssert.AreEqual(v0.A, w0.A);

			// Generated deserializer uses array prefix.
			var w1 = (SampleArray)SampleArray_JsonDeserializer.Instance.FromString(
				"{\n\"A\":[\n3,\n\"a\",\n\"b\",\n\"c\"\n]\n}");
			CollectionAssert.AreEqual(v0.A, w1.A);

			var v2 = new SampleArray();
			var result2 = js.ToString(v2);
			Assert.AreEqual("{\n\"A\":null\n}", result2);
			var w2 = (SampleArray)SampleArray_JsonDeserializer.Instance.FromString(result2);
			CollectionAssert.AreEqual(v2.A, w2.A);
			jd.FromString(w2, result2);
			CollectionAssert.AreEqual(v2.A, w2.A);
		}

		[TestMethod]
		public void TestClassList()
		{
			var js = new JsonSerializer();
			js.Options.TagMode = TagMode.Names;
			js.JsonOptions.SaveRootClass = true;
			var jd = new JsonDeserializer();
			jd.Options.TagMode = TagMode.Names;

			var v = new SampleClassList {
				E = new List<SampleBase> {
					new SampleDerivedA(),
					new SampleDerivedB { FB = 9 },
					new SampleDerivedB { FB = 8 },
				}
			};

			var result = js.ToString(v);
			var w = (SampleClassList)jd.FromString(result);

			Assert.AreEqual(3, w.E.Count);
			Assert.IsInstanceOfType(w.E[0], typeof(SampleDerivedA));
			Assert.IsInstanceOfType(w.E[1], typeof(SampleDerivedB));
			Assert.AreEqual(9, ((SampleDerivedB)w.E[1]).FB);
			Assert.IsInstanceOfType(w.E[2], typeof(SampleDerivedB));
			Assert.AreEqual(8, ((SampleDerivedB)w.E[2]).FB);

			w = (SampleClassList)SampleClassList_JsonDeserializer.Instance.FromString(result);
			Assert.AreEqual(3, w.E.Count);
			Assert.IsInstanceOfType(w.E[0], typeof(SampleDerivedA));
			Assert.IsInstanceOfType(w.E[1], typeof(SampleDerivedB));
			Assert.AreEqual(9, ((SampleDerivedB)w.E[1]).FB);
			Assert.IsInstanceOfType(w.E[2], typeof(SampleDerivedB));
			Assert.AreEqual(8, ((SampleDerivedB)w.E[2]).FB);
		}

		[TestMethod]
		public void TestMatrix()
		{
			var src = "{\"M\":[[1,2,3],[4,5],[6],[]]}";
			var v = new SampleMatrix();
			(new JsonDeserializer()).FromString(v, src);
			Assert.AreEqual(4, v.M.Count);
			CollectionAssert.AreEqual(new int[] { 1, 2, 3 }, v.M[0]);
			CollectionAssert.AreEqual(new int[] { 4, 5 }, v.M[1]);
			CollectionAssert.AreEqual(new int[] { 6 }, v.M[2]);
			Assert.AreEqual(0, v.M[3].Count);

			v = (SampleMatrix)SampleMatrix_JsonDeserializer.Instance.FromString(src);
			Assert.AreEqual(4, v.M.Count);
			CollectionAssert.AreEqual(new int[] { 1, 2, 3 }, v.M[0]);
			CollectionAssert.AreEqual(new int[] { 4, 5 }, v.M[1]);
			CollectionAssert.AreEqual(new int[] { 6 }, v.M[2]);
			Assert.AreEqual(0, v.M[3].Count);

			var js = new JsonSerializer();
			js.JsonOptions.FieldSeparator = "";
			js.JsonOptions.Indent = "";
			Assert.AreEqual(src, js.ToString(v));
		}

		private void CheckSampleRect(SampleRect expected, SampleRect actual)
		{
			Assert.AreEqual(expected.A.X, actual.A.X);
			Assert.AreEqual(expected.A.Y, actual.A.Y);
			Assert.AreEqual(expected.B.X, actual.B.X);
			Assert.AreEqual(expected.B.Y, actual.B.Y);
		}

		[TestMethod]
		public void TestStruct()
		{
			var v = new SampleRect {
				A = new SamplePoint { X = 33, Y = 44 },
				B = new SamplePoint { X = 55, Y = 66 },
			};
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.JsonOptions.FieldSeparator = " ";
			js.JsonOptions.IgnoreCompact = true;
			var result = js.ToString(v);
			Assert.AreEqual("{ \"A\":{ \"X\":33, \"Y\":44 }, \"B\":{ \"X\":55, \"Y\":66 } }", result);

			var jd = new JsonDeserializer();
			var w = new SampleRect();
			jd.FromString(w, result);
			CheckSampleRect(v, w);

			w = (SampleRect)SampleRect_JsonDeserializer.Instance.FromString(result);
			CheckSampleRect(v, w);

			var jdg = new JsonDeserializerGenerator();
			jdg.Assembly = GetType().Assembly;
			var p = (SamplePoint)jdg.FromString(new SamplePoint(), "{ \"X\":34, \"Y\":45 }");
			Assert.AreEqual(34, p.X);
			Assert.AreEqual(45, p.Y);
		}

		[TestMethod]
		public void TestInterface()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.JsonOptions.FieldSeparator = "";
			var v1 = new SampleInterfaceField { I = new SampleInterfaced { X = 34 } };
			var result1 = js.ToString(v1);
			Assert.AreEqual("{\"I\":{\"class\":\"YuzuTest.SampleInterfaced, YuzuTest\",\"X\":34}}", result1);

			var w1 = new SampleInterfaceField();
			var jd = new JsonDeserializer();
			jd.FromString(w1, result1);
			Assert.IsInstanceOfType(w1.I, typeof(SampleInterfaced));
			Assert.AreEqual(34, w1.I.X);

			var w1g = (SampleInterfaceField)SampleInterfaceField_JsonDeserializer.Instance.FromString(result1);
			Assert.IsInstanceOfType(w1g.I, typeof(SampleInterfaced));
			Assert.AreEqual(34, w1g.I.X);

			var w1n = (SampleInterfaceField)SampleInterfaceField_JsonDeserializer.Instance.FromString("{\"I\":null}");
			Assert.AreEqual(null, w1n.I);

			var v2 = new List<ISample> { null, new SampleInterfaced { X = 37 } };
			var result2 = js.ToString(v2);
			Assert.AreEqual("[null,{\"class\":\"YuzuTest.SampleInterfaced, YuzuTest\",\"X\":37}]", result2);

			var w2 = new List<ISample>();
			jd.FromString(w2, result2);
			Assert.AreEqual(2, w2.Count);
			Assert.IsNull(w2[0]);
			Assert.AreEqual(37, w2[1].X);

			ISampleField v3 = new SampleInterfacedField { X = 41 };
			js.JsonOptions.SaveRootClass = true;
			var result3 = js.ToString(v3);
			Assert.AreEqual("{\"class\":\"YuzuTest.SampleInterfacedField, YuzuTest\",\"X\":41}", result3);
			var w3 = (ISampleField)jd.FromString(result3);
			Assert.AreEqual(41, w3.X);
		}

		[TestMethod]
		public void TestAbstract()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.JsonOptions.FieldSeparator = "";
			js.JsonOptions.SaveRootClass = true;
			var jd = new JsonDeserializer();

			SampleAbstract v1 = new SampleConcrete { XX = 81 };
			var result1 = js.ToString(v1);
			Assert.AreEqual("{\"class\":\"YuzuTest.SampleConcrete, YuzuTest\",\"XX\":81}", result1);

			var w1 = jd.FromString<SampleAbstract>(result1);
			Assert.AreEqual((v1 as SampleConcrete).XX, (w1 as SampleConcrete).XX);
			var w1g = (SampleConcrete)SampleAbstract_JsonDeserializer.Instance.FromString(result1);
			Assert.AreEqual((v1 as SampleConcrete).XX, w1g.XX);

			var v2 = new List<SampleAbstract>();
			v2.Add(new SampleConcrete { XX = 51 });

			var w2 = jd.FromString<List<SampleAbstract>>(js.ToString(v2));
			Assert.AreEqual(v2.Count, w2.Count);
			Assert.AreEqual((v2[0] as SampleConcrete).XX, (w2[0] as SampleConcrete).XX);
		}

		[TestMethod]
		public void TestGeneric()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.JsonOptions.FieldSeparator = "";
			var v1 = new SampleInterfaceField { I = new SampleInterfacedGeneric<string> { X = 35, G = "qq" } };
			var n = "YuzuTest.SampleInterfacedGeneric`1[[System.String]], YuzuTest";
			var result1 = js.ToString(v1);
			Assert.AreEqual(
				String.Format("{{\"I\":{{\"class\":\"{0}\",\"G\":\"qq\",\"X\":35}}}}", n), result1);
			var w1 = (SampleInterfaceField)(new JsonDeserializer()).FromString(new SampleInterfaceField(), result1);
			Assert.AreEqual(w1.I.X, 35);
			Assert.AreEqual((w1.I as SampleInterfacedGeneric<string>).G, "qq");
			var w1g = (SampleInterfaceField)SampleInterfaceField_JsonDeserializer.Instance.FromString(result1);
			Assert.AreEqual(w1g.I.X, 35);
			Assert.AreEqual((w1g.I as SampleInterfacedGeneric<string>).G, "qq");
		}

		[TestMethod]
		public void TestCompact()
		{
			var v = new SampleRect {
				A = new SamplePoint { X = 33, Y = 44 },
				B = new SamplePoint { X = 55, Y = 66 },
			};
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.JsonOptions.FieldSeparator = " ";
			var result = js.ToString(v);
			Assert.AreEqual("{ \"A\":[ 33, 44 ], \"B\":[ 55, 66 ] }", result);

			var jd = new JsonDeserializer();
			var w = new SampleRect();
			jd.FromString(w, result);
			CheckSampleRect(v, w);

			w = (SampleRect)SampleRect_JsonDeserializer.Instance.FromString(result);
			CheckSampleRect(v, w);
		}

		[TestMethod]
		public void TestDefault()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.JsonOptions.FieldSeparator = " ";

			var v1 = new Sample1 { X = 6, Y = "ttt" };
			var result1 = js.ToString(v1);
			Assert.AreEqual("{ \"X\":6 }", result1);
			var w1 = (Sample1)Sample1_JsonDeserializer.Instance.FromString(result1);
			Assert.AreEqual(6, w1.X);
			Assert.AreEqual("zzz", w1.Y);

			var v2 = new Sample2 { X = 5, Y = "5" };
			var result2 = js.ToString(v2);
			Assert.AreEqual("{ \"X\":5 }", result2);

			var v3 = new SampleDefault();
			Assert.AreEqual("{ }", js.ToString(new SampleDefault()));
			v3.B = "z";
			var result3 = js.ToString(v3);
			Assert.AreEqual("{ \"B\":\"z\" }", result3);
			var w3 = new SampleDefault();
			(new JsonDeserializer()).FromString(w3, result3);
			Assert.AreEqual(3, w3.A);
			Assert.AreEqual("z", w3.B);
			Assert.AreEqual(new SamplePoint { X = 7, Y = 2 }, w3.P);
		}

		[TestMethod]
		public void TestAlias()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.JsonOptions.FieldSeparator = "";
			js.Options.TagMode = TagMode.Aliases;

			var v = new SampleTree { Value = 9 };
			var result = js.ToString(v);
			Assert.AreEqual("{\"a\":9,\"b\":null}", result);

			js.Options.TagMode = TagMode.Names;
			var result1 = js.ToString(v);
			Assert.AreEqual("{\"Value\":9,\"Children\":null}", result1);

			js.Options.TagMode = TagMode.Ids;
			var result2 = js.ToString(v);
			Assert.AreEqual("{\"AAAB\":9,\"AAAC\":null}", result2);

			var prev = IdGenerator.GetNextId();
			for (int i = 0; i < 2 * 52 - 5; ++i) {
				var next = IdGenerator.GetNextId();
				Assert.IsTrue(String.CompareOrdinal(prev, next) < 0);
				prev = next;
			}
			Assert.AreEqual("AABz", IdGenerator.GetNextId());

			var jd = new JsonDeserializer();
			jd.Options.TagMode = TagMode.Aliases;
			var w = new SampleTree();
			jd.FromString(w, result);
			Assert.AreEqual(9, w.Value);
			Assert.AreEqual(null, w.Children);
		}

		[TestMethod]
		public void TestObject()
		{
			var jd = new JsonDeserializer();
			var w = new SampleObj();
			const string str = "{ \"F\": 123.4 }";
			jd.FromString(w, str);
			Assert.AreEqual(123.4, w.F);
			var wg = new SampleObj();
			SampleObj_JsonDeserializer.Instance.FromString(wg, str);
			Assert.AreEqual(123.4, wg.F);

			jd.FromString(w, "{ \"F\": [1,2,3] }");
			CollectionAssert.AreEqual(new object[] { 1.0, 2.0, 3.0 }, (List<object>)w.F);
			jd.FromString(w, "{ \"F\": {\"a\":\"1\", \"b\": \"2\"} }");
			CollectionAssert.AreEqual(
				new Dictionary<string, object>() { { "a", "1" }, { "b", "2" } },
				(Dictionary<string, object>)w.F);
			Assert.AreEqual(typeof(Dictionary<string, object>), jd.FromString("{}").GetType());

			var d = jd.FromString("{ \"F\": [1,2,3] }");
			Assert.AreEqual(typeof(Dictionary<string, object>), d.GetType());
			CollectionAssert.AreEqual(
				new object[] { 1.0, 2.0, 3.0 },
				(List<object>)((Dictionary<string,object>)d)["F"]);

			d = jd.FromString("{ \"F\": {\"class\": \"YuzuTest.SampleObj, YuzuTest\", \"F\": null } }");
			Assert.AreEqual(typeof(Dictionary<string, object>), d.GetType());
			var f = ((Dictionary<string, object>)d)["F"];
			Assert.IsInstanceOfType(f, typeof(SampleObj));
			Assert.AreEqual(null, ((SampleObj)f).F);
		}

		[TestMethod]
		public void TestNewFields()
		{
			var jd = new JsonDeserializer();
			jd.Options.TagMode = TagMode.Aliases;
			jd.Options.IgnoreUnknownFields = true;

			var w = new SampleTree();
			jd.FromString(w, "{\"a\":9,\"a1\":[],\"b\":null}");
			Assert.AreEqual(9, w.Value);

			jd.FromString(w, "{\"a\":10, \"a1\":[], \"b\":null, \"x\":null}");
			Assert.AreEqual(10, w.Value);

			jd.FromString(w, "{\"a\":11, \"a1\":[], \"x\":null}");
			Assert.AreEqual(11, w.Value);
		}

		[TestMethod]
		public void TestSpaces()
		{
			var jd = new JsonDeserializer();
			var w = new SampleList();
			jd.FromString(w, "{   \t\t\n\n\n\r \"E\":   \t\t\n\n[  \n\t\n\t]    }");
			Assert.AreEqual(0, w.E.Count);
		}

		[TestMethod]
		public void TestEscape()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.JsonOptions.FieldSeparator = "";

			var s = "\"/{\u0001}\n\t\"\"";
			var v = new Sample1 { Y = s };
			var result = js.ToString(v);
			Assert.AreEqual("{\"X\":0,\"Y\":\"\\\"\\/{\\u0001}\\n\\t\\\"\\\"\"}", result);

			var w = new Sample1();
			var jd = new JsonDeserializer();
			jd.FromString(w, result);
			Assert.AreEqual(s, w.Y);

			v.Y = result;
			var result1 = js.ToString(v);
			jd.FromString(w, result1);
			Assert.AreEqual(result, w.Y);

			v.Y = "привет";
			var result2 = js.ToString(v);
			Assert.AreEqual("{\"X\":0,\"Y\":\"привет\"}", result2);

			jd.FromString(w, result2);
			Assert.AreEqual(v.Y, w.Y);
			jd.FromString(w, "{\"X\":0,\"Y\":\"\u043F\u0440\u0438\u0432\u0435\u0442\"}");
			Assert.AreEqual(v.Y, w.Y);
		}

		[TestMethod]
		public void TestDate()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.JsonOptions.FieldSeparator = " ";
			var jd = new JsonDeserializer();

			var v1 = new SampleDate { D = new DateTime(2011, 3, 25), T = TimeSpan.FromMinutes(5) };
			var result1 = js.ToString(v1);
			Assert.AreEqual("{ \"D\":\"2011-03-25T00:00:00.0000000\", \"T\":\"00:05:00\" }", result1);

			js.JsonOptions.DateFormat = @"yyyy";
			Assert.AreEqual("{ \"D\":\"2011\", \"T\":\"00:05:00\" }", js.ToString(v1));

			var w1 = new SampleDate();
			(new JsonDeserializer()).FromString(w1, result1);
			Assert.AreEqual(v1.D, w1.D);
			Assert.AreEqual(v1.T, w1.T);

			w1 = (SampleDate)SampleDate_JsonDeserializer.Instance.FromString(result1);
			Assert.AreEqual(v1.D, w1.D);
			Assert.AreEqual(v1.T, w1.T);

			js.JsonOptions.DateFormat = "O";
			var v2 = new DateTime(2011, 3, 25, 1, 2, 3, DateTimeKind.Utc);
			var result2 = js.ToString(v2);
			Assert.AreEqual("\"2011-03-25T01:02:03.0000000Z\"", result2);
			var w2 = jd.FromString<DateTime>(result2);
			Assert.AreEqual(v2, w2);
			Assert.AreEqual(v2.Kind, w2.Kind);

			var v3 = new DateTime(2011, 3, 25, 1, 2, 3, DateTimeKind.Local);
			var result3 = js.ToString(v3);
			var w3 = jd.FromString<DateTime>(result3);
			Assert.AreEqual(v3, w3);
			Assert.AreEqual(v3.Kind, w3.Kind);
		}

		[TestMethod]
		public void TestDelegate()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.JsonOptions.FieldSeparator = "";

			var v1 = new SampleSelfDelegate { x = 77 };
			v1.OnSomething = v1.Handler1;
			var result = js.ToString(v1);
			Assert.AreEqual("{\"OnSomething\":\"Handler1\",\"x\":77}", result);

			var w1 = new SampleSelfDelegate();
			var jd = new JsonDeserializer();
			jd.FromString(w1, result);
			Assert.AreEqual(v1.x, w1.x);
			w1.OnSomething(10);
			Assert.AreEqual(87, w1.x);

			jd.FromString(w1, result.Replace("Handler1", "Handler2"));
			w1.OnSomething(10);
			Assert.AreEqual(770, w1.x);
		}

		[TestMethod]
		public void TestNullField()
		{
			var js = new JsonSerializer();
			var sample = new SampleWithNullField();
			var s = js.ToString(sample);
			Assert.AreEqual("{\n\t\"About\":null\n}", s);
			var jd = new JsonDeserializer();
			var w = new SampleWithNullField { About = "zzz" };
			jd.FromString(w, s);
			Assert.AreEqual(sample.About, w.About);

			var wg = (SampleWithNullFieldCompact)
				SampleWithNullFieldCompact_JsonDeserializer.Instance.FromString("[null]");
			Assert.AreEqual(null, wg.N);
		}

		[TestMethod]
		public void TestAfterDeserialization()
		{
			var js = new JsonSerializer();
			var v0 = new SampleAfter { X = "m" };
			var result0 = js.ToString(v0);
			Assert.AreEqual("{\n\t\"X\":\"m\"\n}", result0);

			var jd = new JsonDeserializer();
			var w0 = new SampleAfter();
			jd.FromString(w0, result0);
			Assert.AreEqual("m1", w0.X);

			var w1 = new SampleAfter2();
			jd.FromString(w1, result0);
			Assert.AreEqual("m231", w1.X);

			var w2 = (SampleAfter2)SampleAfter2_JsonDeserializer.Instance.FromString(result0);
			Assert.AreEqual("m231", w2.X);
		}

		[TestMethod]
		public void TestMerge()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.JsonOptions.FieldSeparator = "";

			var v1 = new SampleMerge();
			v1.DI.Add(3, 4);
			v1.LI.Add(33);
			v1.M = new Sample1 { X = 768, Y = "ttt" };

			var result1 = js.ToString(v1);
			Assert.AreEqual("{\"DI\":{\"3\":4},\"LI\":[33],\"M\":{\"X\":768}}", result1);

			var jd = new JsonDeserializer();
			var w1 = new SampleMerge();
			w1.DI.Add(5, 6);
			w1.LI.Add(44);
			w1.M = new Sample1 { X = 999, Y = "qqq" };
			jd.FromString(w1, result1);
			CollectionAssert.AreEqual(new Dictionary<int, int> { { 5, 6 }, { 3, 4 } }, w1.DI);
			CollectionAssert.AreEqual(new[] { 44, 33 }, w1.LI);
			Assert.AreEqual(768, w1.M.X);
			Assert.AreEqual("qqq", w1.M.Y);

			var w2 = new SampleMerge();
			w2.DI.Add(51, 61);
			w2.LI.Add(55);
			w2.M = new Sample1 { X = 999, Y = "www" };
			SampleMerge_JsonDeserializer.Instance.FromString(w2, result1);
			CollectionAssert.AreEqual(new Dictionary<int, int> { { 51, 61 }, { 3, 4 } }, w2.DI);
			CollectionAssert.AreEqual(new[] { 55, 33 }, w2.LI);
			Assert.AreEqual(768, w2.M.X);
			Assert.AreEqual("www", w2.M.Y);
		}

		[TestMethod]
		public void TestNamespaces()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.JsonOptions.FieldSeparator = "";
			js.Options.TagMode = TagMode.Names;

			var v1 = new YuzuTest2.SampleNamespace { B = new SampleBase { FBase = 3 } };
			var result1 = js.ToString(v1);
			Assert.AreEqual("{\"B\":{\"FBase\":3}}", result1);

			var w1 = (YuzuTest2.SampleNamespace)
				YuzuGen.YuzuTest2.SampleNamespace_JsonDeserializer.Instance.FromString(result1);
			Assert.AreEqual(3, w1.B.FBase);
		}

		[TestMethod]
		public void TestNestedTypes()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.JsonOptions.FieldSeparator = "";
			js.JsonOptions.EnumAsString = true;

			var v1 = new SampleNested { E = SampleNested.NestedEnum.One, C = new SampleNested.NestedClass() };
			var result1 = js.ToString(v1);
			Assert.AreEqual("{\"C\":{\"Z\":0},\"E\":\"One\"}", result1);

			var w1 = (SampleNested)SampleNested_JsonDeserializer.Instance.FromString(result1);
			Assert.AreEqual(v1.E, w1.E);
			Assert.AreEqual(v1.C.Z, w1.C.Z);
		}

		[TestMethod]
		public void TestMemberOfInterface()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.JsonOptions.FieldSeparator = "";

			var v1 = new List<ISampleMember>();
			var result1 = js.ToString(v1);
			Assert.AreEqual("[]", result1);

			var jd = new JsonDeserializer();
			var w1 = new List<ISampleMember>();
			jd.FromString(w1, result1);
			Assert.AreEqual(0, w1.Count);

			v1.Add(new SampleMemberI());
			var result1p = js.ToString(v1);
			Assert.AreEqual("[{\"class\":\"YuzuTest.SampleMemberI, YuzuTest\"}]", result1p);
			jd.FromString(w1, result1p);
			Assert.AreEqual(71, w1[0].X);

			var v2 = new List<ISampleMember> { new SampleMemberI(), new SampleMemberI { X = 99 } };
			var result2 = js.ToString(v2);
			var w2 = new List<ISampleMember>();
			jd.FromString(w2, result2);
			YuzuGen.System.Collections.Generic.List_ISampleMember_JsonDeserializer.Instance.FromString(w2, result2);
			Assert.AreEqual(v2[0].X, w2[0].X);
			Assert.AreEqual(v2[1].X, w2[1].X);

			Assert.AreEqual("[]", js.ToString(new List<SampleMemberAbstract>()));
			var v3 = new List<SampleMemberAbstract> { new SampleMemberConcrete() };
			var result3 = js.ToString(v3);
			Assert.AreEqual("[{\"class\":\"YuzuTest.SampleMemberConcrete, YuzuTest\"}]", result3);
			var w3 = new List<SampleMemberAbstract>();
			jd.FromString(w3, result3);
			Assert.AreEqual(72, w3[0].X);
		}

		[TestMethod]
		public void TestTopLevelListOfNonPrimitiveTypes()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.JsonOptions.FieldSeparator = "";
			js.Options.TagMode = TagMode.Names;
			var jd = new JsonDeserializer();
			jd.Options.TagMode = TagMode.Names;

			var v1 = new List<object> { new SampleDerivedB { FB = 10 }, new SampleDerivedB { FB = 20 } };

			var result1 = js.ToString(v1);
			Assert.AreEqual(
				"[{\"class\":\"YuzuTest.SampleDerivedB, YuzuTest\",\"FBase\":0,\"FB\":10}," +
				"{\"class\":\"YuzuTest.SampleDerivedB, YuzuTest\",\"FBase\":0,\"FB\":20}]",
				result1);
			var w1 = (List<object>)jd.FromString(result1);
			for (int i = 0; i < v1.Count; i++) {
				Assert.AreEqual((v1[i] as SampleDerivedB).FB, (w1[i] as SampleDerivedB).FB);
			}
		}

		[TestMethod]
		public void TestAssemblies()
		{
			var js = new JsonSerializer();
			js.JsonOptions.Indent = "";
			js.JsonOptions.FieldSeparator = "";
			var jd = new JsonDeserializer();

			var v1 = new List<SampleAssemblyBase> {
				new SampleAssemblyDerivedQ { Q = 10 },
				new SampleAssemblyDerivedR { R = "R1" } };
			var result1 = js.ToString(v1);
			Assert.AreEqual(
				"[{\"class\":\"YuzuTestAssembly.SampleAssemblyDerivedQ, AssemblyTest\",\"Q\":10}," +
				"{\"class\":\"YuzuTest.SampleAssemblyDerivedR, YuzuTest\",\"R\":\"R1\"}]",
				result1);

			var w1 = new List<SampleAssemblyBase>();
			jd.FromString(w1, result1);
			Assert.AreEqual((v1[0] as SampleAssemblyDerivedQ).Q, (w1[0] as SampleAssemblyDerivedQ).Q);
			Assert.AreEqual((v1[1] as SampleAssemblyDerivedR).R, (w1[1] as SampleAssemblyDerivedR).R);

			var w1g = (List<SampleAssemblyBase>)
				YuzuGen.System.Collections.Generic.List_SampleAssemblyBase_JsonDeserializer.Instance.FromString(result1);
			Assert.AreEqual((v1[0] as SampleAssemblyDerivedQ).Q, (w1g[0] as SampleAssemblyDerivedQ).Q);
			Assert.AreEqual((v1[1] as SampleAssemblyDerivedR).R, (w1g[1] as SampleAssemblyDerivedR).R);
		}

		[TestMethod]
		public void TestErrors()
		{
			var js = new JsonSerializer();
			XAssert.Throws<YuzuException>(() => js.ToString(new Empty()), "Empty");
			XAssert.Throws<YuzuException>(() => js.ToString(new SampleCollectionWithField<int>()), "collection");
			XAssert.Throws<YuzuException>(() => js.ToString(new SampleInterfacedFieldDup()), "X");
			XAssert.Throws<YuzuException>(() => js.ToString(new BadMerge1()), "merge");
			XAssert.Throws<YuzuException>(() => js.ToString(new BadMerge2()), "merge");

			var jd = new JsonDeserializer();
			var w = new Sample1();
			XAssert.Throws<YuzuException>(() => jd.FromString(w, "{}"), "X");
			XAssert.Throws<YuzuException>(() => jd.FromString(w, "{ \"X\" }"), ":");
			XAssert.Throws<YuzuException>(() => jd.FromString(w, "nn"), "'u'");
			XAssert.Throws<YuzuException>(() => jd.FromString(w, "{ \"X\":1, \"Y\": \"\\z\" }"), "z");
			XAssert.Throws<YuzuException>(() => jd.FromString(w, "{ \"X\":1, \"Y\": \"\\uQ\" }"), "Q");
			XAssert.Throws<YuzuException>(() => jd.FromString(new SampleBool(), "{ \"B\": 1 }"), "1");
			XAssert.Throws<YuzuException>(() => jd.FromString(w, "{ ,}"), ",");
			XAssert.Throws<YuzuException>(() => jd.FromString(w, "{ \"Y\": \"q\" }"), "'X'");
			XAssert.Throws<YuzuException>(() => jd.FromString(w, "[]"), "'Sample1'");
			XAssert.Throws<YuzuException>(() => jd.FromString(w, "{ \"class\": \"Q\" }"), "'Q'");
			XAssert.Throws<YuzuException>(() => jd.FromString(w, "{ \"class\": \"YuzuTest.Sample2, YuzuTest\" }"), ".Sample2");
			XAssert.Throws<YuzuException>(() => jd.FromString(new SamplePoint(), "[ \"QQ\" ]"), "");
			XAssert.Throws<YuzuException>(() => jd.FromString(new object(), "{}"), "object");
			XAssert.Throws<EndOfStreamException>(() => jd.FromString(""), "");
			XAssert.Throws<EndOfStreamException>(() => jd.FromString(w, "{ \"X\": 1"));
			XAssert.Throws<YuzuException>(() => jd.FromString(
				"{\"class\":\"YuzuTest.SampleInterfaceField, YuzuTest\",\"I\":{}}"), "class");
			XAssert.Throws<YuzuException>(() => jd.FromString(
				"{\"class\":\"YuzuTest.SampleInterfaceField, YuzuTest\",\"I\":{\"class\":\"YuzuTest.Sample1, YuzuTest\"}}"),
				"ISample");

			jd.Options.ReportErrorPosition = true;
			XAssert.Throws<YuzuException>(() => jd.FromString(w, "      z"), "7");
			jd.Options.ReportErrorPosition = false;
			try {
				jd.FromString(w, "      z");
			}
			catch (YuzuException e) {
				Assert.IsFalse(e.Message.Contains("7"));
			}
		}

		[TestMethod]
		public void TestDeclarationErrors()
		{
			var js = new JsonSerializer();
			js.Options.TagMode = TagMode.Aliases;
			XAssert.Throws<YuzuException>(() => js.ToString(new Bad1()), "F");
			XAssert.Throws<YuzuException>(() => js.ToString(new Bad2()), "F");
			XAssert.Throws<YuzuException>(() => js.ToString(new Bad3()), "G");
			XAssert.Throws<YuzuException>(() => js.ToString(new BadPrivate()), "'F'");
			XAssert.Throws<YuzuException>(() => js.ToString(new BadPrivateGetter()), "'F'");
		}
	}
}
