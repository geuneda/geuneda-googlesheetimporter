using System;
using System.Collections.Generic;
using Geuneda.GoogleSheetImporter;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable CheckNamespace
// ReSharper disable UnassignedField.Global

namespace GeunedaEditor.GoogleSheetImporter.Tests
{
	public class CsvParserTest
	{
		public enum MockEnum
		{
			MockValue
		}
		public struct MockKeyValuePair
		{
			public int Key;
			public int Value;

			public MockKeyValuePair(int key, int value)
			{
				Key = key;
				Value = value;
			}
		}

		public class MockClass
		{
			[ParseIgnore]
			public string Ignored;
			public string String;
			public int Int;
			public float Float;
			public double Double;
			public MockEnum Enum;
			public int[] Array;
			public List<int> List;
			// public KeyValuePair<int,int> Pair;
			public Dictionary<int, int> Dictionary;
		}

		public class MockPayload
		{
			// JSON 키(a, b)는 대소문자를 가리지 않고 맞춘다
			public int A;
			public int[] B;
		}

		public class MockJsonHolder
		{
			public string Text;
			public MockPayload Payload;
		}

		public class MockSubListItem
		{
			public int Id;
			public int Amount;
		}

		[Test]
		public void ConvertCsv_Successfully()
		{
			var csv = "Ignored,String,Int,Float,Double,Enum,Array,List,Pair,Dictionary\r\n" +
					  "Ignored,text,1,1.1,1.1,MockValue,\"1,2\",\"1,2\",\"1:2\",\"1,2\"";
			var dic = CsvParser.ConvertCsv(csv);

			Assert.AreEqual(1, dic.Count);
			Assert.AreEqual(10, dic[0].Count);
			Assert.AreEqual("Ignored", dic[0]["Ignored"]);
			Assert.AreEqual("text", dic[0]["String"]);
			Assert.AreEqual("1", dic[0]["Int"]);
			Assert.AreEqual("1.1", dic[0]["Float"]);
			Assert.AreEqual("1.1", dic[0]["Double"]);
			Assert.AreEqual("MockValue", dic[0]["Enum"]);
			Assert.AreEqual("1,2", dic[0]["Array"]);
			Assert.AreEqual("1,2", dic[0]["List"]);
			Assert.AreEqual("1:2", dic[0]["Pair"]);
			Assert.AreEqual("1,2", dic[0]["Dictionary"]);
		}

		[Test]
		public void ConvertCsv_WrongFormatSeparator_EmptyResult()
		{
			var csv = "int,float\n" +
					  "1,1.1";
			var dic = CsvParser.ConvertCsv(csv);

			Assert.AreEqual(0, dic.Count);
		}

		[Test]
		public void ConvertCsv_OnlyHeadlines_EmptyResult()
		{
			var csv = "int,float,double,enum,pair";
			var dic = CsvParser.ConvertCsv(csv);

			Assert.AreEqual(0, dic.Count);
		}

		[Test]
		public void ConvertCsv_MissMatchColumnsCount_FillsWithDefaultData()
		{
			var csv = "int,float,double\r\n" +
					  "1,1.1";
			var dic = CsvParser.ConvertCsv(csv);

			Assert.AreEqual("", dic[0]["double"]);
		}

		[Test]
		public void ConvertCsv_QuotedCells_UnescapeQuotes()
		{
			// 구글 시트는 따옴표·쉼표가 든 셀을 "..." 로 감싸고 안의 " 를 "" 로 내보낸다
			var csv = "Id,Json,Text,Empty,Quote\r\n" +
					  "1,\"{\"\"a\"\":1,\"\"b\"\":[1,2]}\",\"say \"\"hi\"\", ok\",\"\",\"\"\"\"\"\"";
			var dic = CsvParser.ConvertCsv(csv);

			Assert.AreEqual(1, dic.Count);
			Assert.AreEqual("1", dic[0]["Id"]);
			Assert.AreEqual("{\"a\":1,\"b\":[1,2]}", dic[0]["Json"]);
			Assert.AreEqual("say \"hi\", ok", dic[0]["Text"]);
			Assert.AreEqual("", dic[0]["Empty"]);
			Assert.AreEqual("\"\"", dic[0]["Quote"]);
		}

