## CoverPoint 시스템

AI Bot이 교전 중 무작정 노출된 채로 싸우지 않고, 근처의 은폐 지점(엄폐물)을 활용해서 움직이도록 하기 위한 시스템이다. 맵마다 `bl_AICoverPoint` 컴포넌트를 씬에 직접 배치하고, 배치된 포인트들끼리는 에디터의 Bake 버튼 한 번으로 인접 관계를 계산해 그래프 형태로 연결한다. 전투 중에는 `bl_AICoverPointManager`가 전체 CoverPoint를 들고 있다가 Bot의 요청에 따라 두 가지 방식으로 CoverPoint를 골라준다. 하나는 "지금 위치에서 가장 가까운, 최근 다른 Bot이 쓰지 않은 CoverPoint"를 찾는 `GetCloseCover` 계열(사용 이력 + 이웃 우회 로직)이고, 다른 하나는 반경 내에서 무작위로 고르는 `GetCoverOnRadius` 계열이다. 이 저장소의 `04. StateMachine/States/Attacking.cs`가 실제로 호출하는 쪽은 후자이다.

> 근거: 두 파일은 MFPS 프레임워크 경로(`Assets/MFPS/Scripts/GamePlay/AI/`)에 있으며, git 이력(2025-01 이후)에서 확인되는 본인 커밋은 총 4건이다. LR-181(2025-01-10)에서 방향 지정형 `GetCoverOnRadius`를, LR-347(2025-05-21)에서 `except` 인자를 받는 `GetCoverOnRadius`를 추가했고, LR-236·LR-222(2025-03-27)에서 `bl_AICoverPoint`의 기즈모를 껐다가 같은 날 다시 켰다. Bake·커스텀 Inspector·PlayerPrefs 토글 코드의 작성 시점은 이 export(2025-01-06 ~ 2025-11-26)로는 확인되지 않는다.

<img width="1878" height="956" alt="Coverpoint" src="https://github.com/user-attachments/assets/bda63db4-f382-499e-a9a9-a1c2d70a7848" />

### [1단계] 배치 — bl_AICoverPoint

맵마다 `bl_AICoverPoint` 컴포넌트를 원하는 곳에 배치한다. 배치된 CoverPoint는 씬 뷰에서 기즈모(파란 표식과 이웃 연결선)로 표시되어 위치와 연결 관계를 시각적으로 바로 확인할 수 있고, 표시 여부는 `PlayerPrefs` 키(`AICoverPointTool.ShowPoints`, `AICoverPointTool.ShowNeighbors`)로 매니저 Inspector에서 켜고 끈다.

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

각 CoverPoint는 자신과 인접한 다른 CoverPoint 목록(`NeighbordPoints`)과 마지막 사용 시각(`lastUseTime`)을 들고 있고, 그중 사용 가능한(`Time.time - lastUseTime > UsageTime`, 기본 10초) 이웃을 찾아주는 헬퍼도 자체적으로 가지고 있다.

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

인접 관계 자체는 사람이 일일이 연결하는 게 아니라, 매니저의 Bake 버튼(또는 ContextMenu `Calculate Neighbors`)이 호출하는 `CalcuNeighbords()`를 한 번 실행하면 거리 기준(`maxNeighbordDistance`, 기본 25)으로 자동 계산되어 `NeighbordPoints`에 채워진다. 이 메서드는 `bl_AICoverPoint`가 아니라 `bl_AICoverPointManager` 안의 `#if UNITY_EDITOR` 블록에 있어서 `UnityEditor` API를 써도 빌드에는 포함되지 않는다. 판정은 3D 직선거리만 사용하며, 이동 가능 여부를 보는 `HasNavigationPath` 호출은 주석 처리되어 있다.

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

전체 CoverPoint에 대한 관리는 `bl_AICoverPointManager`에서 담당한다. Bot이 실제로 CoverPoint를 활용하고자 할 때는 이 매니저에게 요청해서 결과를 반환받는 구조다. 그 예로 현재 위치에서 `MaxDistance`(기본 50) 안에 있는 가장 가까운 CoverPoint를 찾는 함수는 다음과 같다. 후보가 하나도 없으면 `null`을 반환한다.

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

