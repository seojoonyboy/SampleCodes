## WayPoint 시스템

교전 중이 아닐 때 AI Bot이 맵 전체를 자연스럽게 돌아다니게 만들기 위한 이동 경로 시스템이다. CoverPoint가 "전투 중 은신처"를 다룬다면, WayPoint는 "전투가 시작되기 전 어디를 거쳐서 이동할 것인가"를 다룬다. 포인트는 두 종류로 나뉜다.

<img width="1874" height="953" alt="Waypoint" src="https://github.com/user-attachments/assets/80d09eef-c9c8-4eaa-8b4b-8f637b7533dd" />

- **빨간색 (ESSENTIAL)** : 팀전(TDM, 폭탄모드 DM)에서 주요 거점으로 쓰는 포인트. 게임 시작 시 그룹 단위로 미리 만들어 두는 이동 경로(`InitFirstSpawnPaths`)와, 폭탄모드 수비조가 설치 구역으로 이동하는 경로(`GeneratePathBeginToEndWithEssentialWayPoint`)의 재료가 된다. 이어진 인접 ESSENTIAL 포인트를 따라가며, 분기점이 있으면 무작위로 하나를 선택한다.
- **노란색 (NORMAL)** : 개인전(FFA)에서 Bot이 탐색 상태일 때 무작위로 다음 목적지로 삼는 포인트(`GetRandomNormalWayPoint`). 팀전에서도 리스폰 직후나 ESSENTIAL 경로를 다 돈 이후 다음 목적지를 정할 때, 그리고 폭탄모드 공격조의 다음 목적지로 쓰인다.

> 근거: git 이력(2025-01 이후)에서 확인되는 본인 커밋 기준으로, `AIWayPoint.cs`와 `AIWayPointEditor.cs`는 2025-03-27 LR-236 "AI bot 이동 경로 고도화 작업 1차 마무리" 커밋에서 신규 추가되었다. 같은 커밋에 그룹 경로 설정(`AIGroupPathSettings`)과 `bl_AIManager.cs`의 경로 생성 로직(`InitFirstSpawnPaths`, `GenerateEssentialWayPointsToDestination` 등)도 포함되어 있다. 시작 지점 지정(`IsStartPoint`/`StartPointOwnerTeam`, `GetRandomStartingEssentialWayPoints`)은 2025-03-28, `MAX_PATH_POINT_NUM`은 2025-03-31에 추가되었다(LR-236 태그 커밋 11건, 2025-03-27 ~ 04-01). 이후 `bl_AIManager.cs` 수정 커밋이 51건, `Searching.cs` 수정 커밋이 58건 확인되며(`bl_AIManager.cs`는 스폰/동기화 등 다른 기능도 포함), 아래 `DecideRandomMove`의 현재 형태는 2025-11-19 LR-637 커밋을 반영한 것이다.

### 포인트 정의 — AIWayPoint

각 WayPoint는 자신의 타입(NORMAL/ESSENTIAL)과 다음/이전 포인트 목록(`nextPoints`/`prevPoints`)을 필드로 가진다. 이 목록은 ESSENTIAL 포인트에서만 Inspector에 노출되어 편집되고, 런타임 경로 생성도 ESSENTIAL 그래프에서만 사용한다. 씬 뷰에서는 타입별로 색을 다르게 표시하고(`AICoverPointTool.ShowWayPoints` 토글), `nextPoints` 방향으로 연결선도 함께 그려 그래프 구조를 한눈에 파악할 수 있다.

