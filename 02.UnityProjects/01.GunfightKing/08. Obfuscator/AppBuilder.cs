using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using Framework;
using Game.Data;
using System;
using UnityEngine.Localization.Metadata;
using System.IO;

public class AppBuilder : MonoBehaviour
{
	static string _buildResultBaseDir;

	// 프로비저닝 프로파일 위치
	static string _iosDevProvisionPath => ApplicationInfo.ProjectPath + "/Tools/BuildTool/IosProfile/GunfightKing_dev.mobileprovision";
	static string _iosDistProvisionPath => ApplicationInfo.ProjectPath + "/Tools/BuildTool/IosProfile/GunfightKing_dis.mobileprovision";

	static string _logPath;
	static string _mapPath;
	public static string Lp => _logPath;
	public static string Mp => _mapPath;
	
	//[MenuItem("[DevTools]/Simple App Build", false, 600)]
	//public static void BuildAppMenu()
	//{
	//	if (EditorUtil.MsgBox_OkCancel($"현재 설정({EditorUserBuildSettings.activeBuildTarget})으로 빌드를 시작합니다.") == true)
	//	{
	//		AppBuildParam buildCtx = new AppBuildParam();
	//		BuildApp(buildCtx);
	//	}
	//}

	/// <summary>
	/// 젠킨스 등 커멘드 라인 빌드 호출
	/// </summary>
	/// <exception cref="Exception"></exception>
	public static void BuildApp()
	{
		Debug.Log("BuildApp()");

		AppBuildParam param = new AppBuildParam();
		param.Timestamp = BuildUtil.GetCmdArgAsString("-timestamp", DateTime.Now.ToString("yyMMdd_HHmm"));
		param.BuildTarget = ApplicationInfo.IsAndroidTarget ? BuildTarget.Android : BuildTarget.iOS;
		param.EnvType = BuildUtil.GetCmdArgAsEnum<EnvType>("-envType", EnvType.Dev);
		param.MarketType = BuildUtil.GetCmdArgAsEnum<MarketType>("-marketType", MarketType.Google);
		param.AppVersion = BuildUtil.GetCmdArgAsInt("-AppVersion");
		param.BuildNo = BuildUtil.GetCmdArgAsInt("-BuildNo");
		param.PhotonGameVersion = BuildUtil.GetCmdArgAsString("-PhotonGameVersion");
		
		param.OutputFileTitle = BuildUtil.GetCmdArgAsString("-outputFileTitle", string.Empty);
		param.XcodeProjectRelDir = BuildUtil.GetCmdArgAsString("-xcodeProjectRelDir", string.Empty);
		param.DevelopmentProfile = BuildUtil.GetCmdArgAsBool("-useDevelopmentProfile", false);
		param.InternalTestOnly = BuildUtil.GetCmdArgAsBool("-internalTestOnly", false);

		param.UseDebug = BuildUtil.GetCmdArgAsBool("-useDebug", false);
		param.ObfEnable = BuildUtil.GetCmdArgAsBool("-useOBF", false);
		param.UseAAB = BuildUtil.GetCmdArgAsBool("-useAAB", false);
		param.DeployDir = BuildUtil.GetCmdArgAsString("-deployDir", string.Empty);
		param.AddressableCleanBuild = BuildUtil.GetCmdArgAsBool("-addrClean", false);
		param.Revision = BuildUtil.GetGitRevision();
		//param.ReportFilePath = BuildUtil.GetCmdArgAsString("-reportFilePath", string.Empty).ToOsSlash();
		//param.LogAndTag = (CommitLogImportAction)Enum.Parse(typeof(CommitLogImportAction), BuildUtil.GetCmdArgAsString("-logAndTag", CommitLogImportAction.None.ToString()));
		//param.Comment = BuildUtil.GetCmdArgAsString("-comment", string.Empty);
		//bool mono = BuildUtil.GetCmdArgAsBool("-monoBuild", false);

		BuildAppWithParam(param);
	}

