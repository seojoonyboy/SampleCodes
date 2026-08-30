## CoverPoint 시스템

AI Bot이 교전 중 무작정 노출된 채로 싸우지 않고, 근처의 은폐 지점(엄폐물)을 활용해서 움직이도록 하기 위해 만든 시스템이다. 맵마다 기획자/레벨디자이너가 직접 CoverPoint를 배치하고, 배치된 포인트들끼리는 자동으로 인접 관계를 계산해 그래프 형태로 연결한다. 실제 전투 중에는 `bl_AICoverPointManager`가 이 그래프를 바탕으로 "지금 위치에서 가장 가까운, 아직 다른 Bot이 쓰고 있지 않은 CoverPoint"를 찾아서 반환해주는 역할을 한다.

<img width="1878" height="956" alt="Coverpoint" src="https://github.com/user-attachments/assets/bda63db4-f382-499e-a9a9-a1c2d70a7848" />

### [1단계] 배치 — bl_AICoverPoint

맵마다 `bl_AICoverPoint` 컴포넌트를 원하는 곳에 배치한다. 배치된 CoverPoint는 씬 뷰에서 기즈모로 표시되어 기획자가 위치와 연결 관계를 시각적으로 바로 확인할 수 있다.

```csharp
private void OnDrawGizmos()
{
	if(bl_AICoverPointManager.Instance == null)
	{
		return;
	}
	
	if(PlayerPrefs.GetInt("AICoverPointTool.ShowPoints", 1) == 1)
	{ 
		Gizmos.color = Color.blue;
		Gizmos.DrawWireSphere(transform.position, 1);
		Gizmos.DrawCube(transform.position, new Vector3(1, 0.1f, 1));
	}

	if (PlayerPrefs.GetInt("AICoverPointTool.ShowNeighbors", 1) == 1)
	{
		Gizmos.color = Color.gray;
		if (NeighbordPoints.Count > 0)
		{
			for (int i = 0; i < NeighbordPoints.Count; i++)
			{
				if (NeighbordPoints[i] == null) continue;
				Gizmos.DrawLine(transform.position, NeighbordPoints[i].transform.position);
			}
		}
	}
}
```

각 CoverPoint는 자신과 인접한 다른 CoverPoint 목록(`NeighbordPoints`)을 들고 있고, 그중 사용 가능한(최근에 사용되지 않은) 이웃을 찾아주는 헬퍼도 자체적으로 가지고 있다.

```csharp
public bl_AICoverPoint TryGetAvailableNeighbord()
{
	if (NeighbordPoints == null || NeighbordPoints.Count <= 0) return null;

	for (int i = 0; i < NeighbordPoints.Count; i++)
	{
		if (NeighbordPoints[i] == null) continue;

		if (NeighbordPoints[i].IsAvailable(bl_AICoverPointManager.Instance.UsageTime))
			return NeighbordPoints[i];
	}
	return null;
}
```

인접 관계 자체는 사람이 일일이 연결하는 게 아니라, Editor 확장에서 `CalcuNeighbords()`를 한 번 실행하면 거리 기준(`maxNeighbordDistance`)으로 자동 계산되어 `NeighbordPoints`에 채워진다.

```csharp
[ContextMenu("Calculate Neighbors")]
public void CalcuNeighbords()
{
	bl_AICoverPoint[] allCovers = FindObjectsByType<bl_AICoverPoint>(FindObjectsSortMode.None);
	for (int i = 0; i < allCovers.Length; i++)
	{
		Transform t = allCovers[i].transform;
		allCovers[i].NeighbordPoints.Clear();
		for (int e = 0; e < allCovers.Length; e++)
		{
			if (allCovers[i] == allCovers[e]) continue;

			var posA = allCovers[i].transform.position;
			var posB = allCovers[e].transform.position;

			if (Vector3.Distance(posA, posB) <= maxNeighbordDistance)
			{
				//if(HasNavigationPath(posA, posB))
				allCovers[i].NeighbordPoints.Add(allCovers[e]);
			}
		}
		UnityEditor.EditorUtility.SetDirty(allCovers[i]);
	}
}
```

### [2단계] 관리 — bl_AICoverPointManager

전체 CoverPoint에 대한 관리는 `bl_AICoverPointManager`에서 담당한다. Bot이 실제로 CoverPoint를 활용하고자 할 때는 이 매니저에게 요청해서 결과를 반환받는 구조다. 그 예로 현재 위치에서 가장 가까운 CoverPoint를 찾는 함수는 다음과 같다.

