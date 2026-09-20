## 인게임 튜토리얼 (Tutorial)

신규 유저 온보딩용 인게임 튜토리얼이며, 동시에 PVP 위주 콘텐츠를 보완하는 싱글플레이 학습 콘텐츠로서 폭탄 설치/해체·실전 사격 훈련 단계까지 함께 녹여낸 모드이다.

튜토리얼은 `TutorialTask` 추상 클래스를 상속받은 하위 Task 단위로 구성되어 있고, 각 Task에 대한 흐름 제어는 UniTask를 활용하였다. Task는 `InitTutorial()`에서 순서대로 큐에 쌓이며, 폭탄 설치(`DemolitionTask`)·해체(`DefusingTask`)·표적 사격(`TrainingMarkRemoveTask`, `TrainingGrenadeTask`) 등 실전 사격/폭탄 훈련 단계도 이 큐 안에 다른 Task와 동일한 방식으로 포함되어 있다.

> 근거: 아래는 개발자 본인 커밋만 담은 git export(2025-01-06 ~ 2025-11-26, 약 50개 이전 커밋과 다른 팀원의 작업은 포함되지 않음)에서 확인되는 내용이다.
> - 튜토리얼은 LR-482 커밋 41건(2025-08-04 ~ 2025-09-09)으로 구현되었다. 이 중 첫 3건(2025-08-04 ~ 08-05)은 킬캠 개선이고, 튜토리얼 커밋은 38건이다. 초안은 2025-08-06(`b9d8e465`, 조작법 설정부터 걷기까지)부터 2025-08-09(폭탄 단계)까지 이어졌다.
> - `TutorialManager.cs`를 수정한 본인 커밋은 52건(2025-08-06 ~ 2025-11-17)이다. 2025-08-08(`d6b1e230`)에 파일이 `Modes` 폴더에서 `Tutorial` 폴더로 이동했다.
> - 후속 수정은 LR-483(v0.1.9, 2025-08-11 ~ 08-12: 자동사격 선택 UI 반영, 바닥 화살표 안내), LR-535(v0.2.0, 2025-09-04 ~ 11-05, 튜토리얼 관련 16건: 건너뛰기 버튼, 재장전 단계 등), LR-563(2025-09-16), LR-634(2025-11-04), LR-637(v1.1, 2025-11-10 ~ 11-17, 6건)로 확인된다. 안정화 내용은 아래 "안정화 이력"에 정리했다.
> - 위 범위는 "git 이력(2025-01 이후)에서 확인되는 본인 커밋"이며, 다른 커밋이 없다는 뜻은 아니다.

### 문제와 접근

- **문제**: 튜토리얼은 "이동 -> 사격 -> 재장전 -> 무기 교체 -> 수류탄 -> 폭탄 설치/해체" 같은 단계가 순서대로 이어지고, 단계마다 UI 팝업·버튼 강조·트리거 대기 같은 비동기 흐름이 있으며, 도중에 스킵/퇴장으로 끊길 수 있다.
- **접근**: 단계 하나를 `TutorialTask` 하위 클래스 하나로 만들고(13개), `InitTutorial()`이 순서대로 큐에 넣고, `BeginTutorial()`이 큐를 하나씩 꺼내 실행·완료 대기를 반복한다. 중단은 `TutorialManager`가 가진 취소 토큰 하나로 전파한다. 진입점은 `TutorialMode.Start()`이며 `InitTutorial()` 후 `BeginTutorial().Forget()`을 호출한다.

### Task 기반 구조 (`TutorialTask`)

Execute는 해당 Task 단계가 되었을 때 호출되는 함수이고, Task가 종료되면 EndTask를 거쳐 CancelTask가 호출된다. `CancelTask()`는 `IsFinished` 가드로 한 번만 실행되며 `Cleanup()`(이벤트 구독 해제 등 각 Task의 정리)을 호출한다. `IsFinished`는 `BeginTutorial`이 다음 Task로 넘어가는 기준이기도 하다. Task 안의 대기는 `TutorialCancelToken`(매니저의 공유 토큰)을 사용한다.

