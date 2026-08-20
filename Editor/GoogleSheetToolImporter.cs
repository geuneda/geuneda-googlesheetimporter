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
		private const int MaxConcurrentDownloads = 6;
		private const int ProgressPollIntervalMs = 100;
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

		/// <summary>
		/// 전체 시트를 2단계로 임포트한다.
		/// 1단계: 동시 <see cref="MaxConcurrentDownloads"/>개 제한으로 CSV 를 병렬 다운로드.
		/// 2단계: 임포트 순서대로 적용하며, 에셋 파이프라인 갱신은
		/// <see cref="AssetDatabase.StartAssetEditing"/>/<see cref="AssetDatabase.StopAssetEditing"/> 로 한 번에 묶는다.
		/// </summary>
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
				var csvTexts = await DownloadAllAsync(importers, spreadsheetId, _cts.Token);

				if (_cts.IsCancellationRequested)
				{
					Debug.LogWarning("Google Sheet import cancelled by user.");
					return;
				}

				AssetDatabase.StartAssetEditing();

				try
				{
					for (var i = 0; i < importers.Count; i++)
					{
						var importer = importers[i];
						EditorUtility.DisplayProgressBar(
							"Importing Google Sheets",
							$"Applying ({i + 1}/{importers.Count}) {importer.Type.Name}",
							(float)i / importers.Count);

						if (csvTexts[i] == null)
						{
							failCount++;
							continue;
						}

						try
						{
							if (ApplyCsv(importer, csvTexts[i]))
							{
								successCount++;
							}
							else
							{
								failCount++;
							}
						}
						catch (Exception ex)
						{
							Debug.LogError($"Failed to apply {importer.Type.Name}: {ex.Message}");
							Debug.LogException(ex);
							failCount++;
						}
					}
				}
				finally
				{
					AssetDatabase.StopAssetEditing();
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

		/// <summary>
		/// 모든 임포터의 CSV 를 동시 개수 제한으로 병렬 다운로드한다.
		/// 반환 배열은 <paramref name="importers"/> 와 같은 인덱스이며, 실패한 항목은 null 이다.
		/// 진행 바에서 취소하면 토큰을 취소하고 진행 중인 요청이 끝나는 대로 반환한다.
		/// </summary>
		private static async Task<string[]> DownloadAllAsync(
			List<ImportData> importers, string spreadsheetId, CancellationToken cancellationToken)
		{
			var csvTexts = new string[importers.Count];
			var completed = 0;

			using (var semaphore = new SemaphoreSlim(MaxConcurrentDownloads))
			{
				var tasks = new Task[importers.Count];

				for (var i = 0; i < importers.Count; i++)
				{
					tasks[i] = DownloadSlotAsync(i);
				}

				var all = Task.WhenAll(tasks);

				while (!all.IsCompleted)
				{
					var cancelled = EditorUtility.DisplayCancelableProgressBar(
						"Importing Google Sheets",
						$"Downloading ({completed}/{importers.Count})",
						(float)completed / importers.Count);

					if (cancelled && !_cts.IsCancellationRequested)
					{
						_cts.Cancel();
					}

					await EditorDelay(ProgressPollIntervalMs);
				}

				async Task DownloadSlotAsync(int index)
				{
					await semaphore.WaitAsync();

					try
					{
						if (cancellationToken.IsCancellationRequested)
						{
							return;
						}

						csvTexts[index] = await DownloadWithRetryAsync(importers[index], spreadsheetId, cancellationToken);
					}
					finally
					{
						completed++;
						semaphore.Release();
					}
				}
			}

			return csvTexts;
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
				var csvText = await DownloadWithRetryAsync(data, spreadsheetId, _cts.Token);

				if (csvText != null)
				{
					ApplyCsv(data, csvText);
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
			}
		}

		/// <summary>
		/// 429/5xx 등 일시 오류에 지수 백오프로 재시도하며 CSV 원문을 다운로드한다. 실패 시 null.
		/// </summary>
		private static async Task<string> DownloadWithRetryAsync(
			ImportData data, string spreadsheetId, CancellationToken cancellationToken)
		{
			var delayMs = InitialDelayMs;

			for (var attempt = 0; attempt <= MaxRetryCount; attempt++)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					return null;
				}

				if (attempt > 0)
				{
					Debug.Log($"Retrying {data.Type.Name} (attempt {attempt + 1}/{MaxRetryCount + 1}) after {delayMs}ms delay...");
					await EditorDelay(delayMs);
					delayMs = Math.Min(delayMs * 2, MaxDelayMs);
				}

				var (result, csvText) = await DownloadAsync(data, spreadsheetId);

				switch (result)
				{
					case RequestResult.Success:
						return csvText;
					case RequestResult.RateLimited:
						continue;
					case RequestResult.Failed:
						return null;
				}
			}

			Debug.LogError($"Failed to import {data.Type.Name} after {MaxRetryCount + 1} attempts. Skipping.");
			return null;
		}

		private static async Task<(RequestResult, string)> DownloadAsync(ImportData data, string spreadsheetId)
		{
			if (string.IsNullOrWhiteSpace(data.Importer?.GoogleSheetUrl))
			{
				Debug.LogError($"GoogleSheetUrl is null or empty for {data.Type.Name}. Skipping.");
				return (RequestResult.Failed, null);
			}

			var googleSheetUrl = data.Importer.GoogleSheetUrl;
			var idPrefixIndex = googleSheetUrl.IndexOf("/d/", StringComparison.Ordinal);
			var editIndex = googleSheetUrl.IndexOf("/edit", StringComparison.Ordinal);

			if (idPrefixIndex < 0 || editIndex < 0)
			{
				Debug.LogError($"Invalid Google Sheet URL format for {data.Type.Name}: {googleSheetUrl}");
				return (RequestResult.Failed, null);
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

					if (responseCode == 401 || responseCode == 403 || responseCode == 429 || responseCode >= 500)
					{
						Debug.LogWarning($"Request for {data.Type.Name} returned {responseCode}: {request.error}. Will retry.");
						return (RequestResult.RateLimited, null);
					}

					Debug.LogError($"Failed to import {data.Type.Name}: {request.error} (HTTP {responseCode})");
					return (RequestResult.Failed, null);
				}

				return (RequestResult.Success, request.downloadHandler.text);
			}
		}

		/// <summary>
		/// 다운로드한 CSV 원문을 파싱해 임포터에 적용하고 원본 CSV 를 역동기화한다.
		/// </summary>
		/// <returns>CSV 형식이 아니면 false</returns>
		private static bool ApplyCsv(ImportData data, string csvText)
		{
			var values = CsvParser.ConvertCsv(csvText);

			if (values.Count == 0)
			{
				Debug.LogWarning($"The return sheet for {data.Type.Name} was not in CSV format:\n{csvText}");
				return false;
			}

			data.Importer.Import(values);
			SyncCsvFile(data.Importer, csvText, data.Type.Name);
			Debug.Log($"Finished importing google sheet data from {data.Type.Name}");
			return true;
		}

		/// <summary>
		/// 임포터가 <see cref="ICsvSyncTarget"/> 를 구현하면, 다운로드한 원본 CSV 를 LF 로 정규화하여
		/// 지정 경로에 기록하고 에셋으로 재임포트한다. 기록 실패는 경고만 남기고 임포트 흐름을 막지 않는다.
		/// </summary>
		private static void SyncCsvFile(IGoogleSheetConfigsImporter importer, string csvText, string importerName)
		{
			if (importer is not ICsvSyncTarget syncTarget)
			{
				return;
			}

			var syncPath = syncTarget.CsvSyncPath;

			if (string.IsNullOrWhiteSpace(syncPath))
			{
				return;
			}

			try
			{
				var normalized = NormalizeCsv(csvText);
				System.IO.File.WriteAllText(syncPath, normalized);
				AssetDatabase.ImportAsset(syncPath);
				Debug.Log($"[GoogleSheetImporter] CSV synced: {syncPath}");
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[GoogleSheetImporter] CSV sync failed for {importerName} ({syncPath}): {e.Message}");
			}
		}

		/// <summary>
		/// 구글 시트 export(CRLF)를 버전 관리 친화적인 LF + 단일 트레일링 개행으로 정규화한다.
		/// </summary>
		private static string NormalizeCsv(string csvText)
		{
			if (string.IsNullOrEmpty(csvText))
			{
				return "\n";
			}

			var lf = csvText.Replace("\r\n", "\n").Replace("\r", "\n");
			return lf.TrimEnd('\n') + "\n";
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