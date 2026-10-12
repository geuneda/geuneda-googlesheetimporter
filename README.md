# Geuneda GoogleSheet Importer

Google 스프레드시트를 CSV 로 내려받아 파싱하고, 결과를 ScriptableObject 설정 데이터로 저장하는 Unity 에디터 도구입니다.

- 패키지명: `com.geuneda.googlesheetimporter`
- 네임스페이스: `Geuneda.GoogleSheetImporter`(런타임, `CsvParser`), `GeunedaEditor.GoogleSheetImporter`(에디터, 임포터)
- 어셈블리: `Geuneda.GoogleSheetImporter`, `Geuneda.GoogleSheetImporter.Editor`

## 설치

이 패키지는 `com.geuneda.gamedata` 에 의존하고, gamedata 는 다시 `com.cysharp.unitask` 에 의존합니다. 둘 다 Unity 레지스트리에 없으므로 `Packages/manifest.json` 에 함께 추가합니다.

```json
{
  "dependencies": {
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
    "com.geuneda.gamedata": "https://github.com/geuneda/geuneda-gamedata.git",
    "com.geuneda.googlesheetimporter": "https://github.com/geuneda/geuneda-googlesheetimporter.git"
  }
}
```

Package Manager 의 **+ > Add package from git URL...** 에 `https://github.com/geuneda/geuneda-googlesheetimporter.git` 을 넣어도 됩니다. 특정 버전에 고정하려면 URL 끝에 `#<태그>` 를 붙입니다(레포에 실제로 있는 태그만 쓸 수 있습니다). Newtonsoft.Json 은 Unity 레지스트리에서 자동으로 설치됩니다.

## 빠른 시작

시트(첫 행이 헤더, 헤더 이름은 필드 이름과 같아야 함):

| Name | Health | Speed | $Memo |
|------|--------|-------|-------|
| Slime | 10 | 1.5 | 무시되는 메모 열 |
| Goblin | 30 | 2.0 | |

시트는 "링크가 있는 모든 사용자" 보기 권한으로 공유해야 합니다.

1. 런타임 데이터 타입과 컨테이너를 만듭니다. ScriptableObject 는 클래스 이름과 같은 파일에 둡니다.

```csharp
// Assets/Scripts/EnemyConfig.cs
using System;

[Serializable]
public struct EnemyConfig
{
    public string Name;
    public int Health;
    public float Speed;
}
```

```csharp
// Assets/Scripts/EnemyConfigs.cs
using System.Collections.Generic;
using Geuneda.DataExtensions;
using UnityEngine;

public class EnemyConfigs : ScriptableObject, IConfigsContainer<EnemyConfig>
{
    [SerializeField] private List<EnemyConfig> _configs = new();

    public List<EnemyConfig> Configs
    {
        get => _configs;
        set => _configs = value;
    }
}
```

2. 에디터 폴더에 임포터를 만듭니다. URL 은 브라우저 주소창의 `.../edit#gid=<탭 ID>` 형식이어야 합니다.

```csharp
// Assets/Editor/EnemyConfigsImporter.cs
using GeunedaEditor.GoogleSheetImporter;

public class EnemyConfigsImporter : GoogleSheetConfigsImporter<EnemyConfig, EnemyConfigs>
{
    public override string GoogleSheetUrl =>
        "https://docs.google.com/spreadsheets/d/<SPREADSHEET_ID>/edit#gid=<GID>";
}
```

3. `Tools > GoogleSheet Importer > Import Google Sheet Data` 를 실행합니다. 프로젝트에 `EnemyConfigs` 에셋이 없으면 `Assets/EnemyConfigs.asset` 이 만들어지고 `Configs` 가 시트 내용으로 채워집니다.

## 핵심 개념

### 임포터 종류

에디터 어셈블리(`GeunedaEditor.GoogleSheetImporter`)에 있습니다. 컨테이너 인터페이스(`IConfigsContainer<T>`, `ISingleConfigContainer<T>`)는 gamedata 의 `Geuneda.DataExtensions` 에 있습니다.

