## Bot AI State Machine

AI Bot의 전투/이동/특수행동을 FSM(Finite State Machine)으로 구현한 부분이다. `IAIState`를 상속하는 클래스 하나가 하나의 행동(대기, 탐색, 여러 공격 방식, 회피, 폭탄 설치/해체 등)을 표현하고, 상태 안에서 조건이 맞으면 `nextState`를 지정하고 스스로 `Exit()`을 호출해 다음 상태로 전이한다. 아래 이미지와 같이 미리 세팅한 WayPoint를 따라 이동하면서, 상황에 따라 전투 상태·탐색 상태·특수 행동(폭탄 설치/해체) 상태로 전환하며 움직인다.

<img width="1081" height="611" alt="시리아맵_에디터" src="https://github.com/user-attachments/assets/e41e8774-7893-4fdc-bfb6-74fecce3f645" />
<img width="1086" height="606" alt="시리아맵_에디터_런타임" src="https://github.com/user-attachments/assets/041a07e9-c94e-4135-a717-a74349367bf7" />

> 근거: 아래 내용은 git 이력(2025-01-06 ~ 2025-11-26, 본인 커밋만 추출, 2025-01 이전의 약 50개 커밋과 다른 팀원의 커밋은 포함되지 않음)에서 확인되는 본인 커밋을 기준으로 정리했다.
> - `IAIState.cs`, `States/*.cs`, `AIStateManager.cs`를 수정한 본인 커밋은 총 95개이고, 시기는 2025-01-06 ~ 2025-11-21이다. 티켓별로는 LR-181 "AI 개선 작업/봇 AI 개발" 31개(2025-01-06 ~ 04-02), LR-637 17개(11-10 ~ 11-21), LR-636 "봇 이동 상태 고도화" 11개(11-11 ~ 11-18), LR-656 "봇 이동 방식 리뉴얼" 4개(11-20 ~ 11-21) 등이다.
> - `AIStateManager.cs`는 2025-01-31(LR-181, 66줄 추가)에 생성된 커밋이 있고, 2025-02-26 "AI 로직 리뉴얼" 커밋에서 null 에이전트 정리(`RemoveAll`)가 추가되었다.
> - `States/*.cs`는 2025-03-19(LR-222, 폴더 이동) 커밋부터 export에 나타난다. 이후 2025-05-21 LR-347 "봇 고도화 3차 연구"에서 `Attacking.cs`(+77/-560), `Searching.cs`(+158/-277)를 크게 정리했다.
> - `Avoiding.cs`는 2025-05-28 LR-258(연막탄)에서 생성(58줄)되었고, `FlashAreaAvoiding`은 2025-05-29 LR-259(섬광탄)에서 추가되었다. 2025-11-21 LR-656 커밋 2개에서 `Avoiding` 생성자의 `duration` 인자, 이동 속도 지정, `SmokeAreaAvoiding.UpdateMoving`(도착 시 종료)이 들어갔다.
> - `BotDef.cs`(220줄 추가)와 `Bot.json`은 2025-05-22 LR-261 "봇 밸런싱을 위한 파라메터 정리" 커밋에서 생성되었고 `Bot.xlsx`도 그 커밋에서 수정되었다. 2025-06-16 LR-424에서 난이도별 그룹화, 2025-11-17 LR-636에서 KD 기반 사격 지연(`ImmatureUserShootDelay*` 필드) 관련 수정이 있다.
> - 2025-11-20에는 Revert/Reapply 쌍이 있다. 타겟이 가까울 때 뒤로 물러나는 동작(LR-637)을 Revert 후 Reapply했고, 이어서 "한 번만 뒤로 살짝 이동"하도록 수정해 재적용했다.
> - export에 없는 이력이 있으므로 "커밋이 없다"는 것이 "다른 사람이 작성했다"는 뜻은 아니다. 반대로 `Avoiding.cs` 등 일부 파일의 최종본에는 export에 포함되지 않은 다른 작성자의 변경이 섞여 있을 수 있다.

전투 상태나 이동 상태에서 Bot의 구체적인 수치(이동속도, 사격 딜레이, 각 행동을 선택할 확률 등)는 대부분 코드가 아니라 Bot 테이블로 관리한다. 기획자가 Excel로 관리하는 이 테이블 값이 `BotDef`(`aiSettings`)로 로드되어 상태 클래스들에서 참조된다. 테이블 구성은 아래 "Bot 테이블" 절에 정리했고, 코드에 남아 있는 상수는 "설계 포인트"와 "한계와 개선 방향"에서 수치로 짚었다.

<img width="1666" height="296" alt="image" src="https://github.com/user-attachments/assets/a7afe331-5642-4547-925b-da135e94887a" />

