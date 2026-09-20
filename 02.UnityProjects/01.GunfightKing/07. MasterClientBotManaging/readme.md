## Master Client 기반 Bot 총괄 관리 (`bl_AIManager.cs`, `bl_AIShooterAgent.cs`)

이 폴더는 "봇이 어디서 만들어지고, 누가 판단하고, 그 결과가 어떻게 다른 클라이언트에 전달되는가"를 한 번에 볼 수 있게 모은 코드다. 총싸움의 왕은 전용 서버 없이 Photon PUN2의 Master Client가 방을 대표하기 때문에, 봇 AI 전체가 Master 한 대에서만 돌고 나머지 클라이언트는 결과를 받아 재현한다. 다른 폴더가 개별 기능(경로, 엄폐, FSM, 압축)을 다룬다면, 이 문서는 그 기능들이 어떤 뼈대 위에 얹혀 있는지를 설명한다.

### 1. 파일별 책임

| 파일 | 규모 | 역할 |
|---|---|---|
| `bl_AIManager.cs` | 약 2,280행 | 방 단위 봇 총괄. 슬롯/스폰/리스폰, 봇 통계 동기화(RPC), 유저 입퇴장 시 봇 교체, Master 이전 대응, 그룹 편성·경로, 폭탄 모드 조율 |
| `bl_AIShooterAgent.cs` | 약 1,480행 | 봇 한 마리의 본체. FSM 구동, 시야 검사, 시선(`UpdateLookAt`), 표적 선정과 탄퍼짐 적용, 사격 지연 |
| `IAIState.cs` | 약 590행 | FSM 상태의 원형. 상태 전이는 `nextState = ...; Exit();` 규칙([04. StateMachine](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/04.%20StateMachine) 참고) |
| `BotDef.cs`, `BotRepository.cs` | 약 240행, 약 215행 | 엑셀 기반 봇 난이도 데이터와 방 단위 풀 결정 |
| `AIStructures.cs` | 25행 | 봇 통계 구조체(`MFPSBotProperties`)와 게임 상태 enum |
| (05 폴더) `bl_AIShooterNetwork.cs` | | 봇의 위치·방향 전송. [05. PhotonNetwork](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/05.%20PhotonNetwork) 참고 |

`bl_AIManager.cs`는 [02. WayPoint](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/02.%20WayPoint)에도 같은 파일이 있다(경로 생성 부분을 보려면 그쪽이 문서와 연결된다). 이 폴더의 사본이 총괄 로직을 보는 기준이다.

### 2. 권한 구조: 판단은 Master에서만, 나머지는 재현

`bl_AIShooterAgent.Init`에서 NavMeshAgent는 Master에서만 켜고, FSM 구동(`UpdateState`)과 시야 검사(`CheckVision`)도 Master에서만 돈다. 다른 클라이언트의 봇은 이동 판단을 하지 않는다.

```csharp
// Init: Master만 NavMeshAgent 사용
References.Agent.enabled = PhotonNetworkEx.IsMasterClient;

public void UpdateState()
{
	if (!isGameStarted) return;

	if (PhotonNetworkEx.IsMasterClient && CurrentState != null) { CurrentState.Process(); }
}
```

동기화 채널은 성격에 따라 둘로 나뉜다.

- **매 직렬화 틱 반복되는 값(위치·속도·방향·피치)**: `OnPhotonSerializeView`로 스트리밍한다. 압축 설계는 [05. PhotonNetwork](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/05.%20PhotonNetwork)에 있다.
- **상태가 바뀔 때만 생기는 이벤트(표적 변경, 앉기, 리스폰, 파괴, 점프)**: RPC로 보낸다. 예를 들어 `SetTarget`은 표적이 바뀔 때만 `SyncTarget`을 보내고, 표적이 그대로면 아무것도 보내지 않는다.

```csharp
public void SetTarget(Transform newTarget)
{
	if (Target == newTarget) return;

	OnTargetChanged(Target, newTarget);
	Target = newTarget;

	if (!PhotonNetworkEx.IsMasterClient) return;

	// sync the bot target
	var data = bl_UtilityHelper.CreatePhotonHashTable();
	data.Add("type", AIRemoteCallType.SyncTarget);
	// ... 표적이 없으면 viewID = -1, 있으면 표적의 ViewID를 실어 RpcTarget.Others로 전송
}
```

### 3. 봇 생성: Room Object + 단계별 오류 추적 (`SpawnBot`)

