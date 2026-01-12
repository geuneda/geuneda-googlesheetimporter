# Geuneda GoogleSheet Importer

Google 스프레드시트 데이터를 ScriptableObject 설정 데이터로 가져오는 Unity 도구 패키지입니다.

## 개요

이 패키지는 Google 스프레드시트의 데이터를 Unity의 ScriptableObject로 쉽게 가져올 수 있게 해주는 에디터 도구입니다.

## 주요 기능

- Google 스프레드시트에서 데이터 가져오기
- ScriptableObject로 자동 변환
- 다양한 데이터 타입 지원
- 커스텀 임포터 확장 가능

## 요구 사항

- Unity 2021.3 이상
- Newtonsoft.Json 패키지 (`com.unity.nuget.newtonsoft-json`)
- Geuneda Data Extensions 패키지 (`com.geuneda.dataextensions`)
- Geuneda Configs Provider 패키지 (`com.geuneda.configsprovider`)

## 설치 방법

### Unity Package Manager를 통한 설치

1. Unity 에디터에서 `Window` > `Package Manager`를 엽니다.
2. 좌측 상단의 `+` 버튼을 클릭하고 `Add package from git URL...`을 선택합니다.
3. 다음 URL을 입력합니다:
   ```
   https://github.com/geuneda/geuneda-googlesheetimporter.git
   ```
4. `Add` 버튼을 클릭합니다.

### manifest.json을 통한 설치

프로젝트의 `Packages/manifest.json` 파일에 다음을 추가합니다:

```json
{
  "dependencies": {
    "com.geuneda.googlesheetimporter": "https://github.com/geuneda/geuneda-googlesheetimporter.git"
  }
}
```

## 사용 방법

### 설정 ScriptableObject 생성

프로젝트 뷰에서 우클릭 > `Create` > `ScriptableObjects` > `Editor` > `GoogleSheetImporter`

### 데이터 가져오기

ScriptableObject 없이 모든 Google 스프레드시트 데이터를 가져오려면:
`Tools` > `Import Google Sheet Data`

## 네임스페이스

```csharp
using Geuneda.GoogleSheetImporter;
```

## 라이선스

MIT License