		[Test]
		public void ConvertCsv_QuotedCellWithLineBreak_StaysInOneRow()
		{
			// 행은 \r\n 으로만 나누므로 셀 안의 줄바꿈(\n)은 그 셀에 남는다
			var csv = "Id,Text\r\n" +
					  "1,\"first\nsecond\"\r\n" +
					  "2,plain";
			var dic = CsvParser.ConvertCsv(csv);

			Assert.AreEqual(2, dic.Count);
			Assert.AreEqual("first\nsecond", dic[0]["Text"]);
			Assert.AreEqual("plain", dic[1]["Text"]);
		}

		[Test]
		public void Deserialize_JsonObjectCell_ToClassField()
		{
			var csv = "Text,Payload\r\n" +
					  "\"{\"\"a\"\":1}\",\"{\"\"a\"\":3,\"\"b\"\":[1,2]}\"";
			var dic = CsvParser.ConvertCsv(csv);
			var result = CsvParser.DeserializeTo<MockJsonHolder>(dic[0]);

			// 문자열 필드는 JSON 글자 그대로, 클래스 필드는 JSON 객체로 읽는다
			Assert.AreEqual("{\"a\":1}", result.Text);
			Assert.IsNotNull(result.Payload);
			Assert.AreEqual(3, result.Payload.A);
			Assert.AreEqual(new[] { 1, 2 }, result.Payload.B);
		}

		[Test]
		public void Parse_JsonObjectCell_CustomDeserializerTakesPriority()
		{
			var custom = new MockPayload { A = 9 };
			var result = CsvParser.Parse<MockPayload>("{\"a\":1}", (text, type) => type == typeof(MockPayload) ? custom : null);

			Assert.AreSame(custom, result);
		}

		[Test]
		public void Parse_JsonObjectCell_ObjectTypeBecomesJObject()
		{
			// object 필드도 JSON 객체 셀이면 JObject 로 읽는다(이전에는 따옴표가 있으면 예외, 없으면 문자열)
			var result = CsvParser.Parse<object>("{\"a\":1}");
			var empty = CsvParser.Parse<object>("{}");

			Assert.IsInstanceOf<JObject>(result);
			Assert.AreEqual(1, ((JObject)result)["a"].Value<int>());
			Assert.IsInstanceOf<JObject>(empty);
		}

		[Test]
		public void Deserialize_Successfully()
		{
			var csv = "Ignored,String,Int,Float,Double,Enum,Array,List,Pair,Dictionary\r\n" +
					  "Ignored,text,1,1.1,1.1,MockValue,\"1,2\",\"1,2\",\"1:2\",\"1,2\"";
			var dic = CsvParser.ConvertCsv(csv);
			var result = CsvParser.DeserializeTo<MockClass>(dic[0]);

			Assert.AreEqual(null, result.Ignored);
			Assert.AreEqual("text", result.String);
			Assert.AreEqual(1, result.Int);
			Assert.AreEqual(1.1f, result.Float);
			Assert.AreEqual(1.1d, result.Double);
			Assert.AreEqual(MockEnum.MockValue, result.Enum);
			Assert.AreEqual(new[] { 1, 2 }, result.Array);
			Assert.AreEqual(new List<int> { 1, 2 }, result.List);
			// Assert.AreEqual(new KeyValuePair<int, int>(1, 2), result.Pair);
			Assert.AreEqual(new Dictionary<int, int> { { 1, 2 } }, result.Dictionary);
		}

