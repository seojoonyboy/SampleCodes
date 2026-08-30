## WayPoint 시스템

교전 중이 아닐 때 AI Bot이 맵 전체를 자연스럽게 돌아다니게 만들기 위한 이동 경로 시스템이다. CoverPoint가 "전투 중 은신처"를 다룬다면, WayPoint는 "전투가 시작되기 전 어디를 거쳐서 이동할 것인가"를 다룬다. 포인트는 두 종류로 나뉜다.

<img width="1874" height="953" alt="Waypoint" src="https://github.com/user-attachments/assets/80d09eef-c9c8-4eaa-8b4b-8f637b7533dd" />

- **빨간색 (ESSENTIAL)** : 팀전(팀데스매치, 폭탄모드)에서 Bot이 탐색 상태일 때 반드시 거쳐야 하는 주요 거점. 전투가 벌어지기 전까지는 이어진 인접 ESSENTIAL 포인트를 경로 삼아 이동하며, 분기점이 있으면 무작위로 하나를 선택한다.
- **노란색 (NORMAL)** : 개인전(솔로 데스매치)에서 Bot이 탐색 상태일 때 무작위로 다음 목적지로 삼는 포인트. 팀전에서도 ESSENTIAL 경로를 다 돈 이후 다음 목적지를 정할 때 보조적으로 쓰인다.

### 포인트 정의 — AIWayPoint

각 WayPoint는 자신의 타입(NORMAL/ESSENTIAL)과, ESSENTIAL인 경우 다음/이전 포인트 목록(`nextPoints`/`prevPoints`)을 가진다. 씬 뷰에서는 타입별로 색을 다르게 표시하고, 연결선도 함께 그려 기획자가 그래프 구조를 한눈에 파악할 수 있다.

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

커스텀 Editor 스크립트인 `AIWayPointEditor`를 통해, ESSENTIAL 포인트에서 갈 수 있는 다음 거점들을 기획자가 Inspector에서 직접 연결한다. 시작 지점 여부(`IsStartPoint`)와 그 시작 지점이 어느 팀 소유인지(`StartPointOwnerTeam`)도 여기서 지정한다.

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

`Previous Points`는 읽기 전용(`GUI.enabled = false`)으로 노출되는데, 이는 역방향 연결을 사람이 직접 관리하지 않아도 되게 만든 `HandleAutoBackLink` 때문이다. 기획자가 `nextPoints`에 단방향으로만 연결을 추가하면, 반대편 포인트의 `prevPoints`에 자기 자신을 자동으로 등록해준다.

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

씬이 로드되면 `bl_AIManager`가 ESSENTIAL/NORMAL 포인트들을 각각 모아서 들고 있는다.

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

팀전 시작 시에는 팀별 시작 ESSENTIAL 포인트를 골라 그룹 단위 경로를 미리 생성한다.

```csharp
public void InitFirstSpawnPaths()
{
	if (GetGameMode == BattleMode.FFA) { }
	else
	{
		if (GetGameMode.IsOneOf(BattleMode.DM, BattleMode.TDM))
		{
			List<Vector3> points = new List<Vector3>();
		
			List<IGrouping<int, bl_AIShooter>> team1Group = AllBots.FindAll(x => x.AITeam == Team.Team1).GroupBy(x => x.GroupID).ToList();
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

팀의 시작 지점은 `essentialWayPoints.FindAll(x => x.IsStartPoint && x.StartPointOwnerTeam.ToString().Equals(team.ToString()))`로 걸러낸 뒤 그중 하나를 무작위로 고르는 식으로 정해진다 — Editor에서 지정한 `IsStartPoint`/`StartPointOwnerTeam` 값이 런타임 경로 생성의 시작 조건으로 그대로 쓰이는 지점이다.

시작점부터 목적지까지의 경로는 그래프를 랜덤 워크로 순회하며 만든다. 매 단계에서 `nextPoints`와 `prevPoints`를 모두 후보로 넣기 때문에 양방향 이동이 가능하고, 목적지에 도달하지 못한 채 `MAX_PATH_POINT_NUM`을 넘기면 강제로 목적지를 붙여 종료한다.

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

이 경로 생성 함수들은 실제로는 `Searching` 상태(State)의 `DecideRandomMove()`에서 호출된다. 폭탄모드의 수비조는 ESSENTIAL 경로로 설치 구역까지 이동하고, 공격조나 개인전 Bot은 NORMAL 포인트 하나를 무작위로 골라 이동한다 — WayPoint의 두 색이 실제로 갈리는 지점이다.

```csharp
var gameMode = BattleManager.Instance.GetGameMode;
if (gameMode == BattleMode.DM)
{
	//수비조인 경우, 무작위 설치 지역으로 이동한다.
	if (!IsTerrorlistTeam)
	{
		DemolitionBombZone randomDemolitionZone = aiManager.GetRandomDemolitionZone();
		Vector3 begin = shooterAgent.transform.position;
		Vector3 end = randomDemolitionZone.transform.position;

		List<Vector3> newPoints = aiManager.GeneratePathBeginToEndWithEssentialWayPoint(begin, end);
		MakeWayPoints(newPoints);
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
// ... 중략 (개인전을 포함한 그 외 모드는 NORMAL 포인트로 바로 이동) ...
```

### 설계 포인트

- **단방향 입력 + 자동 역링크** : 기획자는 `nextPoints`만 연결하면 되고, `prevPoints`는 Editor가 대칭적으로 관리해준다. 그래프를 손으로 두 번(정방향/역방향) 그리게 하면 실수로 한쪽만 연결되는 경우가 반드시 생기기 때문에, 이걸 코드 레벨에서 원천 차단한 구조다. 동시에 `prevPoints`를 순회하며 실제로 상대가 나를 `nextPoints`에 갖고 있는지 재검증하는 로직까지 있어, 연결을 끊었을 때도 그래프 정합성이 깨지지 않는다.
- **랜덤 워크 기반 경로 생성** : 최단 경로 알고리즘 대신 인접 포인트 중 무작위 하나를 골라 목적지까지 반복해서 나아가는 방식을 택했다. 정확한 최단 경로보다는 "매번 다른 동선으로 움직이는 것처럼 보이는 것"이 자연스러운 Bot처럼 보이는 데 더 중요했기 때문으로 보이며, `MAX_PATH_POINT_NUM`으로 무한 루프 위험을 방어한다.
- **모드별로 다른 포인트 체계를 하나의 그래프 데이터로 흡수** : ESSENTIAL/NORMAL이라는 두 가지 포인트 타입만으로 개인전의 자유 이동과 팀전의 거점 이동을 모두 표현하고, 실제 분기는 `Searching` 상태 쪽 로직(게임 모드/팀 판별)에서 처리한다. WayPoint 데이터 자체는 게임 모드를 모른 채로도 재사용 가능하게 설계된 셈이다.

이 WayPoint/CoverPoint 알고리즘과 Excel 기반 파라미터 테이블, 에디터 툴링을 포함한 Bot AI 전반의 개선 작업으로 사내 AI 평가 점수가 4.6에서 7.8로(약 1.7배) 상승했다.

관련 코드: [CoverPoint](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/01.%20CoverPoint), [StateMachine](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/04.%20StateMachine), [MasterClientBotManaging](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/07.%20MasterClientBotManaging)