단순히 가장 가까운 포인트를 반환하는 것에서 그치지 않고, 그 포인트가 최근(`UsageTime` 이내) 사용된 곳이라면 이웃 포인트로 대체해주는 로직(`CheckCoverUsage`)이 붙어 있다. 사용 여부는 소유자를 추적하는 방식이 아니라 마지막 사용 시각(`lastUseTime`)만 비교하는 시간 기반 판정이며, 반환되는 순간 그 포인트의 `lastUseTime`이 갱신된다.

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

이 외에도 반경 내 무작위 CoverPoint를 뽑는 `GetCoverOnRadius` 계열 함수들이 있는데, 회피(`AvoidAttacking`)나 은폐 전투(`CoveringAttacking`) 상태에서 "지금 있는 곳 말고 다른 CoverPoint로 이동"할 때 이 함수들을 사용한다(`04. StateMachine/States/Attacking.cs`, 반경 10). 이 계열은 `CheckCoverUsage`나 `lastUseTime`을 거치지 않고 반경 내 후보 중 무작위로 고른다. 반대로 `GetCloseCover`/`CheckCoverUsage`를 호출하는 곳은 이 저장소에 공개된 코드에서는 확인되지 않는다.

Editor 쪽에는 커스텀 Inspector도 붙어 있어서(같은 파일 안 `#if UNITY_EDITOR` 블록), 씬 뷰에서 CoverPoint/이웃 연결/WayPoint 표시 여부를 토글로 켜고 끌 수 있고, 버튼 한 번으로 이웃 계산과 바닥 정렬(위에서 아래로 레이캐스트해 지면에 붙이기, `FixedFloorPos`)을 실행할 수 있다.

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

- **위치/Transform 캐싱** : `bl_AICoverPoint.Position`은 `transform.position`을 매번 읽지 않고 최초 1회만 캐싱해서 반환한다. CoverPoint는 배치 이후 런타임에 움직이지 않는 정적 오브젝트라는 전제이므로, 매니저가 모든 CoverPoint를 순회하며 거리를 비교하는 탐색 루프에서 불필요한 네이티브 호출을 줄여준다.
- **정적 리스트(`AllCovers`)로 관리** : CoverPoint 자신이 `Awake()`에서 `bl_AICoverPointManager.Register`로 스스로를 등록하고, 매니저는 static 리스트로 전체를 들고 있다. 씬 하나에 CoverPoint가 아무리 많아도 전역에서 한 번에 조회할 수 있고, 대신 매니저의 `OnDestroy()`에서 반드시 리스트를 비워줘야 하는 책임이 따라온다는 점도 코드에 그대로 드러난다.
- **"가장 가까운 곳"이 항상 정답은 아니게 만드는 사용 이력 로직** : `CheckCoverUsage`는 여러 Bot이 같은 CoverPoint로 몰리는 것을 줄이기 위한 장치다. 가장 가까운 포인트가 최근 사용된 곳이면 사용 가능한 이웃으로 우회시키고, 사용 가능한 이웃이 없으면 이웃 중 무작위 하나를 반환한다. 이웃이 아예 없는 포인트인 경우에만 `onlyAllowOneBotPerCover`(기본 `true`)에 따라 포기(`null`)하며, `forceAvaliable`을 켠 `GetCloseCoverForced`는 이 포기를 건너뛴다. 완전한 점유 방지가 아니라 시간 기반으로 겹침을 줄이는 방식이다.

### 한계와 개선 방향