봇은 `PhotonNetwork.InstantiateRoomObject`로 만든다. 방 오브젝트라서 생성한 Master가 나가도 방에 남는다(4절의 Master 이전 처리가 이 성질에 의존한다). 이름·팀·`BotDef` 번호·생성 시각은 `BotInstantiationParam`에 담아 인스턴스 파라미터로 전달하고, 각 클라이언트의 `bl_AIShooterNetwork.GetEssentialData`가 이 값을 꺼내 쓴다.

`SpawnBot`은 스폰 지점 확보 → 슬롯 확인 → 풀에서 `BotDef` 번호 획득 → 인스턴싱 → 통계 등록 순서로 진행되는데, 각 단계마다 `debugStep` 값을 올려 두고 전체를 `try/catch`로 감싼다. 실패하면 어느 단계에서 깨졌는지를 Crashlytics에 남긴다.

```csharp
int botDefNo = GetAvailableBotDefFromPool(AITeam.ToString(), AIName)?.BotDefId ?? Invalid.No;
debugStep = 4;

var instParam = new BotInstantiationParam(AIName, AITeam, botDefNo, (float)PhotonNetworkEx.TimeEx);

// BOT 오브젝트 인스턴싱
GameObject bot = PhotonNetwork.InstantiateRoomObject(AiName, spawnPosition, spawnRot, 0, instParam.ToObjects());
// ...
catch (Exception e)
{
	//PhotonRoomInstantiate 오류
	if (debugStep == 4)
	{
		CrashlyticsUtil.LogException(new GameException($"SpawnBot error(step:{debugStep}), InstantiateRoomObjectErrorStatus : {PhotonNetwork.InstantiateRoomObjectErrorStatus}" + e.Message));
	}
	else { /* step 번호와 메시지를 로그로 전달 */ }
}
```

네트워크 호출이 끼는 다단계 절차는 실패 지점을 특정하기가 어렵다. 이런 식으로 단계 번호를 남겨 두면 Crashlytics에서 원인 지점을 좁힐 수 있다([09. Firebase](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/09.%20Firebase) 참고).

### 4. 방에 사람이 드나들 때: 슬롯, 봇 교체, Master 이전

봇은 "빈 자리를 채우는" 존재라서, 유저의 입장과 퇴장, 그리고 Master 교체를 어떻게 다루느냐가 안정성의 핵심이다.

- **슬롯 모델**: 팀별 슬롯(`Team1PlayersSlots`/`Team2PlayersSlots`)에 유저 이름이나 봇 이름을 넣어 자리를 관리한다. 슬롯 정보와 봇 통계는 RPC(`SyncAllBotsStats`)로 모든 클라이언트에 복제해 둔다. 코드 주석에도 "Master Client가 게임을 떠나는 경우를 대비해 모든 플레이어가 같은 목록을 갖게 한다"고 적혀 있다.
- **유저 입장**: `ReplaceBotWithPlayer`가 빈 슬롯에 유저를 넣고 그 슬롯을 쓰던 봇을 제거한다. 커스텀 룸(로비에서 봇 수를 지정한 방)에서는 유저가 봇 자리를 대체하지 않고 빈 슬롯을 쓴다.
- **유저 퇴장**: `OnPlayerLeft`에서 Master가 비워진 슬롯에 새 봇을 만든다(커스텀 룸 제외). 라운드가 끝날 때까지 기다렸다 스폰하는 모드(`WaitUntilRoundFinish`)에서는 그 자리에서 바로 만들지 않는다.
- **Master 이전**: 새 Master가 된 클라이언트는 `OnMasterClientSwitched`에서 씬에 남아 있는 `bl_AIShooter`를 `FindObjectsByType`으로 다시 모은다. 죽어 있던 봇은 리스폰 대기열로 보내고, 살아 있는 봇은 `Init(InitType.MasterClientChanged)`로 다시 초기화해 NavMeshAgent를 켠다.

```csharp
public void OnMasterClientSwitched(Player newMasterClient)
{
	//if the new master client is the local client
	if (newMasterClient.ActorNumber == PhotonNetworkEx.LocalPlayer.ActorNumber)
	{
		if (Team1PlayersSlots == null || Team1PlayersSlots.Count <= 0)
			SetUpSlots(false);

		//since bots where not collected on the new master client, lets take them manually
		bl_AIShooter[] allBots = FindObjectsByType<bl_AIShooter>(FindObjectsSortMode.None);
		foreach (var bot in allBots)
		{
			if (bot.isDeath)//if the bot was death when master client leave the game
			{
				AddBotToRespawn(bot);
				continue;
			}
			AllBots.Add(bot);
			AllBotsTransforms.Add(bot.transform);
			bot.Init(bl_AIManager.InitType.MasterClientChanged);
		}

		_needForceRefreshTargetList = true;

		bl_SpawnPointManager.Instance.UnlockAllSpawnPoints();
	}
}
```

