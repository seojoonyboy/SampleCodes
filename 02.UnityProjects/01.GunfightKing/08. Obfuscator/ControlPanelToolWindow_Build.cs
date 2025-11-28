using AddressablesPlayAssetDelivery;
using Framework;
using Game.Data;
using System;
using System.ComponentModel;
using UnityEditor;
using UnityEngine;

partial class ControlPanelToolWindow
{
	static Texture _appBuildTitleIcon;
	static BoolPref _isAddressableCleanBuild = new BoolPref("BuildTool.AddressableCleanBuild");
	static BoolPref _isSkipBuildAddressable = new BoolPref("BuildTool.SkipBuildAddressable");
	//--static BoolPref _dontRollbackModifiedFiles = new BoolPref("BuildTool.DontRollbackModifiedFiles");
	AppBuildParam _buildParam;
	bool _isBuilding = false;

	void DrawBuildTool()
	{
		GUI.changed = false;

		try
		{
			_buildParam ??= GenerateBuildParam();
		}
		catch (Win32Exception e)
		{
			EditorGUILayout.HelpBox("환경 오류:\n" + e.Message, MessageType.Error);
			return;
		}

		NgEditorTools.BeginContents(new Color(0, 1.0f, 0.2f, 0.2f));
		{
			bool canBuild;
			DrawBuildTool_Header(out canBuild);

			if (canBuild)
			{
				DrawBuildTool_Arguments();

				DrawBuildTool_Buttons();

				GUILayout.Space(20);

				DrawBuildTool_Test();
			}
		}
		NgEditorTools.EndContents();

		if (GUI.changed)
		{
			_buildParam = GenerateBuildParam();
			_devCfg.SaveNow();
		}
	}

	void DrawBuildTool_Header(out bool canBuild)
	{
		string iconName = null;
		canBuild = false;

		if (ApplicationInfo.IsAndroidTarget)
		{
			iconName = "AndroidBuild";
			canBuild = true;
		}
		else if (ApplicationInfo.IsIOSTarget)
		{
			iconName = "IosBuild";
			canBuild = true;
		}
		else if (ApplicationInfo.IsStandAloneWin)
		{
			iconName = "StandAloneWinBuild";
			canBuild = true;
		}

		if (canBuild)
		{
			_appBuildTitleIcon ??= (Texture2D)AssetDatabase.LoadAssetAtPath($"Assets/Editor/ToolIcons/ToolIcon_{iconName}.png", typeof(Texture2D));
			DrawTitle("앱 빌드", _appBuildTitleIcon);
		}
		else
		{
			GUILayout.Label("빌드할 수 없는 플렛폼 설정입니다", _boldFont);
		}
	}