### 상태 등록/조회 — AIStateManager

`AIStateManager`는 씬에 존재하는 모든 Bot 에이전트를 등록해두고, "지금 특정 상태에 있는 Bot들"을 타입 기준으로 조회할 수 있게 해준다. 등록은 `bl_AIShooterAgent.InitStateMachine()`에서, 해제는 Bot 사망 시점에 일어난다.

```csharp
public class AIStateManager : bl_MonoBehaviour
{
	private static AIStateManager _instance;
	public static AIStateManager Instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = FindFirstObjectByType<AIStateManager>();
			}
			return _instance;
		}
	}

	private List<bl_AIShooterAgent> aiAgents = new List<bl_AIShooterAgent>();

	public void RegisterAgent(bl_AIShooterAgent agent)
	{
		if (!aiAgents.Contains(agent))
		{
			aiAgents.Add(agent);
		}
	}

	public void UnregisterAgent(bl_AIShooterAgent agent)
	{
		if (aiAgents.Contains(agent))
		{
			DebugEx.Log("Case 100 Bot Die : " + agent.name);
			aiAgents.Remove(agent);
		}
	}

	public List<bl_AIShooterAgent> GetAgentsInState<T>() where T : IAIState
	{
		List<bl_AIShooterAgent> agentsInState = new List<bl_AIShooterAgent>();

		aiAgents.RemoveAll(x => x == null);
		
		foreach (var agent in aiAgents)
		{
			if (agent.CurrentState != null && agent.CurrentState.GetType() == typeof(T))
			{
				agentsInState.Add(agent);
			}
		}

		return agentsInState;
	}

	protected override void OnDisable()
	{
		base.OnDisable();

		Reset();
	}

	public void Reset()
	{
		aiAgents.Clear();
	}
}
```

`GetType() == typeof(T)`는 정확히 그 타입만 일치시킨다. 예를 들어 `GetAgentsInState<Searching>()`은 `Searching`의 하위 클래스인 `TargetAreaSearching`, `BombSearching`을 세지 않는다. 이 성질 덕분에 `Searching` 계열 안에서도 `BombSearching`만 따로 조회할 수 있다.

`GetAgentsInState<T>()`는 단순 디버깅용이 아니라 실제 행동 결정에 쓰인다. 이 저장소 기준 호출처는 세 곳이다.

- `IAIState.UpdateAttackerBombDroppedBehavior` : 드랍된 폭탄을 이미 회수하러 가는(`BombSearching`) Bot이 있으면 새로 배정하지 않고 그 Bot의 목적지만 갱신한다.
- `IAIState.UpdateDefenderBombActivatedBehavior` : 폭탄이 활성화되었을 때 이미 해체 중(`BombDefusing`)인 Bot이 있으면 아무것도 하지 않는다. 없으면 설치 지역에서 가장 가까운 수비조 Bot 한 명만 `BombDefusing`으로, 나머지는 `CoveringDefusing`으로 보낸다.
- `CoveringDefusing.SlowUpdate` : 엄호 중인 Bot이 해체 담당이 없다고 판단되면 해체 역할을 이어받는다.

```csharp
public class CoveringDefusing : IAIState
{
	// ... 중략 ...
	public override void SlowUpdate()
	{
		base.SlowUpdate();
		
		// ... 중략 ...
		
		//적이 감지된 경우
		if (detactedTargets.Count == 0) 
		{
			if (IsDMMode)
			{
				DemolitionBombManager demolitionBombManager = DemolitionBombManager.Instance;
				DemolitionBomb bomb = demolitionBombManager.Bomb;
				bool isBombActivated = bomb.bombStatus == BombStatus.Actived;
				
				if (isBombActivated)
				{
					if (!IsTerrorlistTeam)
					{
						AIStateManager aiStateManager = AIStateManager.Instance;
						List<bl_AIShooterAgent> otherUnits = aiStateManager.GetAgentsInState<BombDefusing>();
						if ((otherUnits != null) && (otherUnits.Count == 0))
						{
							nextState = new BombDefusing(shooterAgent);	
							Exit();
						}
					}
				}
			}
		}
	}
}
```

원본 주석은 "적이 감지된 경우"이지만 실제 조건은 `detactedTargets.Count == 0`, 즉 감지된 적이 없을 때다. 정리하면 `CoveringDefusing`은 (1) 감지된 적이 없고 (2) 폭탄이 활성화되었으며 (3) 수비조이고 (4) 다른 `BombDefusing` Bot이 없을 때만 해체 역할을 이어받는다.

### 탐색 — Searching / TargetAreaSearching / BombSearching / DemolitionAreaSearching

