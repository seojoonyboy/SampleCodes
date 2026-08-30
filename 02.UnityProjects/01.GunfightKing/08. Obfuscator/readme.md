## 빌드 파이프라인 코드 난독화 자동화 (Obfuscator)

라이브 서비스 중인 클라이언트의 핵심 로직이 역공학이나 코드 조작(핵)에 노출되는 것을 막기 위해, 서드파티 난독화 에셋을 사내 빌드 툴 에디터 창에 통합하고 Jenkins CI/CD 빌드 파이프라인에서 빌드마다 난독화 여부를 자동으로 켜고 끌 수 있게 만들었다.

<img width="640" height="480" alt="image" src="https://github.com/user-attachments/assets/2984349a-3286-4a6d-92f1-25b64e24aef5" />

> 1. Obfuscator Asset을 활용하여 코드 난독화 진행 [링크](https://assetstore.unity.com/packages/tools/utilities/obfuscator-pro-plug-and-play-code-protection-v2026-89589?locale=ko-KR&srsltid=AfmBOormprJ7q97DkigO9Br7CeBG64KHepvv0EowD-8jQI6fbKOj119W)

<img width="395" height="300" alt="image" src="https://github.com/user-attachments/assets/01b80fb7-609f-468d-a238-0d8c9f52a45e" />

> 2. 게임의 핵심 로직이 주로 담겨있는 Namespace를 지정하여, 해당 NameSpace의 Class와 Method, Properties를 난독화

<img width="429" height="394" alt="image" src="https://github.com/user-attachments/assets/fff64847-1461-4930-a7bf-f88bf5ce4d03" />

> 3. 실제 난독화 진행 여부를 빌드 옵션 에디터창에 추가

### 빌드 버튼 클릭 시점 (`ControlPanelToolWindow_Build.cs`)

빌드 Editor 버튼을 클릭하면 난독화 옵션 체크 여부를 확인하여 난독화를 진행할지 여부와 Mapping 파일의 이름을 빌드 직전 결정하고 빌드한다.

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

### 빌드 파이프라인 내 위치 (`AppBuilder.InternalBuildApp`)

`ObfEnable` 플래그는 실제 플레이어 빌드(`BuildIPA`/`BuildAAB`/`BuildAPK`) 직전, 스크립팅 심볼 설정과 같은 시점에 반영된다.

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

Obfuscator 에셋의 활성 여부는 Unity 프로젝트 창이 아니라 에셋이 참조하는 설정 JSON(`Obfuscator_Settings.json`) 파일에 저장되어 있다. 그래서 빌드 시점에 이 JSON에서 `Global_Enable_Obfuscation` 키의 Value만 정규식으로 찾아 교체한다.

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

- **런타임 체크박스 대신 빌드 시점의 JSON 패치**: Obfuscator는 Unity 에디터가 아니라 CI 서버(Jenkins)에서 커맨드라인으로 빌드되는 경우가 많아, 사람이 매번 체크박스를 누르는 방식으로는 설정을 보장할 수 없다. 대신 빌드 파라미터(`AppBuildParam.ObfEnable`)를 기준으로 설정 JSON을 코드로 직접 패치해, 무인 빌드에서도 환경(QA/Live)별로 난독화 여부가 결정된다.
- **전체 JSON을 파싱/재직렬화하지 않고 정규식으로 특정 Key만 치환**: `Global_Enable_Obfuscation` 키가 포함된 객체 블록만 잡아내는 정규식을 써서, 같은 파일에 함께 저장된 네임스페이스 대상 목록·로그/매핑 경로 같은 다른 설정값은 건드리지 않는다. 서드파티 에셋이 자체 포맷으로 관리하는 JSON을 구조체로 역직렬화했다가 다시 쓰면 필드 누락이나 포맷 손상 위험이 있는데, 이를 피하기 위한 선택으로 보인다.
- **빌드마다 로그/매핑 파일명을 새로 결정**(`ChangeObfCustomFilePaths`, `FormatObfFileName`): 마켓/버전/타임스탬프/커밋 리비전이 조합된 이름으로 매핑 파일을 남겨, 이후 특정 빌드의 난독화된 스택 트레이스를 원본 심볼로 되돌려야 할 때 어떤 매핑 파일을 참조해야 하는지 추적할 수 있게 했다.

이 작업을 포함한 빌드/보안 자동화로, 라이브 서비스 기간 동안 클라이언트 코드 조작(핵)이 확인된 사례는 0건이었다.

관련 코드: [09. Firebase](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/09.%20Firebase)
