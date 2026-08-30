## Bot AI State Machine

AI Bot의 전투/이동/특수행동을 FSM(Finite State Machine)으로 구현한 부분이다. `IAIState`를 상속하는 클래스 하나가 하나의 행동(대기, 탐색, 여러 공격 방식, 회피, 폭탄 설치/해체 등)을 표현하고, 상태 안에서 조건이 맞으면 `nextState`를 지정하고 스스로 `Exit()`을 호출해 다음 상태로 전이한다. 아래 이미지와 같이 미리 세팅한 WayPoint를 따라 이동하면서, 상황에 따라 전투 상태·탐색 상태·특수 행동(폭탄 설치/해체) 상태로 전환하며 움직인다.

<img width="1081" height="611" alt="시리아맵_에디터" src="https://github.com/user-attachments/assets/e41e8774-7893-4fdc-bfb6-74fecce3f645" />
<img width="1086" height="606" alt="시리아맵_에디터_런타임" src="https://github.com/user-attachments/assets/041a07e9-c94e-4135-a717-a74349367bf7" />

전투 상태나 이동 상태에서 Bot의 구체적인 수치(이동속도, 사격 딜레이, 각 행동을 선택할 확률 등)는 코드에 하드코딩하지 않고 Bot 테이블로 관리한다. 기획자가 Excel로 관리하는 이 테이블 값이 `aiSettings`로 로드되어 상태 클래스들에서 바로 참조된다.

<img width="1666" height="296" alt="image" src="https://github.com/user-attachments/assets/a7afe331-5642-4547-925b-da135e94887a" />

### 상태 등록/조회 — AIStateManager

`AIStateManager`는 씬에 존재하는 모든 Bot 에이전트를 등록해두고, "지금 특정 상태에 있는 Bot들"을 타입 기준으로 조회할 수 있게 해준다.

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

이 `GetAgentsInState<T>()`는 단순 디버깅용이 아니라 실제 행동 결정에 쓰인다. 예를 들어 폭탄모드에서 "이미 폭탄을 해체 중인 Bot이 있는지" 확인해서 중복 배정을 막는 식이다.

```csharp
public class CoveringDefusing : IAIState
{
	// ... 중략 ...
	public override void SlowUpdate()
	{
		base.SlowUpdate();
		
		if (!shooterAgent.IsCrouch) { shooterAgent.IsCrouch = false; }
		
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

### 탐색 — Searching / TargetAreaSearching / BombSearching / DemolitionAreaSearching

`Searching`은 교전 중이 아닐 때 다음 목적지를 결정하는 상태다. 폭탄모드에서 수비조는 ESSENTIAL WayPoint 경로로 무작위 설치 구역까지, 공격조와 개인전 Bot은 NORMAL WayPoint 하나를 무작위로 골라 이동한다.

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
	else
	{
		List<Vector3> newPoints = new List<Vector3>();
		Vector3 newPoint = aiManager.GetRandomNormalWayPoint().transform.position;
		newPoints.Add(newPoint);
		MakeWayPoints(newPoints);
	}
}
```

`DemolitionAreaSearching`은 폭탄을 소지한 공격조 Bot이 설치 구역까지 이동하는 상태로, 도중에 적을 육안으로 확인하면 즉시 공격 상태로 빠지고, 설치 가능 구역에 도달하면 `Demolitioning`으로 넘어간다.

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

### 폭탄모드 특수 행동 — Demolitioning / BombDefusing

`Demolitioning`은 폭탄 설치 행동이다. 설치 도중 초근접한 적이 나타나면 설치를 취소하고 확률 기반으로 공격 상태로 전환하며, 그렇지 않으면 웅크린 채 설치를 진행한다. 이때 공격 상태를 고르는 확률도 `aiSettings`(Bot 테이블)에서 가져온다.

```csharp
public class Demolitioning : IAIState
{
	public override void SlowUpdate()
	{
		base.SlowUpdate();

		bool isUpdated = UpdateVeryCloseEnemy();
		//초근접한 적이 있는 경우, 폭탄 설치를 중단하고, 교전한다.
		if (isUpdated)
		{
			DemolitionBombManager demolitionBombManager = DemolitionBombManager.Instance;
			demolitionBombManager.BotCancelPlantBomb(shooterAgent.BotMFPSActor);

			nextState = GetRandomAttackState();
			Exit();
		}
		//초근접한 적이 없는 경우
		else
		{
			//주변에 적이 없고, 폭탄 설치가 가능한 경우
			DemolitionBombManager demolitionBombManager = DemolitionBombManager.Instance;
			if (!IsVeryCloseEnemyExist() && demolitionBombManager.CanPlantBomb())
			{
				if (!shooterAgent.IsCrouch) { shooterAgent.SetCrouch(true); }
				
				shooterAgent.ToggleMovable(false);

				DebugEx.Log("[" + shooterAgent.name + "]" + " Case 100 planting bomb....");
				demolitionBombManager.BotPlantBomb(shooterAgent.BotMFPSActor, OnBombPlantFinished);
			}
		}
	}

	private void OnBombPlantFinished()
	{
		nextState = new TargetAreaSearching(shooterAgent);
		Exit();
	}

	//무작위 공격 행동을 결정한다.
	public override IAIState GetRandomAttackState()
	{
		float[] percentageRange = new float[shooterAgent.aiSettings.AttackerBombInstallBehaviorPercentages.Length];
		shooterAgent.aiSettings.AttackerBombInstallBehaviorPercentages.CopyTo(percentageRange, 0);
		
		float sum = 0;

		for (int i = 0; i < percentageRange.Length; i++)
		{
			sum += percentageRange[i];
			percentageRange[i] = sum;
		}

		float randomValue = Random.Range(0, 100);

		for (int i = 0; i < percentageRange.Length; i++)
		{
			if (randomValue < percentageRange[i])
			{
				if (i == 0) return new AggressiveAttacking(shooterAgent);
				else if (i == 1) return new CoveringAttacking(shooterAgent);
			}
		}

		//default attack 상태
		return new CoveringAttacking(shooterAgent);
	}
}
```