`Searching`은 교전 중이 아닐 때 다음 목적지를 결정하는 상태다. `UpdateMoving`은 다음 순서로 판단한다.

1. 감지된 적 중 시야가 확보된 대상이 있으면 `GetRandomAttackState()`로 공격 상태를 고르고 타겟을 지정한다.
2. 폭탄모드에서 폭탄이 활성화되어 있으면 폭탄 근처 무작위 지점으로 경로를 다시 만든다.
3. 보이지는 않지만 감지된 적이 있으면 가장 가까운 감지 대상 쪽으로 목적지를 바꾼다.
4. 감지된 적이 없으면 목적지에 도착했을 때, 폭탄 소지자는 `DemolitionAreaSearching`으로 넘어가고 그 외에는 다음 WayPoint로 이동하거나(마지막 WayPoint면) `DecideRandomMove()`로 새 목적지를 정한다.

폭탄모드에서 수비조는 ESSENTIAL WayPoint 경로로 무작위 설치 구역까지, 공격조와 폭탄모드가 아닌 모드의 Bot은 NORMAL WayPoint 하나를 무작위로 골라 이동한다. 설치 구역은 `DemolitionBombManager.GetRandomDemolitionZone()`으로 얻고, 구역을 찾지 못하면 NORMAL WayPoint로 대체한다.

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

하위 클래스는 다음과 같이 나뉜다.

- `TargetAreaSearching` : 폭탄 설치 후(`Demolitioning.OnBombPlantFinished`)와 폭탄 소지 그룹의 경로 배정(`bl_AIManager.UpdateBombAssignerGroupPath`) 때 쓰인다. 마지막 WayPoint에 도착하면 2초(`repathInterval`)마다 주변 2m 안의 무작위 지점으로 옮겨 다니며 순찰한다.
- `BombSearching` : 공격조가 드랍된 폭탄을 회수하러 가는 상태로, 폭탄 상태가 `Droped`가 아니게 되면 `Searching`으로 돌아간다.
- `DemolitionAreaSearching` : 폭탄을 소지한 공격조 Bot이 설치 구역까지 이동하는 상태다. 설치 가능 구역에 도달하면 `Demolitioning`으로 넘어가고, 공격 상태를 고르는 확률은 이 클래스가 `GetRandomAttackState()`를 override해서 `AttackerBombAssignedBehaviorPercentages`(저돌적/엄폐 2가지)를 쓴다.

```csharp
public class DemolitionAreaSearching : Searching
{
	// ... 중략 ...
	protected override void UpdateMoving()
	{
		//적 감지하는 경우
		if (visableTargets.Count > 0)
		{
			if (shooterAgent.Target != null && IsVisibleTarget(shooterAgent.Target))
			{
				nextState = GetRandomAttackState();
				Exit();
			}
		}
		else
		{
			if (!shooterAgent.IsCarrier) return;

			if (bl_AIManager.Instance.IsCloseToBombInstall(shooterAgent.transform.position))
			{
				//폭탄 설치 가능 지역까지 도달한 경우
				DemolitionBombManager demolitionBombManager = DemolitionBombManager.Instance;

				bool isAvailableToPlant = demolitionBombManager.Bomb.isAvailableToPlant;
				bool isBombActivated = demolitionBombManager.Bomb.bombStatus == BombStatus.Actived;

				if (shooterAgent.IsCarrier && !isBombActivated && isAvailableToPlant)
				{
					nextState = new Demolitioning(shooterAgent);
					Exit();
				}
			}
		}
		
		if (shooterAgent.Agent.remainingDistance <= shooterAgent.Agent.stoppingDistance + 0.5f)
		{
			MoveToNextWayPoint();
		}
	}
}
```

이 코드의 의도는 "이동 중 적이 보이면 즉시 교전"이지만, 현재 코드에는 한 가지 확인된 문제가 있다. `visableTargets`는 `IAIState`에 필드로 선언만 되어 있고 값을 채우는 코드가 저장소 어디에도 없어서(사용처는 이 분기 하나뿐) `visableTargets.Count > 0` 분기는 실행되지 않는다. 즉 이 상태의 Bot이 실제로 공격 상태로 넘어가는 경로는 피격 시 `bl_AIShooterAgent.OnGetHit`의 상태 교체이고, `UpdateMoving` 안의 조기 교전은 동작하지 않는다.

### 폭탄모드 특수 행동 — Demolitioning / BombDefusing

`Demolitioning`은 폭탄 설치 행동이다. 진입하는 즉시 웅크리고(`Enter`), 이후 `SlowUpdate`마다 "`MinRange` 안에 시야가 확보된 적이 없고 설치 가능(`CanPlantBomb`)"이면 이동을 멈추고 설치를 시작한다. 설치가 끝나면 콜백(`OnBombPlantFinished`)에서 `TargetAreaSearching`으로 넘어간다. 웅크리는 시점은 2025-11-21 LR-637 커밋(`SlowUpdate` → `Enter`로 이동)에서 앞당겨졌다.