```csharp
[Serializable]
public class AIWayPoint : MonoBehaviour
{
    [Tooltip("필수로 지나야 하는 경유지인 경우 ESSENTIAL. 일반적인 경유지인 경우 NORMAL로 지정")]
    [SerializeField] public Type type;
    
    [HideInInspector] public EssentialPointTeam StartPointOwnerTeam;
    [HideInInspector] public bool IsStartPoint;
    
    [HideInInspector] public List<AIWayPoint> prevPoints;
    [HideInInspector] public List<AIWayPoint> nextPoints;
    
    public enum Type
    {
        NORMAL = 0,
        ESSENTIAL = 1
    }
    
    public enum EssentialPointTeam
    {
        Team1 = 0,
        Team2 = 1
    }

    private void OnDrawGizmos()
    {
        if (PlayerPrefs.GetInt("AICoverPointTool.ShowWayPoints", 1) != 1)
        {
            return;
        }

        if (type == Type.NORMAL)
        {
            Gizmos.color = Color.yellow;
        }
        else if (type == Type.ESSENTIAL)
        {
            Gizmos.color = Color.red;
        }
        
        Gizmos.DrawWireSphere(transform.position, 1);
        Gizmos.DrawCube(transform.position, new Vector3(1, 0.1f, 1));

        if (nextPoints != null && nextPoints.Count > 0)
        {
            foreach (AIWayPoint nextPoint in nextPoints)
            {
                if(nextPoint == null) continue;
                
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, nextPoint.transform.position);
            }
        }
    }
}
```

### 그래프 편집 — AIWayPointEditor

<img width="598" height="399" alt="image" src="https://github.com/user-attachments/assets/07d2019b-2026-4daf-99f0-75fdfaa572ab" />

커스텀 Editor 스크립트인 `AIWayPointEditor`를 통해, ESSENTIAL 포인트에서 갈 수 있는 다음 거점들을 Inspector에서 직접 연결한다. 시작 지점 여부(`IsStartPoint`)와 그 시작 지점이 어느 팀 소유인지(`StartPointOwnerTeam`)도 여기서 지정한다. 이 스크립트(`Assets/Scripts/GameView/Editor/AIWayPointEditor.cs`)는 이 샘플 폴더에는 포함되어 있지 않아, 아래 코드는 git 패치에서 확인한 최종 형태를 옮긴 것이다.

```csharp
public override void OnInspectorGUI()
{
	AIWayPoint script = (AIWayPoint)target;
	
	base.OnInspectorGUI();
	
	GUILayout.BeginVertical("box");
	if (script.type == AIWayPoint.Type.ESSENTIAL)
	{
		prevPointsProp = serializedObject.FindProperty("prevPoints");
		nextPointsProp = serializedObject.FindProperty("nextPoints");
		
		GUI.enabled = false;
		EditorGUILayout.PropertyField(prevPointsProp, new GUIContent("Previous Points"), true);
		
		GUI.enabled = true;
		EditorGUILayout.PropertyField(nextPointsProp, new GUIContent("Next Points"), true);
	}
	GUILayout.EndVertical();

	if (script.type == AIWayPoint.Type.ESSENTIAL)
	{
		int w = ((int)EditorGUIUtility.currentViewWidth / 3) - 25;
	
		GUILayout.BeginVertical("box");
		EditorGUILayout.Space();
		
		script.IsStartPoint = EditorGUILayout.ToggleLeft("IsStartPoint", script.IsStartPoint, GUILayout.Width(w));
		if (script.IsStartPoint)
		{
			script.StartPointOwnerTeam = (AIWayPoint.EssentialPointTeam)EditorGUILayout
				.EnumPopup("StartPointOwnerTeam", script.StartPointOwnerTeam, EditorStyles.toolbarPopup);
		}
		
		EditorGUILayout.EndVertical();
	}
	
	serializedObject.ApplyModifiedProperties();
	
	HandleAutoBackLink(script);
	
	if (GUI.changed) { EditorUtility.SetDirty(script); }
}
```

`Previous Points`는 읽기 전용(`GUI.enabled = false`)으로 노출되는데, 이는 역방향 연결을 사람이 직접 관리하지 않아도 되게 하는 `HandleAutoBackLink` 때문이다. `nextPoints`에 단방향으로만 연결을 추가하면, 반대편 포인트의 `prevPoints`에 자기 자신을 자동으로 등록해준다.