| 타입 | 용도 |
|------|------|
| `GoogleSheetConfigsImporter<TConfig, TScriptableObject>` | 1행 = `TConfig` 1개. 컨테이너는 `IConfigsContainer<TConfig>`. 행 변환을 바꾸려면 `Deserialize(Dictionary<string, string>)` 를 재정의 |
| `GoogleSheetSingleConfigImporter<TConfig, TScriptableObject>` | 시트 전체 = `TConfig` 1개(Key/Value 행). 컨테이너는 `ISingleConfigContainer<TConfig>`. `Deserialize(List<Dictionary<string, string>>)` 를 직접 구현 |
| `GoogleSheetSingleConfigSubListImporter<TConfig, TScriptableObject>` | 위와 같지만 `Deserialize` 가 구현되어 있고 서브 리스트(`필드명[]`)를 지원. `GetDeserializers()` 만 구현 |
| `GoogleSheetScriptableObjectImportContainer<TScriptableObject>` | 위 세 클래스의 공통 기반. `OnImport` 를 직접 구현할 때 사용 |
| `IGoogleSheetConfigsImporter` | 가장 단순한 계약(`GoogleSheetUrl`, `Import(data)`). ScriptableObject 가 필요 없는 임포터(예: 코드 생성) |
| `ICsvSyncTarget` | 선택. 구현하면 다운로드한 원본 CSV 를 `CsvSyncPath` 에 기록 |
| `[GoogleSheetImportOrder(int)]` | 임포트 순서. 작을수록 먼저, 없으면 `int.MaxValue`, 같으면 타입 이름 순 |

`TConfig` 는 `struct` 여야 합니다. 임포터는 리플렉션으로 자동 수집되므로 추상 클래스가 아니고 public 매개변수 없는 생성자가 있어야 합니다.

ScriptableObject 기반 임포터는 `AssetDatabase.FindAssets("t:<타입 이름>")` 로 찾은 첫 에셋에 저장하고, 없으면 `Assets/<타입 이름>.asset` 을 만듭니다. 저장 후 `OnImportComplete(TScriptableObject)` 가 호출되므로 후처리가 필요하면 재정의합니다.

### 단일 설정(Key/Value) 시트

`GoogleSheetSingleConfigImporter` 는 `Deserialize` 를 직접 구현합니다. `Key` 열에 필드 이름, `Value` 열에 값을 둡니다.

```csharp
using System.Collections.Generic;
using Geuneda.GoogleSheetImporter;
using GeunedaEditor.GoogleSheetImporter;

public class GameConfigsImporter : GoogleSheetSingleConfigImporter<GameConfig, GameConfigs>
{
    public override string GoogleSheetUrl =>
        "https://docs.google.com/spreadsheets/d/<SPREADSHEET_ID>/edit#gid=<GID>";

    protected override GameConfig Deserialize(List<Dictionary<string, string>> data)
    {
        var config = new GameConfig() as object;
        var type = typeof(GameConfig);

        foreach (var row in data)
        {
            var field = type.GetField(row["Key"]);

            if (field == null)
            {
                continue;
            }

            field.SetValue(config, CsvParser.DeserializeObject(row["Value"], field.FieldType));
        }

        return (GameConfig)config;
    }
}
```

`GameConfig` 는 struct 이고, `GameConfigs` 는 `ScriptableObject, ISingleConfigContainer<GameConfig>` 를 구현합니다.

### 서브 리스트

`GoogleSheetSingleConfigSubListImporter` 는 `Key` 가 `필드명[]` 인 행을 서브 리스트의 헤더로, 바로 아래 `Key` 가 `#` 인 행들을 데이터로 읽습니다. 헤더 행의 각 열 값이 하위 타입의 필드 이름이 됩니다. 대상 필드는 `List<T>` 여야 합니다.

| $Memo | Key | Value | Extra |
|-------|-----|-------|-------|
| | MaxLevel | 50 | |
| | Rewards[] | Id | Amount |
| | # | 1 | 100 |
| | # | 2 | 200 |

