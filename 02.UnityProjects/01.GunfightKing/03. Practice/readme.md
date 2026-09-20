## 사격 연습장(Practice) 모드

PVP 대전 위주 콘텐츠를 보완하기 위해 마련한 싱글플레이 전용 콘텐츠 중 하나이다. 연습장에서 레벨 단위의 훈련을 수행하며, 레벨별로 주어지는 탄알 수, 제한시간, 과녁의 배치·활성화 순서·움직임 패턴을 코드가 아닌 `PracticeMode.xlsx` 테이블에서 읽어 처리한다. (이 폴더의 xlsx 사본 기준 50개 레벨, 한 레벨당 과녁 슬롯은 최대 30개이다.)

> 근거: 아래는 개발자 본인 커밋만 담은 git export(2025-01-06 ~ 2025-11-26, 약 50개 이전 커밋과 다른 팀원의 작업은 포함되지 않음)에서 확인되는 내용이다.
> - 사격 연습장은 LR-462 "사격 연습장" 커밋 81건(2025-07-16 ~ 2025-08-20)으로 구현되었다. 첫 커밋(`ac5095ce`, 2025-07-16)에서 `ShootingMode.cs`가 `PracticeMode.cs`로 이름이 바뀌었고, 2025-07-22(`d5099b5c`)에 `TrainingMode` 폴더가 `PracticeMode` 폴더로 이동했다.
> - 그룹 종속(`PrevGroupMarks`/`AfterPrevGroup`)은 2025-07-29 커밋의 `.cs` 패치에서 처음 확인되고, 앞/뒤/좌/우 패턴 세분화는 2025-08-04 커밋(`d22b8fb0`, "과녁 좌/우 패턴 앞/뒤 패턴 제어를 위한 Pattern 세분화")에서 확인된다.
> - 이후 LR-544 전투 훈련(2025-09-04 ~ 09-05, 3건), LR-563 연습모드 개선(2025-09-16), LR-535(오프라인 진입 전환 2025-09-21, 무기 선택 진입·보조무기 전환 보완 2025-10-13 ~ 10-16) 등의 후속 수정이 확인된다.
> - 위 범위는 "git 이력(2025-01 이후)에서 확인되는 본인 커밋"이며, 다른 커밋이 없다는 뜻은 아니다.

### 문제와 접근

- **문제**: 레벨 밸런싱(과녁 수, 등장 순서, 움직임, 탄수, 제한시간)이 반복적으로 조정될 콘텐츠였다. 이 값들이 코드에 들어 있으면 조정할 때마다 코드 수정과 빌드가 필요하다.
- **접근**: 레벨 정보(`PracticeMode` 시트)와 움직임 정의(`MarkPattern` 시트)를 분리하고, 레벨은 패턴을 참조키(`Mark{N}P`)로만 가리키게 했다. 로드 시점에 한 행을 `PracticeModeDef`가 `TrainingMarkData` 목록으로 변환하고, `TrainingMarkManager`가 씬의 과녁에 배치하며, 개별 과녁(`TrainingMark`)이 자기 대기·이동·종료를 처리한다. 진행 상태·시간·탄수·결과·보상은 `PracticeMode`가 맡는다.

### 테이블 구조 (`PracticeMode.xlsx`)

아래는 PracticeMode 테이블 구조 일부이다.

<img width="1672" height="467" alt="image" src="https://github.com/user-attachments/assets/d40a9ae9-f985-4989-a205-8cbb630ac7cf" />

`PracticeMode` 시트는 한 행이 한 레벨이다. 컬럼은 `PracticeModeDef.OnLoad`가 읽는 순서와 같다.

| 컬럼 | 의미 |
| --- | --- |
| `No`, `Level`, `Enable` | 레벨 고유 번호, 화면에 표시되는 레벨, 활성화 여부 (`Enable == 1`인 레벨만 목록에 사용) |
| `TimeLimit` | 제한시간(초) |
| `LimitBulletNum` | 주무기 탄수 제한 |
| `Rwd{1~3}Type / No / Cnt` | 클리어 보상 3슬롯. 타입이 `None`인 슬롯은 로드 시 제거된다 |
| `Mark{N}` | 과녁 ID. 씬에 배치된 과녁 격자의 위치를 가리킨다 (`_Reference` 시트에 배치도). 코드에서는 `행 = ID / 열 수`, `열 = ID % 열 수`로 과녁을 찾는다 |
| `Mark{N}G` | 과녁의 그룹 번호 |
| `Mark{N}P` | `MarkPattern` 시트의 참조키 (움직임 패턴) |
| `Mark{N}T` | `[활성화 대기 시간, 활성화 후 유지 시간]` (초). `[0,0]`이면 즉시 활성화되고 맞아야만 비활성화된다 |
| `Mark{N}PrevGroup` | 1이면 "그룹 번호 - 1"의 과녁이 모두 끝나야 활성화된다 (0/빈칸이면 대기 없음) |