```csharp
public class Demolitioning : IAIState
{
	public override void Enter()
	{
		base.Enter();
		
		shooterAgent.SetCrouch(true);
	}

	public override void SlowUpdate()
	{
		base.SlowUpdate();
		
		//주변에 적이 없고, 폭탄 설치가 가능한 경우
		DemolitionBombManager demolitionBombManager = DemolitionBombManager.Instance;
		if (!IsVeryCloseEnemyExist() && demolitionBombManager.CanPlantBomb())
		{
			shooterAgent.ToggleMovable(false);

			DebugEx.Log($"[Bomb] {shooterAgent.name} 폭탄 설치중");
			demolitionBombManager.BotPlantBomb(shooterAgent.BotMFPSActor, OnBombPlantFinished);
		}
	}

	private void OnBombPlantFinished()
	{
		nextState = new TargetAreaSearching(shooterAgent);
		Exit();
	}

	public override void Update() { }
}
```

이전 버전에는 설치 도중 초근접한 적이 나타나면 설치를 취소(`BotCancelPlantBomb`)하고 확률로 공격 상태에 전환하는 분기가 있었지만, 2025-11-19 LR-637 커밋(7471ed50)에서 제거되었다. 현재 파일에는 그때 쓰던 `GetRandomAttackState()` override(`AttackerBombInstallBehaviorPercentages` 사용)만 남아 있고, 이 저장소에서는 호출하는 곳이 없다.

`BombDefusing`은 수비조가 활성화된 폭탄을 해체 시도하는 상태다. 진입 시 폭탄 위치를 목적지로 잡고, 해체 가능 지역(`IsCloseToBombDefuse`)에 도달하면 웅크리고 `BotDefuseBomb`을 호출하되, 그 와중에도 시야에 들어온 적이 있으면 사격을 계속한다(`CheckFiring`, `UpdateTargeting`). 해체 중이라고 완전히 무방비 상태가 되지는 않는다.

```csharp
protected override void UpdateMoving()
{
	// ... 중략 ...

	//폭탄 해체 가능 지역까지 도달한 경우
	DemolitionBombManager demolitionBombManager = DemolitionBombManager.Instance;
	if (demolitionBombManager.Bomb == null) return;

	DemolitionBomb bomb = demolitionBombManager.Bomb;
	if (bomb == null) return;

	bool isBombActivated = bomb.bombStatus == BombStatus.Actived;
	if (!isBombActivated)
	{
		nextState = new Searching(shooterAgent);
		Exit();
	}

	CheckFiring();
	
	bool isCloseToBomb = bl_AIManager.Instance.IsCloseToBombDefuse(shooterAgent.transform.position);

	if (isCloseToBomb)
	{
		if (!shooterAgent.IsCrouch) { shooterAgent.SetCrouch(true); }

		demolitionBombManager.BotDefuseBomb(shooterAgent.BotMFPSActor);
		DebugEx.Log("[Bomb] Try Defusing by " + shooterAgent.name);
	}
}
```

`CoveringDefusing`은 해체 담당이 아닌 다른 수비조 Bot이 폭탄 근처를 엄호하며 대기하는 상태다. 3초(`randomMoveInterval`)마다 주변 4m 안의 무작위 지점으로 옮겨 다니며 `CheckFiring`으로 사격하고, 앞의 `GetAgentsInState<BombDefusing>` 절에서 본 조건이 맞으면 스스로 해체 역할을 이어받는다.

### 공격 — Attacking 계열

`Attacking`은 기본 사격 로직(`CheckFiring`), 걷기/뛰기 확률 결정(`DecideRandomSpeed`), 앉기/서기 결정(`SetCrouchOrStand`), 타겟이 너무 가까울 때의 후퇴(`MoveBackwardIfTooCloseToTarget`)를 담고, 실제 움직임 패턴은 하위 클래스로 나뉜다. 상태에 새로 진입할 때(생성자)는 사격 지연 시간을 다시 계산한다(`ResetShootDelayTimer`, 아래 Bot 테이블 절 참고). 각 하위 클래스는 유지 시간이 지나면 다시 `GetRandomAttackState()`로 확률 기반 재선택을 하거나 `Searching`으로 돌아간다.

