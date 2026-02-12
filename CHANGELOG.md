# Changelog
이 패키지의 모든 주요 변경사항은 이 파일에 기록됩니다.

이 형식은 [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)를 기반으로 하며,
이 프로젝트는 [Semantic Versioning](http://semver.org/spec/v2.0.0.html)을 따릅니다.

## [0.7.2] - 2026-01-14

**Changed**:
- 의존성 `com.geuneda.dataextensions` 및 `com.geuneda.configsprovider`를 `com.geuneda.gamedata`로 업데이트
- 어셈블리 정의에서 `Geuneda.GameData`를 참조하도록 업데이트

## [0.7.1] - 2023-08-03

**New**:
- `com.geuneda.configsprovider` 패키지(버전 0.1.0)에 대한 새로운 의존성을 추가
- package.json에 저자 및 라이선스 정보를 추가

**Changed**:
- `com.geuneda.dataextensions` 의존성을 버전 0.4.0으로 업데이트
- `ConfigsProvider`, `IConfigsProvider`, `IConfigsAdder`를 새로운 `com.geuneda.configsprovider` 패키지로 이동
- `IConfigsContainer`, `ISingleConfigContainer`, `IPairConfigsContainer`, `IConfig` 인터페이스를 `com.geuneda.configsprovider` 패키지로 이동
- 최소 Unity 버전을 2021.3으로 업데이트

## [0.7.0] - 2023-07-28

**New**:
- 임포터의 코드 재사용성 향상을 위한 `GoogleSheetScriptableObjectImportContainer` 기본 클래스를 추가
- `IScriptableObjectImporter` 및 `IGoogleSheetConfigsImporter` 인터페이스를 추가
- 단일 설정 내 리스트 임포트를 지원하는 `GoogleSheetSingleConfigSubListImporter`를 추가
- 모든 임포터에 임포트 후 처리를 위한 `OnImportComplete` 가상 메서드를 추가
- `CsvParser` 메서드에서 커스텀 역직렬화기 지원을 추가
- CSV 파싱 제어를 위한 `IGNORE_COLUMN_CHAR` ("$"), `SUB_LIST_SUFFIX` ("[]"), `IGNORE_FIELD_CHAR` ("#") 상수를 추가
- `UnitySerializedDictionary` 타입 역직렬화 지원을 추가
- 자동화된 PR 리뷰를 위한 OpenAI 코드 리뷰어 GitHub Action을 추가
- `CsvParser`를 개선된 배열/딕셔너리 파싱 및 커스텀 역직렬화기 지원으로 리팩토링
- 누락된 컬럼을 예외 대신 우아하게 처리하도록 CSV 파싱을 개선

**Changed**:
- `IConfigsProvider.GetSingleConfig<T>()`를 개선된 오류 처리와 함께 `GetConfig<T>()`로 이름 변경
- `IGoogleSheetImporter` 인터페이스를 제거 (`IGoogleSheetConfigsImporter`로 대체)

## [0.6.2] - 2020-09-24

**New**:
- 임포터 예제를 추가

## [0.6.1] - 2020-08-18

**New**:
- *ConfigsProvider*에 의존하지 않고 설정을 추가하려는 객체의 계약 동작을 분리하기 위한 *IConfigsAdder* 인터페이스를 추가

## [0.6.0] - 2020-07-28

**New**:
- 로드된 Google 시트 설정을 제공하는 *ConfigsProvider*를 추가
- 로드된 Google 시트 설정을 유지하기 위한 인터페이스 *IConfigsContainer*를 추가

**Changed**:
- *com.gamelovers.configscontainer* 및 *com.gamelovers.asyncawait* 패키지 의존성을 제거

## [0.5.3] - 2020-04-25

**Changed**:
- *com.gamelovers.configscontainer* 패키지를 버전 0.7.0으로 업데이트
- *GoogleSheetImporter* 문서를 업데이트

## [0.5.2] - 2020-04-06

**Changed**:
- *com.gamelovers.asyncawait* 패키지를 버전 0.2.0으로 업데이트하여 사용
- 패키지에서 UnityWebRequestAwaiter를 제거

## [0.5.1] - 2020-03-07

**New**:
- *com.gamelovers.configscontainer* 버전 1.1.2 패키지 의존성을 추가

**Changed**:
- *com.gamelovers.configscontainer* 패키지를 버전 0.5.0으로 업데이트

## [0.5.0] - 2020-02-26

**New**:
- 단일 고유 설정을 임포트하기 위한 *GoogleSheetSingleConfigImporter*를 추가
- 타입을 파라미터로 전달하여 파싱할 수 있도록 파싱 기능을 개선

**Changed**:
- *com.gamelovers.configscontainer* 패키지를 버전 0.4.0으로 업데이트

**Fixed**:
- KeyValuePair 타입의 값이 문자열 트림되지 않던 버그를 수정

## [0.4.0] - 2020-02-25

**New**:
- CSV 쌍(예: 1:2,2<3,3>4)을 딕셔너리 및 값 쌍 타입으로 파싱하는 기능을 추가
- 파싱 성능을 개선

**Changed**:
- 제네릭 타입의 값이 항상 문자열로 파싱되는 대신 해당 값 타입으로 올바르게 파싱되도록 변경. 이를 통해 임포터에서 불필요한 후속 변환을 방지
- *com.gamelovers.configscontainer* 패키지를 버전 0.3.0으로 업데이트

## [0.3.0] - 2020-01-20

**New**:
- 통화 기호와 점 등 특수 문자를 포함하도록 *CsvParser*를 개선
- *GoogleSheetImporter.asset* 파일을 쉽게 선택할 수 있는 기능을 추가. *Tools > Select GoogleSheetImporter.asset*으로 이동하면 됨. *GoogleSheetImporter.asset*이 존재하지 않으면 Assets 폴더에 새로 생성됨

**Changed**:
- *com.gamelovers.configscontainer* 패키지를 버전 0.2.0으로 업데이트

## [0.2.1] - 2020-01-15

**Changed**:
- Debug.Log 라인을 제거

## [0.2.0] - 2020-01-15

**New**:
- 역직렬화 시 필드를 무시할 수 있는 *ParseIgnoreAttribute*를 추가
- [ParseIgnore] 어트리뷰트 테스트로 테스트를 개선

**Changed**:
- 프로젝트 런타임에서 사용할 수 있도록 CsvParser를 런타임 어셈블리로 이동

## [0.1.2] - 2020-01-08

**New**:
- 누락된 meta 파일을 추가

## [0.1.1] - 2020-01-08

**New**:
- AsyncAwait 패키지 의존성을 제거하기 위한 *UnityWebRequestAwaiter*를 추가
- *GameIdsImporter* 예제를 추가

## [0.1.0] - 2020-01-06

- 패키지 배포를 위한 최초 제출