		[Test]
		public void Deserialize_MissingFields_Successfully()
		{
			var csv = "Int,Float\r\n" +
					  "1,1.1";
			var dic = CsvParser.ConvertCsv(csv);
			var result = CsvParser.DeserializeTo<MockClass>(dic[0]);

			Assert.AreEqual(null, result.String);
			Assert.AreEqual(1, result.Int);
			Assert.AreEqual(1.1f, result.Float);
			Assert.AreEqual(0, result.Double);
			Assert.AreEqual(MockEnum.MockValue, result.Enum);
			Assert.AreEqual(null, result.Array);
			Assert.AreEqual(null, result.List);
			// Assert.AreEqual(new KeyValuePair<int, int>(), result.Pair);
			Assert.AreEqual(null, result.Dictionary);
		}

		[Test]
		public void Deserialize_ExtraFields_Successfully()
		{
			var csv = "Int,Float,ExtraField\r\n" +
					  "1,1.1,extraValue";
			var dic = CsvParser.ConvertCsv(csv);
			var result = CsvParser.DeserializeTo<MockClass>(dic[0]);

			Assert.AreEqual(null, result.String);
			Assert.AreEqual(1, result.Int);
			Assert.AreEqual(1.1f, result.Float);
			Assert.AreEqual(0, result.Double);
			Assert.AreEqual(MockEnum.MockValue, result.Enum);
			Assert.AreEqual(null, result.Array);
			Assert.AreEqual(null, result.List);
			// Assert.AreEqual(new KeyValuePair<int, int>(), result.Pair);
			Assert.AreEqual(null, result.Dictionary);
		}

		[Test]
		public void ConvertCsv_DescriptionRow_Skipped()
		{
			var csv = "Key,Value\r\n" +
					  "#,설명 행\r\n" +
					  "MaxLevel,50\r\n" +
					  "#설명,설명 행";
			var dic = CsvParser.ConvertCsv(csv);

			Assert.AreEqual(1, dic.Count);
			Assert.AreEqual("MaxLevel", dic[0]["Key"]);
		}

		[Test]
		public void ConvertCsv_SubListRowsWithKeyFirstColumn_Kept()
		{
			var csv = "Key,Value,Extra\r\n" +
					  "MaxLevel,50,\r\n" +
					  "Rewards[],Id,Amount\r\n" +
					  "#,1,100\r\n" +
					  "#,2,200\r\n" +
					  "MinLevel,1,\r\n" +
					  "#,설명 행,";
			var dic = CsvParser.ConvertCsv(csv);

			Assert.AreEqual(5, dic.Count);
			Assert.AreEqual("Rewards[]", dic[1]["Key"]);
			Assert.AreEqual("#", dic[2]["Key"]);
			Assert.AreEqual("#", dic[3]["Key"]);
			Assert.AreEqual("MinLevel", dic[4]["Key"]);
		}

		[Test]
		public void ConvertCsv_SubListQuotedHashRow_KeepsSubListState()
		{
			var csv = "Key,Value\r\n" +
					  "Rewards[],Id\r\n" +
					  "\"#\",1\r\n" +
					  "#설명,설명 행\r\n" +
					  "#,2\r\n" +
					  "MinLevel,1";
			var dic = CsvParser.ConvertCsv(csv);

			// 인용된 "#" 행 뒤에도 서브 리스트가 이어지고, 중간 설명 행만 빠져야 한다
			Assert.AreEqual(4, dic.Count);
			Assert.AreEqual("Rewards[]", dic[0]["Key"]);
			Assert.AreEqual("#", dic[1]["Key"]);
			Assert.AreEqual("1", dic[1]["Value"]);
			Assert.AreEqual("#", dic[2]["Key"]);
			Assert.AreEqual("2", dic[2]["Value"]);
			Assert.AreEqual("MinLevel", dic[3]["Key"]);
		}