| 하위 클래스 | 진입 시 동작 | 유지 시간 | 종료 시 전이 |
| --- | --- | --- | --- |
| `AggressiveAttacking` | 타겟을 바라봄 | `AttackBehaviorRemainTimes[0]` | 타겟이 있으면 `GetRandomAttackState()`, 없으면 `Searching` |
| `HoldingPositionAttacking` | 이동을 멈추고 제자리 사격, 진입 후 1초 안에 타겟이 너무 가까우면 한 번 뒤로 물러남 | `AttackBehaviorRemainTimes[1]` | 타겟이 안 보이면 즉시 `Searching`, 시간이 지나면 `GetRandomAttackState()` |
| `MovingAttacking` | 4m 반경 무작위 지점으로 이동, 앉기/서기는 50% | `AttackBehaviorRemainTimes[2]` | 타겟이 보이면 `GetRandomAttackState()`, 아니면 `Searching` |
| `CoveringAttacking` | 10m 안의 CoverPoint로 이동 | `AttackBehaviorRemainTimes[3]` | 타겟이 보이면 `GetRandomAttackState()`, 아니면 `Searching` |
| `AvoidAttacking` | 피격 방향(`lastHitDirection`)의 CoverPoint로 이동 | 코드 상수 `5.0f` (테이블 미사용) | 타겟이 보이면 `GetRandomAttackState()`, 아니면 `Searching` |

`CoveringAttacking`은 CoverPoint 시스템을 그대로 재사용한다.

```csharp
/// <summary>
/// [은폐] 근처 CoverPoint로 이동하여 공격하는 형태
/// </summary>
public class CoveringAttacking : Attacking
{
	float coveringTime = 5.0f;
	float passTime = 0.0f;
	
	bl_AICoverPoint currentCoverPoint;

	public override void Enter()
	{
		base.Enter();

		coveringTime = shooterAgent.aiSettings.AttackBehaviorRemainTimes[3];
		_moveBackwardTime = 0.0f;
		
		shooterAgent.SetLookAtState(AILookAt.Target);
		
		MoveToNextCoverPoint();
	}

	void MoveToNextCoverPoint(Transform from = null)
	{
		SetCrouchOrStand();
		
		if (from == null)
		{
			currentCoverPoint = GetCloseCoverPoint(currentCoverPoint);
		}
		else
		{
			currentCoverPoint = GetCloseCoverPoint(from, currentCoverPoint);
		}

		if (currentCoverPoint != null)
		{
			shooterAgent.SetDestination(currentCoverPoint.Position);
		}
	}
}
```

`AvoidAttacking`은 피격을 받았을 때만 만들어지는 형태다(`GetRandomDamagedState`, `bl_AIShooterAgent.OnGetHit`에서 상태 교체). 타겟을 바라본 채, 피격 방향(총알이 날아온 방향을 추측한 값) 쪽 반경 10m 안의 CoverPoint로 이동하며 사격한다.

```csharp
public class AvoidAttacking : Attacking
{
	public override void Enter()
	{
		base.Enter();

		//총알이 날아온 방향 (추측)
		Vector3 targetCoverPoint = GetCloseCoverPoint(lastHitDirection * 10.0f);
		
		shooterAgent.SetLookAtState(AILookAt.Target);
		shooterAgent.SetDestination(targetCoverPoint);

		_currentAvoidTime = 0.0f;
		_moveBackwardTime = 0.0f;
		
		SetCrouchOrStand();
	}

	private Vector3 GetCloseCoverPoint(Vector3 moveDir)
	{
		return bl_AICoverPointManager.Instance.GetCoverOnRadius(shooterAgent.transform, 10, moveDir);
	}
}
```

공격 방식은 매번 고정된 우선순위가 아니라, Bot 테이블에 정의된 확률 테이블(`AttackBehaviorPercentages`)에서 누적 확률 구간을 만들어 무작위로 뽑는다. 저돌적/제자리/이동/은폐 네 가지 공격 유형을 이 방식으로 고르며(`IAIState.GetRandomAttackState`, 07 폴더), 예를 들어 `[10, 50, 10, 30]`은 누적 `[10, 60, 70, 100]`이 되어 `Random.Range(0, 100)` 값이 속한 구간의 상태가 선택된다. 피격 시에는 같은 확률표로 `GetRandomDamagedState`를 호출하는데, 네 번째 항목만 `CoveringAttacking` 대신 `AvoidAttacking`으로 바뀐다.