`N`은 1~30이며, 패턴이 비어 있거나(`Mark{N}P`가 0) `Mark{N}T`가 비어 있는 슬롯은 로드 시 건너뛴다.

`MarkPattern` 시트는 패턴 하나가 한 행이다.

| 컬럼 | 의미 |
| --- | --- |
| `No` | 패턴 참조키 (`Mark{N}P`가 가리키는 값) |
| `Desc` | 참고용 설명 문구 |
| `MoveForward / MoveBackward / MoveRight / MoveLeft` | 해당 방향 이동량 (0/빈칸이면 비활성) |
| `MoveSpeed` | 이동 시간(초) 값. 코드에서는 DOTween `DOMove`의 duration으로 쓰인다 |

예시 (`MarkPattern` 시트 일부): `1` = 우측으로 4 이동, `MoveSpeed` 3 / `3` = 앞으로 4 이동, `MoveSpeed` 1 / `5` = 고정(모든 값이 비어 있음).

**레벨 예시 (No 12 / Level 12)**: 제한시간 40초, 탄수 80, 과녁 11개. 패턴은 모두 `5`(고정), `Mark{N}T`는 모두 `[0,0]`이다.

| 슬롯 N | Mark{N} (과녁 ID) | Mark{N}G | Mark{N}P | Mark{N}T | Mark{N}PrevGroup |
| --- | --- | --- | --- | --- | --- |
| 1 | 3 | 1 | 5 | [0,0] | 0 |
| 2 | 9 | 1 | 5 | [0,0] | (빈칸) |
| 3 | 11 | 1 | 5 | [0,0] | (빈칸) |
| 4 | 15 | 2 | 5 | [0,0] | 1 |
| 5 | 19 | 2 | 5 | [0,0] | 1 |
| 6 | 23 | 2 | 5 | [0,0] | 1 |
| 7 | 25 | 2 | 5 | [0,0] | 1 |
| 8 | 8 | 3 | 5 | [0,0] | 1 |
| 9 | 21 | 3 | 5 | [0,0] | 1 |
| 10 | 12 | 3 | 5 | [0,0] | 1 |
| 11 | 27 | 3 | 5 | [0,0] | 1 |

그룹 1의 과녁 3개가 동시에 등장하고, 그 3개가 모두 처리되면 그룹 2의 4개가 동시에 등장하며, 이어 그룹 3이 등장한다. 이 예시는 `MarkPattern`의 이동 패턴을 쓰지 않는 레벨이다. 시트 전체(722개 과녁 배치)에서는 고정 패턴(`5`)이 439개이고 나머지는 이동 패턴이다.

### 순차 클리어와 동시 클리어를 나누는 규칙

`Mark{N}G` 하나만으로 순차/동시가 결정되는 것이 아니라, **`Mark{N}G`(그룹 번호)와 `Mark{N}PrevGroup`(선행 그룹 대기 여부)의 조합**이 결정한다.

- 같은 그룹 안의 과녁은 서로 기다리지 않고 동시에 활성화된다.
- `PrevGroup`이 1인 과녁은 "자신의 그룹 번호 - 1"에 속한 과녁이 모두 끝날 때까지 활성화를 미룬다. 이전 그룹은 항상 `현재 그룹 - 1`로 가정하며 (시트 헤더에도 같은 설명이 있다), 그룹 번호가 1 이하이면 대기하지 않는다.
- `PrevGroup`이 0/빈칸이면 그룹 번호가 2 이상이어도 대기하지 않는다. 그룹 번호는 이때 라벨에 그친다.
- "끝났다"는 `TrainingMark.IsFinished`가 true라는 뜻이며, 과녁이 맞았거나 유지 시간(`Mark{N}T`의 두 번째 값)이 지나 자동으로 내려간 경우이다. 놓쳐도 끝난 것으로 처리되어 다음 그룹이 진행된다.

시트에서 세 경우를 확인할 수 있다.