봇 통계를 동기화받지 못한 상태(`AllBotsStatsSyncDone == false`)의 Master는 정상 진행이 불가능하므로, `OnPlayerEnter`에서 새로 들어온 유저를 `KickReason.SyncUnavailable`로 내보내는 방어 분기도 있다.

### 5. 데이터 주도 난이도: `BotDef`와 `BotRepository`

봇의 능력치는 코드에 하드코딩하지 않고 엑셀([04. StateMachine](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/04.%20StateMachine)의 `Bot.xlsx`)에서 읽는다. `BotDef.OnLoad`가 `DefLoader`로 체력, 이동 속도, 사거리, 시야각, 반응 속도, 사격 지연, 무기군별 확률 등을 순서대로 읽는다. 무기 선택은 카테고리(AR/SMG/SG/SR)를 가중치로 뽑고, 그 안에서 다시 총기를 가중치로 뽑는 2단계 확률(`PeekRandomGunID`)이다.

`BotRepository.DecideBotDefPool`은 방마다 이번 게임의 봇 구성을 정한다. 난이도(Novice/Casual/Pro)별로 몇 마리를 쓸지는 `MiscCDB`의 분포표(`[최소, 최대)` 구간, `-1`이면 남은 인원 전부)에서 방의 `RoomRandomSeed`로 결정한다.

```csharp
System.Random rand = new System.Random(roomSeed);

for (int i = 0; i < botDistData.Length; i++)
{
	if(prevBotNumber >= maxBotNumber) break;

	int botNum = botDistData[i][0] != -1
		? rand.Next(botDistData[i][0], botDistData[i][1])
		: maxBotNumber - prevBotNumber;

	AIDifficulty difficulty = (AIDifficulty)i;
	List<BotDef> botSettings = BotCDB.Instance.GetAllDefs().FindAll(x =>
		x.Difficulty == difficulty &&
		x.MultiMode == isMultiMode).ToList();

	for (int j = 0; j < botNum; j++)
	{
		int rndIndex = Random.Range(0, botSettings.Count);
		result.Add(botSettings[rndIndex]);
	}

	prevBotNumber += botNum;
}
```

풀의 종류는 게임 유형(멀티 개인전/팀전, 오프라인 난이도별)과 신입 특수 미션 여부(`RoomFlags.IsTrial`)로 나뉜다. `bl_AIManager.Awake`에서 시드는 방 속성으로 공유되므로 같은 방의 클라이언트들이 같은 난이도별 인원수 배분을 얻는다.

난이도와 별개로, 상대가 초보 유저로 추정되면 봇의 반응을 늦추는 적응형 조절이 있다. `ResetShootDelayTimer`는 표적이 실제 유저일 때 그 유저의 KD 값을 `MiscCDB.ImmatureUserKD` 구간에 정규화하고, 그 위치에 따라 `ShootDelay*`(기본)와 `ImmatureUserShootDelay*`(초보용) 값 사이를 보간해 사격 지연 시간을 뽑는다.

```csharp
float? kdrValue = BattleManager.Instance.GetKDValueIfRealPlayer(view.ViewID);
float[] immatureKDRange = MiscCDB.Instance.ImmatureUserKD;

if (kdrValue.HasValue)
{
	var clampedKdr = Mathf.Clamp(kdrValue.Value, immatureKDRange[0], immatureKDRange[1]);

	//immatureKDRange 에서 정규화된 kdr값의 위치를 찾고
	float t = Mathf.InverseLerp(immatureKDRange[0], immatureKDRange[1], clampedKdr);

	float min = Mathf.Lerp(aiSettings.ShootDelayMinTime, aiSettings.ImmatureUserShootDelayMinTime, 1 - t);
	float max = Mathf.Lerp(aiSettings.ShootDelayMaxTime, aiSettings.ImmatureUserShootDelayMaxTime, 1 - t);

	ShootDelayTime = Random.Range(min, max);
}
```

KD가 구간 하한이면 초보용 지연, 상한이면 기본 지연이 되고 그 사이는 연속으로 이어진다. 난이도 계층을 나누는 것에서 끝나지 않고, 유저 한 명 한 명의 숙련도에 맞춰 체감 난이도를 부드럽게 조정하는 장치다.

### 6. 그룹 편성과 폭탄 모드 조율