```csharp
private void HandleAutoBackLink(AIWayPoint script)
{
	// 현재 nextPoints와 비교
	foreach (AIWayPoint newPoint in script.nextPoints)
	{
		if (newPoint == null) continue;
		if (!previousNextPoints.Contains(newPoint))
		{
			// 새로 추가된 경우
			if (!newPoint.prevPoints.Contains(script))
			{
				Undo.RecordObject(newPoint, "Add Prev Point");
				newPoint.prevPoints.Add(script);
			}
		}
		
		EditorUtility.SetDirty(newPoint);
	}

	//prev에서 실제로 next로 이 WayPoint를 참조하고 있는지 재확인
	foreach (AIWayPoint prevPoint in script.prevPoints.ToList())
	{
		if (prevPoint == null) continue;
		if (!prevPoint.nextPoints.Contains(script))
		{
			script.prevPoints.Remove(prevPoint);
		}
	}
	
	// 최신 상태 저장
	StoreCurrentNextPoints();
}
```

### 런타임 경로 탐색 — bl_AIManager

씬이 로드되어 `bl_AIManager.Awake()`가 실행되면 ESSENTIAL/NORMAL 포인트들을 각각의 부모 오브젝트(`bl_AICoverPointManager`가 참조를 들고 있다) 아래에서 모아서 들고 있는다.

```csharp
private void InitWayPointSettings()
{
	essentialWayPoints.Clear();
	normalWayPoints.Clear();
	
	essentialWayPoints.AddRange(
		_cpMgr.EssentialWayPointsParent.GetComponentsInChildren<AIWayPoint>()
	);
	
	normalWayPoints.AddRange(
		_cpMgr.NormalWayPointsParent.GetComponentsInChildren<AIWayPoint>()
	);
}
```

팀전에서는 팀별 시작 ESSENTIAL 포인트를 골라 그룹 단위 경로를 미리 생성한다. 팀의 봇들은 `DecideBotGroupIDs`에서 `AIGroupPathSettings`가 정한 분할 유형(2+3, 2+2+1, 1+4, 1x5 중 하나)에 따라 그룹으로 나뉘고, 그룹마다 경로 하나가 만들어진다. 호출 시점은 모드마다 다르다. TDM은 마스터의 `FirstSpawnAllBots()`에서 호출되고(`GetGameMode != BattleMode.DM` 조건), 폭탄모드(DM)는 폭탄 소지 봇이 배정되는 `CarrierAssign` 이벤트(`bl_AIShooterAgent`)에서 호출된다. FFA는 아래 코드에서 보듯 분기가 비어 있어 그룹 경로를 만들지 않고, `DecideBotGroupIDs`에서 `GroupID = -1`만 부여한다.

```csharp
public void InitFirstSpawnPaths()
{
	if (GetGameMode == BattleMode.FFA) { }
	else
	{
		if (GetGameMode.IsOneOf(BattleMode.DM, BattleMode.TDM))
		{
			List<Vector3> points = new List<Vector3>();
		
			List<IGrouping<int, bl_AIShooter>> team1Group = AllBots
				.FindAll(x => x != null && x.AITeam == Team.Team1)
				.GroupBy(x => x.GroupID).ToList();

			foreach (IGrouping<int, bl_AIShooter> groupItem in team1Group)
			{
				AIWayPoint endWayPoint = GetRandomEssentialWayPoint();
				points = GeneratePathStartPointToEndWithEssentialWayPoint(Team.Team1, endWayPoint);
			
				int groupID = groupItem.Key;
				UpdateGroupPath(Team.Team1, groupID, points);
			}
			
			// ... 중략 (Team2도 동일한 방식으로 처리) ...
		}
	}
}
```

팀의 시작 지점은 `GetRandomStartingEssentialWayPoints`에서 `essentialWayPoints.FindAll(x => x.IsStartPoint && x.StartPointOwnerTeam.ToString().Equals(team.ToString()))`로 걸러낸 뒤 그중 하나를 무작위로 고르는 식으로 정해진다 — Editor에서 지정한 `IsStartPoint`/`StartPointOwnerTeam` 값이 런타임 경로 생성의 시작 조건으로 그대로 쓰이는 지점이다. 목적지는 `GetRandomEssentialWayPoint()`로 ESSENTIAL 포인트 중 무작위 하나를 고른다. 이렇게 만들어진 그룹 경로는 봇이 게임 시작 후 처음 스폰될 때 `GetMyPath`로 받아 가고(리스폰 시에는 그룹 경로 대신 NORMAL 포인트 하나를 무작위로 받는다), 폭탄을 소지한 그룹의 경로는 `UpdateBombAssignerPath`가 현재 위치와 설치 구역을 잇는 2점 경로로 덮어쓴다.

