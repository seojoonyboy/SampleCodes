using Framework;
using Game.Data;
using Game.View.UI;
using Photon.Realtime;
using System;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Game.View.BattleSystem
{
	/// <summary>
	/// 인게임 튜토리얼 모드.
	/// </summary>
	public class TutorialMode : bl_PhotonHelper, IBattleMode
	{
		[SerializeField] Transform _content;
		
		TutorialManager TutorialManager => TutorialManager.Instance;
		
		private static TutorialMode _instance;
		public static TutorialMode Instance
		{
			get
			{
				if (_instance == null) { _instance = FindFirstObjectByType<TutorialMode>(); }
				return _instance;
			}
		}
		
		void Awake()
		{
			Initialize();
		}

		void Start()
		{
			BattleMainUi battleMainUi = BattleMainUi.Instance;
			battleMainUi.TutorialModeUi.SetActiveGo(true);
			
			TutorialManager.InitTutorial();
			TutorialManager.BeginTutorial().Forget();
		}

		public void Initialize()
		{
			bool meetMode = BattleManager.Instance.IsGameMode(BattleMode.Tutorial, this);
			enabled = meetMode;

			if (!enabled)
			{
				_content.SetActiveGo(false);
				return;
			}
			
			bl_EventHandler.onLocalPlayerSpawn += OnLocalSpawn;
		}

		void OnLocalSpawn()
		{
			bl_MFPS.LocalPlayerReferences.playerHealthManager.CanDamageByLocal = false;
		}

		void OnDisable()
		{
			bl_EventHandler.onLocalPlayerSpawn -= OnLocalSpawn;

			if (GetGameMode == BattleMode.Tutorial)
			{
				bl_UtilityHelper.LockCursor(false, LockCursorMask.TutorialPopup);
			}
		}

		public void OnFinishTime() { }
		public void OnLocalPoint(int points, Team teamToAddPoint) { }
		public void OnLocalPlayerKill() { }
		public void OnLocalPlayerDeath() { }
		public void OnOtherPlayerEnter(Player newPlayer) { }
		public void OnOtherPlayerLeave(Player otherPlayer) { }
		public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) { }
		public BattleResultType LocalPlayerBattleResultType => BattleResultType.None;
		public RoundResultSummary MakeRoundResultSummary() { return null; }
	}
}