	void DrawBuildTool_Arguments()
	{
		GUILayout.Space(5);

		using (new GUILayout.HorizontalScope())
		{
			SubtitleLabel("앱버전");

			_devCfg.Build.Version = EditorGUILayout.IntField(_devCfg.Build.Version, GUILayout.Width(50f));
			int ver = _devCfg.Build.Version;
			GUILayout.Label($"(v{ver / 100}.{ver % 100 / 10}.{ver % 10})");
			GUILayout.Space(5);

			SubtitleLabel("빌드번호");
			_devCfg.Build.BuildNo = Mathf.Clamp(EditorGUILayout.IntField(_devCfg.Build.BuildNo, GUILayout.Width(40f)), 1, 9);

			GUILayout.FlexibleSpace();
		}

		GUILayout.Space(5);

		using (new GUILayout.HorizontalScope())
		{
			SubtitleLabel("빌드 환경");
			_devCfg.Build.EnvType = (EnvType)EditorGUILayout.EnumPopup(_devCfg.Build.EnvType, GUILayout.Width(50f));

			GUILayout.Space(5);

			SubtitleLabel("갤럭시 스토어");
			_devCfg.Build.SamsungBuild = EditorGUILayout.Toggle(_devCfg.Build.SamsungBuild, GUILayout.Width(50f));
			
			GUILayout.FlexibleSpace();
		}

		GUILayout.Space(5);

		using (new GUILayout.HorizontalScope())
		{
			SubtitleLabel("포톤게임버전");
			_devCfg.Build.IngameVersion = EditorGUILayout.TextField(_devCfg.Build.IngameVersion, GUILayout.Width(50f));
			GUILayout.Label($"{_devCfg.Build.EnvType.ToString()[0]}");

			GUILayout.FlexibleSpace();
		}
		GUILayout.Space(5);

		if (ApplicationInfo.IsAndroidTarget)
		{
			using (new GUILayout.HorizontalScope())
			{
				SubtitleLabel("AAB 적용");
				_devCfg.Build.AAB = (AutoBoolBuildOptionType)EditorGUILayout.EnumPopup(_devCfg.Build.AAB, GUILayout.Width(50f));
				GUILayout.FlexibleSpace();
			}
			GUILayout.Space(5);
		}

		using (new GUILayout.HorizontalScope())
		{
			SubtitleLabel("디버깅 모드");
			_devCfg.Build.Debug = (DebugBuildOptionType)EditorGUILayout.EnumPopup(_devCfg.Build.Debug, GUILayout.Width(50f));
			GUILayout.FlexibleSpace();
		}
		GUILayout.Space(5);

		
		/*--
		using (new GUILayout.HorizontalScope())
		{
			SubtitleLabel("Debug Compile");
			_devCfg.Build.CppDebug = EditorGUILayout.Toggle(_devCfg.Build.CppDebug, GUILayout.Width(50f));
			GUILayout.FlexibleSpace();
		}
		GUILayout.Space(5);
		--*/
		using (new GUILayout.HorizontalScope())
		{
			SubtitleLabel("버전정보");
			GUILayout.Label($"{BuildUtil.GetVersionStringByBuildParam(_buildParam)}");
			GUILayout.FlexibleSpace();
		}
		
		GUILayout.Space(5);

		using (new GUILayout.HorizontalScope())
		{
			_isAddressableCleanBuild.Value = EditorGUILayout.Toggle(_isAddressableCleanBuild.Value, GUILayout.Width(20f));
			SubtitleLabel("어드레서블 클린 빌드");
			GUILayout.FlexibleSpace();
		}
		using (new GUILayout.HorizontalScope())
		{
			_isSkipBuildAddressable.Value = EditorGUILayout.Toggle(_isSkipBuildAddressable.Value, GUILayout.Width(20f));
			SubtitleLabel("어드레서블 빌드 스킵");
			GUILayout.FlexibleSpace();
		}
		using (new GUILayout.HorizontalScope())
		{
			_devCfg.Build.DevelopmentProfile = EditorGUILayout.Toggle(_devCfg.Build.DevelopmentProfile, GUILayout.Width(20f));
			SubtitleLabel(ApplicationInfo.IsIOSTarget ? "Development Profile 사용" : "Development build");
			GUILayout.FlexibleSpace();
		}
		GUILayout.Space(5);

		/*--
		using (new GUILayout.HorizontalScope())
		{
			_dontRollbackModifiedFiles.Value = EditorGUILayout.Toggle(_dontRollbackModifiedFiles.Value, GUILayout.Width(20f));
			SubtitleLabel("빌드 후 바뀐 파일 롤백 금지(빌드 로그 확인 위해)");
			GUILayout.FlexibleSpace();
		}
		--*/

		GUILayout.Space(5);

		using (new GUILayout.HorizontalScope())
		{
			_devCfg.Build.ObfEnable = EditorGUILayout.Toggle(_devCfg.Build.ObfEnable, GUILayout.Width(20f));
			SubtitleLabel("난독화 적용");
			GUILayout.FlexibleSpace();
		}
		
		GUILayout.Space(5);
	}