주의: `CsvParser.ConvertCsv` 는 줄이 `#` 으로 시작하면 설명 행으로 보고 버립니다. `Key` 가 첫 열이면 `#` 데이터 행이 모두 사라지므로, 위처럼 `Key` 앞에 `$` 주석 열을 두어 줄이 `#` 으로 시작하지 않게 합니다.

### CSV 규칙

- 행 구분자는 `\r\n` 입니다(Google 시트 CSV 내보내기 형식). `\n` 만 있는 텍스트는 빈 결과가 됩니다.
- 첫 행은 헤더입니다. 헤더가 `$` 로 시작하는 열은 무시됩니다.
- `#` 으로 시작하는 줄은 설명 행으로 건너뜁니다.
- 셀 값은 앞뒤 공백이 제거되고, 값이 모자란 열은 빈 문자열로 채워집니다.

### 셀 값 파싱(`CsvParser`)

`CsvParser.DeserializeTo<T>` 는 헤더 이름과 같은 public 필드에 값을 넣습니다. 헤더가 없는 필드는 기본값으로 남고, `[ParseIgnore]` 가 붙은 필드는 건너뜁니다.

| 필드 타입 | 셀 형식 |
|-----------|---------|
| `string`, enum, 숫자/`bool` 등 값 타입 | 그대로. 값 타입은 `Convert.ChangeType`(현재 컬처) |
| `DateTime`, `TimeSpan` | `DateTime.Parse`, `TimeSpan.Parse` |
| `int?` 등 `Nullable<T>` | 빈 셀은 `null` |
| `List<T>` | `1,2,3`, `[1,2]{3}(4)` 처럼 `, ( ) [ ] { }` 로 구분 |
| `Dictionary<K, V>`, `UnitySerializedDictionary<K, V>` 파생 | `a:1,b:2`(쌍 구분자 `: < > = \|`) 또는 `a,1,b,2`. 빈 셀은 `null` |
| 필드가 `Key`/`Value` 또는 `Value1`/`Value2` 두 개인 제네릭 구조체(예: `StructPair<K, V>`) | `1:2` |
| 그 밖의 참조 타입 | 셀 텍스트를 JSON 문자열로 감싸 `JsonConvert.DeserializeObject` |

Key/Value 구조체는 `(key, value)` 생성자가 있어야 합니다. `System.Collections.Generic.KeyValuePair<K, V>` 는 public 필드가 없어서 이 규칙에 해당하지 않습니다.

그 밖의 타입은 `Func<string, Type, object>` 역직렬화기를 넘겨 처리합니다. `null` 을 반환하면 다음 단계로 넘어갑니다. 역직렬화기는 string, enum, Key/Value 구조체 판별 다음에 실행되고, 배열/딕셔너리의 각 요소에도 적용됩니다.

```csharp
using System;
using System.Collections.Generic;
using Geuneda.GoogleSheetImporter;
using UnityEngine;

public struct SpawnConfig
{
    public string Id;
    public Vector2 Position; // 셀 형식: "1.5;2"
}

public static class SpawnCsv
{
    public static SpawnConfig ParseFirstRow(string csvText)
    {
        List<Dictionary<string, string>> rows = CsvParser.ConvertCsv(csvText);
        return CsvParser.DeserializeTo<SpawnConfig>(rows[0], ParseVector2);
    }

    private static object ParseVector2(string text, Type type)
    {
        if (type != typeof(Vector2))
        {
            return null;
        }

        var parts = text.Split(';');
        return new Vector2(float.Parse(parts[0]), float.Parse(parts[1]));
    }
}
```

`T[]` 배열 필드는 쓰지 마세요. 배열도 `List<T>` 로 파싱되어 필드에 넣을 때 예외가 납니다.

값 타입 필드에 빈 셀이 있으면 예외가 납니다(숫자는 `FormatException`, enum 은 `ArgumentException`). 비어 있을 수 있는 값은 `Nullable<T>` 를 쓰거나 역직렬화기로 처리합니다.