```csharp
public virtual IAIState GetRandomAttackState()
{
	float[] percentageRange = new float[shooterAgent.aiSettings.AttackBehaviorPercentages.Length];
	shooterAgent.aiSettings.AttackBehaviorPercentages.CopyTo(percentageRange, 0);

	float sum = 0;

	for (int i = 0; i < percentageRange.Length; i++)
	{
		sum += percentageRange[i];
		percentageRange[i] = sum;
	}

	float randomValue = Random.Range(0, 100);

	for (int i = 0; i < percentageRange.Length; i++) 
	{
		if(randomValue < percentageRange[i])
		{
			switch (i)
			{
				case 0:
					return new AggressiveAttacking(shooterAgent);
				case 1:
					return new HoldingPositionAttacking(shooterAgent);
				case 2:
					return new MovingAttacking(shooterAgent);
				case 3:
					return new CoveringAttacking(shooterAgent);
			}
		}
	}

	//default attack 상태
	return new AggressiveAttacking(shooterAgent);
}
```

### 회피 — Avoiding / SmokeAreaAvoiding / FlashAreaAvoiding

연막탄/섬광탄 범위에 들어갔을 때 벗어나는 행동이다. `bl_AIShooterAgent.OnSmokeBombArea/OnFlashBombArea`가 현재 상태의 같은 이름 메서드를 호출하고, `IAIState`가 목적지를 계산해 `SmokeAreaAvoiding`/`FlashAreaAvoiding`을 만든다. 이미 `Avoiding` 계열 상태이면 에이전트 쪽에서 중복 진입을 막는다. 연막탄은 연막탄 중심에서 멀어지는 방향으로 `weaponDef.Range * 1.5f`만큼, 섬광탄은 바라보던 방향으로 같은 거리만큼 이동한다.

- 생성자는 지속 시간 `duration`(무기 정의의 `DamageDuration`)을 받아 `avoidTime`으로 쓰고, 생성 시 서 있는 자세와 `RunSpeed`로 맞춘다. 이 시간이 지나면 기본 `Avoiding.UpdateMoving`이 `Searching`으로 돌아가며, `FlashAreaAvoiding`은 이 기본 동작을 그대로 쓴다.
- `SmokeAreaAvoiding`은 `UpdateMoving`을 override해서, 목적지(연막탄을 벗어나는 위치)에 도달하는 즉시(`remainingDistance <= 0.1f`) `Searching`으로 빠져나간다. 이 override는 base를 호출하지 않으므로 `duration` 타이머는 연막탄 회피에서는 쓰이지 않고 도착 조건만으로 종료된다.
- 두 하위 클래스는 `IsVisibleTarget(Transform)`을 `false`로 override해서 시야가 가려진 상황을 표현하려는 구조다. 다만 오버라이드 대상은 `Transform` 인자를 받는 오버로드뿐이고(사격 판정에 쓰는 인자 없는 `IsVisibleTarget()`은 그대로), `Avoiding` 계열 상태 자체에는 사격 코드가 없다. 이 저장소 기준으로 이 계열 상태 안에서 해당 오버로드를 호출하는 경로는 없어서, 실질적으로는 기반 클래스 헬퍼가 호출될 경우를 대비한 방어 성격이다.

```csharp
public class SmokeAreaAvoiding : Avoiding
{
	public SmokeAreaAvoiding(bl_AIShooterAgent shooterAgent, Vector3 avoidTargetPos, float duration) : base(shooterAgent, avoidTargetPos, duration) { }
	
	public override void Enter()
	{
		base.Enter();
		
		shooterAgent.SetDestination(_avoidTargetPos);
	}

	protected override bool IsVisibleTarget(Transform target)
	{
		return false;
	}

	protected override void UpdateMoving()
	{
		//목적지 [연막탄을 벗어나는 위치] 도달시 즉시 해당 행동을 Exit 한다.
		if (shooterAgent.Agent.remainingDistance <= 0.1f)
		{
			nextState = new Searching(shooterAgent);
			Exit();
		}
	}
}
```

### Idle — 최초 진입 상태

```csharp
/// <summary>
/// 첫 진입 상태 [Entry State]
/// </summary>
public class Idle : IAIState
{
	public Idle(bl_AIShooterAgent shooterAgent) : base(shooterAgent) { }
}
```

별도 로직 없이 `IAIState`의 기본 동작만 그대로 사용하는 자리표시자 상태다. `bl_AIShooterAgent.InitStateMachine()`이 에이전트 등록과 함께 `CurrentState = new Idle(this)`로 최초 할당하고, `UpdateState()`는 게임이 시작되기 전(`isGameStarted == false`)에는 상태를 갱신하지 않는다. 게임이 시작되면 에이전트 초기화 흐름이 `Idle`을 거치지 않고 `Searching`(폭탄 소지 그룹은 `TargetAreaSearching`)으로 직접 교체한다.

### Bot 테이블 — 난이도별 파라미터

