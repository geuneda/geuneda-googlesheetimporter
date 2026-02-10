using System;
using System.Collections.Generic;
using Geuneda.GameData;
using Geuneda.GoogleSheetImporter;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.GoogleSheetImporter
{
	/// <summary>
	/// 단일 ScriptableObject를 임포트하기 위한 인터페이스.
	/// 모든 처리는 에디터 타임에 수행됩니다.
	/// </summary>
	/// <remarks>
	/// 단일 구글 시트를 ScriptableObject로 임포트하려면 이 인터페이스를 구현하세요.
	/// </remarks>
	public interface IScriptableObjectImporter
	{
		/// <summary>
		/// 저장될 ScriptableObject의 타입. 런타임에 ScriptableObject 타입을 캐스팅하기 위한 헬퍼
		/// </summary>
		Type ScriptableObjectType { get; }
	}

	/// <summary>
	/// 여러 설정을 하나의 ScriptableObject에 로드하는 임포터의 제네릭 구현.
	/// 단일 구글 시트를 임포트하려면 이 인터페이스를 구현하세요.
	/// 모든 처리는 에디터 타임에 수행됩니다.
	/// </summary>
	public interface IGoogleSheetConfigsImporter
	{
		/// <summary>
		/// 전체 구글 시트 URL
		/// </summary>
		string GoogleSheetUrl { get; }

		/// <summary>
		/// <seealso cref="CsvParser.ConvertCsv"/>에서 처리된 <paramref name="data"/>를 게임에 임포트합니다
		/// </summary>
		// ReSharper disable once ParameterTypeCanBeEnumerable.Global
		void Import(List<Dictionary<string, string>> data);
	}

	/// <inheritdoc cref="IGoogleSheetConfigsImporter"/>
	/// <remarks>
	/// 임포트된 구글 시트를 <typeparamref name="TScriptableObject"/> 타입의 ScriptableObject에 저장합니다
	/// </remarks>
	public abstract class GoogleSheetScriptableObjectImportContainer<TScriptableObject> :
		IScriptableObjectImporter, IGoogleSheetConfigsImporter where TScriptableObject : ScriptableObject
	{
		/// <inheritdoc />
		public abstract string GoogleSheetUrl { get; }

		/// <inheritdoc />
		public Type ScriptableObjectType => typeof(TScriptableObject);

		/// <inheritdoc />
		public void Import(List<Dictionary<string, string>> data)
		{
			var type = typeof(TScriptableObject);
			var assets = AssetDatabase.FindAssets($"t:{type.Name}");
			var scriptableObject = assets.Length > 0
									   ? AssetDatabase.LoadAssetAtPath<TScriptableObject>(AssetDatabase.GUIDToAssetPath(assets[0]))
									   : ScriptableObject.CreateInstance<TScriptableObject>();

			if (assets.Length == 0)
			{
				AssetDatabase.CreateAsset(scriptableObject, $"Assets/{type.Name}.asset");
			}

			OnImport(scriptableObject, data);

			EditorUtility.SetDirty(scriptableObject);
			OnImportComplete(scriptableObject);
		}

		protected abstract void OnImport(TScriptableObject scriptableObject, List<Dictionary<string, string>> data);

		protected virtual void OnImportComplete(TScriptableObject scriptableObject) { }
	}

	/// <inheritdoc cref="IGoogleSheetConfigsImporter"/>
	/// <remarks>
	/// 데이터 항목당 1행을 임포트합니다. 즉, 각 행이 1개의 <typeparamref name="TConfig"/> 항목을 나타내며
	/// 여러 <typeparamref name="TConfig"/>를 임포트합니다
	/// </remarks>
	public abstract class GoogleSheetConfigsImporter<TConfig, TScriptableObject> :
		GoogleSheetScriptableObjectImportContainer<TScriptableObject>
		where TConfig : struct
		where TScriptableObject : ScriptableObject, IConfigsContainer<TConfig>
	{
		protected override void OnImport(TScriptableObject scriptableObject, List<Dictionary<string, string>> data)
		{
			var configs = new List<TConfig>();

			foreach (var row in data)
			{
				configs.Add(Deserialize(row));
			}

			scriptableObject.Configs = configs;
		}

		protected virtual TConfig Deserialize(Dictionary<string, string> data)
		{
			return CsvParser.DeserializeTo<TConfig>(data);
		}
	}

	/// <inheritdoc cref="IGoogleSheetConfigsImporter"/>
	/// <remarks>
	/// 전체 시트 1개를 하나의 <typeparamref name="TConfig"/>로 임포트합니다. 즉, 각 행이
	/// Key/Value 쌍으로 표현되는 <typeparamref name="TConfig"/>의 서로 다른 필드에 매핑됩니다.
	/// </remarks>
	public abstract class GoogleSheetSingleConfigImporter<TConfig, TScriptableObject> :
		GoogleSheetScriptableObjectImportContainer<TScriptableObject>
		where TConfig : struct
		where TScriptableObject : ScriptableObject, ISingleConfigContainer<TConfig>
	{
		protected override void OnImport(TScriptableObject scriptableObject, List<Dictionary<string, string>> data)
		{
			scriptableObject.Config = Deserialize(data);
		}

		protected abstract TConfig Deserialize(List<Dictionary<string, string>> data);
	}

	/// <inheritdoc cref="GoogleSheetSingleConfigImporter{TConfig,TScriptableObject}"/>
	/// <remarks>
	/// <see cref="GoogleSheetSingleConfigImporter{TConfig,TScriptableObject}"/>와 동일한 방식으로 동작하지만
	/// 설정 내 리스트 임포트를 지원합니다.
	/// </remarks>
	public abstract class GoogleSheetSingleConfigSubListImporter<TConfig, TScriptableObject> :
		GoogleSheetSingleConfigImporter<TConfig, TScriptableObject>
		where TConfig : struct
		where TScriptableObject : ScriptableObject, ISingleConfigContainer<TConfig>
	{
		protected override TConfig Deserialize(List<Dictionary<string, string>> data)
		{
			var config = new TConfig() as object;
			var type = typeof(TConfig);

			for (var i = 0; i < data.Count; i++)
			{
				var row = data[i];
				var fieldName = row["Key"];
				var isSubList = fieldName.EndsWith(CsvParser.SUB_LIST_SUFFIX);
				if (isSubList)
				{
					fieldName = fieldName.Replace(CsvParser.SUB_LIST_SUFFIX, "");
				}

				var field = type.GetField(fieldName);

				if (field == null)
				{
					continue;
				}

				object value;

				if (isSubList)
				{
					value = CsvParser.DeserializeSubList(data, i, field.FieldType, field.Name, GetDeserializers());
				}
				else
				{
					value = CsvParser.DeserializeObject(row["Value"], field.FieldType, GetDeserializers());
				}


				field.SetValue(config, value);
			}

			return (TConfig)config;
		}

		protected abstract Func<string, Type, object>[] GetDeserializers();
	}
}