| 레벨 | 시트 구성 | 동작 |
| --- | --- | --- |
| Level 1 ~ 7 | 그룹 컬럼 비어 있음 | 모든 과녁이 즉시 동시에 활성화 |
| Level 8 ~ 10 | 그룹 번호(3, 4 등)가 있으나 `PrevGroup` 없음 | 그룹 번호가 있어도 모든 과녁이 동시에 활성화 |
| Level 11 | 과녁 7개가 각각 그룹 1 ~ 7, 2번째부터 `PrevGroup` = 1 | 한 개씩 순차 등장 |
| Level 12 | 그룹 1(3개) -> 그룹 2(4개) -> 그룹 3(4개), 그룹 2 이후 `PrevGroup` = 1 | 그룹 안에서는 동시, 그룹 사이는 순차 |

### 코드 흐름

**1. 테이블 행 -> 데이터 (`PracticeModeDef.OnLoad`)**: 컬럼 5개(`Mark{N}` ~ `Mark{N}PrevGroup`)를 30번 반복해 읽는다. 빈 슬롯은 건너뛰고, 그룹 번호 순으로 정렬해 `ActiveMarks`에 보관한다.

```csharp
// Mark1 ~ Mark30T 컬럼까지
ActiveMarks = new List<TrainingMarkData>();
for (int i = 0; i < ACTIVE_MARK_MAX_COUNT; i++)
{
	int markNo = loader.ReadInt();			//1. Mark{N}
	int groupNo = loader.ReadInt();			//2. Mark{N}G

	int markPatternKey = loader.ReadInt();		//3. Mark{N}P

	try
	{
		string timeArrStr = loader.ReadString();			//4. Mark{N}T
		bool afterPrevGroup = loader.ReadInt() == 1;		//5. Mark{N}PrevGroup

		//빈칸인 경우 skip 한다.
		if(markPatternKey == 0 || !timeArrStr.HasContent()) continue;

		int[] markTimeArr = JsonConvert.DeserializeObject<int[]>(timeArrStr);

		// ... 중략 (TrainingMarkData 생성 후 ActiveMarks.Add) ...
	}
	catch (Exception ex)
	{
		DebugEx.Log($"{i + 1}번째 타겟 정보를 읽어오는데 실패했습니다! {ex.Message}", LogColorType.Red);
	}
}

ActiveMarks = ActiveMarks.OrderBy(x => x.GroupNo).ToList();
```

**2. 데이터 -> 씬의 과녁 (`TrainingMarkManager.SetShootingMode`)**: 과녁 ID로 격자 위치를 찾고, 패턴 참조키로 `PracticeModeMarkPatternDef`를 조회해 파라미터를 채운다. 선행 그룹 대기가 켜져 있으면 "그룹 - 1"의 과녁 목록을 런타임에 연결한다.

```csharp
foreach (TrainingMarkData activeMark in selectedPracticeModeDef.ActiveMarks)
{
	int markIndex = activeMark.MarkNo;
	int currentGroupId = activeMark.GroupNo;

	bool afterPrevGroup = activeMark.AfterPrevGroup;

	int targetRow = markIndex / GetColumnCount();
	int targetColumn = markIndex % GetColumnCount();

	TrainingMark trainingMark = _trainingMarkMap[targetRow].GetMark(targetColumn);
	var targetPatternDef = PracticeModeMarkPatternCDB.Instance.GetDef(activeMark.MarkPatternKey);

	TrainingMark.TradingMarkParams tradingMarkParams = new()
	{
		MoveLeft = targetPatternDef.MoveLeft,
		MoveRight = targetPatternDef.MoveRight,

		MoveForward = targetPatternDef.MoveForward,
		MoveBackward =  targetPatternDef.MoveBackward,

		MoveSpeed = targetPatternDef.MoveSpeed,

		ActiveTime = activeMark.MarkBeginTime,
		DeactiveTime = activeMark.MarkEndTime,

		OnHit = OnHitTrainingMark
	};

	if (afterPrevGroup && (currentGroupId > 1))
	{
		tradingMarkParams.PrevGroupMarks = GetTrainingMarksByGroupID(currentGroupId - 1);
	}

	trainingMark.InitContent(tradingMarkParams, false);
}
```

**3. 과녁 하나의 생명주기 (`TrainingMark.InitTweenMove`)**: 선행 그룹이 끝날 때까지 대기 -> 활성화 대기 시간 -> 활성화 -> DOTween Yoyo 이동 -> (유지 시간이 있으면) 시간 초과 처리. 앞뒤 이동과 좌우 이동은 동시에 하지 않으며, 코드 주석대로 앞뒤 이동이 켜져 있으면 좌우 이동 값은 무시한다.