### 임포트 실행

- 전체 임포트: `Tools > GoogleSheet Importer > Import Google Sheet Data`, 또는 `GoogleSheetImporter` 에셋 인스펙터의 **Import All Sheets**.
- 개별 임포트: 인스펙터에서 임포터별 **Import** 버튼. **Select Object** 는 저장 대상 ScriptableObject 를 선택(없으면 생성)합니다.
- 설정 에셋: `Tools > GoogleSheet Importer > Select GoogleSheetImporter.asset`(없으면 `Assets/GoogleSheetImporter.asset` 생성) 또는 `Create > ScriptableObjects > Editor > GoogleSheetImporter`.
- `ReplaceSpreadsheetId` 를 채우면 모든 URL 의 스프레드시트 ID 를 그 값으로 바꿔 받습니다(테스트용 복제 시트). 인스펙터 버튼에만 적용되고 메뉴 실행에는 적용되지 않습니다.

전체 임포트는 최대 6개를 동시에 다운로드한 뒤 임포트 순서대로 적용합니다. 다운로드 중에는 진행 바에서 취소할 수 있습니다. HTTP 401/403/429/5xx 는 지수 백오프(1초부터 최대 60초)로 최대 6회까지 시도하고, 그 밖의 오류나 CSV 가 아닌 응답은 해당 시트만 실패로 처리합니다.

### 원본 CSV 역동기화(`ICsvSyncTarget`)

임포터가 `ICsvSyncTarget` 도 구현하면 임포트에 성공한 직후 다운로드한 CSV 를 LF 줄바꿈으로 정규화해 `CsvSyncPath`(프로젝트 상대 경로, 예: `Assets/Data/Enemy.csv`)에 쓰고 다시 임포트합니다. 경로가 비어 있으면 건너뛰고, 쓰기에 실패하면 경고만 남깁니다. 폴더는 미리 있어야 합니다. 빠른 시작의 임포터에 적용하면 다음과 같습니다.

```csharp
public class EnemyConfigsImporter : GoogleSheetConfigsImporter<EnemyConfig, EnemyConfigs>, ICsvSyncTarget
{
    public override string GoogleSheetUrl =>
        "https://docs.google.com/spreadsheets/d/<SPREADSHEET_ID>/edit#gid=<GID>";

    public string CsvSyncPath => "Assets/Data/Enemy.csv";
}
```

## 샘플

Package Manager 에서 **Importers Example** 을 가져올 수 있습니다. 참고용 코드이며 그대로는 컴파일되지 않습니다.

- `DataConfigsImporter`: `GoogleSheetConfigsImporter` 예제
- `GameConfigsImporter`: `GoogleSheetSingleConfigImporter` 예제
- `GameIdsImporter`: 시트로 enum 과 조회 코드를 생성하는 `[GoogleSheetImportOrder(0)]` 예제

`Configs` 네임스페이스의 데이터 타입(`DataConfig`, `GameConfig` 등)은 포함되어 있지 않으므로 직접 만들어야 합니다. `GameIdsImporter` 는 제거된 `IGoogleSheetImporter` 를 구현하고 있으니 `IGoogleSheetConfigsImporter` 로 바꾸고 `using Geuneda.GoogleSheetImporter;` 를 추가해야 합니다.

## 요구 사항과 의존성

- Unity 6000.0 이상
- `com.geuneda.gamedata` 1.0.0 이상(`IConfigsContainer<T>`, `ISingleConfigContainer<T>`, `UnitySerializedDictionary<TKey, TValue>`)
- `com.unity.nuget.newtonsoft-json` 2.0.0 이상

변경 내역은 [CHANGELOG.md](CHANGELOG.md) 에 있습니다.

## 라이선스

MIT. 원본 저작권: Miguel Tomas (GameLovers). 자세한 내용은 [LICENSE.md](LICENSE.md) 를 참고하세요.
