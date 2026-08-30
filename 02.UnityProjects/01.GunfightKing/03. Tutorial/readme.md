## 인게임 튜토리얼 (Tutorial)

신규 유저 온보딩용 인게임 튜토리얼이며, 동시에 PVP 위주 콘텐츠를 보완하는 싱글플레이 학습 콘텐츠로서 폭탄 설치/해체·실전 사격 훈련 단계까지 함께 녹여낸 모드이다.

튜토리얼은 `TutorialTask` 추상 클래스를 상속받은 하위 Task 단위로 구성되어 있고, 각 Task에 대한 흐름 제어는 UniTask를 활용하였다. Task는 `InitTutorial()`에서 순서대로 큐에 쌓이며, 폭탄 설치(`DemolitionTask`)·해체(`DefusingTask`)·표적 사격(`TrainingMarkRemoveTask`, `TrainingGrenadeTask`) 등 실전 사격/폭탄 훈련 단계도 이 큐 안에 다른 Task와 동일한 방식으로 포함되어 있다.

### Task 기반 구조 (`TutorialTask`)

Execute는 해당 Task 단계가 되었을 때 호출되는 함수이고, Task가 종료되면 EndTask를 거쳐 CancelTask가 호출되어 자원을 정리한다.

```csharp
public abstract class TutorialTask
{
	public bool IsFinished;
	public CancellationTokenSource CancellationTokenSource;
	
	protected Hashtable _hashtable;

	public virtual void Execute() { }

	protected TutorialTask()
	{
		_hashtable = new Hashtable();
		IsFinished = false;
		
		CancellationTokenSource = new CancellationTokenSource();
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
		CancellationTokenSource?.Cancel();
		CancellationTokenSource?.Dispose();
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
		base.Execute();
		
		bl_UtilityHelper.LockCursor(false, LockCursorMask.TutorialPopup);
		
		TutorialTypeSelectUiParam uiParam = new TutorialTypeSelectUiParam() { OnSelect = OnSelected };
		TutorialTypeSelectUi popUp = Navigator.OpenUi<TutorialTypeSelectUi>(uiParam).Ui;

		TutorialManager.Instance.IsPopupExist = true;
		await UniTask.WaitUntil(() => popUp.IsClosed, cancellationToken: CancellationTokenSource.Token);
		TutorialManager.Instance.IsPopupExist = false;
		
		bl_UtilityHelper.LockCursor(true, LockCursorMask.TutorialPopup);

		EndTask();
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

- **2단계 취소 토큰**: 큐 전체를 순회하는 `TutorialCancellationToken`(TutorialManager 소유)과, Task 개별의 `CancellationTokenSource`가 분리되어 있다. `StopTutorial()`은 이 둘을 각각 취소/정리하므로, 스킵 등으로 튜토리얼 전체를 중단해도 마지막까지 실행 중이던 Task의 자원 정리(`Cleanup`)가 항상 보장된다.

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

관련 코드: [03. Practice](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/03.%20Practice)
