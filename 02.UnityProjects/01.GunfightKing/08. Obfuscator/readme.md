## 빌드 파이프라인 코드 난독화 자동화 (Obfuscator)

라이브 서비스 중인 클라이언트의 핵심 로직이 역공학이나 코드 조작(핵)에 노출되는 것을 막기 위해, 서드파티 난독화 에셋을 사내 빌드 툴 에디터 창에 통합하고, 빌드마다 난독화 여부를 켜고 끌 수 있게 만들었다. 켜고 끄는 값은 에디터 창의 체크박스 또는 커맨드라인 인자(`-useOBF`)로 들어온다. `AppBuilder.BuildApp()`의 주석에 "젠킨스 등 커멘드 라인 빌드 호출"이라고 적혀 있고 인자를 파싱하는 코드까지는 저장소에서 확인되지만, Jenkins 잡 설정 자체는 저장소에 없다.

<img width="640" height="480" alt="image" src="https://github.com/user-attachments/assets/2984349a-3286-4a6d-92f1-25b64e24aef5" />

> 1. Obfuscator Asset을 활용하여 코드 난독화 진행 [링크](https://assetstore.unity.com/packages/tools/utilities/obfuscator-pro-plug-and-play-code-protection-v2026-89589?locale=ko-KR&srsltid=AfmBOormprJ7q97DkigO9Br7CeBG64KHepvv0EowD-8jQI6fbKOj119W)

<img width="395" height="300" alt="image" src="https://github.com/user-attachments/assets/01b80fb7-609f-468d-a238-0d8c9f52a45e" />

> 2. 게임의 핵심 로직이 주로 담겨있는 Namespace를 지정하여, 해당 NameSpace의 Class와 Method, Properties를 난독화

<img width="429" height="394" alt="image" src="https://github.com/user-attachments/assets/fff64847-1461-4930-a7bf-f88bf5ce4d03" />

> 3. 실제 난독화 진행 여부를 빌드 옵션 에디터창에 추가

### 빌드 버튼 클릭 시점 (`ControlPanelToolWindow_Build.cs`)

빌드 창의 "빌드 시작" 버튼을 누르면 `GenerateBuildParam()`이 창의 "난독화 적용" 체크박스 값을 `AppBuildParam.ObfEnable`에 담고, 이 파라미터로 빌드를 시작한다. 난독화 On/Off 반영과 매핑 파일 이름 결정은 이후 `InternalBuildApp` 안에서 이뤄진다. `EditorUtil.Defer(0.1f, ...)`로 빌드 호출을 0.1초 미루고, `_isBuilding` 플래그로 중복 실행을 막는다.

```csharp
EditorUtil.Defer(0.1f, () =>
{
	try
	{
		_buildParam = GenerateBuildParam();
		_buildParam.BuildAddressableOnly = onlyAddressable;

		AppBuilder.BuildAppWithParam(_buildParam);
	}
	finally
	{
		_isBuilding = false;
	}
});
```

### 커맨드라인 진입점 (`AppBuilder.BuildApp`)

커맨드라인 빌드는 `BuildApp()`이 인자를 `AppBuildParam`으로 옮긴 뒤 같은 `BuildAppWithParam`으로 합류한다. 난독화는 `-useOBF` 인자가 없으면 꺼진 상태(기본값 `false`)다.

```csharp
param.UseDebug = BuildUtil.GetCmdArgAsBool("-useDebug", false);
param.ObfEnable = BuildUtil.GetCmdArgAsBool("-useOBF", false);
param.UseAAB = BuildUtil.GetCmdArgAsBool("-useAAB", false);
param.DeployDir = BuildUtil.GetCmdArgAsString("-deployDir", string.Empty);
```

`GetCmdArgAsBool` 자체(`BuildUtil`)는 이 폴더에 없다. 에디터 버튼 경로와 커맨드라인 경로가 같은 `InternalBuildApp`을 타므로, 난독화 반영 로직은 한 곳에만 있다.

### 빌드 파이프라인 내 위치 (`AppBuilder.InternalBuildApp`)

`ObfEnable` 플래그는 실제 플레이어 빌드(`BuildIPA`/`BuildAAB`/`BuildAPK`) 직전, 스크립팅 심볼 설정과 같은 시점에 반영된다. 매핑/로그 파일 경로는 그보다 조금 뒤, 빌드 옵션을 만든 직후에 정해진다. 어드레서블 빌드는 이 두 단계보다 앞서 실행되고, `BuildAddressableOnly`면 그 직후 반환되므로 난독화 설정은 건드리지 않는다.

```csharp
static void InternalBuildApp(AppBuildParam buildParam)
{
	... 중략 ...
	SetDefinedSymbols(buildParam);
	
	SetupObf(buildParam.ObfEnable);

    ... 중략 ...
	var buildOption = MakeBuildPalyerOptions(buildParam);
	
	ChangeObfCustomFilePaths(FormatObfFileName(buildParam));

	AndroidManifestModifier.BuildParam = buildParam;

	if (buildParam.BuildTarget == BuildTarget.iOS)
	{
		BuildIPA(buildOption, buildParam);
	}
	else
	{
		if (buildParam.UseAAB)
		{
			BuildAAB(buildOption, buildParam);
		}
		else
		{
			BuildAPK(buildOption, buildParam);
		}
	}

	if (buildParam.DeployDir.HasContent())
	{
		string outputFilePath = buildOption.locationPathName;
		BuildUtil.DeployFiles(outputFilePath, buildParam);
	}
}
```

### 난독화 On/Off (`ObfuscatorSettingsModifier.ToggleObfuscator`)

Obfuscator 에셋의 활성 여부는 에셋이 참조하는 설정 JSON(`Assets/GUPS/Obfuscator/Settings/Obfuscator_Settings.json`) 파일에 저장되어 있다. 그래서 빌드 시점에 이 JSON에서 `Global_Enable_Obfuscation` 키의 Value만 정규식으로 찾아 교체한다. `ObfEnable`이 `false`인 빌드에서도 이 함수가 호출되어 값을 `False`로 되돌려 쓴다.

```csharp
public static void ToggleObfuscator(bool isEnabled)
{
	if (!File.Exists(_obfSettingsPath)) return;

	string text = File.ReadAllText(_obfSettingsPath);

	// "Key": "Global_Enable_Obfuscation" 인 객체 내부의 "Value": "True"/"False"만 교체
	string pattern = @"(""Key""\s*:\s*""Global_Enable_Obfuscation""[^}]*?""Value""\s*:\s*"")(true|false|""True""|""False"")";
	string replacement = $"$1{(isEnabled ? "True" : "False")}";

	string newText = Regex.Replace(text, pattern, replacement, RegexOptions.IgnoreCase);

	File.WriteAllText(_obfSettingsPath, newText);
}
```

### 설계 포인트

- **런타임 체크박스 대신 빌드 시점의 JSON 패치**: 난독화 여부가 에디터에서 사람이 손으로 누르는 체크박스에만 있으면 무인 빌드에서는 값을 보장할 수 없다. 그래서 빌드 파라미터(`AppBuildParam.ObfEnable`)를 기준으로 설정 JSON을 코드로 패치하고, 같은 파라미터가 에디터 체크박스(`_devCfg.Build.ObfEnable`)와 커맨드라인 인자(`-useOBF`) 양쪽에서 채워지게 했다. 이 코드에서 난독화 여부는 `EnvType`(Dev/QA/Live)과 자동으로 연동되지 않고 인자 값 그대로 결정된다. 환경별로 어떤 값을 넘기는지는 Jenkins 쪽 설정이라 저장소로는 알 수 없다.
- **전체 JSON을 파싱/재직렬화하지 않고 정규식으로 특정 Key만 치환**: `Global_Enable_Obfuscation` 키가 포함된 객체 블록만 잡아내는 정규식을 써서, 같은 파일의 네임스페이스 대상 목록 같은 다른 설정값은 건드리지 않는다. 서드파티 에셋이 자체 포맷으로 관리하는 JSON을 구조체로 역직렬화했다가 다시 쓰면 필드 누락이나 포맷 손상 위험이 있는데, 이를 피하기 위한 선택으로 보인다. 다만 같은 파일의 로그/매핑 경로는 `SetupObfCustomFilePaths`가 별도의 정규식(`Custom_Log_FilePath`, `Save_Mapping_FilePath`)으로 다시 패치하므로, 이 함수가 건드리지 않는 것은 그 두 경로를 제외한 나머지 값이다.
- **빌드마다 로그/매핑 파일명을 새로 결정**(`ChangeObfCustomFilePaths`, `FormatObfFileName`): `GunfightKing_{마켓}_v{버전}({빌드번호})_{환경}_{타임스탬프}_{Photon 게임버전}_{커밋 리비전 앞 5자리}` 형태로 이름을 만들어 `BuildResult/Obf`(iOS는 `XcodeProject/Obf`) 아래에 남긴다. 마켓 태그(AOS/IOS/WOS/SGS), 버전, 타임스탬프, 커밋 리비전이 모두 이름에 들어가므로, 이후 특정 빌드의 난독화된 스택 트레이스를 원본 심볼로 되돌려야 할 때 어떤 매핑 파일을 참조해야 하는지 추적할 수 있다. `DeployDir`가 지정된 빌드에서는 git diff(2025-10-15 커밋)상 `BuildUtil.DeployFiles`가 매핑/로그 파일을 그 폴더로 옮기는 것까지 확인된다(`BuildUtil`은 이 폴더에 없다).

### 난독화 예외 처리 (`[DoNotObfuscateClass]`)

난독화를 켜는 것과 별개로, 이름이 바뀌면 안 되는 클래스를 골라 빼는 작업도 필요했다. git 이력(2025-01 이후)에서 확인되는 본인 커밋은 다음과 같다.

> 근거:
> - 2025-09-22 `[LR-535] Obfuscator 애셋 추가`, 2025-09-23 `Game.View 제외시켜봄`, `로그 심어봄`, `문자열 복호화 옵션 비활성화 CDB 딕셔너리 관련 로그 추가` 등, 난독화된 빌드를 확인하면서 설정을 조정한 것으로 보이는 커밋들.
> - 2025-09-23/24 `StringCDB는 난독화 되지 않게 예외처리`, `프로토콜, DeviceConfig, UserConfig, BuildInfo 클래스 난독화 예외처리`, `프로토콜 Request Param 클래스 난독화 예외처리`, `프로토콜 요청/응답 내부의 Data를 담는 용도로 사용되는 Class도 추가 난독화 예외항목으로 지정`.
> - 2025-09-24 상위 인터페이스(`INetParam`, `INetResult`)에만 어트리뷰트를 붙여 보는 시도(`578d908`, 구체 클래스에서 26줄 제거)를 했고, 같은 날 `부모 클래스에만 [DoNotObfuscateClass] 어트리뷰트를 적용했을 때 자식 클래스까지 반영되지는 않기 때문에 각각의 클래스에 어트리뷰트 적용하는 방식으로 재변경`으로 되돌렸다.
> - 2025-09-26 `feature/0.2.0/AddObfuscate Merge Revert`(`[DoNotObfuscateClass]` 110줄이 제거됨) 이후, 같은 날 `[LR-440] Game.View, Game.Data 코드 난독화` 커밋에서 `Game.Data` 쪽 32개 파일에 어트리뷰트 96줄이 다시 들어간다. Revert 사유는 커밋 메시지에 없다.
> - `[LR-440]` 4건(2025-09-26 3건, 09-30 1건): 코드 난독화 대상 지정, 랜덤 코드 생성 끄기, 로그/매핑 파일을 `BuildResult` 아래로 지정, 빌드 컨트롤 패널에 "난독화 적용" 옵션(`ObfuscatorSettingsModifier.cs`가 이 커밋에서 추가됨) 추가.
> - 2025-10-15 `난독화 로그 및 매핑 파일 Deploy 경로로 생성시키는 방식 조금 수정`, 2025-10-30 `iOS 난독화 mapping file, log file 이름 표기 방식 Android 와 동일하게 맞춤`(`FormatObfFileName`, `MarketTagName` 분리).
> - 위 목록은 export된 2025-01-06 ~ 2025-11-26 범위의 본인 커밋만 반영한 것이다.

예외는 클래스마다 어트리뷰트를 직접 붙이는 방식이다. 상위 클래스에 붙여도 하위 클래스에는 적용되지 않는다는 것을 확인한 뒤, 실제 커밋에서는 다음처럼 파생 클래스에도 하나씩 붙였다(2025-09-23 `StringCDB` 커밋의 diff).

```csharp
[DoNotObfuscateClass]
public class StringUiCDB : StringCDB<StringUiCDB> { }

[DoNotObfuscateClass]
public class StringCharacterCDB : StringCDB<StringCharacterCDB> { }
```

왜 이 클래스들이 예외인지는 커밋 메시지에 이유가 적혀 있지 않아, diff에서 읽히는 범위로만 정리한다. 프로토콜 파라미터/응답 클래스는 `Newtonsoft.Json`을 쓰는 `BackendNetCommand`와 함께 쓰이고 `game_code` 같은 snake_case 멤버를 가지고 있어, 멤버 이름이 곧 서버와 주고받는 JSON 키일 가능성이 높아 보인다(리플렉션·직렬화 대상으로 보인다). `StringCDB`는 같은 커밋에서 생성자가 `base(typeof(T).Name, true)`에서 `base(null)`로 바뀌어 있어 타입 이름에 의존하던 경로가 있었음을 짐작하게 하고, `DeviceConfig`/`UserConfig`/`BuildInfo`는 저장/로드되는 설정 객체라는 점이 공통이다. 다만 어느 쪽이 실제 원인이었는지는 diff만으로 확정할 수 없다.

난독화 이후 실제로 스택 트레이스가 어떻게 보이는지 확인하려고, 2025-09-26에 개발자 옵션에 "Crash Report 발생" 버튼을 넣었다. 누르면 테스트용 예외 하나를 Crashlytics에 보고한다(`[LR-535] 난독화 이후 로그 확인을 위한 Exception 을 통한 Crashlytics Report를 임의로 발생시키는 테스트 버튼 추가`).

```csharp
CrashlyticsUtil.LogException(new NullReferenceException("Test NullReferenceException"));
```

이 작업을 포함한 빌드/보안 자동화로, 라이브 서비스 기간 동안 클라이언트 코드 조작(핵)이 확인된 사례는 0건이었다(포트폴리오 기재 성과이며 코드로 검증되는 수치는 아니다).


이 작업을 포함한 빌드/보안 자동화로, 라이브 서비스 기간 동안 클라이언트 코드 조작(핵)이 확인된 사례는 0건이었다.

관련 코드: [09. Firebase](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/09.%20Firebase)