```csharp
public abstract class TutorialTask
{
	public bool IsFinished;

	protected Hashtable _hashtable;
	protected CancellationToken TutorialCancelToken => TutorialManager.Instance.TutorialCancellationToken.Token;

	public virtual void Execute() { }

	protected TutorialTask()
	{
		_hashtable = new Hashtable();
		IsFinished = false;
	}

	//마지막에 반드시 호출
	protected virtual void EndTask()
	{
		CancelTask();
	}

	public void CancelTask()
	{
		if (IsFinished) return;
		IsFinished = true;

		Cleanup();
	}

	protected virtual void Cleanup() { }
}
```

아래는 조작법을 선택하는 Task에 대한 처리 구조이다.

```csharp
public class SelectFiringOptionTask : TutorialTask
{
	public SelectFiringOptionTask() : base() { }

	public override async void Execute()
	{
		try
		{
			base.Execute();

			bl_UtilityHelper.LockCursor(false, LockCursorMask.TutorialPopup);

			TutorialTypeSelectUiParam uiParam = new TutorialTypeSelectUiParam() { OnSelect = OnSelected };
			TutorialTypeSelectUi popUp = Navigator.OpenUi<TutorialTypeSelectUi>(uiParam).Ui;

			TutorialManager.Instance.IsPopupExist = true;
			await UniTask.WaitUntil(() => popUp.IsClosed, cancellationToken: TutorialCancelToken);
			TutorialManager.Instance.IsPopupExist = false;

			bl_UtilityHelper.LockCursor(true, LockCursorMask.TutorialPopup);

			EndTask();
		}
		catch (OperationCanceledException) { }
	}

	void OnSelected(int selectedIndex)
	{
		DeviceConfig.Instance.AutoFire = TutorialManager.Instance.AutoFire = selectedIndex == 0;

		MobileControlsUi mobileControlsUi = BattleMainUi.Instance.MobileControlsUi;
		mobileControlsUi.UpdateFireButtons();
	}
}
```

<img width="915" height="513" alt="image" src="https://github.com/user-attachments/assets/a017ef47-96b5-479f-a7f9-5864a2ff1bc6" />

### 전체 Task 흐름 제어 (`TutorialManager.BeginTutorial`)

`InitTutorial()`은 Task를 다음 18단계 순서로 `Queue<TutorialTask>`에 넣는다 (분기 없이 순서대로 나열한다).

대기 -> 조작법 선택(`SelectFiringOptionTask`) -> 대기 -> 거점 안내(`WaitAndSeeWayPointTask`) -> 대기 -> 기본 조작 설명(`ShowDescriptionTask`) -> 시작 Decal 정리(`CleanBeginDecalsTask`) -> 이동(`MoveToTask`, 4개 지점) -> 사격/재장전/표적 제거(`ReloadAndTrainingMarkRemoveTask`) -> 무기 교체(`ChangeWeaponTask`) -> 표적 제거(`TrainingMarkRemoveTask`) -> 수류탄(`TrainingGrenadeTask`) -> 대기 -> 폭탄 설치(`DemolitionTask`) -> 대기 -> 폭탄 해체(`DefusingTask`) -> 대기 -> 종료(`TutorialFinishTask`)

```csharp
public void InitTutorial()
{
	_currentTutorialTaskIndex = 0;

	BeginDecalWayPoint = new List<GameObject>();

	BattleMainUi.Instance.TutorialModeUi.SkipBt.SetClickHandler(OnSkipBt);
	MobileControlsUi.OnReload += OnReload;

	AutoFire = true;
	_tutorialTasks = new Queue<TutorialTask>();
	TutorialCancellationToken = new CancellationTokenSource();

	//0. 잠시 몇 초 기다림
	WaitSecTask waitTask = new WaitSecTask(1.0f);
	_tutorialTasks.Enqueue(waitTask);

	//1. 조작법 선택하기
	SelectFiringOptionTask selectFiringOptionTask = new();
	_tutorialTasks.Enqueue(selectFiringOptionTask);

	// ... 중략 (2 ~ 16단계) ...

	//17. 튜토리얼 종료
	{
		TutorialFinishTask task14 = new TutorialFinishTask();
		_tutorialTasks.Enqueue(task14);
	}
}
```