Bot 테이블의 원본은 `Bot.xlsx`의 `Database` 시트다(3행 한글 설명, 4행 필드명, 5행부터 Bot 한 줄). 현재 18행(No 1 ~ 18)이며, `Enum` 시트에서 난이도를 Pro(0, 난이도높음) / Casual(1, 난이도중간) / Novice(2, 난이도낮음)로 정의한다. `Export` 시트는 출력 파일(`Bot.json`, `Bot.cdb.bytes`)과 필드 타입을 지정한다.

한 행에서 조절할 수 있는 항목은 다음과 같다.

- 구분 : `Difficulty`(난이도), `MultiMode`(멀티플레이용 여부)
- 공격 패턴 : `AttackBehaviorPercentages`(저돌적/제자리/무작위 이동/엄폐 확률), `AttackBehaviorRemainTimes`(패턴별 유지 시간), 폭탄모드 전용 확률 `AttackerBombAssignedBehaviorPercentages`, `AttackerBombInstallBehaviorPercentages`
- 이동/시야 : `WalkSpeed`, `CrouchSpeed`, `RunSpeed`, `RotationSmoothing`, `StopDistance`, `MinRange`, `FiringDistance`, `ViewAngle`, `DetectNoiseRange`
- 반응/사격 지연 : `ReactSpeed`, `EnableShootDelay`, `ShootDelayMinTime/MaxTime`, 그리고 2025-11 LR-636에서 추가된 `ShootDelayImmatureUserMinTime/MaxTime`
- 전투 자원 : `Health`, `GrenadeNum`, `MinimumDistanceForGrenades`
- 무기 : 카테고리(AR/SMG/SG/SR)별 사용 여부(`*Available`), 선택 확률(`*Percentage`), 총기 확률표 참조키(`*_GunList`)

실제 값 예시는 다음과 같다(멀티플레이용 행, 난이도별로 한 행씩 발췌).

| No | 난이도 | 공격 확률 [저돌/제자리/이동/엄폐] | 유지(초) | ReactSpeed | 사격 지연(초) | 초보 유저 대상 사격 지연(초) | ViewAngle | GrenadeNum |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Novice | 10 / 50 / 10 / 30 | 4 | 1.3 | 1.3 ~ 1.6 | 2.5 ~ 4 | 80 | 1 |
| 4 | Casual | 10 / 50 / 10 / 30 | 3 | 1.0 | 1.0 ~ 1.2 | 1.5 ~ 2.5 | 90 | 2 |
| 7 | Pro | 20 / 50 / 10 / 20 | 2 | 0.6 | 0.6 ~ 0.9 | 1.0 ~ 1.5 | 100 | 2 |

18행 전체를 보면 난이도 사이의 차이는 주로 반응 속도, 사격 지연, 시야각, 패턴 유지 시간, 수류탄 수, 공격 패턴 확률에 있다. 반면 `Health`(100), 걷기/앉아 걷기/뛰기 속도(3.5 / 2 / 6.5, 다만 7번 행의 뛰기 속도만 6.45), 폭탄모드 확률(`[30, 70]`, `[50, 50]`), 무기 카테고리 확률(AR 65 / SMG 30 / SG 5 / SR 0, SR은 미사용)은 모든 행이 같다. 공격 패턴 확률은 난이도마다 "제자리 중심(`[10, 70, 10, 10]`)", "엄폐 중심(`[10, 10, 10, 70]`)" 같은 성향 변형을 3행씩 둔 구조다. 오프라인용 행(No 10 ~ 18)은 `MultiMode`가 0이며, 같은 난이도에서 멀티용 행과 반응 속도/사격 지연 값이 다르고(예: Novice ReactSpeed 2.5, 사격 지연 3 ~ 5) 초보 유저 대상 사격 지연은 일반 값과 같다.

**로딩.** `BotDef.OnLoad(ref DefLoader)`가 필드를 정해진 순서대로 읽는다(`ReadInt`/`ReadFloat`/`ReadString`). 배열 컬럼은 JSON 문자열로 저장되어 있어 `JsonConvert.DeserializeObject<float[]>`로 변환한다. 게임 시작 시 `BotRepository.DecideBotDefPool`이 Misc 테이블의 난이도별 인원 분포를 바탕으로 `Difficulty`와 `MultiMode`가 맞는 `BotDef` 중 무작위로 골라 Bot 슬롯을 채운다(멀티플레이 여부는 풀 키에 "Multi"가 포함되는지로 판단). Bot 에이전트는 `bl_AIShooterAgent.Init`에서 `aiSettings`를 받아 상태 클래스들이 `shooterAgent.aiSettings.*`로 참조한다. `aiSettings`를 결정하는 `GameSettings.GetRandomAISetting()`의 구현은 이 저장소에 없어 확인하지 못했다.

