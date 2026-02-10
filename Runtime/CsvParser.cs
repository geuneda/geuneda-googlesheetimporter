using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Geuneda.GameData;
using Newtonsoft.Json;

// ReSharper disable once CheckNamespace

namespace Geuneda.GoogleSheetImporter
{
	/// <summary>
	/// CSV 텍스트를 파싱하는 헬퍼 클래스
	/// </summary>
	public static class CsvParser
	{
		public const string IGNORE_COLUMN_CHAR = "$";
		public const string IGNORE_FIELD_CHAR = "#";
		public const string SUB_LIST_SUFFIX = "[]";

		public static readonly char[] PairSplitChars = { ':', '<', '>', '=', '|' };
		public static readonly char[] ArraySplitChars = { ',', '(', ')', '[', ']', '{', '}' };
		public static readonly string[] NewLineChars = { "\r\n", "\r", "\n" };

		/// <summary>
		/// 전체 <paramref name="csv"/> 텍스트를 파싱합니다.
		/// 각 행은 반환되는 리스트의 요소입니다.
		/// 각 열은 반환되는 딕셔너리의 요소입니다. 딕셔너리 키는 CSV 헤더가 됩니다.
		/// </summary>
		public static List<Dictionary<string, string>> ConvertCsv(string csv)
		{
			var lines = csv.Split(new[] { "\r\n" }, StringSplitOptions.None);
			var list = new List<Dictionary<string, string>>(lines.Length - 1);
			var headlines = EnumerateCsvLine(lines[0]);

			for (var i = 1; i < lines.Length; i++)
			{
				var dictionary = new Dictionary<string, string>(headlines.Length);
				var values = EnumerateCsvLine(lines[i]);

				for (var j = 0; j < headlines.Length; j++)
				{
					if (headlines[j].StartsWith(IGNORE_COLUMN_CHAR))
					{
						continue;
					}

					// 추가적인 유효하지 않은 열에 대한 수정
					// 연속된 두 개의 빈 열은 하나의 빈 값으로 파싱됨
					// 예: (one,two,,)는 [one, two, ""]로 파싱됨
					if (j >= values.Length)
					{
						dictionary.Add(headlines[j], "");
					}
					else
					{
						dictionary.Add(headlines[j], values[j].Trim());
					}
				}

				list.Add(dictionary);
			}

			return list;
		}

		/// <inheritdoc cref="DeserializeTo"/>
		public static T DeserializeTo<T>(Dictionary<string, string> data,
										 params Func<string, Type, object>[] deserializers)
		{
			return (T)DeserializeTo(typeof(T), data, deserializers);
		}

		/// <summary>
		/// 주어진 CSV <paramref name="data"/> 셀 값을 주어진 <paramref name="type"/> 타입의 객체로 역직렬화합니다
		/// </summary>
		/// <remarks>
		/// 특정 파싱을 위한 추가 사용자 정의 <paramref name="deserializers"/>를 제공합니다
		/// </remarks>
		public static object DeserializeTo(Type type, Dictionary<string, string> data,
										   params Func<string, Type, object>[] deserializers)
		{
			var ignoreType = typeof(ParseIgnoreAttribute);
			var instance = Activator.CreateInstance(type);

			foreach (var field in type.GetFields())
			{
				if (!data.ContainsKey(field.Name))
				{
					continue;
				}

				if (field.GetCustomAttributes(ignoreType, false).Length == 1)
				{
					continue;
				}

				field.SetValue(instance, DeserializeObject(data[field.Name], field.FieldType, deserializers));
			}

			return instance;
		}

		/// <summary>
		/// 주어진 <paramref name="data"/>를 주어진 <paramref name="type"/>으로 <see cref="object"/>
		/// 형식으로 역직렬화하여 리플렉션을 통해 설정할 수 있는 결과를 반환합니다.
		/// </summary>
		/// <remarks>
		/// 특정 파싱을 위한 추가 사용자 정의 <paramref name="deserializers"/>를 제공합니다
		/// </remarks>
		public static object DeserializeObject(string data, Type type,
											   params Func<string, Type, object>[] deserializers)
		{
			var listType = typeof(IList);
			var dictionaryType = typeof(IDictionary);
			var unityDictionaryType = typeof(UnitySerializedDictionary<,>).GetGenericTypeDefinition();

			if (type.IsArray)
			{
				return ArrayParse(data, type.GetElementType(), deserializers);
			}

			if (listType.IsAssignableFrom(type))
			{
				return ArrayParse(data, type.GenericTypeArguments[0], deserializers);
			}

			if (type.BaseType != null && type.BaseType.IsGenericType && type.BaseType.GetGenericTypeDefinition()
					.IsAssignableFrom(unityDictionaryType))
			{
				var types = type.BaseType.GenericTypeArguments;
				return DictionaryParse(data, types[0], types[1], type, deserializers);
			}

			if (dictionaryType.IsAssignableFrom(type))
			{
				var types = type.GenericTypeArguments;
				var keyType = types[0];
				var valueType = types[1];
				return DictionaryParse(data, keyType, valueType,
					typeof(Dictionary<,>).MakeGenericType(keyType, valueType), deserializers);
			}

			return Parse(data, type, deserializers);
		}