시작점부터 목적지까지의 경로는 그래프를 랜덤 워크로 순회하며 만든다. 매 단계에서 `nextPoints`와 `prevPoints`를 모두 후보로 넣기 때문에 양방향 이동이 가능하고, 이미 경로(`result`)에 들어간 포인트는 후보에서 제외하므로 같은 포인트를 다시 밟지는 않는다. 후보가 없거나 `MAX_PATH_POINT_NUM`(10)에 도달하면 종료하는데, 이때 목적지를 무조건 마지막에 붙인다. 즉 랜덤 워크는 목적지를 향해 움직이지 않으며(목적지 방향 필터는 주석 처리되어 있다), 목적지에 닿았는지 검사하지도 않는다. 최종 경로는 시작점 + 무작위 포인트 최대 10개 + 목적지이고, 마지막 구간은 인접하지 않은 포인트 사이일 수 있어 NavMesh 경로 탐색에 맡긴다.

```csharp
private void GenerateEssentialWayPointsToDestination(Team team, AIWayPoint current, AIWayPoint destination, ref List<AIWayPoint> result, int totalPointNum = 0)
{
	List<AIWayPoint> availableWayPoints = new List<AIWayPoint>();
	foreach (AIWayPoint nextPoint in current.nextPoints)
	{
		if(nextPoint == null) continue;
		if(result.Contains(nextPoint)) continue;
		
		availableWayPoints.Add(nextPoint);
	}

	foreach (AIWayPoint prevPoint in current.prevPoints)
	{
		if(prevPoint == null) continue;
		if(result.Contains(prevPoint)) continue;
		
		availableWayPoints.Add(prevPoint);
	}
	
	if(availableWayPoints.Count == 0 || totalPointNum >= MAX_PATH_POINT_NUM)
	{
		result.Add(destination);
		return;
	}
	
	int rndIndex = Random.Range(0, availableWayPoints.Count);

	current = availableWayPoints[rndIndex];
	
	result.Add(current);

	totalPointNum += 1;
	
	GenerateEssentialWayPointsToDestination(team, current, destination, ref result, totalPointNum);
}
```

`GeneratePathBeginToEndWithEssentialWayPoint`는 `Searching` 상태(State)의 `DecideRandomMove()`에서 호출된다. `DecideRandomMove()`는 Bot이 마지막 경유지에 도착했을 때(`IsLastWayPoint()`) 다음 목적지를 정하는 함수이며, 아직 경유지가 남아 있으면 `MoveToNextWayPoint()`로 그대로 이어서 이동한다. 폭탄모드의 수비조는 현재 위치에서 가장 가까운 ESSENTIAL 포인트부터 설치 구역 근처의 ESSENTIAL 포인트까지의 경로를 만들어 이동하고, 공격조나 개인전/TDM Bot은 NORMAL 포인트 하나를 무작위로 골라 이동한다 — WayPoint의 두 색이 실제로 갈리는 지점이다. 2025-11-19 수정으로 설치 구역을 찾을 수 없는 경우의 예외 처리(NORMAL 포인트로 대체)가 추가되었고, `GetRandomDemolitionZone()`의 위치도 `DemolitionBombManager`로 옮겨졌다.