팀전/폭탄 모드에서는 봇을 그룹으로 묶어 같은 경로를 함께 이동시킨다. `DecideBotGroupIDs`는 `AiGroupPathSettings`에서 뽑은 그룹 유형(2+3, 2+2+1, 1+4, 1×5)에 따라 각 팀의 봇에 `GroupID`를 부여하고, 남는 봇은 마지막 그룹에 넣는다. 그룹별 경로는 `InitFirstSpawnPaths`가 필수 웨이포인트를 골라 만든다(경로 생성은 [02. WayPoint](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/02.%20WayPoint) 참고). 개인전(FFA)은 `GroupID = -1`이다.

폭탄 모드에서는 폭탄을 든 봇(`IsCarrier`)이 있는 그룹 전체를 같은 설치 구역으로 보낸다. 상태 쪽 `UpdateAttackerBombAssignedBehavior`가 목표 구역을 뽑아 `UpdateBombAssignerPath`/`UpdateBombAssignerGroupPath`를 부르면, 매니저가 그 그룹 봇들의 `CurrentState`를 `TargetAreaSearching`으로 바꾸고 경로를 내려준다. 개별 봇의 판단은 FSM에, 여러 봇에 걸친 조율은 매니저에 둔 구조다.

### 설계 포인트

- **권한 집중과 재현의 분리**: 판단(FSM, NavMesh, 시야)은 Master에만 두고 나머지는 상태를 받아 보간해서 그린다. 봇이 늘어도 다른 클라이언트의 연산이 늘지 않는다.
- **연속 값은 스트림, 이벤트는 RPC**: 매 틱 바뀌는 값과 가끔 바뀌는 상태를 다른 채널로 나눠 불필요한 전송을 줄였다.
- **Room Object + 전원 복제 슬롯/통계**: Master가 나가도 봇 오브젝트가 남고, 슬롯과 통계는 이미 모든 클라이언트에 있어서 새 Master가 이어받을 수 있다.
- **난이도를 데이터로**: 능력치, 무기 확률, 난이도별 인원 분포를 엑셀/설정 테이블로 빼서 코드 수정 없이 튜닝할 수 있다. KD 기반 적응형 사격 지연도 같은 시트의 컬럼으로 조절한다.
- **실패 지점 추적**: 스폰과 타겟 동기화 같은 네트워크 의존 구간은 예외를 잡아 Crashlytics로 보내 라이브에서 원인을 추적할 수 있게 했다.

> 근거:
> - 이 문서의 구조 설명과 코드 발췌는 이 폴더의 `bl_AIManager.cs`, `bl_AIShooterAgent.cs`, `BotRepository.cs`, `BotDef.cs`를 직접 읽고 작성했다. 라인 수는 이 저장소 사본 기준이다.
> - git 이력(2025-01 이후, 본인 커밋)에서 봇 AI 개선 `[LR-181]`이 94건(2025-01~04), 봇 KD 기반 난이도 조절 `[LR-636]`이 2025-11에 확인된다. 2025-11의 크래시 방어 수정(`LR-627`, `LR-629`, `LR-633`, `LR-634`, `LR-637`)이 이 파일들의 예외 처리와 관련된다.
> - `RoomRandomSeed`가 결정하는 것은 난이도별 인원수뿐이다. 그 안에서 어떤 `BotDef`를 고를지는 `UnityEngine.Random`이라 클라이언트마다 다를 수 있다.
> - `aiSettings`는 `bl_AIShooterAgent.Init`에서 `GameSettings.GetRandomAISetting()`으로 받는다. 이 메서드의 정의는 업로드된 프로젝트 사본에서 찾지 못했다. 풀이 정한 `BotDef` 번호가 여기에 어떻게 반영되는지는 이 문서에서 단정하지 않는다.
> - 봇 AI 점수 개선이나 크래시 프리 비율 같은 성과 수치는 이력서/포트폴리오에 적은 수치이며, 이 폴더의 코드만으로 재현되지 않는다.

관련 코드:
- [01. CoverPoint](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/01.%20CoverPoint) — 봇이 쓰는 엄폐 지점
- [02. WayPoint](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/02.%20WayPoint) — 그룹 경로가 만들어지는 웨이포인트 그래프
- [04. StateMachine](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/04.%20StateMachine) — 봇 한 마리의 행동을 정하는 FSM과 `Bot.xlsx`
- [05. PhotonNetwork](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/05.%20PhotonNetwork) — 봇 위치/방향 압축 전송
- [06. BulletSpread](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/06.%20BulletSpread) — 봇 탄퍼짐(`GetSpreadAngle`)이 쓰는 무기 데이터