```csharp
public bl_AICoverPoint GetCloseCover(Transform target)
{
	if (AllCovers == null || AllCovers.Count <= 0)
	{
		Debug.LogWarning("There is no Cover Points for bots in this scene, bots behave will be limited.");
		return null;
	}

	bl_AICoverPoint cover = null;
	float d = MaxDistance;
	for (int i = 0; i < AllCovers.Count; i++)
	{
		float dis = bl_UtilityHelper.Distance(target.localPosition, AllCovers[i].Position);
		if (dis < MaxDistance && dis < d)
		{
			d = dis;
			cover = AllCovers[i];
		}
	}
	cover = CheckCoverUsage(cover);
	return cover;
}
```

단순히 가장 가까운 포인트를 반환하는 것에서 그치지 않고, 그 포인트가 이미 다른 Bot이 최근에 사용한 곳이라면 이웃 포인트로 대체해주는 로직(`CheckCoverUsage`)이 붙어 있다.

```csharp
public bl_AICoverPoint CheckCoverUsage(bl_AICoverPoint coverSource, bool forceAvaliable = false)
{
	if (coverSource == null)
	{
		return null;
	}

	// If this cover has been used recently, try to find another one
	if ((Time.time - coverSource.lastUseTime) <= UsageTime)
	{
		if (coverSource.HasNeighbords())
		{
			var neighbord = coverSource.TryGetAvailableNeighbord();
			if (neighbord == null)
				neighbord = coverSource.NeighbordPoints[Random.Range(0, coverSource.NeighbordPoints.Count)];

			coverSource = neighbord;
		}
		else
		{
			if (onlyAllowOneBotPerCover && !forceAvaliable)
			{
				coverSource = null;
			}
		}
	}

	if (coverSource == null)
	{
		return null;
	}

	coverSource.lastUseTime = Time.time;
	return coverSource;
}
```

이 외에도 반경 내 무작위 CoverPoint를 뽑는 `GetCoverOnRadius` 계열 함수들이 있는데, 회피(`AvoidAttacking`)나 은폐 전투(`CoveringAttacking`) 상태에서 "지금 있는 곳 말고 다른 CoverPoint로 이동"할 때 이 함수들을 사용한다.

Editor 쪽에는 커스텀 Inspector도 붙어 있어서, 기획자가 씬 뷰에서 CoverPoint/이웃 연결/WayPoint 표시 여부를 토글로 켜고 끌 수 있고, 버튼 한 번으로 이웃 계산과 바닥 정렬(레이캐스트로 지면에 붙이기)을 실행할 수 있다.

```csharp
[CustomEditor(typeof(bl_AICoverPointManager))]
public class bl_AICovertPointManagerEditor : Editor
{
	bl_AICoverPointManager script;

	private void OnEnable()
	{
		script = (bl_AICoverPointManager)target;
	}

	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();
		
		GizmoToggle("Show CoverPoints", "AICoverPointTool.ShowPoints");
		GizmoToggle("Show CoverPoint Neighbors", "AICoverPointTool.ShowNeighbors");
		GizmoToggle("Show WayPoints", "AICoverPointTool.ShowWayPoints");

		GUILayout.Space(10);

		if (GUILayout.Button("Bake Neighbors points"))
		{
			script.CalcuNeighbords();
		}
		if (GUILayout.Button("Align points to floors"))
		{
			script.FixedFloorPos();
		}
	}
	// ... 중략 ...
}
```

### 설계 포인트

- **위치/Transform 캐싱** : `bl_AICoverPoint.Position`은 `transform.position`을 매번 읽지 않고 최초 1회만 캐싱해서 반환한다. CoverPoint는 배치 이후 런타임에 움직이지 않는 정적 오브젝트이므로, 다수의 Bot이 매 프레임 거리 계산에 이 값을 참조하는 상황에서 불필요한 네이티브 호출을 줄이기 위한 선택이다.
- **정적 리스트(`AllCovers`)로 관리** : CoverPoint 자신이 `Awake()`에서 매니저에 스스로를 등록하고, 매니저는 static 리스트로 전체를 들고 있다. 씬 하나에 CoverPoint가 아무리 많아도 전역에서 한 번에 조회할 수 있고, 대신 `OnDestroy()`에서 반드시 리스트를 비워줘야 하는 책임이 따라온다는 점도 코드에 그대로 드러난다.
- **"가장 가까운 곳"이 항상 정답은 아니게 만드는 큐잉 로직** : `CheckCoverUsage`가 존재하는 이유는, 여러 Bot이 동시에 같은 CoverPoint로 몰리는 것을 막기 위함이다. 최근 사용 이력이 있으면 이웃으로 자동 우회시키고, 이웃도 없으면 아예 포기(`null`)하게 되어 있어 — 다수 Bot이 섞여 플레이하는 PVP 환경에서 겹침을 줄이는 장치다.

관련 코드: [WayPoint](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/02.%20WayPoint), [StateMachine](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/04.%20StateMachine), [MasterClientBotManaging](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/07.%20MasterClientBotManaging)
