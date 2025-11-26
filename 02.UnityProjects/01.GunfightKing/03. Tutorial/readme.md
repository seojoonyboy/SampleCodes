튜토리얼은 TutorialTask 추상 클래스를 상속받은 하위 Task 단위로 구성되어 있다.
각각의 Task에 대한 흐름 제어는 UniTask를 활용하였다.

아래는 기반이 되는 추상 클래스 Task 에 대한 예시이다.

Execute는 해당 Task 단계가 되었을 때 호출되는 함수이고,
해당 Task가 종료되면 CancelTask가 호출된다.

<pre>
  <code>
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
  </code>
</pre>

아래는 조작법을 선택하는 Task에 대한 처리 구조이다.

<pre>
  <code>
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
  </code>
</pre>
<img width="915" height="513" alt="image" src="https://github.com/user-attachments/assets/a017ef47-96b5-479f-a7f9-5864a2ff1bc6" />

아래는 전체 Task 흐름에 대한 제어 예시이다.

<pre>
  <code>
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
  </code>
</pre>
