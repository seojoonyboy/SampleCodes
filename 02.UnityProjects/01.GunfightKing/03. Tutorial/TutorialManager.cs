using Cysharp.Threading.Tasks;
using Framework;
using Framework.UI;
using Game.Data;
using Game.View.CustomSeraizeDictionary;
using Game.View.UI;
using MFPS.Audio;
using MFPS.Mobile;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Game.View.BattleSystem
{
	public class TutorialManager : MonoBehaviour
	{
		[SerializeField] SerializableDictionary<int, Sprite> tutorialSpriteMap;		//튜툐리얼에 사용되는 이미지 맵
		[SerializeField] SerializableDictionary<int, Transform> tutorialWayPointMap;	//튜토리얼에 사용되는 거점 포인트 맵
		[SerializeField] SerializableDictionary<int, Transform> tutorialTargetPointMap;	//튜토리얼에 사용되는 표적 포인트 맵
		[SerializeField] SerializableDictionary<int, Transform> tutorialColliderBlockMap;	//튜토리얼에 사용되는 보이지 않는 벽 [진입 차단용]
		[SerializeField] SerializableDictionary<int, Transform> tutorialWallMeshMap;		//튜토리얼에 사용되는 울타리 벽 [진입 차단용 매시]
		
		[SerializeField] GameObject _groundDecalPrefab;		//특정 장소 이동 표시 지면 Decal
		[SerializeField] GameObject _groundArrowPrefab;		//특정 장소 이동 표시 지면 화살표 Decal
		[SerializeField] GameObject _trainingMarkPrefab;	//과녁 프리팹
		[SerializeField] GameObject _grenadeAreaDecal;		//폭탄 던지는 영역 표시용 바닥 Decal
		[SerializeField] TutorialBomb _bomb;		//폭탄
		
		[SerializeField] TutorialColliderTrigger demolitionAreaColliderTrigger;	//폭탄 설치 지역
		[SerializeField] TutorialColliderTrigger defusingAreaColliderTrigger;	//폭탄 해체 지역
		[SerializeField] Transform etcTriggersParent;		//기타 맵 환경 Trigger들 Parent
		
		[SerializeField] bl_VirtualAudioController _virtualAudioController;
		
		BattleMainUi BattleMainUI => BattleMainUi.Instance;
		
		public WeaponSwitcherSlotManager WeaponSwitcherSlotManager => WeaponSwitcherSlotManager.Instance;
		public ButtonEx Weapon0 => WeaponSwitcherSlotManager.GetSlotButton(0);		//주무기
		public ButtonEx Weapon1 => WeaponSwitcherSlotManager.GetSlotButton(1);		//보조무기
		public ButtonEx Weapon2 => WeaponSwitcherSlotManager.GetSlotButton(2);		//근접무기
		public ButtonEx LoadOutBt => BattleMainUI.LoadoutBt;
		
		MobileControlsUi MobileControlsUi => MobileControlsUi.Instance;
		public GameObject FireBt => MobileControlsUi.FireBtn;
		public GameObject ReloadBt => MobileControlsUi.ReloadBt;
		public GameObject GrenadeBt => MobileControlsUi.GrenadeBt;
		public GameObject TacticalBt => MobileControlsUi.TacticalBt;
		public GameObject AimBt => MobileControlsUi.AimBtn;
		public GameObject CrouchBt => MobileControlsUi.CrouchBtn;
		public GameObject JumpBt => MobileControlsUi.JumpBt;
		
		public GameObject GroundDecalPrefab => _groundDecalPrefab;
		public GameObject GroundArrowPrefab => _groundArrowPrefab;
		public GameObject TrainingMarkPrefab => _trainingMarkPrefab;
		public GameObject GrenadeAreaDecal => _grenadeAreaDecal;
		public TutorialBomb Bomb => _bomb;

		public bool BombInteract = false;
		public bool CanLocalPlant = false;
		public bool CanLocalDefuse = false;

		public bool CanReloadInteract = false;		//키보드 재장전 입력을 받기 위함
		
		public bl_VirtualAudioController VirtualAudioController => _virtualAudioController;
		public TutorialColliderTrigger DemolitionAreaColliderTrigger => demolitionAreaColliderTrigger;
		public TutorialColliderTrigger DefusingAreaColliderTrigger => defusingAreaColliderTrigger;
		public MinimapItem DemolitionMiniMapItem => demolitionAreaColliderTrigger.GetComponent<MinimapItem>();
		public MinimapItem DefusingMiniMapItem => defusingAreaColliderTrigger.GetComponent<MinimapItem>();

		public List<GameObject> BeginDecalWayPoint;		//첫 시작과 동시에 나오는 지면 Decal과 화살표

		public bool IsPopupExist = false;
		
		CancellationTokenSource TutorialCancellationToken;

		public Action OnReloadButton;

		private static TutorialManager _instance;
		public static TutorialManager Instance
		{
			get
			{
				if(_instance == null) { _instance = FindFirstObjectByType<TutorialManager>(); }
				return _instance;
			}
		}
		
		Queue<TutorialTask> _tutorialTasks;
		TutorialSubType _selectedTutorialType;

		public TutorialTask CurrentTutorialTask;
		int _currentTutorialTaskIndex;
		int _currentButtonActiveFlag;		//현재 버튼 활성화 상태 비트 Flag

		public Action OnPlantFinished;
		public Action OnDefuseFinished;

		public bool AutoFire;
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
			
			//2. 잠시 몇 초 기다림
			waitTask = new WaitSecTask(1.0f);
			_tutorialTasks.Enqueue(waitTask);
			
			//3. Waypoint를 보여주고 몇 초 기다림
			{
				Hashtable moveToTaskData = new Hashtable();
				List<Transform> targetPoints = new List<Transform>();
				targetPoints.Add(GetTutorialWayPoint(0));
				
				moveToTaskData.Add("TargetPosList", targetPoints);
				moveToTaskData.Add("WaitTime", 0.0f);
				
				WaitAndSeeWayPointTask moveToTask = new WaitAndSeeWayPointTask(moveToTaskData);
				_tutorialTasks.Enqueue(moveToTask);
			}
			
			//4. 잠시 몇 초 기다림
			waitTask = new WaitSecTask(1.0f);
			_tutorialTasks.Enqueue(waitTask);
			
			//5. 기본 조작 방법 설명
			Hashtable virtualPadDescTaskData = new Hashtable();
			virtualPadDescTaskData.Add("Image", GetTutorialSprite(1));
			virtualPadDescTaskData.Add("Header", I18N.Translate("TutorialMode.VirtualPadDesc."));
			virtualPadDescTaskData.Add("AutoSkip", 1);
			virtualPadDescTaskData.Add("Duration", 3.0f);
			ShowDescriptionTask showDescriptionTask = new ShowDescriptionTask(virtualPadDescTaskData);
			_tutorialTasks.Enqueue(showDescriptionTask);
			
			//6. 시작 Decal 정리하기
			CleanBeginDecalsTask cleanBeginDecalsTask = new CleanBeginDecalsTask();
			_tutorialTasks.Enqueue(cleanBeginDecalsTask);
			
			//7. 이동하기
			{
				Hashtable moveToTaskData = new Hashtable();
				List<Transform> targetPoints = new List<Transform>();
				targetPoints.Add(GetTutorialWayPoint(0));
				targetPoints.Add(GetTutorialWayPoint(1));
				targetPoints.Add(GetTutorialWayPoint(2));
				targetPoints.Add(GetTutorialWayPoint(3));
				
				moveToTaskData.Add("TargetPosList", targetPoints);
				moveToTaskData.Add("VisibleMessageUI", 1);
				
				moveToTaskData.Add("MsgBoxText",  I18N.Translate("TutorialMode.MoveGuilde."));
				
				MoveToTask moveToTask = new MoveToTask(moveToTaskData);
				_tutorialTasks.Enqueue(moveToTask);
			}
			
			//8. 사격 -> 재장전 -> 모든 타겟 제거 하기
			{
				Hashtable task7Table = new Hashtable();
				string context = I18N.Translate("TutorialMode.RemoveAllTargets.");
				List<Transform> targetList = new List<Transform>()
				{
					GetTutorialTargetPoint(0),
					GetTutorialTargetPoint(1),
					GetTutorialTargetPoint(2)
				};

				List<Quaternion> targetRotList = new List<Quaternion>()
				{
					GetTutorialTargetPoint(0).rotation,
					GetTutorialTargetPoint(1).rotation,
					GetTutorialTargetPoint(2).rotation
				};
			
				task7Table.Add("WeaponSlotIndex", 1);
				task7Table.Add("TargetPosList", targetList);
				task7Table.Add("TargetRotList", targetRotList);
				task7Table.Add("Context", context);
				ReloadAndTrainingMarkRemoveTask task7 = new ReloadAndTrainingMarkRemoveTask(task7Table);
				_tutorialTasks.Enqueue(task7);
			}
			
			//9. 무기 바꾸기
			{
				ChangeWeaponTask task8 = new ChangeWeaponTask(1, 0);
				_tutorialTasks.Enqueue(task8);
			}
			
			//10. 표적 제거하기
			{
				Hashtable task9Table = new Hashtable();
				string context = I18N.Translate("TutorialMode.RemoveAllTargets.");
				List<Vector3> targetList = new List<Vector3>()
				{
					GetTutorialTargetPoint(0).position,
					GetTutorialTargetPoint(1).position,
					GetTutorialTargetPoint(2).position
				};

				List<Quaternion> targetRotList = new List<Quaternion>()
				{
					GetTutorialTargetPoint(0).rotation,
					GetTutorialTargetPoint(1).rotation,
					GetTutorialTargetPoint(2).rotation
				};
			
				task9Table.Add("IsInfinityAmmo", 1);
				task9Table.Add("TargetPosList", targetList);
				task9Table.Add("TargetRotList", targetRotList);
				task9Table.Add("Context", context);
				
				TrainingMarkRemoveTask task9 = new TrainingMarkRemoveTask(task9Table);
				_tutorialTasks.Enqueue(task9);
			}
			
			//11. 수류탄으로 표적 제거하기
			{
				TrainingGrenadeTask task10 = new TrainingGrenadeTask();
				_tutorialTasks.Enqueue(task10);
			}
			
			//12. 잠시 대기
			{
				WaitSecTask task9 = new WaitSecTask(1.0f);
				_tutorialTasks.Enqueue(task9);
			}
			
			//13. 타겟에 폭탄 설치하기
			{
				DemolitionTask task10 = new DemolitionTask();
				_tutorialTasks.Enqueue(task10);
			}
			
			//14. 잠시 몇 초 기다림
			{
				WaitSecTask task11 = new WaitSecTask(1.0f);
				_tutorialTasks.Enqueue(task11);
			}

			//15. 폭탄을 해체하기
			{
				DefusingTask task12 = new DefusingTask();
				_tutorialTasks.Enqueue(task12);
			}
			
			//16. 폭탄을 해체하기
			{
				WaitSecTask task13 = new WaitSecTask(1.0f);
				_tutorialTasks.Enqueue(task13);
			}

			//17. 튜토리얼 종료
			{
				TutorialFinishTask task14 = new TutorialFinishTask();
				_tutorialTasks.Enqueue(task14);
			}
		}

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
		
		void Update()
		{
			if (
				Input.GetKeyDown(KeyCode.Escape) && 
				(BattleMainUi.Instance != null) &&
			    (BattleMainUi.Instance.PauseMenu == null) && 
			    !IsPopupExist)
			{
				//테스트 편의를 위해서 에디터에서는 Escape를 통한 나가기 팝업 띄우지 않음
#if !UNITY_EDITOR
				OnSkipBt();
#endif
			}
			
			if (CanLocalPlant && BombInteract)
			{
				if (bl_GameInput.Interact(GameInputType.Hold))
				{
					bl_MFPS.LocalPlayerReferences.firstPersonController.SetCrouch(true);
				}
			
				if (bl_GameInput.Interact())
				{
					PlantBomb();
				}
				else if (bl_GameInput.Interact(GameInputType.Up))//if for some reason him is not keep pressing the plant button
				{
					bl_MFPS.LocalPlayerReferences.firstPersonController.SetCrouch(false);
					CancelPlant();
				}
			}

			if (CanReloadInteract && bl_GameInput.Reload())
			{
				OnReloadButton?.Invoke();
			}

			if (CanLocalDefuse && BombInteract)
			{
				if (bl_GameInput.Interact(GameInputType.Hold))
				{
					bl_MFPS.LocalPlayerReferences.firstPersonController.SetCrouch(true);
				}
			
				if (bl_GameInput.Interact())
				{
					DefuseBomb();
				}
				else if (bl_GameInput.Interact(GameInputType.Up))//if for some reason him is not keep pressing the plant button
				{
					bl_MFPS.LocalPlayerReferences.firstPersonController.SetCrouch(false);
					CancelDefuse();
				}
			}
		}

		void OnReload()
		{
			if(!CanReloadInteract) return;
			
			// DebugEx.Log($"SJW 111 OnReload.....");
			
			GunManager gunManager = bl_MFPS.LocalPlayerReferences.gunManager;
			gunManager.CurrentGun.RemainingClipBullets = 20;
			gunManager.CurrentGun.SetInifinityAmmo(true);
			gunManager.CurrentGun.Reload();
				
			OnReloadButton?.Invoke();
		}

		void OnNextTutorialStep()
		{
			_currentTutorialTaskIndex++;
			
			DecideButtonsState();
			
			var gunManager = bl_MFPS.LocalPlayerReferences.gunManager;
			gunManager.EquipWeapons[0].CanFire = CanFire();
			gunManager.EquipWeapons[1].CanFire = CanFire();
			gunManager.EquipWeapons[2].CanFire = CanFire();

			MobileControlsUi.SetActiveGo(CanMove());
		}

		bool CanMove()
		{
			return _currentTutorialTaskIndex >= 7;
			// return false;		//test code
		}
		
		void OnSkipBt()
		{
			RoomMenu.Instance.TogglePause();
		}

		void PlantBomb()
		{
			StartCoroutine(nameof(DoPlant));
			
			DemolitionModeUi.Instance.UpdateProgress(0);
			DemolitionModeUi.Instance.ProgressUi.SetActive(true);
			
			bl_MFPS.LocalPlayerReferences.gunManager.BlockAllWeapons();
			bl_MFPS.LocalPlayerReferences.firstPersonController.isControlable = false;
		}

		void CancelPlant()
		{
			StopCoroutine(nameof(DoPlant));
			
			DemolitionModeUi.Instance.ProgressUi.SetActive(false);
			
			bl_MFPS.LocalPlayerReferences.gunManager.ReleaseWeapons(true);
			bl_MFPS.LocalPlayerReferences.firstPersonController.isControlable = true;
		}

		IEnumerator DoPlant()
		{
			float d = 0;
			while (d < 1)
			{
				d += Time.deltaTime / DemolitionMode.Instance.plantDuration;
				//d = normalized plant time (0 - 1)
				//plantTime = complete countdown time (0 - plantDuration)
				DemolitionModeUi.Instance.UpdateProgress(d);
				yield return null;
			}
			//plant complete
			DemolitionModeUi.Instance.ProgressUi.SetActive(false);
			DemolitionModeUi.Instance.ShowPlantGuide(false);
			bl_MFPS.LocalPlayerReferences.firstPersonController.isControlable = true;
			bl_MFPS.LocalPlayerReferences.gunManager.ReleaseWeapons(true);
			//send plantation event
			
			VirtualAudioController.PlayClip("bomb planted");
			VirtualAudioController.PlayClip("planted loop");

			Bomb.SetActiveGo(true);
			Bomb.OnPlant();
			OnPlantFinished?.Invoke();
		}

		void DefuseBomb()
		{
			StartCoroutine(nameof(DoDefuse));
			
			DemolitionModeUi.Instance.UpdateProgress(0);
			DemolitionModeUi.Instance.ProgressUi.SetActive(true);
			
			bl_MFPS.LocalPlayerReferences.gunManager.BlockAllWeapons();
			bl_MFPS.LocalPlayerReferences.firstPersonController.isControlable = false;
		}

		void CancelDefuse()
		{
			StopCoroutine(nameof(DoDefuse));
			
			DemolitionModeUi.Instance.ProgressUi.SetActive(false);
			
			bl_MFPS.LocalPlayerReferences.gunManager.ReleaseWeapons(true);
			bl_MFPS.LocalPlayerReferences.firstPersonController.isControlable = true;
		}

		IEnumerator DoDefuse()
		{
			float d = 0;
			while (d < 1)
			{
				d += Time.deltaTime / DemolitionMode.Instance.defuseDuration;
				//d = normalized plant time (0 - 1)
				//plantTime = complete countdown time (0 - plantDuration)
				DemolitionModeUi.Instance.UpdateProgress(1 - d);
				yield return null;
			}
			//defuse complete
			DemolitionModeUi.Instance.ProgressUi.SetActive(false);
			DemolitionModeUi.Instance.ShowPlantGuide(false);
			bl_MFPS.LocalPlayerReferences.firstPersonController.isControlable = true;
			bl_MFPS.LocalPlayerReferences.gunManager.ReleaseWeapons(true);
			//send defuse event
			
			Bomb.OnDefuse();
			OnDefuseFinished?.Invoke();
		}

		public void OnPlantButton(bool press)
		{
			if (press) PlantBomb();
			else CancelPlant();
		}

		public void OnDefuseButton(bool press)
		{
			if (press) DefuseBomb();
			else CancelDefuse();
		}

		public bool CanFire()
		{
			if (_currentTutorialTaskIndex == 0) return false;

			bool isAutoFire = DeviceConfig.Instance.AutoFire;
			return isAutoFire || this.FireBt.gameObject.activeInHierarchy;
		}

		public bool CanChangeWeapon(int slotIndex)
		{
			switch (slotIndex)
			{
				case 0:
					return (_currentButtonActiveFlag & (1 << 0)) != 0;
				case 1:
					return (_currentButtonActiveFlag & (1 << 1)) != 0;
				case 2:
					return (_currentButtonActiveFlag & (1 << 2)) != 0;
				case 3:
					return (_currentButtonActiveFlag & (1 << 6)) != 0;
				case 4:
					return (_currentButtonActiveFlag & (1 << 7)) != 0;
				
				default:
					return false;
			}
		}

		void DecideButtonsState()
		{
			DebugEx.Log("CurrentTutorialTaskIndex : " + _currentTutorialTaskIndex);
			
			// 점프 / 앉기 / 조준 / 전술무기 ~~ 폭탄 / 재장전 / 사격 / 로드아웃 ~~ 보조무기2 / 보조무기1 / 주무기
			switch (_currentTutorialTaskIndex)
			{
				case 1:
					MobileControlsUi.Canvas.gameObject.SetActiveGo(true);
					SetButtonsActive(0b00000000000);		
					break;
				case 2: case 3: case 4:
				case 5: case 6: case 7:
					SetButtonsActive(0b11000000000);
					break;
				
				//재장전 하기
				case 8:
					SetButtonsActive(0b11000010010);
					break;
				
				default:
					SetButtonsActive(0b11101111111);
					break;
			}
		}

		void OnDisable()
		{
			StopTutorial();
		}

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

		public void RestartTutorial()
		{
			StopTutorial();
			
			InitTutorial();
			BeginTutorial().Forget();
		}
		
		/// <summary>
		/// (0)주무기, (1)보조무기1, (2)보조무기2, (3)로드아웃, (4)발사, (5)재장전, (6)폭탄, (7)전술무기, (8)조준, (9)앉기, (10)점프 순으로 (11자리)
		/// bit 형태로 전달 [0 비활성화, 1 활성화]
		/// </summary>
		public void SetButtonsActive(int bitFlag)
		{
			Weapon0.gameObject.SetActiveGo((bitFlag & (1 << 0)) != 0); //0
			Weapon1.gameObject.SetActiveGo((bitFlag & (1 << 1)) != 0); //1
			Weapon2.gameObject.SetActiveGo((bitFlag & (1 << 2)) != 0); //2
			
			LoadOutBt.gameObject.SetActiveGo((bitFlag & (1 << 3)) != 0); //3
			
			// FireBt.gameObject.SetActiveGo((bitFlag & (1 << 4)) != 0); //4
			FireBt.SetActiveGo(!DeviceConfig.Instance.AutoFire);
			
			ReloadBt.gameObject.SetActiveGo((bitFlag & (1 << 5)) != 0);	//5
			GrenadeBt.gameObject.SetActiveGo((bitFlag & (1 << 6)) != 0); //6
			
			TacticalBt.gameObject.SetActiveGo((bitFlag & (1 << 7)) != 0); //7
			AimBt.gameObject.SetActiveGo((bitFlag & (1 << 8)) != 0); //8
			CrouchBt.gameObject.SetActiveGo((bitFlag & (1 << 9)) != 0); //9
			JumpBt.gameObject.SetActiveGo((bitFlag & (1 << 10)) != 0); //10

			_currentButtonActiveFlag = bitFlag;
		}

		public Sprite GetTutorialSprite(int imageIndex)
		{
			tutorialSpriteMap.TryGetValue(imageIndex, out Sprite sprite);
			return sprite;
		}

		public Transform GetTutorialWayPoint(int wayPointIndex)
		{
			tutorialWayPointMap.TryGetValue(wayPointIndex, out Transform tutorialWayPoint);
			return tutorialWayPoint;
		}

		public Transform GetTutorialTargetPoint(int targetPointIndex)
		{
			tutorialTargetPointMap.TryGetValue(targetPointIndex, out Transform tutorialTargetPoint);
			return tutorialTargetPoint;
		}

		public Transform GetTutorialBlockPoint(int blockPointIndex)
		{
			tutorialColliderBlockMap.TryGetValue(blockPointIndex, out Transform tutorialBlockPoint);
			return tutorialBlockPoint;
		}

		public Transform GetTutorialWallMesh(int wallMeshIndex)
		{
			tutorialWallMeshMap.TryGetValue(wallMeshIndex, out Transform tutorialWallMesh);
			return tutorialWallMesh;
		}
		
		public void ShowMessage(string message)
		{
			BattleMainUi.Instance.TutorialModeUi.ShowMessage(message);
		}

		public void HideMessage()
		{
			BattleMainUi.Instance.TutorialModeUi.HideMessage();
		}
	}

	public enum TutorialSubType
	{
		AutoFire = 0,
		NoneAutoFire = 1,
	}

	/// <summary>
	/// 일정 시간(초) 대기하는 단계
	/// </summary>
	public class WaitSecTask : TutorialTask
	{
		private float _waitTime;
		public WaitSecTask(float waitTime) : base()
		{
			_waitTime = waitTime;
		}

		public override async void Execute()
		{
			base.Execute();

			try
			{
				await UniTask.WaitForSeconds(_waitTime, cancellationToken: CancellationTokenSource.Token);
			}
			catch (OperationCanceledException) { }

			EndTask();
		}
	}

	/// <summary>
	/// 웨이포인트를 활성화 시키고 일정 시간 대기하는 단계
	/// </summary>
	public class WaitAndSeeWayPointTask : TutorialTask
	{
		private float _waitTime;
		Transform _currentTargetZone;
		
		public WaitAndSeeWayPointTask(Hashtable hashtable) : base()
		{
			_hashtable = hashtable;
			_waitTime = (float)_hashtable["WaitTime"];
		}

		public override async void Execute()
		{
			base.Execute();

			try
			{
				List<Transform> targetPosList = _hashtable["TargetPosList"] as List<Transform>;

				foreach (Transform targetPosTF in targetPosList)
				{
					GameObject targetObj = Object.Instantiate(TutorialManager.Instance.GroundDecalPrefab) as GameObject;
					targetObj.transform.position = targetPosTF.position;
					targetObj.SetActiveGo(true);
					
					foreach (Transform arrowPoint in targetPosTF)
					{
						if (arrowPoint.gameObject.activeSelf)
						{
							GameObject targetArrowObj = Object.Instantiate(
								TutorialManager.Instance.GroundArrowPrefab,
								targetObj.transform, true) as GameObject;

							targetArrowObj.SetActiveGo(true);
							targetArrowObj.transform.position = arrowPoint.position;
							targetArrowObj.transform.rotation = Quaternion.LookRotation(arrowPoint.forward);
						}
					}
					
					TutorialManager.Instance.BeginDecalWayPoint.Add(targetObj);
				}
				
				await UniTask.WaitForSeconds(_waitTime, cancellationToken: CancellationTokenSource.Token);
				
				EndTask();
			}
			catch
			{
				// ignored
			}
		}
	}

	/// <summary>
	/// 특정 장소(Collider) 까지 이동하는 단계
	/// </summary>
	public class MoveToTask : TutorialTask
	{
		Queue<GameObject> _targetQueue;
		Transform _currentTargetZone;
		
		int _currentTargetZoneIndex;
		int _totalTargetZoneCount;

		bool _isAllFinished;
		
		//1. TargetPos  ([Vector3]표시 위치)
		//2. VisibleMessageUI  ([bool]메시지 보여줄지 여부)
		//3. MsgBoxText  ([String] 메시지 내용)
		//4. 비활성화 시킬 UI
		//HashTable로 전달
		public MoveToTask(Hashtable hashtable) : base()
		{
			_hashtable = hashtable;
			_currentTargetZoneIndex = 0;
			_targetQueue = new Queue<GameObject>();
		}
		
		public override async void Execute()
		{
			base.Execute();
			
			List<Transform> targetPosList = _hashtable["TargetPosList"] as List<Transform>;

			if (targetPosList == null || targetPosList.Count == 0)
			{
				EndTask();
				return;
			}
			
			_totalTargetZoneCount = targetPosList.Count;

			foreach (Transform targetPosTF in targetPosList)
			{
				GameObject targetObj = Object.Instantiate(TutorialManager.Instance.GroundDecalPrefab) as GameObject;
				targetObj.transform.position = targetPosTF.position;

				foreach (Transform arrowPoint in targetPosTF)
				{
					if (arrowPoint.gameObject.activeSelf)
					{
						GameObject targetArrowObj = Object.Instantiate(
							TutorialManager.Instance.GroundArrowPrefab, 
							targetObj.transform, true) as GameObject;

						targetArrowObj.SetActiveGo(true);
						targetArrowObj.transform.position = arrowPoint.position;
						targetArrowObj.transform.rotation = Quaternion.LookRotation(arrowPoint.forward);
					}
				}
				
			
				TutorialWayPointZone zone = targetObj.GetComponent<TutorialWayPointZone>();
				zone.AddOnTriggerListener(OnEnterZone);
				
				_targetQueue.Enqueue(targetObj);
			}
			
			DecideTarget();

			try
			{
				await UniTask.WaitUntil(() => _isAllFinished, cancellationToken: CancellationTokenSource.Token);
				
				EndTask();
			}
			catch
			{
				// ignored
			}
		}

		protected override void EndTask()
		{
			base.EndTask();
			
			TutorialManager.Instance.HideMessage();
		}

		void OnEnterZone(Transform enteredZone)
		{
			if (_currentTargetZone == enteredZone)
			{
				if (_currentTargetZoneIndex == _totalTargetZoneCount - 1)
				{
					_isAllFinished = true;
					_currentTargetZone.DestroyGo();
				}
				else
				{
					_currentTargetZoneIndex++;
					_currentTargetZone.DestroyGo();
					DecideTarget();
				}
			}
		}

		void DecideTarget()
		{
			_currentTargetZone = _targetQueue.Dequeue().transform;
			_currentTargetZone.SetActiveGo(true);
			
			bool needMsgBox = (int)_hashtable["VisibleMessageUI"] == 1;
			if (needMsgBox)
			{
				int msgBoxDuration = _hashtable.ContainsKey("MsgBoxDuration") ? 
					(int)_hashtable["MsgBoxDuration"] : 0;
				string message = _hashtable.ContainsKey("MsgBoxText") ? 
					(string)_hashtable["MsgBoxText"] : string.Empty;

				message += $"  [{_currentTargetZoneIndex}/{_totalTargetZoneCount}]";
				ShowMessage(message, msgBoxDuration).Forget();
			}
		}
		
		async UniTaskVoid ShowMessage(string message, float duration)
		{
			try
			{
				TutorialManager.Instance.ShowMessage(message);
				
				if (duration > 0)
				{
					await UniTask.WaitForSeconds(
						duration, 
						cancellationToken: CancellationTokenSource.Token);
				
					TutorialManager.Instance.HideMessage();
				}
			}
			catch
			{
				// ignored
			}
		}
	}

	/// <summary>
	/// 폭탄을 설치하는 단계
	/// </summary>
	public class DemolitionTask : TutorialTask
	{
		List<GameObject> _arrowDecals;
		GameObject _groundDecal;
		
		public DemolitionTask() : base()
		{
			TutorialManager.Instance.Bomb.SetActiveGo(false);
		}

		public override void Execute()
		{
			base.Execute();
			
			string context = I18N.Translate("TutorialMode.PlantBomb.");
			
			TutorialManager tutorialManager = TutorialManager.Instance;
			tutorialManager.ShowMessage(context);
			tutorialManager.GetTutorialBlockPoint(0).SetActiveGo(false);
			tutorialManager.GetTutorialBlockPoint(1).SetActiveGo(false);
			
			tutorialManager.GetTutorialWallMesh(0).SetActiveGo(false);
			tutorialManager.GetTutorialWallMesh(1).SetActiveGo(false);
			
			tutorialManager.DemolitionAreaColliderTrigger.SetActiveGo(true);
			tutorialManager.DemolitionAreaColliderTrigger.AddOnTriggerEnterListener(OnDemolitionAreaTriggered);
			
			PlaceBomb();
			PlaceGroundDecal();
			PlaceGroundArrow();
			
			tutorialManager.OnPlantFinished += OnPlantFinished;
			tutorialManager.BombInteract = true;
			
			BattleMainUi.Instance.DemolitionUi.SetActiveGo(true);
		}

		List<GameObject> PlaceArrowDecals(Transform parent)
		{
			List<GameObject> decalList = new List<GameObject>();

			foreach (Transform child in parent)
			{
				if (child.gameObject.activeSelf)
				{
					GameObject targetArrowObj = Object.Instantiate(
						TutorialManager.Instance.GroundArrowPrefab, 
						child, true) as GameObject;

					targetArrowObj.SetActiveGo(true);
					targetArrowObj.transform.position = child.position;
					targetArrowObj.transform.rotation = Quaternion.LookRotation(child.forward);
					
					decalList.Add(targetArrowObj);
				}
			}
			
			return decalList;
		}
		
		void PlaceBomb()
		{
			TutorialManager tutorialManager = TutorialManager.Instance;
			Vector3 targetBombLocation = tutorialManager.DemolitionAreaColliderTrigger.transform.position;
			NavMeshHit hit;
			NavMesh.SamplePosition(targetBombLocation, out hit, 1.0f, NavMesh.AllAreas);
			
			tutorialManager.Bomb.transform.position = hit.position;
		}

		void PlaceGroundDecal()
		{
			Vector3 bombLocation = TutorialManager.Instance.Bomb.transform.position;
			
			_groundDecal = Object.Instantiate(TutorialManager.Instance.GroundDecalPrefab) as GameObject;
			_groundDecal.transform.position = bombLocation;
			_groundDecal.SetActiveGo(true);
			
			TutorialWayPointZone zone = _groundDecal.GetComponent<TutorialWayPointZone>();
			zone.AddOnTriggerListener(OnEnterZone);
		}

		void OnEnterZone(Transform enterZone)
		{
			enterZone.DestroyGo();
		}

		void PlaceGroundArrow()
		{
			_arrowDecals = new List<GameObject>();
			Transform decalParent = TutorialManager.Instance.GetTutorialWayPoint(5);
			_arrowDecals.AddRange(PlaceArrowDecals(decalParent));
		}

		void OnPlantFinished()
		{
			ShowPlantFinishMessageForSeconds().Forget();
		}

		async UniTaskVoid ShowPlantFinishMessageForSeconds()
		{
			await UniTask.WaitForSeconds(1.0f, cancellationToken: CancellationTokenSource.Token);
			
			string context = I18N.Translate("TutorialMode.PlantBombComplete.");
			TutorialManager.Instance.ShowMessage(context);
			await UniTask.WaitForSeconds(2.0f, cancellationToken: CancellationTokenSource.Token);
			TutorialManager.Instance.HideMessage();
			
			await UniTask.WaitForSeconds(1.0f, cancellationToken: CancellationTokenSource.Token);
			
			EndTask();
		}

		void OnDemolitionAreaTriggered(GameObject player, bool isEntered)
		{
			TutorialManager.Instance.CanLocalPlant = isEntered;
			BattleMainUi.Instance.DemolitionUi.ShowPlantGuide(isEntered);
		}

		protected override void EndTask()
		{
			TutorialManager tutorialManager = TutorialManager.Instance;
			
			tutorialManager.DemolitionAreaColliderTrigger.Reset();
			tutorialManager.DemolitionAreaColliderTrigger.SetActiveGo(false);
			
			tutorialManager.DemolitionMiniMapItem.HideItem();
			
			tutorialManager.CanLocalPlant = false;
			tutorialManager.BombInteract = false;
			tutorialManager.HideMessage();
			
			BattleMainUi.Instance.DemolitionUi.SetActiveGo(false);

			foreach (GameObject arrowDecal in _arrowDecals) { arrowDecal.DestroyGo(); }
			if(_groundDecal != null) { _groundDecal.DestroyGo(); }
			
			base.EndTask();
		}

		protected override void Cleanup()
		{
			TutorialManager tutorialManager = TutorialManager.Instance;
			
			tutorialManager.OnPlantFinished -= OnPlantFinished;
			tutorialManager.DemolitionAreaColliderTrigger.RemoveOnTriggerEnterListener(OnDemolitionAreaTriggered);
		}
	}

	/// <summary>
	/// 폭탄을 해체하는 단계
	/// </summary>
	public class DefusingTask : TutorialTask
	{
		List<GameObject> _arrowDecals;
		GameObject _groundDecal;
		
		public DefusingTask() : base() { }

		public override void Execute()
		{
			base.Execute();

			PlaceBomb();
			
			string context = I18N.Translate("TutorialMode.DefuseBomb.");
			
			TutorialManager tutorialManager = TutorialManager.Instance;
			tutorialManager.DefusingAreaColliderTrigger.SetActiveGo(true);
			tutorialManager.DefusingAreaColliderTrigger.AddOnTriggerEnterListener(OnDefusingArea);
			
			tutorialManager.BombInteract = true;
			tutorialManager.OnDefuseFinished += OnDefused;
			tutorialManager.ShowMessage(context);

			_arrowDecals = new List<GameObject>();
			_arrowDecals.AddRange(PlaceArrowDecals(TutorialManager.Instance.GetTutorialWayPoint(6)));
			
			PlaceGroundDecal();
			
			BattleMainUi.Instance.DemolitionUi.SetActiveGo(true);
		}

		List<GameObject> PlaceArrowDecals(Transform parent)
		{
			List<GameObject> decalList = new List<GameObject>();

			foreach (Transform child in parent)
			{
				if (child.gameObject.activeSelf)
				{
					GameObject targetArrowObj = Object.Instantiate(
						TutorialManager.Instance.GroundArrowPrefab, 
						child, true) as GameObject;

					targetArrowObj.SetActiveGo(true);
					targetArrowObj.transform.position = child.position;
					targetArrowObj.transform.rotation = Quaternion.LookRotation(child.forward);
					
					decalList.Add(targetArrowObj);
				}
			}
			
			return decalList;
		}

		void PlaceBomb()
		{
			TutorialManager tutorialManager = TutorialManager.Instance;
			Vector3 targetBombLocation = tutorialManager.DefusingAreaColliderTrigger.transform.position;
			NavMeshHit hit;
			NavMesh.SamplePosition(targetBombLocation, out hit, 1.0f, NavMesh.AllAreas);
			
			tutorialManager.Bomb.transform.position = hit.position;
		}

		void PlaceGroundDecal()
		{
			Vector3 bombLocation = TutorialManager.Instance.Bomb.transform.position;
			_groundDecal = Object.Instantiate(TutorialManager.Instance.GroundDecalPrefab) as GameObject;
			_groundDecal.transform.position = bombLocation;
			_groundDecal.SetActiveGo(true);
			
			TutorialWayPointZone zone = _groundDecal.GetComponent<TutorialWayPointZone>();
			zone.AddOnTriggerListener(OnEnterZone);
		}

		void OnEnterZone(Transform enterZone)
		{
			enterZone.DestroyGo();
		}

		void OnDefused()
		{
			EndTask();
		}

		void OnDefusingArea(GameObject detectedTarget, bool isEntered)
		{
			TutorialManager tutorialManager = TutorialManager.Instance;
			tutorialManager.CanLocalDefuse = isEntered;
			
			DemolitionModeUi.Instance.ShowDefuseGuide(isEntered);
		}

		protected override void EndTask()
		{
			TutorialManager tutorialManager = TutorialManager.Instance;
			
			tutorialManager.DefusingAreaColliderTrigger.Reset();
			tutorialManager.DefusingAreaColliderTrigger.SetActiveGo(false);
			tutorialManager.DefusingMiniMapItem.HideItem();
			
			tutorialManager.CanLocalDefuse = false;
			tutorialManager.BombInteract = false;
			tutorialManager.HideMessage();

			foreach (GameObject arrowDecal in _arrowDecals) { arrowDecal.DestroyGo(); }
			if(_groundDecal != null) { _groundDecal.DestroyGo(); }
			
			BattleMainUi.Instance.DemolitionUi.SetActiveGo(false);
			BattleMainUi.Instance.DemolitionUi.ShowDefuseGuide(false);
			
			base.EndTask();
		}

		protected override void Cleanup()
		{
			TutorialManager tutorialManager = TutorialManager.Instance;
			
			tutorialManager.DefusingAreaColliderTrigger.RemoveOnTriggerEnterListener(OnDefusingArea);
			tutorialManager.OnDefuseFinished -= OnDefused;
		}
	}
	
	/// <summary>
	/// 수류탄을 이용해 타겟을 제거하는 단계
	/// </summary>
	public class TrainingGrenadeTask : TutorialTask
	{
		bool _isGrenadeEntered;

		GameObject _grenadeBt;
		GameObject _grenadeArea;
		
		public TrainingGrenadeTask() : base() { }

		public override async void Execute()
		{
			base.Execute();

			string context = I18N.Translate("TutorialMode.ThrowGrenade.");
			TutorialManager.Instance.ShowMessage(context);

			_grenadeBt = TutorialManager.Instance.GrenadeBt;
			BattleMainUi.Instance.ButtonLayoutMgr.ToggleHighlight(_grenadeBt.transform, true);

			_grenadeArea = TutorialManager.Instance.GrenadeAreaDecal;
			_grenadeArea.SetActiveGo(true);

			TutorialGrenadeArea trigger = _grenadeArea.GetComponent<TutorialGrenadeArea>();
			trigger.AddBombTriggeredActionListener(OnBombEntered);
			
			var gunManager = bl_MFPS.LocalPlayerReferences.gunManager;
			gunManager.EquipWeapons[3].SetInifinityAmmo(true);

			UpdateGrenade();

			try
			{
				await UniTask.WaitUntil(() => _isGrenadeEntered, cancellationToken: CancellationTokenSource.Token);
				await UniTask.WaitForSeconds(3.0f, cancellationToken: CancellationTokenSource.Token);
				
				BattleMainUi.Instance.ButtonLayoutMgr.ToggleHighlight(_grenadeBt.transform, false);
				TutorialManager.Instance.HideMessage();
				EndTask();
			}
			catch
			{
				// ignored
			}
		}

		void OnBombEntered()
		{
			_isGrenadeEntered = true;
		}

		protected override void EndTask()
		{
			TutorialManager.Instance.GrenadeAreaDecal.SetActiveGo(false);
			TutorialManager.Instance.HideMessage();
			
			base.EndTask();
		}

		protected override void Cleanup()
		{
			TutorialGrenadeArea trigger = _grenadeArea.GetComponent<TutorialGrenadeArea>();
			trigger.RemoveBombTriggeredActionListener(OnBombEntered);
			
			var gunManager = bl_MFPS.LocalPlayerReferences.gunManager;
			gunManager.EquipWeapons[3].SetInifinityAmmo(false);
		}

		void UpdateGrenade()
		{
			var gunManager = bl_MFPS.LocalPlayerReferences.gunManager;
			gunManager.EquipWeapons[3].CanFire = true;
			gunManager.EquipWeapons[3].bulletsLeft = 1;
			gunManager.EquipWeapons[3].SetInifinityAmmo(true);
			
			gunManager.EquipWeapons[4].CanFire = true;
			gunManager.EquipWeapons[4].bulletsLeft = 1;
			gunManager.EquipWeapons[4].SetInifinityAmmo(true);

			//Note. 초기에 GrenadeBt 을 비활성화 하기 때문에 LocalSpawn 이벤트를 못받아 갱신을 따로 해준다.
			var consumableWeaponCtl = MobileControlsUi.Instance.GrenadeBt.GetComponent<ConsumableWeaponButtonCtl>();
			consumableWeaponCtl.GunManager = gunManager;
			consumableWeaponCtl.UpdateAmmoUIs();
		}
	}

	/// <summary>
	/// 표적 제거하는 단계
	/// </summary>
	public class TrainingMarkRemoveTask : TutorialTask
	{
		List<Vector3> _targetPos;
		List<Quaternion> _targetRot;
		List<TrainingMark> _targetList;

		int _currentHitCount;	//현재 맞춘 과녁 수
		string _context;

		bool _isInfinityAmmo;
		
		// 1. TargetPosList ([Vector3]제거 대상 타겟 위치)
		// 2. TargetRotList ([Quaternion] 제거 대상 타겟 회전값)
		// 2. Context ([String] 설명 문구)
		public TrainingMarkRemoveTask(Hashtable hashtable) : base()
		{
			_hashtable = hashtable;
			
			_isInfinityAmmo = (int)_hashtable["IsInfinityAmmo"] == 1;
			_targetPos = _hashtable["TargetPosList"] as List<Vector3>;
			_targetRot = _hashtable["TargetRotList"] as List<Quaternion>;
			
			_context = _hashtable["Context"] as string;
		}

		void InitTrainingMark(int markID, Vector3 targetPos, Quaternion targetRot, GameObject trainingMarkObj)
		{
			trainingMarkObj.SetActiveGo(true);
			trainingMarkObj.transform.position = targetPos;
			trainingMarkObj.transform.rotation = targetRot;
			
			TrainingMark trainingMark = trainingMarkObj.GetComponent<TrainingMark>();

			TrainingMark.TradingMarkParams tradingMarkParams = new() { OnHit = OnHitMark };
			trainingMark.InitContent(tradingMarkParams);
			_targetList.Add(trainingMark);
		}

		void OnHitMark(bool isSuccess)
		{
			string newContext = $"{_context} : [{++_currentHitCount}/{_targetList.Count}]";
			TutorialManager.Instance.ShowMessage(newContext);
		}
		
		public override async void Execute()
		{
			base.Execute();
			
			try
			{
				var gunManager = bl_MFPS.LocalPlayerReferences.gunManager;
				
				gunManager.CurrentGun.ResetAmmo();
				gunManager.CurrentGun.SetInifinityAmmo(true);
				
				_targetList = new List<TrainingMark>();

				int index = 0;
				foreach (Vector3 targetPos in _targetPos)
				{
					GameObject trainingMarkObj = Object
						.Instantiate(TutorialManager.Instance.TrainingMarkPrefab) as GameObject;

					InitTrainingMark(index, targetPos, _targetRot[index], trainingMarkObj);
					
					index++;
				}
				
				TutorialManager.Instance.ShowMessage(_context);
			
				await UniTask.WaitUntil(() => 
						_targetList.TrueForAll(target => target.IsFinished), 
					cancellationToken: CancellationTokenSource.Token);
				
				//과녁이 쓰러지는걸 보여주고 끝마침.
				await UniTask.WaitForSeconds(1.0f, cancellationToken: CancellationTokenSource.Token);
			
				TutorialManager.Instance.HideMessage();

				foreach (TrainingMark trainingMark in _targetList)
				{
					trainingMark.gameObject.DestroyGo();
				}
				
				EndTask();
			}
			catch
			{
				// ignored
			}
		}
	}

	/// <summary>
	/// 재장전 하고 표적 제거하는 단계
	/// </summary>
	public class ReloadAndTrainingMarkRemoveTask : TutorialTask
	{
		List<Transform> _targetPos;
		List<Quaternion> _targetRot;
		List<TrainingMark> _targetList;

		int _targetWeaponSlotIndex;		//무기 슬롯 지정

		int _currentHitCount;	//현재 맞춘 과녁 수
		string _context;

		int _remainClipBullets = 0;
		
		GameObject _reloadButton;
		GameObject _fireButton;
		
		// 1. TargetPosList ([Vector3]제거 대상 타겟 위치)
		// 2. TargetRotList ([Quaternion] 제거 대상 타겟 회전값)
		// 2. Context ([String] 설명 문구)
		public ReloadAndTrainingMarkRemoveTask(Hashtable hashtable) : base()
		{
			_hashtable = hashtable;

			_reloadButton = TutorialManager.Instance.ReloadBt;
			_fireButton = TutorialManager.Instance.FireBt;

			_targetWeaponSlotIndex = (int)_hashtable["WeaponSlotIndex"];
			_targetPos = _hashtable["TargetPosList"] as List<Transform>;
			_targetRot = _hashtable["TargetRotList"] as List<Quaternion>;
			
			_context = _hashtable["Context"] as string;
		}

		public override async void Execute()
		{
			base.Execute();
			
			try
			{
				_targetList = new List<TrainingMark>();

				TutorialManager.Instance.CanReloadInteract = true;
				
				bl_EventHandler.onOutOfAmmoEvent += OnOutOfAmmo;
				TutorialManager.Instance.OnReloadButton = OnReloadButton;
				
				var gunManager = bl_MFPS.LocalPlayerReferences.gunManager;
				gunManager.ChangeCurrentWeaponTo(_targetWeaponSlotIndex);
				
				//2발만 주어진다.
				_remainClipBullets = gunManager.CurrentGun.RemainingClipBullets;
				gunManager.CurrentGun.UpdateBulletLeft(2);
				
				BattleMainUi.Instance.ButtonLayoutMgr
					.ToggleHighlight(_fireButton.transform, false);

				int index = 0;
				foreach (Transform targetPos in _targetPos)
				{
					GameObject trainingMarkObj = Object
						.Instantiate(TutorialManager.Instance.TrainingMarkPrefab) as GameObject;

					InitTrainingMark(index, targetPos.position, _targetRot[index], trainingMarkObj);
					
					index++;
				}
				
				TutorialManager.Instance.ShowMessage(_context);
			
				await UniTask.WaitUntil(() => 
						_targetList.TrueForAll(target => target.IsFinished), 
					cancellationToken: CancellationTokenSource.Token);
				
				//과녁이 쓰러지는걸 보여주고 끝마침.
				await UniTask.WaitForSeconds(1.0f, cancellationToken: CancellationTokenSource.Token);
			
				TutorialManager.Instance.HideMessage();
				
				foreach (TrainingMark trainingMark in _targetList)
				{
					trainingMark.gameObject.DestroyGo();
				}
				
				BattleMainUi.Instance.ButtonLayoutMgr
					.ToggleHighlight(_fireButton.transform, false);
				BattleMainUi.Instance.ButtonLayoutMgr
					.ToggleHighlight(_reloadButton.transform, false);
				
				TutorialManager.Instance.CanReloadInteract = false;
				TutorialManager.Instance.OnReloadButton = null;
				
				EndTask();
			}
			catch (Exception)
			{
				// ignored
			}
		}

		protected override void Cleanup()
		{
			bl_EventHandler.onOutOfAmmoEvent -= OnOutOfAmmo;
			TutorialManager.Instance.OnReloadButton = null;
		}

		void OnReloadButton()
		{
			// DebugEx.Log($"SJW 111 On Reload Callback Invoked.....");
			
			var gunManager = bl_MFPS.LocalPlayerReferences.gunManager;
			gunManager.EquipWeapons[1].RemainingClipBullets = _remainClipBullets;
			gunManager.EquipWeapons[1].SetInifinityAmmo(true);
			gunManager.EquipWeapons[1].Reload();
				
			string newContext = $"{_context} : [{_currentHitCount}/{_targetList.Count}]";
			TutorialManager.Instance.ShowMessage(newContext);
		}
		
		void OnOutOfAmmo()
		{
			string context = I18N.Translate("TutorialMode.Reload.");
			ShowMessageAfterSec(context, 1.0f).Forget();
			
			TutorialManager.Instance.SetButtonsActive(0b11000110010);
			BattleMainUi.Instance.ButtonLayoutMgr.ToggleHighlight(_reloadButton.transform, true);
		}

		async UniTaskVoid ShowMessageAfterSec(string message, float duration)
		{
			try
			{
				await UniTask.WaitForSeconds(
					duration, 
					cancellationToken: CancellationTokenSource.Token);
			
				TutorialManager.Instance.ShowMessage(message);
			}
			catch
			{
				// ignored
			}
		}

		void InitTrainingMark(int markID, Vector3 targetPos, Quaternion targetRot, GameObject trainingMarkObj)
		{
			trainingMarkObj.SetActiveGo(true);
			trainingMarkObj.transform.position = targetPos;
			trainingMarkObj.transform.rotation = targetRot;
			
			TrainingMark trainingMark = trainingMarkObj.GetComponent<TrainingMark>();

			TrainingMark.TradingMarkParams tradingMarkParams = new() { OnHit = OnHitMark };
			trainingMark.InitContent(tradingMarkParams);
			_targetList.Add(trainingMark);
		}

		void OnHitMark(bool isSuccess)
		{
			string newContext = $"{_context} : [{++_currentHitCount}/{_targetList.Count}]";
			TutorialManager.Instance.ShowMessage(newContext);
		}
	}

	/// <summary>
	/// 총기 변경을 하는 단계
	/// </summary>
	public class ChangeWeaponTask : TutorialTask
	{
		GunManager _gunManager;
		int _toWeaponSlotIndex;
		int _fromWeaponSlotIndex;

		GameObject _toWeaponSlot;
		
		/// <param name="fromWeaponSlotIndex">현재 슬롯</param>
		/// <param name="toWeaponSlotIndex">타겟 슬롯</param>
		public ChangeWeaponTask(int fromWeaponSlotIndex, int toWeaponSlotIndex) : base()
		{
			_toWeaponSlotIndex = toWeaponSlotIndex;
			_fromWeaponSlotIndex = fromWeaponSlotIndex;
		}

		void OnWeaponChanged(WeaponCode weaponCode)
		{
			try
			{
				if (_gunManager.currentWeaponIndex == _toWeaponSlotIndex)
				{
					BattleMainUi.Instance.ButtonLayoutMgr.ToggleHighlight(_toWeaponSlot.transform, false);
					TutorialManager.Instance.HideMessage();
					EndTask();
				}
			}
			catch
			{
				// .ignored
			}
		}

		protected override void Cleanup()
		{
			bl_EventHandler.onLocalChangeWeapon -= OnWeaponChanged;
		}

		public override void Execute()
		{
			base.Execute();
			
			int bitFrom = 1 << _fromWeaponSlotIndex;
			int bitTo = 1 << _toWeaponSlotIndex;
			
			int combined = bitFrom | bitTo;
			int otherButtons = 0b00000000000;
			
			int finalResult = otherButtons | combined;
			TutorialManager.Instance.SetButtonsActive(finalResult);

			_toWeaponSlot = TutorialManager.Instance.WeaponSwitcherSlotManager
				.GetSlotButton(_toWeaponSlotIndex)
				.gameObject;
			
			BattleMainUi.Instance.ButtonLayoutMgr.ToggleHighlight(_toWeaponSlot.transform, true);
			
			bl_EventHandler.onLocalChangeWeapon += OnWeaponChanged;
			
			_gunManager = bl_MFPS.LocalPlayerReferences.gunManager;

			string context = I18N.Translate("TutorialMode.ChangeWeapon.");
			TutorialManager.Instance.ShowMessage(context);
		}
	}

	public class CleanBeginDecalsTask : TutorialTask
	{
		public CleanBeginDecalsTask() : base() { }

		public override void Execute()
		{
			base.Execute();

			foreach (GameObject beginDecalWayPoint in TutorialManager.Instance.BeginDecalWayPoint)
			{
				beginDecalWayPoint.DestroyGo();
			}
			
			EndTask();
		}
	}

	/// <summary>
	/// 설명 보기 단계
	/// </summary>
	public class ShowDescriptionTask : TutorialTask
	{
		private Sprite _mainSprite;	//메인 설명 이미지
		
		private string _header;		//제목
		private string _context;	//본문

		private bool _autoSkip;		//자동 닫힘 옵션 제공 여부
		private float _duration;	//자동 닫힘인 경우, 대기 시간
		
		// 1. Image ([Sprite]보여줄 설명 이미지)
		// 2. Header ([String]상단 헤더 내용)
		// 3. Context ([String]본문 내용)
		// 4. AutoSkip ([0또는 1]자동 넘김 여부)
		// 5. Duration ([float] 자동 비활성화 시간(초))
		public ShowDescriptionTask(Hashtable hashtable) : base()
		{
			_hashtable = hashtable;
			
			_mainSprite = _hashtable["Image"] as Sprite;
			_header = _hashtable["Header"] as string;
			_context = _hashtable["Context"] as string;
			_autoSkip = (int)_hashtable["AutoSkip"] == 1;
			_duration = _autoSkip ? (float)_hashtable["Duration"] : 0;
		}

		public override async void Execute()
		{
			base.Execute();
			
			bl_UtilityHelper.LockCursor(false, LockCursorMask.TutorialPopup);
			
			TutorialHelpInfoUiParam uiParam = new TutorialHelpInfoUiParam()
			{
				MainImage = _mainSprite,
				Header = _header, 
				Context = _context, 
				AutoSkip = _autoSkip, 
				Duration = _duration
			};
			
			TutorialManager.Instance.IsPopupExist = true;
			
			TutorialHelpInfoUi popUp = Navigator.OpenUi<TutorialHelpInfoUi>(uiParam).Ui;
			await UniTask.WaitUntil(() => popUp.IsClosed, cancellationToken: CancellationTokenSource.Token);
			
			TutorialManager.Instance.IsPopupExist = false;
			bl_UtilityHelper.LockCursor(true, LockCursorMask.TutorialPopup);
			
			EndTask();
		}
	}

	/// <summary>
	/// 조작법 선택 단계
	/// </summary>
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

	/// <summary>
	/// 튜토리얼 완료 단계
	/// </summary>
	public class TutorialFinishTask : TutorialTask
	{
		bool _popupFinished;
		
		public TutorialFinishTask() : base() { }

		public override void Execute()
		{
			base.Execute();
			
			string context = I18N.Translate("TutorialMode.AllFinish.");
			DoTask(context).Forget();
		}

		async UniTaskVoid DoTask(string message)
		{
			try
			{
				TutorialManager.Instance.ShowMessage(message);
			
				await UniTask.WaitForSeconds(
					1.0f, 
					cancellationToken: CancellationTokenSource.Token);
			
				TutorialManager.Instance.HideMessage();
				
				bl_UtilityHelper.LockCursor(false, LockCursorMask.TutorialPopup);
			
				await UniTask.WaitForSeconds(
					0.5f, 
					cancellationToken: CancellationTokenSource.Token);
				
				//재시작이 가능하게 하려면 이 주석을 해제하여 사용
				/*
				var result = await Navigator.ConfirmA(
					"TutorialMode.Result.Content.",
					null,
					"Exit",
					"TutorialMode.Result.Button.Retry");
				
				bl_UtilityHelper.LockCursor(true, LockCursorMask.TutorialPopup);
				
				if (result == UiResult.Primary)
				{
					BattleManager.Instance.SpawnLocalPlayer(Team.Team2);
                
					await UniTask.WaitForSeconds(
						0.5f, 
						cancellationToken: CancellationTokenSource.Token);

					TutorialManager.Instance.RestartTutorial();
				}
				else
				{
					RoomMenu.Instance.LeaveRoom().Forget();
					EndTask();
				}
				*/

				//재시작 가능하게 한다면 여기 주석 처리
				//주석 구간 시작
				await Navigator.NoticeA(
					"TutorialMode.Result.Content.",
					"TutorialMode.AllFinish");
				
				bl_UtilityHelper.LockCursor(true, LockCursorMask.TutorialPopup);
				
				// RoomMenu.Instance.LeaveRoom().Forget();
				EndTask();
				//주석 구간 종료
			}
			catch
			{
				// ignored
			}
		}

		protected override void Cleanup()
		{
			RoomMenu.Instance.LeaveRoom().Forget();
		}
	}

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
}