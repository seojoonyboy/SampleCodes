<img width="640" height="480" alt="image" src="https://github.com/user-attachments/assets/2984349a-3286-4a6d-92f1-25b64e24aef5" />

> 1. Obfuscator Asset을 활용하여 코드 난독화 진행 [링크](https://assetstore.unity.com/packages/tools/utilities/obfuscator-pro-plug-and-play-code-protection-v2026-89589?locale=ko-KR&srsltid=AfmBOormprJ7q97DkigO9Br7CeBG64KHepvv0EowD-8jQI6fbKOj119W)


<img width="395" height="300" alt="image" src="https://github.com/user-attachments/assets/01b80fb7-609f-468d-a238-0d8c9f52a45e" />

> 2. 게임의 핵심 로직이 주로 담겨있는 Namespace를 지정하여,
> 해당 NameSpace의 Class와 Method, Properties를 난독화


<img width="429" height="394" alt="image" src="https://github.com/user-attachments/assets/fff64847-1461-4930-a7bf-f88bf5ce4d03" />

> 3. 실제 난독화 진행 여부를 빌드 옵션 에디터창에 추가


빌드 Editor 버튼을 클릭하면 난독화 옵션 체크 여부를 확인하여 난독화를 진행할지 여부와 Mapping 파일의 이름을 빌드 직전 결정하고 빌드합니다. 
<pre>
  <code>
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
  </code>
</pre>

<pre>
  <code>
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
  </code>
</pre>


<pre>
  <code>
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
  </code>
</pre>