	//에디터에서 빌드 호출
	public static void BuildAppWithParam(AppBuildParam buildParam)
	{
		Debug.Log("BuilBuildAppFromMenu()");

		try
		{
			BuildUtil.IsAppBuilding = true;
			//--BuildUtil.BackupAssets(buildParam);
			
			InternalBuildApp(buildParam);

			//--if (!buildParam.DontRollbackModifiedFiles)
			//--{
			//--	BuildUtil.RestoreAssets(buildParam);
			//--}
		}
		finally
		{
			BuildUtil.IsAppBuilding = false;
		}
		
		
	}

	static void InternalBuildApp(AppBuildParam buildParam)
	{
		SetPackageName(buildParam);

		ModifyFiles(buildParam);

		if (buildParam.BuildTarget == BuildTarget.iOS)
		{
			BuildUtil.ClearPreloadShaders();
		}

		// Keystore지정은 Addressable 빌드 전 필요하다.
		SetKeystore(buildParam);

		SetAndroidDevelopmentBuild(buildParam);
		
		// 'Separate Base Assets'옵션은 항상 켜둔다.
		BuildUtil.ForceSeparateBaseAssets();

		if (!buildParam.SkipBuildAddressable)
		{
			BuildUtil.BuildAddressable(buildParam.UseAAB, buildParam.AddressableCleanBuild);
		}

		if (buildParam.BuildAddressableOnly)
		{
			return;
		}

		if (buildParam.BuildTarget == BuildTarget.iOS)
		{
			_buildResultBaseDir = ApplicationInfo.ProjectPath + "/XcodeProject"; 
		}
		else
		{
			_buildResultBaseDir = ApplicationInfo.ProjectPath + "/BuildResult";
		}

		SetAppVersionAndBuildNumber(buildParam);

		SetPlayerConfiguration(buildParam);

		if (buildParam.BuildTarget == BuildTarget.iOS)
		{
			SetupIOS(buildParam);
		}

		SetDefinedSymbols(buildParam);
		
		SetupObf(buildParam.ObfEnable);

		BuildUtil.SaveBuildInfo(buildParam);

		//--PlayerSettings.SetIl2CppCompilerConfiguration(buildParam.BuildTargetGroup, buildParam.CppDebug ? Il2CppCompilerConfiguration.Debug : Il2CppCompilerConfiguration.Release);

		BuildUtil.BuildAddressLookup();

		if (buildParam.BuildTarget == BuildTarget.Android)
		{
			EditorUserBuildSettings.buildAppBundle = buildParam.UseAAB;
		
			// 구글 Live빌드만 심볼 생성
			bool makeSymbol = buildParam.MarketType == MarketType.Google && buildParam.EnvType == EnvType.Live && buildParam.UseAAB;
			UnityEditor.Android.UserBuildSettings.DebugSymbols.level = makeSymbol
				? Unity.Android.Types.DebugSymbolLevel.Full
				: Unity.Android.Types.DebugSymbolLevel.None;
		}

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

	static void SetupIOS(AppBuildParam buildParam)
	{
		PlayerSettings.iOS.buildNumber = buildParam.BuildNo.ToString();

		PlayerSettings.iOS.appleDeveloperTeamID = "P86JHS6HXW";
		PlayerSettings.iOS.appleEnableAutomaticSigning = false;

		// XCode에서의 자동인식을 위해서 ProfileID를 UUID로 설정.
		// UUID는 provision파일을 열어 얻을 수 있음.
		if (buildParam.DevelopmentProfile)
		{
			PlayerSettings.iOS.iOSManualProvisioningProfileType = ProvisioningProfileType.Development;
			PlayerSettings.iOS.iOSManualProvisioningProfileID = BuildUtil.GetIosProfileID(_iosDevProvisionPath);
		}
		else
		{
			PlayerSettings.iOS.iOSManualProvisioningProfileType = ProvisioningProfileType.Distribution;
			PlayerSettings.iOS.iOSManualProvisioningProfileID = BuildUtil.GetIosProfileID(_iosDistProvisionPath);
		}
	}


	static void BuildIPA(BuildPlayerOptions buildOption, AppBuildParam buildParam)
	{
		BuildPlayer(buildOption, buildParam);

		if (!buildParam.BuildFromEditorMenu)
		{
			BuildUtil.CreateExportOptionPList(buildOption.locationPathName, buildParam.InternalTestOnly);
		}
	}

	static void BuildAPK(BuildPlayerOptions buildOption, AppBuildParam buildParam)
	{
		BuildPlayer(buildOption, buildParam);
	}

	static void BuildPlayer(BuildPlayerOptions buildOption, AppBuildParam buildParam)
	{
		BuildReport buildReport = BuildPipeline.BuildPlayer(buildOption);

		if (buildReport.summary.result == BuildResult.Succeeded)
		{
			if (buildParam.BuildFromEditorMenu)
			{
				System.Diagnostics.Process.Start(Path.GetDirectoryName(buildOption.locationPathName));
			}
		}
		else
		{
			throw new System.Exception(buildReport.summary.result.ToString());
		}
	}

	static void BuildAAB(BuildPlayerOptions buildOption, AppBuildParam buildParam)
	{
		var assetPackConfig = Google.Android.AppBundle.Editor.AssetPacks.AssetPackConfigSerializer.LoadConfig();

		var success = Google.Android.AppBundle.Editor.Bundletool.BuildBundle(buildOption, assetPackConfig, forceSynchronousBuild: true);

		if (success == true)
		{
			if (buildParam.BuildFromEditorMenu)
			{
				System.Diagnostics.Process.Start(Path.GetDirectoryName(buildOption.locationPathName));
			}
		}
		else
		{
			throw new System.Exception("##Error Build AAB");
		}
	}

	static BuildPlayerOptions MakeBuildPalyerOptions(AppBuildParam buildParam)
	{
		var buildTarget = buildParam.BuildTarget;
		
		string appFileExt = AppFileExtensionName(buildTarget, buildParam.UseAAB);
		
		BuildPlayerOptions buildOption = new BuildPlayerOptions();
		buildOption.target = buildTarget;
		buildOption.targetGroup = buildParam.BuildTargetGroup;
		buildOption.scenes = new string[] { "Assets/Resources/IntroScene/IntroScene.unity", "Assets/ResourcesEx/Scenes/TitleScene.unity" };

		if (buildParam.BuildTarget == BuildTarget.iOS)
		{
			buildOption.locationPathName = $"{_buildResultBaseDir}/{FormatFileName(buildParam)}";
			if (buildParam.XcodeProjectRelDir.HasContent())
			{
				buildOption.locationPathName = ApplicationInfo.ProjectPath + "/" + buildParam.XcodeProjectRelDir;
			}
		}
		else
		{
			string fileName = FormatFileName(buildParam);
			if (buildParam.OutputFileTitle.HasContent())
			{
				fileName = buildParam.OutputFileTitle;
			}
			buildOption.locationPathName = $"{_buildResultBaseDir + buildTarget}/{fileName}.{appFileExt}";
		}
		buildOption.options = BuildOptions.CleanBuildCache;
		if (buildParam.DevelopmentProfile)
		{
			buildOption.options |= BuildOptions.Development;
		}

		return buildOption;
	}

	static void SetAppVersionAndBuildNumber(AppBuildParam buildParam)
	{
		PlayerSettings.bundleVersion = AppVersionUtil.ToDotVersion(buildParam.AppVersion);

		if (buildParam.BuildTarget == BuildTarget.Android)
		{
			PlayerSettings.Android.bundleVersionCode = buildParam.VersionCode;
		}
		else if (buildParam.BuildTarget == BuildTarget.iOS)
		{
			PlayerSettings.iOS.buildNumber = buildParam.BuildNo.ToString();
		}
		else if (buildParam.BuildTarget == BuildTarget.StandaloneWindows64)
		{
		}
		else
		{
			throw new System.Exception("Invalid BuildTarget:" + buildParam.BuildTarget);
		}
	}

	static void SetPlayerConfiguration(AppBuildParam param)
	{
		PlayerSettings.SetIl2CppStacktraceInformation(param.NamedBuildTarget,
			param.UseDebug
			? Il2CppStacktraceInformation.MethodFileLineNumber
			: Il2CppStacktraceInformation.MethodOnly);
	}

	static void SetPackageName(AppBuildParam param)
	{
		string appId = "com.mobirix.fps";
		if(param.MarketType == MarketType.Samsung)
		{
			appId = "com.mobirix.fpssgs";
		}
		PlayerSettings.SetApplicationIdentifier(param.NamedBuildTarget, appId);
	}

	static void ModifyFiles(AppBuildParam param)
	{
		if (param.BuildTarget == BuildTarget.Android)
		{
			//ChangeAndroidResolverDependenciesXml(param);

			ChangeGoogleServiceXml(param);

			BuildUtil.SwitchFilesForStore(param.MarketType, "Dependencies.xml");
			
			BuildUtil.SwitchFilesForStore(param.MarketType, "Assets/Mobirix/Admob/Resources/AdmobSettings.asset");

			BuildUtil.SwitchFilesForStore(param.MarketType, "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset");
		}
	}

	static void ChangeObfCustomFilePaths(string fileName)
	{
		string basePath = $"{_buildResultBaseDir}/Obf/{fileName}";
		_logPath = $"{basePath}_logFile.txt";
		_mapPath = $"{basePath}_mappingFile.txt";
		
		DebugEx.Log($"ChangeObfCustomFilePaths LogPath : {_logPath}, MapPath : {_mapPath}");
		
		ObfuscatorSettingsModifier.SetupObfCustomFilePaths(_logPath, _mapPath);
	}

	static void ChangeGoogleServiceXml(AppBuildParam param)
	{
		string srcPath = $"Tools/BuildTool/Platform/{param.MarketType}/google-services.xml";
		string destPath = "Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml";

		File.Copy(srcPath, destPath, true);
	}

	static void ChangeAndroidResolverDependenciesXml(AppBuildParam param)
	{
		string filePath = "ProjectSettings/AndroidResolverDependencies.xml";
		string content = File.ReadAllText(filePath);

		// androidResolverDependencies.xml 파일 내의 패키지명 교체
		if (param.MarketType == MarketType.Google)
		{
			content = content.Replace("\"com.mobirix.fpssgs\"", "\"com.mobirix.fps\"");
		}
		else if (param.MarketType == MarketType.Samsung)
		{
			content = content.Replace("\"com.mobirix.fps\"", "\"com.mobirix.fpssgs\"");
		}
		File.WriteAllText(filePath, content);
	}

	static void SetKeystore(AppBuildParam buildParam)
	{
		PlayerSettings.Android.useCustomKeystore = true;

		string keystoreName = /*buildParam.DevelopmentProfile ? "debug.keystore" :*/ "mobirix.keystore";
		string aliasName = /*buildParam.DevelopmentProfile ? "androiddebugkey" :*/ "key0";
		string passwd = /*buildParam.DevelopmentProfile ? "android" :*/ "Mobirix2025^2";

		PlayerSettings.Android.keystoreName = $"{Application.dataPath}/../Tools/BuildTool/Keystore/{keystoreName}";
		PlayerSettings.Android.keystorePass = passwd;
		PlayerSettings.Android.keyaliasName = aliasName;
		PlayerSettings.Android.keyaliasPass = passwd;
	}

	static void SetAndroidDevelopmentBuild(AppBuildParam buildParam)
	{
		EditorUserBuildSettings.development = false;
		EditorUserBuildSettings.connectProfiler = false;
		EditorUserBuildSettings.buildWithDeepProfilingSupport = false;

		if (buildParam.BuildTarget == BuildTarget.Android && buildParam.DevelopmentProfile)
		{
			Debug.Log("development build activated.");
			EditorUserBuildSettings.development = true;
			EditorUserBuildSettings.connectProfiler = true;
			EditorUserBuildSettings.buildWithDeepProfilingSupport = false; // 필요시 사용
		}
	}

	static void SetDefinedSymbols(AppBuildParam buildParam)
	{
		string[] symbols;
		PlayerSettings.GetScriptingDefineSymbols(buildParam.NamedBuildTarget, out symbols);
		// ENV_ 키워드
		symbols = symbols.GetRemoved("ENV_DEV");
		symbols = symbols.GetRemoved("ENV_QA");
		symbols = symbols.GetRemoved("ENV_LIVE");
		symbols = symbols.GetAdded($"ENV_{buildParam.EnvType.ToString().ToUpper()}");
		// DEBUG 키워드
		// 주의: 에디터 환경 또는 DevelopmentBuild옵션시엔 DEBUG 키워드가 유니티에 의해 활성화된다.
		// 조건에 따라서 DEBUG와 RELEASE키워드가 양립할 수 있음을 주의.
		symbols = symbols.GetRemoved("DEBUG");
		symbols = symbols.GetRemoved("RELEASE");
		if (buildParam.UseDebug)
		{
			symbols = symbols.GetAdded("DEBUG");
		}
		else
		{
			symbols = symbols.GetAdded("RELEASE");
		}

		PlayerSettings.SetScriptingDefineSymbols(buildParam.NamedBuildTarget, symbols);
	}

	//코드 난독화 적용 여부
	static void SetupObf(bool isEnabled)
	{
		ObfuscatorSettingsModifier.ToggleObfuscator(isEnabled);
		
		DebugEx.Log($"Obfuscator enabled : {isEnabled}");
	}

	static string FormatFileName(AppBuildParam buildParam)
	{
		var marketTag = MarketTagName(buildParam);

		DateTime buildTime = DateTime.ParseExact(buildParam.Timestamp, "yyMMdd_HHmm", null);

		if (buildParam.BuildTarget == BuildTarget.iOS)
		{
			return $"{buildTime.Year:D4}-{buildTime.Month:D2}-{buildTime.Day:D2}_{buildTime.Hour:D2}-{buildTime.Minute:D2}_{buildParam.EnvType}";
		}
		else
		{
			return $"GunfightKing_{marketTag}_v{buildParam.AppVersion:D3}({buildParam.BuildNo})_{buildParam.EnvType}_{buildTime.ToString("yyMMdd_HHmm")}_{buildParam.PhotonGameVersion}_{buildParam.Revision.Substring(0, 5)}";
		}
	}

	static string FormatObfFileName(AppBuildParam buildParam)
	{
		DateTime buildTime = DateTime.ParseExact(buildParam.Timestamp, "yyMMdd_HHmm", null);

		var marketTag = MarketTagName(buildParam);
		
		return $"GunfightKing_{marketTag}_v{buildParam.AppVersion:D3}({buildParam.BuildNo})_{buildParam.EnvType}_{buildTime.ToString("yyMMdd_HHmm")}_{buildParam.PhotonGameVersion}_{buildParam.Revision.Substring(0, 5)}";
	}

	static string MarketTagName(AppBuildParam buildParam)
	{
		var marketTag = buildParam.MarketType switch
		{
			MarketType.Google => "AOS",
			MarketType.Apple => "IOS",
			MarketType.Window => "WOS",
			MarketType.Samsung => "SGS",
			_ => throw new Exception("Unhandled marketType: " + buildParam.MarketType)
		};

		return marketTag;
	}

	static string AppFileOSTag(BuildTarget buildTarget)
	{
		return buildTarget switch
		{
			BuildTarget.StandaloneWindows64 => "W64",
			BuildTarget.Android => "AOS",
			BuildTarget.iOS => "IOS",
			_ => throw new System.Exception("Not supported buildTarget: " + buildTarget)
		};
	}

	// 아직은 자동 결정. 추후 직접 선택으로 변경 필요.
	public static MarketType DetermineMargetType(BuildTarget buildTarget, bool samsungBuild)
	{
		return buildTarget switch
		{
			BuildTarget.StandaloneWindows64 => MarketType.Window,
			BuildTarget.Android => samsungBuild ? MarketType.Samsung : MarketType.Google,
			BuildTarget.iOS => MarketType.Apple,
			_ => throw new System.Exception("Not supported buildTarget: " + buildTarget)
		};
	}
	static string AppFileExtensionName(BuildTarget buildTarget, bool useAAB)
	{
		return buildTarget switch
		{
			BuildTarget.StandaloneWindows64 => "exe",
			BuildTarget.Android => useAAB ? "aab" : "apk",
			BuildTarget.iOS => "ipa",
			_ => throw new System.Exception("Not supported buildTarget: " + buildTarget)
		}; ;
	}
}