	void DrawBuildTool_Buttons()
	{
		bool doBuild = false;
		bool onlyAddressable = false;
		using (new GUILayout.HorizontalScope())
		{
			if (GUILayout.Button("빌드 시작", GUILayout.Width(80), GUILayout.Height(50)))
			{
				doBuild = EditorUtil.MsgBox_OkCancel($"현재 설정({EditorUserBuildSettings.activeBuildTarget})으로 빌드를 시작합니다.");
			}
			
			GUILayout.Space(10);

			if (GUILayout.Button("어드레서블만\n빌드", GUILayout.Width(90), GUILayout.Height(50)))
			{
				doBuild = EditorUtil.MsgBox_OkCancel($"현재 설정({EditorUserBuildSettings.activeBuildTarget})으로 어드레서블만 빌드를 시작합니다.");
				onlyAddressable = true;
			}
		}

		if (doBuild && !_isBuilding)
		{
			_isBuilding = true;

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
		}
	}

	string _switchTargetFileName;

	void DrawBuildTool_Test()
	{
		NgEditorTools.BeginContents();
		if (NgEditorTools.DrawHeader("Switch File Test", false))
		{
			GUILayout.Label("TargetFilePath");
			_switchTargetFileName = GUILayout.TextField(_switchTargetFileName);

			using (new GUILayout.HorizontalScope())
			{
				if (GUILayout.Button("구글", GUILayout.Width(80), GUILayout.Height(50)))
				{
					BuildUtil.SwitchFilesForStore(MarketType.Google, _switchTargetFileName, false);
				}
				if (GUILayout.Button("삼성", GUILayout.Width(80), GUILayout.Height(50)))
				{
					BuildUtil.SwitchFilesForStore(MarketType.Samsung, _switchTargetFileName, false);
				}
				if (GUILayout.Button("애플", GUILayout.Width(80), GUILayout.Height(50)))
				{
					BuildUtil.SwitchFilesForStore(MarketType.Apple, _switchTargetFileName, false);
				}
			}
		}
		NgEditorTools.EndContents();
	}

	AppBuildParam GenerateBuildParam()
	{
		AppBuildParam buildParam = new AppBuildParam();
		buildParam.BuildFromEditorMenu = true;
		buildParam.BuildTarget = EditorUserBuildSettings.activeBuildTarget;
		buildParam.AppVersion = _devCfg.Build.Version;
		buildParam.BuildNo = _devCfg.Build.BuildNo;
		
		buildParam.EnvType = _devCfg.Build.EnvType;

		buildParam.MarketType = AppBuilder.DetermineMargetType(buildParam.BuildTarget, _devCfg.Build.SamsungBuild);

		buildParam.PhotonGameVersion = $"{_devCfg.Build.IngameVersion}{_devCfg.Build.EnvType.ToString()[0]}";
		buildParam.Revision = BuildUtil.GetGitRevision();
		buildParam.Timestamp = DateTime.Now.ToString("yyMMdd_HHmm");
		buildParam.ObfEnable = _devCfg.Build.ObfEnable;
		buildParam.UseDebug = _devCfg.Build.Debug switch
		{
			DebugBuildOptionType.Auto => (buildParam.EnvType != EnvType.Live),
			DebugBuildOptionType.Release => false,
			DebugBuildOptionType.Debug or _ => true,
		};
		buildParam.UseAAB = _devCfg.Build.AAB switch
		{
			AutoBoolBuildOptionType.Auto => (buildParam.EnvType == EnvType.Live && buildParam.MarketType == MarketType.Google),
			AutoBoolBuildOptionType.True => true,
			AutoBoolBuildOptionType.False or _ => false,
		};
		buildParam.CppDebug = _devCfg.Build.CppDebug;
		buildParam.AddressableCleanBuild = _isAddressableCleanBuild.Value;
		buildParam.SkipBuildAddressable = _isSkipBuildAddressable.Value;
		//--buildParam.DontRollbackModifiedFiles = _dontRollbackModifiedFiles.Value;
		buildParam.DevelopmentProfile = _devCfg.Build.DevelopmentProfile;

		return buildParam;
	}
}