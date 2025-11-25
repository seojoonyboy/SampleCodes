using Cysharp.Threading.Tasks;
using Photon.Realtime;
using UnityEngine;
using Game.View.UI;
using Framework;
using Framework.UI;
using Game.Data;
using Game.View.TrackingSystem;

namespace Game.View.BattleSystem
{
	/// <summary>
	/// 사격 연습장 모드.
	/// </summary>
	public class PracticeMode : bl_PhotonHelper, IBattleMode
	{
		BattleResultType _localPlayerBattleResultType = BattleResultType.None;

		[SerializeField] GameObject _content;
		[SerializeField] bl_PracticeMovableArea _movableArea;

		[SerializeField] TrainingMarkManager _trainingMarkManager;
		[SerializeField] PracticeModeTimer _practiceModeTimer;

		int _remainBullet;
		
		bool _canMove = true;
		bool _isEnd = false;
		bool _isCountdown = false;
		bool _outOfAmmo = false;
		
		State _currentState = State.NONE;

		GunManager _gunManager;
		BattleMainUi _battleMainUi;
		PracticeModeUi _practiceModeUi;

		WeaponCode _targetWeaponCode;
		
		public State CurrentState => _currentState;
		public bool IsEnd => _isEnd;
		public bool IsCountdown => _isCountdown;
		
		void Awake()
		{
			Initialize();
		}

		private void OnDisable()
		{
			bl_EventHandler.onPracticeLevelChanged -= OnPracticeLevelChanged;
			bl_EventHandler.onLocalPlayerFire -= OnLocalPlayerFire;
			bl_EventHandler.onOutOfAmmoEvent -= OnOutOfAmmo;
			bl_EventHandler.OnLocalLoadoutChanged -= OnLoadoutChanged;
			bl_EventHandler.onLocalPlayerSpawn -= OnLocalSpawn;

			if (GetGameMode == BattleMode.Practice)
			{
				bl_UtilityHelper.LockCursor(false, LockCursorMask.PracticePopup);
			}
		}

		public void Initialize()
		{
			bool meetMode = BattleManager.Instance.IsGameMode(BattleMode.Practice, this);
			enabled = meetMode;
			_trainingMarkManager.SetActiveGo(enabled);
			_content.SetActive(enabled);

			if (!enabled) return;

			_battleMainUi = BattleMainUi.Instance;
			_practiceModeUi = _battleMainUi.PracticeModeUi;
			
			_outOfAmmo = false;
			
			PracticeModeRepository.Instance.UpdateAvailableLevelDefs();
			
			_battleMainUi.PracticeModeUi.SetActiveGo(true);

			_movableArea.OnTriggerAction += OnTriggerMovableCollider;	//유저 collider 내 움직임 제어 관련 callback

			bl_EventHandler.onLocalPlayerFire += OnLocalPlayerFire;	//Fire 이벤트
			bl_EventHandler.onPracticeLevelChanged += OnPracticeLevelChanged;	//훈련장 레벨 변동 이벤트
			bl_EventHandler.onOutOfAmmoEvent += OnOutOfAmmo;	//총알 소진 이벤트
			bl_EventHandler.OnLocalLoadoutChanged += OnLoadoutChanged;		//로드아웃 변경 이벤트
			
			bl_EventHandler.onLocalPlayerSpawn += OnLocalSpawn;
			
			SetWeaponSlots();
		}

		void Start()
		{
			SetModeState(State.FREE_MODE);
		}

		void SetWeaponSlots()
		{
			//특정 무기 연습 모드인 경우만 보조 무기 교체를 막는다.
			if (BattleManager.Instance.GetPracticeModeEnteringType() == PracticeModeEnteringType.WithWeapon)
			{
				var slotManager = WeaponSwitcherSlotManager.Instance;
				slotManager.ToggleEnable(1, false);
				slotManager.ToggleEnable(2, false);
			}
		}
		
		void OnLocalPlayerFire(WeaponCode weaponCode)
		{
			if(CurrentState != State.TRAINING_MODE) return;
			if(_targetWeaponCode != weaponCode) return;
			
			_battleMainUi.PracticeModeUi.UpdateBulletLeftUI(--_remainBullet);
		}

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
		
		void OnLocalSpawn()
		{
			bl_MFPS.LocalPlayerReferences.playerHealthManager.CanDamageByLocal = false;
		}
		
		void OnLoadoutChanged()
		{
			if(BattleManager.Instance.LocalActor == null) return;
			
			SetModeState(State.FREE_MODE);
		}

		//[디버깅용] 훈련 현재 레벨 즉시 클리어 처리
		public void DebugSetEndState(EndCause endCause)
		{
			SetEndState(endCause);
		}