		/// <summary>
		/// 고유한 헤더/정의를 가진 사용자 정의 복합(비원시) 타입의 리스트를 역직렬화합니다.
		/// </summary>
		public static object DeserializeSubList(List<Dictionary<string, string>> data, int startIndex, Type type,
												string fieldName,
												params Func<string, Type, object>[] deserializers)
		{
			var subType = type.GetGenericArguments()[0];
			var subData = GetSubListDictionary(data, startIndex);

			var listType = typeof(List<>).MakeGenericType(subType);
			var list = Activator.CreateInstance(listType);
			var addMethod = listType.GetMethod("Add")!;

			foreach (var dict in subData)
			{
				addMethod.Invoke(list, new[] { DeserializeTo(subType, dict, deserializers) });
			}

			return list;
		}

		/// <summary>
		/// 객체의 기본 역직렬화 데이터에서 서브 리스트의 데이터 딕셔너리를 추출합니다.
		/// </summary>
		private static List<Dictionary<string, string>> GetSubListDictionary(
			List<Dictionary<string, string>> data, int startIndex)
		{
			var headerMap = new Dictionary<string, string>();

			foreach (var (key, value) in data[startIndex])
			{
				if (value.EndsWith(SUB_LIST_SUFFIX)) continue;

				headerMap.Add(key, value);
			}

			var objData = new List<Dictionary<string, string>>();
			for (int i = startIndex + 1; i < data.Count; i++)
			{
				var dataLine = data[i];

				if (dataLine["Key"] != IGNORE_FIELD_CHAR) break;

				var rowData = new Dictionary<string, string>();

				foreach (var (key, value) in dataLine)
				{
					if (headerMap.TryGetValue(key, out var header))
					{
						rowData.Add(header, value);
					}
				}

				objData.Add(rowData);
			}

			return objData;
		}

		/// <summary>
		/// 주어진 <paramref name="text"/>를 주어진 <typeparamref name="T"/>의 배열로 파싱합니다.
		/// 텍스트가 ',', '{}', '()' 또는 '[]'로 구분되어 있으면 배열 형식입니다 (예: 1,2,3; {1,2}{4,5}, [1,2,3])
		/// 주어진 <paramref name="text"/>가 배열 형식이 아닌 경우, <paramref name="text"/>를 유일한 요소로 가진 배열을 반환합니다
		/// </summary>
		/// <remarks>
		/// 특정 파싱을 위한 추가 사용자 정의 <paramref name="deserializers"/>를 제공합니다
		/// </remarks>
		/// <exception cref="FormatException">
		/// 주어진 <paramref name="text"/>가 주어진 <typeparamref name="T"/> 타입 형식이 아닌 경우 발생합니다
		/// </exception>
		public static List<T> ArrayParse<T>(string text, params Func<string, Type, object>[] deserializers)
		{
			return ArrayParse(text, typeof(T), deserializers) as List<T>;
		}

		/// <inheritdoc cref="ArrayParse{T}" />
		public static object ArrayParse(string data, Type type, params Func<string, Type, object>[] deserializers)
		{
			var split = data.Split(ArraySplitChars, StringSplitOptions.RemoveEmptyEntries);
			var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type));

			foreach (var value in split)
			{
				list.Add(Parse(value, type, deserializers));
			}