```csharp
IEnumerator InitTweenMove()
{
	if (_prevGroupMarks != null && _prevGroupMarks.Count > 0)
	{
		yield return new WaitUntil(() => _prevGroupMarks.TrueForAll(mark => mark.IsFinished));
	}

	yield return new WaitForSeconds(_activeTime);
	PlayAnimation("Activate");

	_hitHandler.ToggleCollider(true);
	_body.tag = bl_MFPS.AI_TAG;

	//Note. 앞뒤 이동과 좌우 이동을 동시에 할 때 움직임이 애매하기 때문에 앞뒤 이동과 좌우 이동을 동시에 하지 않는다.
	//만약, 앞뒤 이동이 활성화 되어 있으면 좌우 이동 여부는 무시한다.
	if (_canMoveVertical)
	{
		Vector3 targetPos = _moveForward > 0 ? GetForwardEnd() : GetBackEnd();
		transform
			.DOMove(targetPos, _moveSpeed)
			.SetLoops(-1, LoopType.Yoyo)
			.SetEase(Ease.Linear);
	}
	else if (_canMoveHorizontal) { /* ... 중략 (좌/우 이동, 동일한 DOMove + Yoyo) ... */ }

	if (_deactiveTime > 0)
	{
		yield return new WaitForSeconds(_deactiveTime);

		PlayAnimation("Deactivate");

		IsFinished = true;
		_onHitCallback?.Invoke(false);

		OnDisable();
	}
}
```

### 훈련 진행 (`PracticeMode`)

`PracticeMode`는 두 상태(`FREE_MODE` 자유 연습장, `TRAINING_MODE` 레벨 훈련)를 오간다. 진입 시 자유 연습장으로 시작하고(`Start`), 레벨을 시작하면 훈련 모드로, 결과창을 닫거나 나가면 다시 자유 연습장으로 돌아온다.

```csharp
void SetModeState(State newState)
{
	switch (newState)
	{
		case State.NONE:
			break;

		case State.FREE_MODE:
			OnFreeMode();
			break;

		case State.TRAINING_MODE:
			OnTrainingMode();
			break;
	}

	_currentState = newState;
	bl_UtilityHelper.LockCursor(true, LockCursorMask.PracticePopup);
}
```

- **자유 연습장**: 주무기·보조무기 탄약이 무한이고(`SetInifinityAmmo(true)`), 과녁은 `SetFreeShootingMode`로 `isFreeMode = true`로 초기화된다. 이 모드의 과녁은 맞아도 성공/실패를 집계하지 않고 3초 뒤 스스로 다시 활성화된다.
- **레벨 시작**: 플레이어를 스폰하고 3초 오프라인 카운트다운이 끝나면 선택된 레벨 정의를 `PracticeModeRepository`에 동기화하고 레벨 변경 이벤트(`DispatchPracticeLevelChange`)를 발행한다.

```csharp
public void OnStartLevel(PracticeModeDef newPracticeModeDef)
{
	BattleManager.Instance.SpawnLocalPlayer(Team.Team1);

	_battleMainUi.ShowLoadoutButton(false, LoadoutButtonShowBits.ByWaitingTime);

	PracticeModeUi practiceModeUi = _battleMainUi.PracticeModeUi;
	practiceModeUi.ToggleTrainingStartUi(false);
	practiceModeUi.ToggleRoundInfoUI(false);

	_isCountdown = true;

	CountDownUi.Instance.StartOfflineCountDown(3, () =>
	{
		PracticeModeRepository.Instance.SyncSelectModeDef(newPracticeModeDef);
		bl_EventHandler.DispatchPracticeLevelChange();

		_isCountdown = false;
	});
}
```

- **탄수 제한**: 이벤트를 받은 `OnPracticeLevelChanged`가 주무기 탄수를 `LimitBulletNum`으로 제한하고 훈련 모드로 전환한다. 발사 이벤트마다 남은 탄수 UI를 차감한다 (주무기일 때만).

```csharp
void OnPracticeLevelChanged()
{
	var selectedPracticeModeDef = PracticeModeRepository.Instance.SelectedPracticeModeDef;
	var gunManager = BattleManager.Instance.LocalActor.GetComponent<GunManager>();

	//주무기는 탄수를 LimitBulletNum 으로 제한한다.
	//보조무기는 기존 스텟을 따른다.
	var equipWeapons = gunManager.EquipWeapons;
	for (int i = 0; i < equipWeapons.Count; i++)
	{
		equipWeapons[i].SetInifinityAmmo(false);

		//주 무기
		if (i == 0)
		{
			_remainBullet = selectedPracticeModeDef.LimitBulletNum;
			_practiceModeUi.UpdateBulletLeftUI(_remainBullet);
			equipWeapons[i].UpdateBulletLeft(_remainBullet);
		}
		//보조 무기 [권총]
		else
		{
			if(i == 1) equipWeapons[i].ResetAmmo();
		}
	}

	_practiceModeUi.ToggleRoundInfoUI(true);

	SetModeState(State.TRAINING_MODE);
}
```