`BeginTutorial`은 큐가 빌 때까지 `Dequeue -> Execute -> IsFinished 대기`를 반복한다. 일시정지 메뉴가 열려 있는 동안에는 다음 Task로 진행하지 않는다.

```csharp
public async UniTaskVoid BeginTutorial(Action onFinished = null)
{
	if (_tutorialTasks == null || TutorialCancellationToken == null)
		return;

	VirtualAudioController.Initialized(this);

	foreach (Transform triggerTF in etcTriggersParent)
	{
		TutorialColliderTrigger trigger = triggerTF.GetComponent<TutorialColliderTrigger>();
		if(trigger != null) trigger.Reset();
	}

	//Note. WeaponSlot들을 활성화 처리가 여러번 호출되면서 시작되기 때문에 그 흐름을
	//각각 직접 제어하기에는 위험성이 있어, Canvas 자체를 활성화 / 비활성화 처리함
	MobileControlsUi.Canvas.gameObject.SetActiveGo(false);

	while (_tutorialTasks.Count > 0)
	{
		try
		{
			await UniTask.WaitUntil(
				() => BattleMainUi.Instance.PauseMenu == null,
				cancellationToken: TutorialCancellationToken.Token);

			TutorialTask currentTutorialTask = _tutorialTasks.Dequeue();
			CurrentTutorialTask = currentTutorialTask;

			currentTutorialTask.Execute();

			await UniTask.WaitUntil(() => currentTutorialTask.IsFinished,
				cancellationToken: TutorialCancellationToken.Token);

			CurrentTutorialTask = null;

			OnNextTutorialStep();
		}
		catch (OperationCanceledException)
		{
			break;
		}
		catch (Exception e)
		{
			DebugEx.Log(e.Message);
			break;
		}
	}

	CurrentTutorialTask = null;
	onFinished?.Invoke();
}
```

### 설계 포인트

- **공유 취소 토큰 하나 + 한 번만 실행되는 정리**: 큐를 순회하는 `BeginTutorial`과 각 Task의 대기(`WaitUntil`, `WaitForSeconds`)가 모두 `TutorialManager.TutorialCancellationToken` 하나를 쓴다 (Task는 기반 클래스의 `TutorialCancelToken`으로 접근). `StopTutorial()`은 `OnDisable`(그리고 `RestartTutorial`)에서 호출되며, 토큰을 취소·폐기하고, 현재 Task의 `CancelTask()`를 부르고, 대기 중인 큐를 비운다.
  - 얻는 것: 취소 한 번으로 진행 중인 모든 대기가 `OperationCanceledException`으로 풀리므로, Task마다 `CancellationTokenSource`를 만들고 폐기할 필요가 없다. 각 Task의 `Execute`는 이 예외를 잡아 조용히 끝난다.
  - 남는 것: `CancelTask()`는 이제 무엇을 취소하지는 않고, `IsFinished` 가드로 `Cleanup()`을 정확히 한 번만 실행하는 역할이다. 정상 종료(`EndTask`)와 중단(`StopTutorial`) 어느 경로든 정리는 한 번만 일어난다. 그래서 중단 시에도 실행 중이던 Task가 잡아 둔 이벤트 구독 같은 자원이 해제된다.

```csharp
public void StopTutorial()
{
	// 1. 루프용 토큰 취소
	if (TutorialCancellationToken != null)
	{
		if (!TutorialCancellationToken.IsCancellationRequested)
			TutorialCancellationToken.Cancel();

		TutorialCancellationToken.Dispose();
		TutorialCancellationToken = null;
	}

	// 2. 현재 Task 정리
	CurrentTutorialTask?.CancelTask();
	CurrentTutorialTask = null;

	// 3. 대기중 큐 비우기
	_tutorialTasks?.Clear();
}
```

- **거대한 switch 대신 큐로 순서를 데이터처럼 다룸**: `InitTutorial()`에서 Task 인스턴스를 순서대로 `Enqueue`하고, `BeginTutorial`은 큐가 빌 때까지 `Dequeue → Execute → 완료 대기`만 반복한다. 튜토리얼 단계를 추가하거나 순서를 바꿀 때도 이 나열만 수정하면 되고, 각 Task는 앞뒤 Task의 존재를 몰라도 동작한다.

관련 코드: [03. Tutorial](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/03.%20Tutorial)