		public void OnLeaveBt()
		{
			if (this.CurrentState == State.TRAINING_MODE)
			{
				SetModeState(State.FREE_MODE);
			}
			else
			{
				bl_UtilityHelper.LockCursor(false, LockCursorMask.PracticeLeaveConfirmBox);

				Navigator.Confirm("AreYouQuitGame?", "GameQuit", onClose: (result) =>
				{
					bl_UtilityHelper.LockCursor(true, LockCursorMask.PracticeLeaveConfirmBox);

					if (result == UiResult.Primary)
					{
						// Battle씬 단독 실행한 경우인지
						bool isBattleSceneStandAlone = SceneManager.IsFirstScene();
						if (isBattleSceneStandAlone)
						{
							FrameworkApp.QuitApplication();
							return;
						}

						//Close();
						RoomMenu.Instance.SetPause(false);

						RoomMenu.Instance.LeaveRoom().Forget();
					}
				});
			}
		}

		public void OnResumeBt()
		{
			ResumeTimer();
		}

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

		void SetEndState(EndCause endCause)
		{
			if(_isEnd) return;

			BattleMainUi.Instance.PracticeModeUi.GetCurrentMissionCounts(
				out int successCount, 
				out int failedCount, 
				out int currentTotal, 
				out int missionTotal);
			
			float remainTime = _practiceModeTimer.CurrentTimeLeft;
			
			_practiceModeTimer.StopTimer();
			
			_isEnd = true;
			
			//마지막 표적이 넘어지는 것을 보여주기 위해 결과창을 바로 띄우지 않고 대기한다.
			this.InvokeAfter(1.0f, () =>
			{
				_battleMainUi
					.OpenPracticeLevelResultUi(
						endCause, 
						currentTotal, 
						remainTime, 
						PracticeModeRepository.Instance.SelectedPracticeModeDef,
						OnClosePracticeLevelResultUi
					);
			});
			
			bl_UtilityHelper.LockCursor(false, LockCursorMask.PracticePopup);
		}

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
					PracticeModeDef nextLevelDef = PracticeModeRepository.Instance.GetNextPracticeModeDef();
					if (nextLevelDef != null)
					{
						PracticeLevelInfoUiParam levelInfoUIParam = MakePracticeLevelInfoUiParam(nextLevelDef);
						levelInfoUIParam.onClose = (isStart) =>
						{
							if (isStart)
							{
								OnStartLevel(nextLevelDef);
							}
							else
							{
								SetModeState(State.FREE_MODE);
							}
						};
						
						Navigator.OpenUi<PracticeLevelInfoUi>(levelInfoUIParam);
					}
					else{
						Navigator.Notice("PracticeMode.LastLevel.");
						SetModeState(State.FREE_MODE);
					}
					
					break;
				
