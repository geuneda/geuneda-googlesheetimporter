using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.GoogleSheetImporter
{
	/// <summary>
	/// 전체 또는 특정 구글 시트 데이터를 임포트하는 ScriptableObject 도구
	/// </summary>
	[CreateAssetMenu(fileName = "GoogleSheetImporter", menuName = "ScriptableObjects/Editor/GoogleSheetImporter")]
	public class GoogleSheetImporter : ScriptableObject
	{
		public string ReplaceSpreadsheetId;

		[MenuItem("Tools/GoogleSheet Importer/Select GoogleSheetImporter.asset")]
		private static void SelectSheetImporter()
		{
			var assets = AssetDatabase.FindAssets($"t:{nameof(GoogleSheetImporter)}");
			var scriptableObject = assets.Length > 0 ?
									   AssetDatabase.LoadAssetAtPath<GoogleSheetImporter>(AssetDatabase.GUIDToAssetPath(assets[0])) :
									   CreateInstance<GoogleSheetImporter>();

			if (assets.Length == 0)
			{
				AssetDatabase.CreateAsset(scriptableObject, $"Assets/{nameof(GoogleSheetImporter)}.asset");
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
			}

			Selection.activeObject = scriptableObject;
		}
	}
}