		[Test]
		public void DeserializeSubList_KeyFirstColumn_Successfully()
		{
			var csv = "Key,Value,Extra\r\n" +
					  "MaxLevel,50,\r\n" +
					  "Rewards[],Id,Amount\r\n" +
					  "#,1,100\r\n" +
					  "#,2,200";
			var dic = CsvParser.ConvertCsv(csv);
			var result = (List<MockSubListItem>)CsvParser.DeserializeSubList(dic, 1, typeof(List<MockSubListItem>),
				"Rewards");

			Assert.AreEqual(2, result.Count);
			Assert.AreEqual(1, result[0].Id);
			Assert.AreEqual(100, result[0].Amount);
			Assert.AreEqual(2, result[1].Id);
			Assert.AreEqual(200, result[1].Amount);
		}

		[Test]
		public void DeserializeObject_ArrayType_ReturnsArray()
		{
			var result = CsvParser.DeserializeObject("1,2,3", typeof(int[]));

			Assert.IsInstanceOf<int[]>(result);
			Assert.AreEqual(new[] { 1, 2, 3 }, result);
		}

		[Test]
		public void ArrayParse_Successfully()
		{
			var result = CsvParser.ArrayParse<int>("1,[2],{3,4},(5),6");

			Assert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 }, result);
		}

		// [Test]
		// public void ArrayParsePair_Successfully()
		// {
		// 	var result = CsvParser.ArrayParse<KeyValuePair<int,int>>("1:2,(3 < 4),[5 > 6],{7 = 8}");
		// 	var pairArray = new[]
		// 	{
		// 		new KeyValuePair<int,int>(1,2),
		// 		new KeyValuePair<int,int>(3,4),
		// 		new KeyValuePair<int,int>(5,6),
		// 		new KeyValuePair<int,int>(7,8), 
		// 	};
		// 	
		// 	Assert.AreEqual(pairArray, result);
		// }

		// [Test]
		// public void ArrayParsePair_ElementOddAmount_ThrowsException()
		// {
		// 	Assert.Throws<IndexOutOfRangeException>(() => CsvParser.ArrayParse<KeyValuePair<int,int>>("1:2,(3 < 4),5"));
		// }

		[Test]
		public void DictionaryParse_Successfully()
		{
			var result = CsvParser.DictionaryParse<int, int>("1,2,3,4");

			Assert.AreEqual(new Dictionary<int, int> { { 1, 2 }, { 3, 4 } }, result);
		}

		[Test]
		public void DictionaryParsePair_Successfully()
		{
			var result = CsvParser.DictionaryParse<int, int>("1:2,(3>4)");

			Assert.AreEqual(new Dictionary<int, int> { { 1, 2 }, { 3, 4 } }, result);
		}

		[Test]
		public void DictionaryParse_ElementOddAmount_ThrowsException()
		{
			Assert.Throws<IndexOutOfRangeException>(() => CsvParser.DictionaryParse<int, int>("1,2,3"));
		}

		[Test]
		public void ParseInt_Successfully()
		{
			var result = CsvParser.Parse<int>("1");

			Assert.AreEqual(1, result);
		}

		[Test]
		public void ParseInt_WithFloat_ThrowsException()
		{
			Assert.Throws<FormatException>(() => CsvParser.Parse<int>("1.1f"));
		}

		// [Test]
		// public void ParsePair_Successfully()
		// {
		// 	var pair1 = CsvParser.Parse<KeyValuePair<int, int>>("1:2");
		// 	var pair2 = CsvParser.Parse<MockKeyValuePair>("1:2");
		// 	var result1 = new KeyValuePair<int, int>(1, 2);
		// 	var result2 = new MockKeyValuePair(1, 2);
		// 	
		// 	Assert.AreEqual(result1, pair1);
		// 	Assert.AreEqual(result2, pair2);
		// }
		//
		// [Test]
		// public void ParsePair_OneElement_ThrowsException()
		// {
		// 	Assert.Throws<IndexOutOfRangeException>(() => CsvParser.Parse<KeyValuePair<int, int>>("1"));
		// }
	}
}