				default:
					SetModeState(State.FREE_MODE);
					break;
			}
		}

		public PracticeLevelInfoUiParam MakePracticeLevelInfoUiParam(PracticeModeDef practiceModeDef)
		{
			PracticeLevelInfoUiParam param = new PracticeLevelInfoUiParam()
			{
				Header = $"LV.{practiceModeDef.Level}",
				MarkCount = practiceModeDef.ActiveMarks.Count,
				TimeLimit = practiceModeDef.TimeLimit,
				BulletLimit = practiceModeDef.LimitBulletNum
			};
			return param;
		}

		public bool RequestSavePracticeClearedRecord(PracticeModeDef practiceModeDef)
		{
			MissionManager.Instance.RequestSavePracticeClearedRecord(practiceModeDef.No);
			return true;
		}

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
		
		void OnOutOfAmmo()
		{
			_outOfAmmo = true;
			
			if (_currentState == State.TRAINING_MODE)
			{
				DoTask();
			}

			async void DoTask()
			{
				var cancellationToken = this.GetCancellationTokenOnDestroy();
				
				await UniTask.WaitForSeconds(1.0f, cancellationToken: cancellationToken);
				_battleMainUi.PracticeModeUi.OnOutOfAmmo();
			}
		}

		void OnFreeMode()
		{
			_practiceModeUi.ToggleRoundInfoUI(false);			//라운드 정보 UI 비활성화
			_practiceModeUi.ToggleBulletInfoUI(false);
			
			_practiceModeUi.ToggleTrainingStartUi(true);	//훈련 시작 버튼 활성화

			bool isWithWeapon = BattleManager.Instance.GetPracticeModeEnteringType() == PracticeModeEnteringType.WithWeapon;
			
			var mobileUis = MobileControlsUi.Instance;
			mobileUis.TacticalBt.SetActive(!isWithWeapon);
			mobileUis.GrenadeBt.SetActive(!isWithWeapon);
			
			TrainingMarkManager.Instance.SetFreeShootingMode();		//과녁 자유 사격 모드 세팅

			_battleMainUi.ShowLoadoutButton(
				OfflineRoom.Instance.RoomSettings.EnteringType == PracticeModeEnteringType.Mode, 
				LoadoutButtonShowBits.ByWaitingTime);
			
			DoTask().Forget();

			async UniTaskVoid DoTask()
			{
				var token = this.GetCancellationTokenOnDestroy();
				await UniTask.WaitUntil(() => BattleManager.Instance.LocalActor != null, cancellationToken: token);
				
				_gunManager = bl_MFPS.LocalPlayerReferences.gunManager;
				_targetWeaponCode = _gunManager.EquipWeapons[0].WeaponCode;
				
				_gunManager.EquipWeapons[0].ResetAmmo();
				_gunManager.EquipWeapons[0].SetInifinityAmmo(true);
				_gunManager.EquipWeapons[1].ResetAmmo();
				_gunManager.EquipWeapons[1].SetInifinityAmmo(true);
			}
		}

		void OnTrainingMode()
		{
			_isEnd = false;
			
			var mobileUis = MobileControlsUi.Instance;
			mobileUis.TacticalBt.SetActive(false);
			mobileUis.GrenadeBt.SetActive(false);
			
			var selectedPracticeModeDef = PracticeModeRepository.Instance.SelectedPracticeModeDef;
			
			TrainingMarkManager.Instance.SetShootingMode(selectedPracticeModeDef);
			
			_practiceModeUi.InitRoundInfoUI(
				selectedPracticeModeDef.ActiveMarks.Count, 
				OnFinishMission
			);
			
			_practiceModeTimer.BeginTimer(selectedPracticeModeDef.TimeLimit, OnUpdatedTimer, OnEndTimer);
		}
		
		void OnTriggerMovableCollider(bool isOn)
		{
			_canMove = isOn;
		}

		public void PauseTimer()
		{
			_practiceModeTimer.PauseTimer();
		}

		public void ResumeTimer()
		{
			_practiceModeTimer.ResumeTimer();
		}

		public bool CanMove(float horizontalInput, float verticalInput, Transform playerTF)
		{
			Vector3 playerForward = playerTF.forward;

			Vector3 backColliderPoint =
				new Vector3(playerTF.position.x, playerTF.position.y, _movableArea.GetCenterPosition().z);
			
			// DebugEx.Log($"backColliderPoint : {backColliderPoint.ToString()}");
			
			Vector3 colliderToPlayerTF = (playerTF.position - backColliderPoint).normalized;
			
			float dotRes = Vector3.Dot(playerForward, colliderToPlayerTF);
			
			// DebugEx.Log($"dotRes = {dotRes}");
			
			//collider쪽을 향하고 있음
			if (dotRes < 0 && verticalInput > 0) return true;
			
			if (!_canMove && verticalInput < 0) return true;
			return _canMove;
		}

		public RoundResultSummary MakeRoundResultSummary()
		{
			bool isWin = _localPlayerBattleResultType == BattleResultType.Win;

			string title = I18N.Translate($"BattleResultType.{(isWin ? BattleResultType.Win : BattleResultType.Lose)}");
			title += "!";

			string desc = string.Empty;

			return new RoundResultSummary() { RoundResult = _localPlayerBattleResultType, Title = title, Description = desc };
		}
		
		void OnFinishMission(bool isSuccess)
		{
			if (_outOfAmmo && !isSuccess)
			{
				SetEndState(EndCause.OUT_OF_AMMO);
				return;
			}
			
			SetEndState(isSuccess ? EndCause.CLEAR : EndCause.TIME_OVER);
		}

		void OnUpdatedTimer(float currentTimeLeft)
		{
			_practiceModeUi.UpdateTimeUI(currentTimeLeft);
		}
		
		void OnEndTimer()
		{
			SetEndState(EndCause.TIME_OVER);
		}

		/// <inheritdoc/>
		public void OnFinishTime() { }

		/// <inheritdoc/>
		public void OnLocalPlayerDeath() { }

		/// <inheritdoc/>
		public void OnLocalPlayerKill() { }

		/// <inheritdoc/>
		public void OnLocalPoint(int points, Team teamToAddPoint) { }

		/// <inheritdoc/>
		public void OnOtherPlayerEnter(Player newPlayer) { }

		public void OnOtherPlayerDeath(PlayerSeat player) { }

		/// <inheritdoc/>
		public void OnOtherPlayerLeave(Player otherPlayer) { }

		/// <inheritdoc/>
		public void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged) { }

		/// <inheritdoc/>
		public BattleResultType LocalPlayerBattleResultType => _localPlayerBattleResultType;
		
		private static PracticeMode _instance;
		public static PracticeMode Instance
		{
			get
			{
				if (_instance == null) { _instance = FindFirstObjectByType<PracticeMode>(); }
				return _instance;
			}
		}
		
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
		
		public enum State
		{
			NONE = 0,
			FREE_MODE = 1,		//자유 공간
			TRAINING_MODE = 2,	//레벨 단위 훈련 모드
		}

		public enum EndCause
		{
			TIME_OVER = 0,
			OUT_OF_AMMO = 1,
			CLEAR = 2,
		}
	}
}