```csharp
private void DecideRandomMove()
{
	bl_AIManager aiManager = bl_AIManager.Instance;

	var gameMode = BattleManager.Instance.GetGameMode;
	if (gameMode == BattleMode.DM)
	{
		//수비조인 경우, 무작위 설치 지역으로 이동한다.
		if (!IsTerrorlistTeam)
		{
			DemolitionBombZone randomDemolitionZone = DemolitionBombManager.Instance.GetRandomDemolitionZone();
			if (randomDemolitionZone != null)
			{
				Vector3 begin = shooterAgent.transform.position;
				Vector3 end = randomDemolitionZone.transform.position;

				List<Vector3> newPoints = aiManager.GeneratePathBeginToEndWithEssentialWayPoint(begin, end);
				MakeWayPoints(newPoints);
			}
			//예외 처리 [폭탄 지역을 찾을 수 없음]
			else
			{
				List<Vector3> newPoints = new List<Vector3>();
				Vector3 newPoint = aiManager.GetRandomNormalWayPoint().transform.position;
				newPoints.Add(newPoint);
				MakeWayPoints(newPoints);
			}
		}
		//공격조인 경우, 무작위 NormalPoints 지역으로 이동한다.
		else
		{
			List<Vector3> newPoints = new List<Vector3>();
			Vector3 newPoint = aiManager.GetRandomNormalWayPoint().transform.position;
			newPoints.Add(newPoint);
			MakeWayPoints(newPoints);
		}
	}
	else
	{
		List<Vector3> newPoints = new List<Vector3>();
		Vector3 newPoint = aiManager.GetRandomNormalWayPoint().transform.position;
		newPoints.Add(newPoint);
		MakeWayPoints(newPoints);
	}
}
```

`MakeWayPoints`(`IAIState`)는 받은 각 지점에 반경 2 이내의 무작위 오프셋을 더하고 `NavMesh.SamplePosition`으로 보정한 뒤 첫 지점을 목적지로 지정하므로, 같은 경로를 따라가는 Bot들도 정확히 같은 좌표를 밟지 않는다.

### 설계 포인트

- **단방향 입력 + 자동 역링크** : 기획자는 `nextPoints`만 연결하면 되고, `prevPoints`는 Editor가 대칭적으로 관리해준다. 그래프를 손으로 두 번(정방향/역방향) 그리게 하면 실수로 한쪽만 연결되는 경우가 생기기 쉬우므로, 이를 코드 레벨에서 막은 구조다. 동시에 `prevPoints`를 순회하며 실제로 상대가 나를 `nextPoints`에 갖고 있는지 재검증하는 로직이 있어, 연결을 끊은 뒤 해당 포인트를 Inspector로 열면 남은 역링크가 정리된다.
- **랜덤 워크 기반 경로 생성** : 최단 경로 알고리즘 대신 인접 포인트 중 무작위 하나를 골라 이어 붙이는 방식을 택했다. 정확한 최단 경로보다는 "매번 다른 동선으로 움직이는 것처럼 보이는 것"이 자연스러운 Bot처럼 보이는 데 더 중요했기 때문으로 보인다. 방문한 포인트를 제외하는 `result.Contains` 검사가 있어 유한한 그래프에서는 자연히 끝나고, `MAX_PATH_POINT_NUM`은 경로 길이(최대 10개 + 목적지)의 상한 역할을 한다.
- **모드별로 다른 포인트 체계를 하나의 그래프 데이터로 흡수** : ESSENTIAL/NORMAL이라는 두 가지 포인트 타입만으로 개인전의 자유 이동과 팀전의 거점 이동을 모두 표현하고, 실제 분기는 `Searching.DecideRandomMove`와 `bl_AIManager.InitFirstSpawnPaths` 쪽 로직(게임 모드/팀 판별)에서 처리한다. `AIWayPoint` 데이터 자체는 게임 모드를 모른 채로도 재사용 가능하게 설계된 셈이다.

이 WayPoint/CoverPoint 알고리즘과 Excel 기반 파라미터 테이블, 에디터 툴링을 포함한 Bot AI 전반의 개선 작업으로 사내 AI 평가 점수가 4.6에서 7.8로(약 1.7배) 상승했다.

관련 코드: [CoverPoint](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/01.%20CoverPoint), [StateMachine](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/04.%20StateMachine), [MasterClientBotManaging](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/07.%20MasterClientBotManaging)