`BombDefusing`은 수비조가 활성화된 폭탄을 해체 시도하는 상태다. 해체 가능 지역에 도달하면 웅크리고 `BotDefuseBomb`을 호출하되, 그 와중에도 시야에 들어온 적이 있으면 사격을 계속한다 — 해체 중이라고 완전히 무방비 상태가 되지는 않는다.

```csharp
protected override void UpdateMoving()
{
	if (!shooterAgent.IsCrouch) { shooterAgent.IsCrouch = false; }

	//폭탄 설치 가능 지역까지 도달한 경우
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
	
	bool isCloseToBomb = bl_AIManager.Instance.IsCloseToBombInstall(shooterAgent.transform.position);

	if (isCloseToBomb)
	{
		if (!shooterAgent.IsCrouch) { shooterAgent.SetCrouch(true); }

		demolitionBombManager.BotDefuseBomb(shooterAgent.BotMFPSActor);
		DebugEx.Log("[Bomb] Try Defusing by " + shooterAgent.name);
	}
}
```

`CoveringDefusing`은 해체 담당이 아닌 다른 수비조 Bot이 폭탄 근처를 엄호하며 대기하는 상태로, 해체 담당이 없어졌을 때만(`GetAgentsInState<BombDefusing>().Count == 0`) 스스로 해체 역할을 이어받는다.

### 공격 — Attacking 계열

`Attacking`은 기본 사격 로직만 담고, 실제 움직임 패턴은 하위 클래스로 나뉜다. 각 하위 클래스가 유지 시간(`AttackBehaviorRemainTimes`)이 지나면 다시 `GetRandomAttackState()`로 확률 기반 재선택을 하거나 `Searching`으로 돌아간다.

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

`AvoidAttacking`은 피격 방향의 반대편 CoverPoint를 찾아 뒷걸음질치며 사격하는 형태로, CoverPoint 시스템을 그대로 재사용한다.

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
		
		SetCrouchOrStand();
	}

	private Vector3 GetCloseCoverPoint(Vector3 moveDir)
	{
		return bl_AICoverPointManager.Instance.GetCoverOnRadius(shooterAgent.transform, 10, moveDir);
	}
}
```

공격 방식은 매번 고정된 우선순위가 아니라, Bot 테이블에 정의된 확률 테이블(`AttackBehaviorPercentages`)에서 누적 확률 구간을 만들어 무작위로 뽑는다. 저돌적/제자리/이동/은폐 네 가지 공격 유형을 이 방식으로 고른다.

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

연막탄/섬광탄 범위에 들어갔을 때 벗어나는 행동이다. 두 하위 클래스 모두 `IsVisibleTarget`을 강제로 `false`로 오버라이드해서, 시야가 실제로 가려진 상황을 표현한다.

```csharp
public class SmokeAreaAvoiding : Avoiding
{
	public override void Enter()
	{
		base.Enter();
		
		avoidTime = 3.0f;
		shooterAgent.SetDestination(_avoidTargetPos);
	}

	protected override bool IsVisibleTarget(Transform target)
	{
		return false;
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

별도 로직 없이 `IAIState`의 기본 `Enter()`/`Update()`만 그대로 사용하는 상태로, Bot이 스폰된 직후 다른 상태(주로 `Searching`)로 넘어가기 전 한 프레임을 차지하는 시작점 역할만 한다.

### 설계 포인트

- **행동별 클래스 분리 + 얕은 상속** : 하나의 거대한 switch문 대신 `IAIState`를 상속하는 클래스를 행동 단위로 쪼갰고, `Attacking`/`Avoiding`/`Searching`처럼 같은 계열 안에서도 다시 하위 클래스로 세분화했다(`CoveringAttacking`, `SmokeAreaAvoiding` 등). 공통 로직(사격 판정, 시야 체크, WayPoint 이동)은 `IAIState`에 두고, 각 하위 클래스는 진입 조건과 전이 조건만 오버라이드하면 되어 새 행동을 추가할 때 기존 상태를 건드릴 필요가 없다.
- **파라미터의 완전한 데이터화** : 이동속도, 유지시간(`AttackBehaviorRemainTimes`), 확률(`AttackBehaviorPercentages`, `AttackerBombInstallBehaviorPercentages` 등)이 전부 `aiSettings`를 통해 Bot 테이블에서 주입된다. 상태 클래스 코드에는 수치가 거의 등장하지 않고 "무엇을 기준으로 고를지"만 남아 있어, 밸런스 조정을 기획자가 코드 재배포 없이 테이블 수정만으로 할 수 있는 구조다.
- **AIStateManager를 통한 상태 간 조율** : 각 Bot의 상태는 스스로 판단해서 전이하지만, 폭탄 해체처럼 "한 번에 한 명만 해야 하는" 행동은 `GetAgentsInState<T>()`로 다른 Bot들의 현재 상태를 조회해 중복 배정을 막는다. 개별 FSM들이 서로를 직접 참조하지 않고 매니저를 거쳐서만 상호작용하도록 만든 지점이다.

관련 코드: [CoverPoint](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/01.%20CoverPoint), [WayPoint](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/02.%20WayPoint), [MasterClientBotManaging](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/07.%20MasterClientBotManaging)