**사용처.** 이 저장소의 코드에서 확인되는 사용처는 이동 속도(`IAIState.DecideAgentSpeed`, `Attacking.DecideRandomSpeed` 등), `ReactSpeed`(`IAIState.reactionTime`, 즉 `SlowUpdate` 호출 간격의 기준), `StopDistance`/`MinRange`/`FiringDistance`(후퇴·초근접 판정·사격 가능 거리), 공격 패턴 확률과 유지 시간, `Health`, 무기 선택(`BotDef.PeekRandomGunID`)이다. `ViewAngle`, `DetectNoiseRange`, `RotationSmoothing`, `GrenadeNum`, `MinimumDistanceForGrenades`를 읽는 코드는 이 저장소에서 찾지 못했다(감지/무기 시스템 등 저장소 밖 코드에서 쓸 수 있으나 확인하지 못했다).

**KD 기반 사격 지연.** 상대가 실제 플레이어이면 `ResetShootDelayTimer`가 그 플레이어의 KD 값을 `MiscCDB.ImmatureUserKD` 범위에서 정규화하고, 일반 사격 지연과 초보 유저 대상 사격 지연 사이를 선형 보간(`Mathf.Lerp`)해 사격 지연을 정한다. KD가 낮을수록 지연이 길어진다. `Attacking` 생성자가 이 계산을 호출하므로 공격 상태에 새로 들어갈 때마다 다시 계산된다.

### 설계 포인트

- **행동별 클래스 분리 + 얕은 상속** : 하나의 거대한 switch문 대신 `IAIState`를 상속하는 클래스를 행동 단위로 쪼갰다(States 폴더에 클래스 18개). `Attacking`/`Avoiding`/`Searching`처럼 같은 계열 안에서도 하위 클래스로 세분화했지만 상속 깊이는 최대 2단계(`IAIState` → `Attacking` → `AggressiveAttacking` 등)로 유지했다. 시야 체크(`IsVisibleTarget`), 사격 가능 거리(`IsCloseToFire`), WayPoint 이동(`MakeWayPoints`, `MoveToNextWayPoint`) 같은 공통 헬퍼는 `IAIState`에 두었고, 각 하위 클래스는 진입 조건과 전이 조건 위주로 override한다. 다만 새 공격 유형을 추가하려면 클래스 하나로 끝나지 않고 `IAIState.GetRandomAttackState`/`GetRandomDamagedState`의 switch, `bl_AIShooterAgent.OnGetHit`의 타입 분기 등도 함께 손봐야 한다(자세한 내용은 아래 "한계와 개선 방향").
- **파라미터의 데이터화** : 이동속도, 반응 속도, 확률(`AttackBehaviorPercentages`, `AttackerBombInstallBehaviorPercentages` 등), 공격 패턴 유지 시간(`AttackBehaviorRemainTimes`), 사격 지연이 `aiSettings`를 통해 Bot 테이블에서 주입되어, 밸런스 조정을 코드 재배포 없이 테이블 수정만으로 할 수 있다. 다만 "상태 코드에 수치가 없다"고는 할 수 없다. States 폴더만 단순 grep해도 0 초기화, 배열 인덱스, `Random.Range(0, 100)`의 기준값을 제외하고 숫자 리터럴이 50여 곳 남아 있다. 이 중 5개(`Aggressive`/`Moving`/`Holding`/`Covering`의 유지 시간 기본값과 `Avoiding.avoidTime`)는 진입 시 테이블 값이나 생성자 인자로 덮어써지는 초기값이지만, 나머지는 실제로 쓰이는 상수다(예: `AvoidAttacking`의 유지 시간 `5.0f`, 후퇴 속도 `2.0f`, 무작위 이동 반경 `4.0f`, CoverPoint 탐색 반경 `10`, 도착 판정 여유 `0.5f`, 연막탄 회피 도착 판정 `0.1f`).
- **AIStateManager를 통한 상태 간 조율** : 각 Bot의 상태는 스스로 판단해서 전이하지만, 폭탄 해체처럼 "한 번에 한 명만 해야 하는" 행동은 `GetAgentsInState<T>()`로 다른 Bot들의 현재 상태를 조회해 중복 배정을 막는다. 다른 Bot의 상태를 조회하는 경로는 이 매니저로 모여 있지만, 상태 클래스끼리 서로를 전혀 참조하지 않는 것은 아니다. 전이할 때 다른 상태 클래스를 직접 `new`하고, `IAIState.UpdateAttackerBombDroppedBehavior`는 다른 Bot의 `BombSearching` 인스턴스에 접근해 `UpdateDestinationToBomb()`를 직접 호출한다.


관련 코드: [CoverPoint](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/01.%20CoverPoint), [WayPoint](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/02.%20WayPoint), [MasterClientBotManaging](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/07.%20MasterClientBotManaging)