```csharp
void OnLocalPlayerFire(WeaponCode weaponCode)
{
	if(CurrentState != State.TRAINING_MODE) return;
	if(_targetWeaponCode != weaponCode) return;

	_battleMainUi.PracticeModeUi.UpdateBulletLeftUI(--_remainBullet);
}
```

- **종료**: 시간이 다 되면(`OnEndTimer`) 또는 라운드 정보 UI가 미션 종료 콜백(`OnFinishMission`)을 부르면 `SetEndState`로 수렴한다. `SetEndState`는 `_isEnd`로 중복 진입을 막고 타이머를 멈춘 뒤, 마지막 과녁이 넘어지는 것을 보여주기 위해 1초 뒤에 결과창을 연다. 결과창은 종료 사유(`CLEAR`/`TIME_OVER`/`OUT_OF_AMMO`)를 받는다. (성공/실패 집계는 `PracticeModeUi`가 하며 이 폴더에는 포함되어 있지 않다.)
- **결과창 후속 처리**: 결과창이 닫힐 때 `closedType` 값으로 분기한다 (0: 나가기, 1: 다시하기, 2: 다음 레벨).

```csharp
void OnClosePracticeLevelResultUi(int closedType)
{
	if(this == null) return;
	// DebugEx.Log($"closeType :{closedType}");

	PracticeModeDef currentLevelDef = PracticeModeRepository.Instance.SelectedPracticeModeDef;

	//closedType 0 : (일반)나가기
	//closedType 1 : 다시하기
	//closedType 2 : 다음 레벨
	switch (closedType)
	{
		case 0:
			SetModeState(State.FREE_MODE);
			break;

		case 1:
			OnStartLevel(currentLevelDef);
			break;

		case 2:
			// ... 중략 (다음 레벨 정보 팝업 -> OnStartLevel(nextLevelDef), 마지막 레벨이면 안내 후 FREE_MODE) ...
			break;

		default:
			SetModeState(State.FREE_MODE);
			break;
	}
}
```

- **보상**: 보상 처리는 `OnReceiveReward`가 맡는다 (호출부인 결과창 UI는 이 폴더에 없다). `ShopTransaction`으로 일반 보상을 지급하고 보상 팝업을 띄운 뒤, 광고 시청 제안 확인창의 결과에 따라 두 번째 보상 팝업을 띄우는 순서를 `UniTask`의 `await`로 이어 붙였다. 각 단계가 앞 단계의 UI가 닫히기를 기다리므로 콜백이 중첩되지 않는다.

```csharp
public async UniTask<bool> OnReceiveReward(PracticeModeDef practiceModeDef)
{
	//1. 일반 보상 처리
	var tx = new ShopTransaction();
	tx.GiveRewards(practiceModeDef.Rewards, ItemGetReason.AdsRW, practiceModeDef.No);
	if (!await tx.Commit()) { return false; }

	RewardPopupUi normalRewardPopup = Navigator.OpenUi<RewardPopupUi>(new RewardPopupUiParam(practiceModeDef.Rewards, true)).Ui;
	await UniTask.WaitUntil(() => normalRewardPopup.IsClosed);

	//2. 광고 보기 제안 팝업
	string msg = I18N.Translate("PracticeMode.WatchADAndRewardOnceMore?");
	var confirmUi = Navigator.Confirm(msg, closeBtTextKey: "No", primaryBtTextKey: "Yes");
	await UniTask.WaitUntil(() => confirmUi.IsClosed);

	//3. 광고 추가 보상 처리
	if (confirmUi.Result == UiResult.Primary)
	{
		RewardPopupUi adRewardPopup = Navigator.OpenUi<RewardPopupUi>(new RewardPopupUiParam(practiceModeDef.Rewards, true)).Ui;
		await UniTask.WaitUntil(() => adRewardPopup.IsClosed);
	}

	return true;
}
```

### 설계 포인트

과녁의 위치·그룹·움직임 패턴을 코드가 아닌 테이블에서 읽어오게 한 이유는, 레벨 밸런싱이 반복적으로 조정되는 콘텐츠였기 때문이다. `Mark1G` 값 하나로 순서 종속(순차 클리어형)과 독립(동시 클리어형) 스테이지를 모두 표현할 수 있어, 새 레벨을 추가할 때 코드 수정 없이 시트 값만 바꾸면 된다.

관련 코드: [03. Practice](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/03.%20Practice)