- **static `AllCovers`의 등록/해제 비대칭** : `Register`는 `Awake()`에서 중복 검사 없이 `Add`만 하고, 해제는 매니저 `OnDestroy()`의 `AllCovers.Clear()` 한 곳뿐이다(`bl_AICoverPoint`에는 `OnDestroy`/`OnDisable`이 없다). 런타임에 CoverPoint 하나만 파괴돼도 목록에 남아 후보로 계속 잡히고, `Position`이 캐시된 뒤라면 옛 위치를 그대로 돌려준다. → `OnEnable`/`OnDisable`에서 등록/해제하는 쌍을 만들고 `HashSet`으로 중복을 막는다.
- **`localPosition`과 월드 좌표의 혼용** : `GetCloseCover`, `GetCoverOnRadius`, `GetCloseCoverForced` 등은 `target.localPosition`을 CoverPoint의 월드 좌표(`Position`)와 비교한다. 방향 오버로드 `GetCoverOnRadius(Transform, float, Vector3)`는 방향 계산에는 `target.position`, 거리 계산에는 `target.localPosition`을 함께 쓴다. 부모가 없는 루트 오브젝트만 넘긴다면 같은 값이지만, 부모 Transform이 있으면 거리가 어긋나며 코드상 이를 보장하지 않는다. → `target.position`으로 통일한다.
- **점유가 아니라 시간 기반 예약** : `CheckCoverUsage`는 `lastUseTime`과 `UsageTime`(기본 10초)만 비교하므로, Bot이 엄폐물 뒤에서 10초 넘게 머물면 다른 Bot에게 다시 배정될 수 있다. 사용 가능한 이웃이 없을 때는 이미 쓰는 이웃을 무작위로 반환하고(`onlyAllowOneBotPerCover`는 이웃이 없는 포인트에만 적용), 반환 시점에 그 이웃의 `lastUseTime`까지 갱신한다. → 점유자(Bot)를 기록하고 상태 `Exit()`에서 반납하는 방식으로 바꾸며, 이웃이 모두 사용 중이면 무작위 반환 대신 다음 후보를 탐색하거나 `null`을 돌려준다.
- **실사용 경로와 사용 이력 로직의 분리** : `Attacking.cs`가 호출하는 `GetCoverOnRadius` 계열은 `IsAvailable`/`lastUseTime`을 보지 않으므로, `CoveringAttacking`에서는 여러 Bot이 같은 CoverPoint를 고를 수 있다(`except`는 자기 직전 포인트만 제외한다). → `GetCoverOnRadius` 후보 필터에 `IsAvailable(UsageTime)`을 넣거나 `CheckCoverUsage`를 거치게 해 두 경로의 규칙을 통일한다.
- **후보가 없을 때의 폴백과 호출당 할당** : 반경 내 후보가 없으면 `GetCoverOnRadius`는 맵 전체 `AllCovers`에서 무작위 하나를 반환해 Bot이 멀리 있는 포인트로 이동할 수 있고, 방향 오버로드는 NavMesh 검사 없이 `target.position + targetDir * radius` 좌표를 반환한다. `AllCovers`가 비면 호출 때마다 `Debug.LogWarning`을 남기고, 후보 수집용 `new List<bl_AICoverPoint>()`도 호출마다 새로 할당한다. → 반경 밖이면 `null`이나 가장 가까운 포인트를 반환하고, 좌표는 `NavMesh.SamplePosition`으로 보정하며, 경고는 1회만 남기고 임시 리스트는 재사용한다.
- **Bake의 직선거리 판정과 수동 갱신** : `CalcuNeighbords`는 O(N²) 순회에 3D 직선거리(`maxNeighbordDistance`)만 보므로 벽 너머의 포인트도 이웃이 되고(`HasNavigationPath`는 주석 처리), `FindObjectsByType`의 기본 동작상 비활성 오브젝트는 계산에서 빠진다. 결과는 Bake 버튼을 다시 눌러야 갱신되고 `Undo` 기록 없이 `SetDirty`만 호출한다. → `NavMesh.CalculatePath`로 도달 가능한 이웃만 남기고, `Undo.RecordObject`를 추가하며, 씬 저장 시점 검증(이웃 누락/비대칭 경고)을 붙인다.

관련 코드: [WayPoint](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/02.%20WayPoint), [StateMachine](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/04.%20StateMachine), [MasterClientBotManaging](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/07.%20MasterClientBotManaging)