			return list;
		}

		/// <summary>
		/// 주어진 <paramref name="text"/>를 <seealso cref="Dictionary{TKey, TValue}"/> 타입으로 파싱합니다.
		/// <seealso cref="ArrayParse{T}"/>와 동일한 규칙을 따르고 최소 2개의 요소가 있으면
		/// <seealso cref="Dictionary{TKey, TValue}"/> 타입 형식입니다.
		/// 주어진 <paramref name="text"/>가 <seealso cref="Dictionary{TKey, TValue}"/> 타입 형식이 아닌 경우
		/// 빈 딕셔너리를 반환합니다
		/// </summary>
		/// <remarks>
		/// 특정 파싱을 위한 추가 사용자 정의 <paramref name="deserializers"/>를 제공합니다
		/// </remarks>
		/// <exception cref="FormatException">
		/// 주어진 <paramref name="text"/>가 주어진 <typeparamref name="TKey"/> 또는 <typeparamref name="TValue"/> 타입 형식이 아닌 경우 발생합니다
		/// </exception>
		/// <exception cref="IndexOutOfRangeException">
		/// 주어진 <paramref name="text"/>에 쌍을 이룰 수 없는 홀수 개의 값이 있는 경우 발생합니다. 항상 짝수 개의 값이어야 합니다
		/// </exception>
		public static Dictionary<TKey, TValue> DictionaryParse<TKey, TValue>(
			string text, params Func<string, Type, object>[] deserializers)
		{
			var keyType = typeof(TKey);
			var valueType = typeof(TValue);
			return DictionaryParse(text, keyType, valueType, typeof(Dictionary<,>).MakeGenericType(keyType, valueType),
				deserializers) as Dictionary<TKey, TValue>;
		}

		/// <inheritdoc cref="DictionaryParse{TKey,TValue}" />
		private static object DictionaryParse(string text, Type keyType, Type valueType, Type dictionaryType,
											  params Func<string, Type, object>[] deserializers)
		{
			var items = ArrayParse<string>(text, deserializers);
			var dictionary = (IDictionary)Activator.CreateInstance(dictionaryType);

			if (items.Count == 0)
			{
				return null;
			}

			if (items[0].IndexOfAny(PairSplitChars) != -1)
			{
				foreach (var item in items)
				{
					var split = item.Split(PairSplitChars);
					var key = Parse(split[0], keyType, deserializers);
					var value = Parse(split[1], valueType, deserializers);

					dictionary.Add(key, value);
				}
			}
			else if (items.Count % 2 == 1)
			{
				throw new
					IndexOutOfRangeException($"Dictionary must have an even amount of values and the following text" +
						$"has {items.Count.ToString()} values. \nText:{text}");
			}
			else
			{
				for (var i = 0; i < items.Count; i += 2)
				{
					var key = Parse(items[i], keyType, deserializers);
					var value = Parse(items[i + 1], valueType, deserializers);

					dictionary.Add(key, value);
				}
			}

			return dictionary;
		}

		/// <summary>
		/// 주어진 <paramref name="text"/>를 주어진 <typeparamref name="T"/> 타입으로 파싱합니다
		/// </summary>
		/// <remarks>
		/// 특정 파싱을 위한 추가 사용자 정의 <paramref name="deserializers"/>를 제공합니다
		/// </remarks>
		/// <exception cref="FormatException">
		/// 주어진 <paramref name="text"/>가 주어진 <typeparamref name="T"/> 타입 형식이 아닌 경우 발생합니다
		/// </exception>
		public static T Parse<T>(string text, params Func<string, Type, object>[] deserializers)
		{
			return (T)Parse(text, typeof(T), deserializers);
		}

		/// <inheritdoc cref="Parse{T}" />
		public static object Parse(string text, Type type, params Func<string, Type, object>[] deserializers)
		{
			text = text.Trim();

			if (type == typeof(string))
			{
				return text;
			}

			if (type.IsEnum)
			{
				return Enum.Parse(type, text);
			}

			if (TryGetKeyValuePair(text, type, out var key, out var value, deserializers))
			{
				return Activator.CreateInstance(type, key, value);
			}

			deserializers ??= Array.Empty<Func<string, Type, object>>();

			foreach (var func in deserializers)
			{
				var res = func(text, type);

				if (res != null)
				{
					return res;
				}
			}

			if (type == typeof(DateTime))
			{
				return DateTime.Parse(text);
			}

			if (type == typeof(TimeSpan))
			{
				return TimeSpan.Parse(text);
			}

			//Nullable 타입 처리 (예: int?, double?, bool? 등)
			if (type.IsValueType && Nullable.GetUnderlyingType(type) != null)
			{
				// ReSharper disable once PossibleNullReferenceException
				return TypeDescriptor.GetConverter(type).ConvertFrom(text);
			}

			try
			{
				if (type.IsValueType)
				{
					return Convert.ChangeType(text, type);
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				throw;
			}

			return JsonConvert.DeserializeObject($"\"{text}\"", type);
		}

		private static string[] EnumerateCsvLine(string line)
		{
			// 정규식 출처: http://wiki.unity3d.com/index.php?title=CSVReader
			const string match = @"(((?<x>(?=[,\r\n]+))|""(?<x>([^""]|"""")+)""|(?<x>[^,\r\n]+)),?)";

			var matches = Regex.Matches(line, match, RegexOptions.ExplicitCapture);
			var ret = new string[matches.Count];

			for (var i = 0; i < matches.Count; i++)
			{
				ret[i] = matches[i].Groups[1].Value;
			}

			return ret;
		}

		private static string SerializedKeyValuePair(string text)
		{
			var split = text.Split(PairSplitChars);

			return $"{{\"Key\":\"{split[0].Trim()}\",\"Value\":\"{split[1].Trim()}\"}}";
		}

		private static bool TryGetKeyValuePair(string data, Type type, out object key, out object value,
											   params Func<string, Type, object>[] deserializers)
		{
			if (!type.IsValueType)
			{
				key = null;
				value = null;

				return false;
			}

			var fields = type.GetFields();
			var split = data.Split(PairSplitChars);

			if (type.IsGenericType && fields.Length == 2 && (fields[0].Name == "Key" || fields[0].Name == "Value1") &&
				(fields[1].Name == "Value" || fields[1].Name == "Value2"))
			{
				key = string.IsNullOrWhiteSpace(data) ? default : Parse(split[0], fields[0].FieldType, deserializers);
				value = string.IsNullOrWhiteSpace(data) ? default : Parse(split[1], fields[1].FieldType, deserializers);

				return true;
			}

			key = null;
			value = null;

			return false;
		}
	}
}