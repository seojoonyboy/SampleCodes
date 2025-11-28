using Framework;
using System;
using UnityEditor;
using UnityEditor.Build;

/// <summary>
/// AppBuild 스크립트의 인수 통합 구조체.
/// </summary>
public class AppBuildParam
{
	BuildTarget _buildTarget;

	public bool BuildFromEditorMenu;

	public bool BuildAddressableOnly;

	public string Timestamp;
	public BuildTarget BuildTarget
	{
		get => _buildTarget;
		set {
			_buildTarget = value;
			NamedBuildTarget = BuildUtil.GetNamedBuildTarget(value);
			BuildTargetGroup = BuildPipeline.GetBuildTargetGroup(_buildTarget);
		}
	}
	public BuildTargetGroup BuildTargetGroup { get; private set; }
	public NamedBuildTarget NamedBuildTarget { get; private set; }
	public EnvType EnvType;
	public MarketType MarketType;

	public string Revision;

	public int AppVersion;

	/// <summary>
	/// 1~9의 값. 9를 초과할 수 없으며, 대신 마이너 버전을 증가해야 한다.
	/// </summary>
	public int BuildNo;

	/// <summary>
	/// 5자리코드 MMmPB M:메이저버전, m:마이너버전, P:패치버전, B:빌드번호
	/// </summary>
	public int VersionCode => AppVersion * 10 + BuildNo;

	public string PhotonGameVersion;

	/// <summary>
	/// 출력파일 이름 커스텀 지정
	/// </summary>
	public string OutputFileTitle;

	/// <summary>
	/// XCode프로젝트 폴더 이름
	/// </summary>
	public string XcodeProjectRelDir;

	/// <summary>
	/// iOS인경우 Development 프로파일 사용 여부
	/// Android인 경우 debug keystore 사용 여부 (라이센스 결제 테스트시 필요)
	/// </summary>
	public bool DevelopmentProfile;

	/// <summary>
	/// 내부 테스트용 빌드인가 (AppStore Connect에서 출시불가로 표기된다)
	/// </summary>
	public bool InternalTestOnly;

	/// <summary>
	/// DEBUG 정의심볼 생성 여부. (false면 RELEASE심볼 생성됨)
	/// </summary>
	public bool UseDebug;

	/// <summary>
	/// AAB 빌드 여부
	/// </summary>
	public bool UseAAB;

	/// <summary>
	/// C++ Complier Configuration
	/// </summary>
	public bool CppDebug;

	/// <summary>
	/// 이전 빌드 제거 (캐시제거)
	/// </summary>
	public bool AddressableCleanBuild;

	/// <summary>
	/// 어드레서블 빌드 스킵하기
	/// </summary>
	public bool SkipBuildAddressable;

	/// <summary>
	/// 결과물 이동할 최종 위치 
	/// </summary>
	public string DeployDir;
	
	/// <summary>
	/// 난독화 적용 여부
	/// </summary>
	public bool ObfEnable;
}