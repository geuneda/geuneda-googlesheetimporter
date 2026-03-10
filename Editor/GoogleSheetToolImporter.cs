using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Geuneda.GoogleSheetImporter;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.Networking;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.GoogleSheetImporter
{
	/// <summary>
	/// 구글 시트 임포트 도구 <seealso cref="GoogleSheetImporter"/>의 인스펙터 UI를 커스터마이즈합니다
	/// </summary>
	[CustomEditor(typeof(GoogleSheetImporter))]
	public class GoogleSheetToolImporter : Editor
	{
		private const int MaxRetryCount = 5;
		private const int InitialDelayMs = 1000;
		private const int MaxDelayMs = 60000;
		private const int RequestIntervalMs = 200;
		private const string IsImportingSessionKey = "GoogleSheetToolImporter_IsImporting";

		private static List<ImportData> _importers;
		private static CancellationTokenSource _cts;

		private static readonly GUIContent SpreadsheetIdGuiContent = new GUIContent(
			"Spreadsheet ID (optional)",
			"(Optional) Put the Google Spreadsheet Id to replace from the one set in SheetImporter. " +
			"Will use the one set in the SheetImporter by default if not set or empty. " +
			"Use this option if you duplicate the Google Sheet file for testing purposes.");

		private static bool IsImporting
		{
			get => SessionState.GetBool(IsImportingSessionKey, false);
			set => SessionState.SetBool(IsImportingSessionKey, value);
		}

		[InitializeOnLoadMethod]
		private static void OnDomainReload()
		{
			if (!IsImporting)
			{
				return;
			}

			Debug.LogWarning("Google Sheet import was interrupted by domain reload.");
			IsImporting = false;
			EditorUtility.ClearProgressBar();
		}

		[MenuItem("Tools/GoogleSheet Importer/Import Google Sheet Data")]
		private static void ImportAllGoogleSheetData()
		{
			if (IsImporting)
			{
				Debug.LogWarning("Google Sheet import is already in progress.");
				return;
			}

			_importers = GetAllImporters();
			ImportAllSheetsAsync(_importers, "");
		}

		[DidReloadScripts]
		public static void OnCompileScripts()
		{
			_importers = GetAllImporters();
		}

		/// <inheritdoc />
		public override void OnInspectorGUI()
		{
			if (_importers == null)
			{
				return;
			}

			var typeCheck = typeof(IScriptableObjectImporter);
			var tool = (GoogleSheetImporter)target;

			tool.ReplaceSpreadsheetId = EditorGUILayout.TextField(SpreadsheetIdGuiContent, tool.ReplaceSpreadsheetId);

			EditorGUI.BeginDisabledGroup(IsImporting);

			if (GUILayout.Button(IsImporting ? "Importing..." : "Import All Sheets"))
			{
				ImportAllSheetsAsync(_importers, tool.ReplaceSpreadsheetId);
			}

			EditorGUILayout.Space();

			foreach (var importer in _importers)
			{
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.PrefixLabel(importer.Type.Name);
				if (GUILayout.Button("Import"))
				{
					ImportSingleSheetAsync(importer, tool.ReplaceSpreadsheetId);
				}
				if (typeCheck.IsAssignableFrom(importer.Type) && GUILayout.Button("Select Object"))
				{
					var scriptableObjectType = (importer.Importer as IScriptableObjectImporter)?.ScriptableObjectType;
					var assets = AssetDatabase.FindAssets($"t:{scriptableObjectType?.Name}");
					var scriptableObject = assets.Length > 0 ?
											   AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(assets[0]), scriptableObjectType) :
											   CreateInstance(scriptableObjectType);

					if (assets.Length == 0 && scriptableObjectType != null)
					{
						AssetDatabase.CreateAsset(scriptableObject, $"Assets/{scriptableObjectType.Name}.asset");
						AssetDatabase.SaveAssets();
						AssetDatabase.Refresh();
					}

					Selection.activeObject = scriptableObject;
				}
				EditorGUILayout.EndHorizontal();
			}

			EditorGUI.EndDisabledGroup();
		}

		private static async void ImportAllSheetsAsync(List<ImportData> importers, string spreadsheetId)
		{
			if (IsImporting)
			{
				Debug.LogWarning("Google Sheet import is already in progress.");
				return;
			}

			IsImporting = true;
			_cts = new CancellationTokenSource();
			var successCount = 0;
			var failCount = 0;

			try
			{
				for (var i = 0; i < importers.Count; i++)
				{
					if (_cts.IsCancellationRequested)
					{
						Debug.LogWarning("Google Sheet import cancelled by user.");
						break;
					}

					var importer = importers[i];
					var progress = (float)i / importers.Count;
					var cancelled = EditorUtility.DisplayCancelableProgressBar(
						"Importing Google Sheets",
						$"({i + 1}/{importers.Count}) {importer.Type.Name}",
						progress);

					if (cancelled)
					{
						_cts.Cancel();
						Debug.LogWarning("Google Sheet import cancelled by user.");
						break;
					}

					var success = await ImportSheetWithRetryAsync(importer, spreadsheetId, _cts.Token);

					if (success)
					{
						successCount++;
					}
					else
					{
						failCount++;
					}

					if (i < importers.Count - 1)
					{
						await EditorDelay(RequestIntervalMs);
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogException(ex);
			}
			finally
			{
				EditorUtility.ClearProgressBar();
				IsImporting = false;
				_cts?.Dispose();
				_cts = null;

				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();

				Debug.Log($"Google Sheet import completed. Success: {successCount}, Failed: {failCount}, Total: {importers.Count}");
			}
		}

		private static async void ImportSingleSheetAsync(ImportData data, string spreadsheetId)
		{
			if (IsImporting)
			{
				Debug.LogWarning("Google Sheet import is already in progress.");
				return;
			}

			IsImporting = true;
			_cts = new CancellationTokenSource();

			try
			{
				EditorUtility.DisplayProgressBar("Importing Google Sheet", data.Type.Name, 0.5f);
				await ImportSheetWithRetryAsync(data, spreadsheetId, _cts.Token);
			}
			catch (Exception ex)
			{
				Debug.LogException(ex);
			}
			finally
			{
				EditorUtility.ClearProgressBar();
				IsImporting = false;
				_cts?.Dispose();
				_cts = null;

				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
			}
		}

		private static async Task<bool> ImportSheetWithRetryAsync(
			ImportData data, string spreadsheetId, CancellationToken cancellationToken)
		{
			var delayMs = InitialDelayMs;

			for (var attempt = 0; attempt <= MaxRetryCount; attempt++)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					return false;
				}

				if (attempt > 0)
				{
					Debug.Log($"Retrying {data.Type.Name} (attempt {attempt + 1}/{MaxRetryCount + 1}) after {delayMs}ms delay...");
					await EditorDelay(delayMs);
					delayMs = Math.Min(delayMs * 2, MaxDelayMs);
				}

				var result = await SendRequestAsync(data, spreadsheetId);

				switch (result)
				{
					case RequestResult.Success:
						return true;
					case RequestResult.RateLimited:
						continue;
					case RequestResult.Failed:
						return false;
				}
			}

			Debug.LogError($"Failed to import {data.Type.Name} after {MaxRetryCount + 1} attempts. Skipping.");
			return false;
		}

		private static async Task<RequestResult> SendRequestAsync(ImportData data, string spreadsheetId)
		{
			if (string.IsNullOrWhiteSpace(data.Importer?.GoogleSheetUrl))
			{
				Debug.LogError($"GoogleSheetUrl is null or empty for {data.Type.Name}. Skipping.");
				return RequestResult.Failed;
			}

			var googleSheetUrl = data.Importer.GoogleSheetUrl;
			var idPrefixIndex = googleSheetUrl.IndexOf("/d/", StringComparison.Ordinal);
			var editIndex = googleSheetUrl.IndexOf("/edit", StringComparison.Ordinal);

			if (idPrefixIndex < 0 || editIndex < 0)
			{
				Debug.LogError($"Invalid Google Sheet URL format for {data.Type.Name}: {googleSheetUrl}");
				return RequestResult.Failed;
			}

			var indexStart = idPrefixIndex + 3;
			var indexCount = editIndex - indexStart;
			var url = googleSheetUrl.Replace("edit#", "export?format=csv&");
			var finalUrl = string.IsNullOrWhiteSpace(spreadsheetId)
				? url
				: url.Remove(indexStart, indexCount).Insert(indexStart, spreadsheetId);

			using (var request = UnityWebRequest.Get(finalUrl))
			{
				await WaitForAsyncOperation(request.SendWebRequest());

				if (request.result != UnityWebRequest.Result.Success)
				{
					var responseCode = request.responseCode;

					if (responseCode == 429 || responseCode >= 500)
					{
						Debug.LogWarning($"Request for {data.Type.Name} returned {responseCode}: {request.error}. Will retry.");
						return RequestResult.RateLimited;
					}

					Debug.LogError($"Failed to import {data.Type.Name}: {request.error} (HTTP {responseCode})");
					return RequestResult.Failed;
				}

				var values = CsvParser.ConvertCsv(request.downloadHandler.text);

				if (values.Count == 0)
				{
					Debug.LogWarning($"The return sheet was not in CSV format:\n{request.downloadHandler.text}");
					return RequestResult.Failed;
				}

				data.Importer.Import(values);
				Debug.Log($"Finished importing google sheet data from {data.Type.Name}");
				return RequestResult.Success;
			}
		}

		private static List<ImportData> GetAllImporters()
		{
			var importerInterface = typeof(IGoogleSheetConfigsImporter);
			var importerAttribute = typeof(GoogleSheetImportOrderAttribute);
			var importers = new List<ImportData>();

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type[] types;

				try
				{
					types = assembly.GetTypes();
				}
				catch (ReflectionTypeLoadException ex)
				{
					types = Array.FindAll(ex.Types, t => t != null);
				}

				foreach (var type in types)
				{
					if (type.IsAbstract || type.IsInterface || !importerInterface.IsAssignableFrom(type))
					{
						continue;
					}

					var importOrder = int.MaxValue;
					var attribute = type.GetCustomAttribute(importerAttribute);

					if (attribute != null)
					{
						importOrder = ((GoogleSheetImportOrderAttribute)attribute).ImportOrder;
					}

					try
					{
						var instance = Activator.CreateInstance(type) as IGoogleSheetConfigsImporter;

						if (instance == null)
						{
							continue;
						}

						importers.Add(new ImportData
						{
							Type = type,
							Importer = instance,
							ImportOrder = importOrder
						});
					}
					catch (Exception ex)
					{
						Debug.LogWarning($"Failed to create importer instance for {type.Name}: {ex.Message}");
					}
				}
			}

			importers.Sort((elem1, elem2) =>
			{
				var orderCompare = elem1.ImportOrder.CompareTo(elem2.ImportOrder);

				return orderCompare != 0
					? orderCompare
					: string.Compare(elem1.Type.Name, elem2.Type.Name, StringComparison.Ordinal);
			});

			return importers;
		}

		private static Task WaitForAsyncOperation(UnityEngine.AsyncOperation operation)
		{
			var tcs = new TaskCompletionSource<bool>();

			operation.completed += _ => tcs.TrySetResult(true);

			if (operation.isDone)
			{
				tcs.TrySetResult(true);
			}

			return tcs.Task;
		}

		private static Task EditorDelay(int milliseconds)
		{
			var tcs = new TaskCompletionSource<bool>();
			var targetTime = EditorApplication.timeSinceStartup + milliseconds / 1000.0;

			void Check()
			{
				if (EditorApplication.timeSinceStartup >= targetTime)
				{
					tcs.TrySetResult(true);
				}
				else
				{
					EditorApplication.delayCall += Check;
				}
			}

			EditorApplication.delayCall += Check;
			return tcs.Task;
		}

		private sealed class ImportData
		{
			public Type Type { get; set; }
			public IGoogleSheetConfigsImporter Importer { get; set; }
			public int ImportOrder { get; set; }
		}

		private enum RequestResult
		{
			Success,
			RateLimited,
			Failed
		}
	}
}