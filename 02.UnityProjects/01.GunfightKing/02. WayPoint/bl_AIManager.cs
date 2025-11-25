using Framework;
using Game.Data;
using Game.View;
using Game.View.AI.State;
using Game.View.BattleSystem;
using MFPS.Runtime.AI;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Random = UnityEngine.Random; //Replace default Hashtables with Photon hashtables


[DefaultExecutionOrder(BattleSceneScriptExecutionOrders.AiManager)]
public class bl_AIManager : bl_PhotonHelper
{
	[Header("Settings")]
	public int updateBotsLookAtEach = 50;
	/// <summary>
	/// References to all the currently instanced bots in the scene
	/// </summary>
	[Header("[관찰용]")]
	[SerializeField] public List<Transform> AllBotsTransforms = new List<Transform>();

	/// <summary>
	/// Information and stats of all the bots currently playing
	/// </summary>
	[Header("[관찰용]")]
	[SerializeField] public List<MFPSBotProperties> BotsStatistics = new List<MFPSBotProperties>();

	List<AIWayPoint> essentialWayPoints = new List<AIWayPoint>();
	List<AIWayPoint> normalWayPoints = new List<AIWayPoint>();
	
	#region Public properties
	/// <summary>
	/// Is this game using bots?
	/// </summary>
	public bool BotsActive
	{
		get;
		set;
	}

	/// <summary>
	/// Is the bots information already synced by the Mater client?
	/// </summary>
	public bool HasMasterInfo
	{
		get;
		set;
	} = false;
	#endregion

	#region Events
	public delegate void EEvent(List<MFPSBotProperties> stats);
	public static EEvent OnMaterStatsReceived;
	public delegate void StatEvent(MFPSBotProperties stat);
	public static StatEvent OnBotStatUpdate;
	#endregion

	#region Private members
	BattleManager _battleManager;
	BattleModeSettings _modeSettings;
	bl_AICoverPointManager _cpMgr;
	bool _isBeingDestroyed;

	[Header("[관찰용]")]
	[SerializeField] List<PlayersSlots> Team1PlayersSlots = new List<PlayersSlots>();
	[SerializeField] List<PlayersSlots> Team2PlayersSlots = new List<PlayersSlots>();
	
	[SerializeField] List<bl_AIShooter> SpawningBots = new List<bl_AIShooter>();

	List<string> lastLifeBots = new List<string>();
	int _numOfTeam1Slots = 5; // (OneTeam모드에서는 전체 플레이어 수)
	int _numOfTeam2Slots = 5;

	// 커스텀 게임에서의 봇은 로비에서 지정된 수만큼만 스폰된다.
	bool _isCustomRoom;
	int _numOfTeam1FixedBots = 0;
	int _numOfTeam2FixedBots = 0;

	[SerializeField] List<bl_AIShooter> AllBots = new List<bl_AIShooter>();
	List<bl_PlayerReferencesCommon> targetsLists = new List<bl_PlayerReferencesCommon>();

	// Master용. Slave들에게 첫 Bot정보를 넘겼는지 여부.
	bool _firstSyncBotsToOthers;

	// 도중 Master가 되었을 때 AI들의 타겟목록 생성 유도. 
	bool _needForceRefreshTargetList;

	/// <summary>
	/// 봇 정보 첫동기화 완료 되었나.
	/// </summary>
	public bool AllBotsStatsSyncDone { get; private set; }

	DemolitionBombZone targetBombZone;		//AI가 목표로 하는 폭탄 설치 지역
	bl_AIShooter bombAssignedShooter;
	
	const int MAX_PATH_POINT_NUM = 10;
	
	//--bool isMasterAlredyInTeam = false;
	#endregion

	/// <summary>
	/// 
	/// </summary>
	private void Awake()
	{
		DebugEx.Log($"[bl_AIManager] Awake() ViewID={photonView.ViewID}");

		_cpMgr = bl_AICoverPointManager.Instance;

		InitWayPointSettings();
		
		if (!PhotonNetworkEx.IsConnected) { return; }

		_battleManager = BattleManager.Instance;
		_modeSettings = _battleManager.GetGameMode.GetGameModeInfo();

		//bl_PhotonCallbacks.PlayerEnteredRoom += OnPlayerEnter;
		bl_PhotonCallbacks.MasterClientSwitched += OnMasterClientSwitched;
		bl_PhotonCallbacks.PlayerLeftRoom += OnPlayerLeft;

		CheckViewAllocation();
		BotsActive = (bool)PhotonNetwork.CurrentRoom.CustomProperties[PropertiesKeys.WithBotsKey];

		if (_modeSettings.SinglePlayerMode)
		{
			_numOfTeam1Slots = 0;
			_numOfTeam2Slots = 0;
		}
		else if (isOneTeamMode)
		{
			_numOfTeam1Slots = PhotonNetwork.CurrentRoom.MaxPlayers;
		}
		else
		{
			int halfMaxPlayers = PhotonNetwork.CurrentRoom.MaxPlayers / 2;
			_numOfTeam1Slots = halfMaxPlayers;
			_numOfTeam2Slots = halfMaxPlayers;
		}

		_isCustomRoom = BattleManager.Instance.IsCustomRoom;

		// 커스텀 게임에서 봇은, 로비에서 지정한 수만큼만 스폰된다.
		if (_isCustomRoom)
		{
			_numOfTeam1FixedBots = PhotonNetwork.CurrentRoom.GetCustomProperty<int>(PropertiesKeys.Team1WantBotCnt);
			_numOfTeam2FixedBots = PhotonNetwork.CurrentRoom.GetCustomProperty<int>(PropertiesKeys.Team2WantBotCnt);
		}
		
		bl_EventHandler.onRemoteActorChange += OnRemotePlayerChange;
		bl_EventHandler.onLocalPlayerDeath += OnLocalDeath;
		bl_EventHandler.onLocalPlayerSpawn += OnLocalPlayerSpawn;

		int roomSeed = PhotonNetwork.CurrentRoom.GetCustomProperty<int>(PropertiesKeys.RoomRandomSeed);

		if (!BattleManager.IsTrainingMode)
		{
			BotRepository botRepo = BotRepository.Instance;
			botRepo.LoadAllAvailableWeaponSkins();
			string poolKey = isOneTeamMode ? "BotMultiPersonal" : "BotMultiParty";

			//신입 특수미션 경기는 쉽게
			if(BattleManager.RoomFlags.HasFlag(RoomFlags.IsTrial))
			{
				poolKey = isOneTeamMode ? "BotOfflinePersonalEasy" : "BotOfflineTeamNormal";
			}

			botRepo.DecideBotDefPool(
				poolKey,
				isOneTeamMode,
				_numOfTeam1Slots + _numOfTeam2Slots,
				roomSeed
			);
		}
	}

	void OnDestroy()
	{
		_isBeingDestroyed = true;
	}

	public static BotProfileDef GetBotProfileDef(string botName)
	{
		botName = GetNameWithoutSeqNo(botName);
		
		return BotProfileCDB.Instance.GetDef(botName);
	}

	public void FirstSpawnAllBots()
	{
		if(!PhotonNetwork.IsMasterClient) { return; }

		DebugEx.Log($"[bl_AIManager] FirstSpawnBots()");

		FirstSpawn();
		UpdateTargetList();

		// 마스터 스스로는 Sync가 된 셈이다.
		AllBotsStatsSyncDone = true;
		DebugEx.Log($"[bl_AIManager] AllBotsStatsSyncDone = true (self)");

		SyncBotsDataToAllOthers();

		if (GetGameMode != BattleMode.DM)
		{
			DecideBotGroupIDs();
			InitFirstSpawnPaths();
		}
		
		//모든 그룹의 경로를 일단 지정한다.

		_firstSyncBotsToOthers = true;
	}

	/// <summary>
	/// Instance all bots for the first time
	/// </summary>
	void FirstSpawn()
	{
		if (PhotonNetworkEx.IsMasterClient)
		{
			SetUpSlots(true);
			if (BotsActive)
			{
				if (isOneTeamMode)
				{
					int requiredBots = EmptySlotsCount(Team.All);
					
					// 커스텀 룸은 로비에서 지정된 수량까지만 스폰.
					if(_isCustomRoom)
					{
						requiredBots = Math.Min(requiredBots, _numOfTeam1FixedBots);
					}

					for (int i = 0; i < requiredBots; i++)
					{
						SpawnBot(null, Team.All, isFirstSpawn: true);
					}
				}
				else
				{
					int half = EmptySlotsCount(Team.Team1);
					// 커스텀 룸은 로비에서 지정된 수량까지만 스폰.
					if (_isCustomRoom)
					{
						half = Math.Min(half, _numOfTeam1FixedBots);
					}

					for (int i = 0; i < half; i++)
					{
						SpawnBot(null, Team.Team1, isFirstSpawn: true);
					}

					half = EmptySlotsCount(Team.Team2);
					// 커스텀 룸은 로비에서 지정된 수량까지만 스폰.
					if (_isCustomRoom)
					{
						half = Math.Min(half, _numOfTeam2FixedBots);
					}

					for (int i = 0; i < half; i++)
					{
						SpawnBot(null, Team.Team2, isFirstSpawn: true);
					}
				}

				if (BattleManager.Instance.GameState == BattleStates.Waiting)
				{
					BattleManager.Instance.CheckRequiredMembers();
				}
			}
		}
	}
	
	public void RespawnAllBots(bool forcedRespawn = false)
	{
		if (!PhotonNetworkEx.IsMasterClient) return;

		DebugEx.Log("[bl_AIManager] RespawnAllBots()");

		List<GameObject> allBot = new();
		DestroyAllBots(allBot);

		/*--
		int counter = 0;
		for (int i = AllBots.Count - 1; i >= 0; i--)
		{
			{
				counter++;
				DebugEx.Log($"[bl_AIManager] RespawnAllBots() - [{counter}] deadBot: {AllBots[i].AITeam}, {AllBots[i].AIName}");
				
				var bot = AllBots[i];
				SpawnBot(bot);

				AllBots.Remove(bot);
				AllBotsTransforms.Remove(bot.AimTarget);
				
				//if (bot != null)
				{
					//if (bot.gameObject)
					//{
						PhotonView bv = bot.GetComponent<PhotonView>();
						PhotonNetwork.Destroy(bv.gameObject);
					//}
					//else
					//{
					//	bot.GetComponent<bl_AIShooterHealth>().DestroyEntity();
					//}
				}
			}
		}

		for (int i = SpawningBots.Count - 1; i >= 0; i--)
		{
			if (SpawningBots[i] == null || SpawningBots[i].gameObject == null) continue;

			DebugEx.Log($"[bl_AIManager] RespawnAllBots() - [{counter}] spawningBot: {SpawningBots[i].AITeam}, {SpawningBots[i].AIName}");

			SpawnBot_ReusingAgentInfo(i);
			
			counter++;
			
		}
		--*/

		// 사망하여 Destroy된 봇 스폰
		/*--
		int team1Count = AllBots.Count(bot => bot.AITeam == Team.Team1);
		int team2Count = AllBots.Count(bot => bot.AITeam == Team.Team2);
		int team1AiSlots = Team1PlayersSlots.Count(slot => slot.Player == string.Empty);
		int team2AiSlots = Team2PlayersSlots.Count(slot => slot.Player == string.Empty);
		int team1NeedCount = Mathf.Max(0, team1AiSlots - team1Count);
		int team2NeedCount = Mathf.Max(0, team2AiSlots - team2Count);
		
		for (int i = 0; i < team1NeedCount; i++)
		{
			SpawnBot(null, Team.Team1);
		}
		for (int i = 0; i < team2NeedCount; i++)
		{
			SpawnBot(null, Team.Team2);
		}
		--*/

		var team1Users = BattleManager.Instance.GetMFPSPlayerInTeam(Team.Team1, onlyAlive:true, onlyUser:true);
		var team2Users = BattleManager.Instance.GetMFPSPlayerInTeam(Team.Team2, onlyAlive: true, onlyUser: true);

		int team1NeedBots = Team1PlayersSlots.Count - team1Users.Count();
		int team2NeedBots = Team2PlayersSlots.Count - team2Users.Count();

		// 커스텀 룸은 지정된 봇 수량까지만 스폰.
		if (_isCustomRoom)
		{
			team1NeedBots = Mathf.Min(team1NeedBots, _numOfTeam1FixedBots);
			team2NeedBots = Mathf.Min(team2NeedBots, _numOfTeam2FixedBots);
		}

		foreach (var slot in Team1PlayersSlots)
		{
			if(team1NeedBots == 0) { break; }

			var bot = RespawnBotWithSlot(slot, Team.Team1);
			if(bot) { team1NeedBots--; }
		}
		foreach (var slot in Team2PlayersSlots)
		{
			if (team2NeedBots == 0) { break; }

			var bot = RespawnBotWithSlot(slot, Team.Team2);
			if (bot) { team2NeedBots--; }
		}


		foreach (var bot in allBot)
		{
			PhotonNetwork.Destroy(bot.gameObject);
		}
	}

	bl_AIShooter SpawnBot_ReusingAgentInfo(int pendingIndex)
	{
		var agent = SpawnBot(SpawningBots[pendingIndex]);
		SpawningBots[pendingIndex].GetComponent<bl_AIShooterHealth>().DestroyEntity();
		SpawningBots.RemoveAt(pendingIndex);

		return agent;
	}

	bl_AIShooter RespawnBotWithSlot(PlayersSlots slot, Team team)
	{
		if(slot.Player.HasContent()) { return null; }

		if (slot.Bot == string.Empty)
		{
			return SpawnBot(null, team, null);
		}
		else if(AllBots.Find(bot => bot.AIName == slot.Bot) == null)
		{
			return SpawnBot(null, team, slot.Bot);
		}
		return null;
	}
	/// <summary>
	/// 
	/// </summary>
	private void OnDisable()
	{
		//bl_PhotonCallbacks.PlayerEnteredRoom -= OnPlayerEnter;
		bl_PhotonCallbacks.MasterClientSwitched -= OnMasterClientSwitched;
		bl_PhotonCallbacks.PlayerLeftRoom -= OnPlayerLeft;
		bl_EventHandler.onRemoteActorChange -= OnRemotePlayerChange;
		bl_EventHandler.onLocalPlayerDeath -= OnLocalDeath;
		bl_EventHandler.onLocalPlayerSpawn -= OnLocalPlayerSpawn;
	}

	/// <summary>
	/// Send the bots data to all other clients in the room
	/// This data will automatically send to new players
	/// </summary>
	void SyncBotsDataToAllOthers()
	{
		if (!PhotonNetworkEx.IsMasterClient) return;

		DebugEx.Log($"[bl_AIManager] SyncBotsDataToAllOthers()");

		Player[] players = PhotonNetworkEx.PlayerList;
		string line = GetCompiledBotsData();
		//and send to the new player so him can have the data and update locally.
		photonView.RPC(nameof(SyncAllBotsStats), RpcTarget.Others, line, 0);

		//also send the slots data so all player have the same list in case the Master Client leave the game
		line = GetCompiledSlotsData();
		//and send to the new player so him can have the data and update locally.
		photonView.RPC(nameof(SyncAllBotsStats), RpcTarget.Others, line, 1);
		bl_EventHandler.onBotsInitializated?.Invoke();
	}

	/// <summary>
	/// Gets the bots data as a string line
	/// </summary>
	/// <returns></returns>
	public string GetCompiledBotsData()
	{
		//so first we recollect all the stats from the master client and join it in a string line
		string line = string.Empty;
		for (int i = 0; i < BotsStatistics.Count; i++)
		{
			MFPSBotProperties b = BotsStatistics[i];
			line += string.Format("{0},{1},{2},{3},{4},{5},{6}|", b.Name, b.Kills, b.Deaths, b.Assists, (int)b.Team, b.ViewID, (byte)b.GameState);
		}
		return line;
	}

	/// <summary>
	/// Get the slots list in a string line
	/// </summary>
	/// <returns></returns>
	public string GetCompiledSlotsData()
	{
		string line = string.Empty;
		for (int i = 0; i < Team1PlayersSlots.Count; i++)
		{
			var d = Team1PlayersSlots[i];
			line += string.Format("{0},{1}|", d.Player, d.Bot);
		}
		line += "&";
		if (!isOneTeamMode)
		{
			for (int i = 0; i < Team2PlayersSlots.Count; i++)
			{
				var d = Team2PlayersSlots[i];
				line += string.Format("{0},{1}|", d.Player, d.Bot);
			}
		}
		return line;
	}

	/// <summary>
	/// Setup the team slots where players and bots can be assigned.
	/// </summary>
	void SetUpSlots(bool addExistingPlayers)
	{
		Team1PlayersSlots.Clear();
		Team2PlayersSlots.Clear();

		if(_modeSettings.SinglePlayerMode)
		{
			return;
		}

		var team1Players = PhotonNetworkEx.PlayerList.GetPlayersInWantTeam(isOneTeamMode ? Team.All : Team.Team1).ToList();

		if (!isOneTeamMode)
		{
			var team2Players = PhotonNetworkEx.PlayerList.GetPlayersInWantTeam(Team.Team2).ToList();

			int ptp = _numOfTeam1Slots;
			for (int i = 0; i < ptp; i++)
			{
				PlayersSlots s = new PlayersSlots();
				s.Bot = string.Empty;
				if (addExistingPlayers && team1Players.Count > 0)
				{
					s.Player = team1Players[0].NickName;
					team1Players.RemoveAt(0);
				}
				else
				{
					s.Player = string.Empty;
				}
				Team1PlayersSlots.Add(s);
			}
			ptp = _numOfTeam2Slots;
			for (int i = 0; i < ptp; i++)
			{
				PlayersSlots s = new PlayersSlots();
				s.Bot = string.Empty;
				if (addExistingPlayers && team2Players.Count > 0)
				{
					s.Player = team2Players[0].NickName;
					team2Players.RemoveAt(0);
				}
				else
				{
					s.Player = string.Empty;
				}
				Team2PlayersSlots.Add(s);
			}
		}
		else
		{
			for (int i = 0; i < _numOfTeam1Slots; i++)
			{
				PlayersSlots s = new PlayersSlots();
				s.Bot = string.Empty;
				if (addExistingPlayers && team1Players.Count > 0)
				{
					s.Player = team1Players[0].NickName;
					team1Players.RemoveAt(0);
					Debug.Log("Set default player in slot: " + s.Player);
				}
				else
				{
					s.Player = string.Empty;
				}
				Team1PlayersSlots.Add(s);
			}
		}
	}

	/// <summary>
	/// 몬스터 역할의 BOT을 생성한다.
	/// </summary>
	/// <returns></returns>
	public bl_AIShooter SpawnMonster(Team team)
	{
		return SpawnBot(null, team, null, true);
	}

	/// <summary>
	/// [Master 전용] Bot을 생성한다.
	/// 'agent' 또는 'wantBotName'을 지정하면 Bot이름이 재사용된다.
	/// </summary>
	public bl_AIShooter SpawnBot(bl_AIShooter agent = null, Team team = Team.None, string wantBotName = null, bool isMonster = false, bool isFirstSpawn = false)
	{
		bl_AIShooter newAgent = null;
		bl_SpawnPointBase spawnPoint;
		
		string AiName = null;
		int debugStep = 0;

		try
		{

			if (agent != null)//if is a already instanced bot
			{
				AiName = GameSettings.Instance.BotPlayer.name;

				if (agent.AITeam == Team.None) { Debug.LogError($"bot {agent.AIName} has not team"); }

				//Check if the bot has been assigned to a team, or if not, check if there's a space for him
				if (VerifyTeamAffiliation(agent, agent.AITeam)) { }
				else // there's not space in the team for this bot
				{
					//Check if the bot was registered in a team before
					int ind = BotsStatistics.FindIndex(x => x.Name == agent.AIName);
					if (ind != -1 && ind <= BotsStatistics.Count - 1)
					{
						//delete the bot data since it won't play anymore.
						BotsStatistics.RemoveAt(ind);
					}
					return null;
				}
			}
			else
			{
				AiName = GameSettings.Instance.BotPlayer.name;
			}
			debugStep = 1;

			string AIName = agent == null ? CreateAIName() : agent.AIName;

			if (wantBotName != null) { AIName = wantBotName; }

			Team AITeam = agent == null ? team : agent.AITeam;

			if (!isOneTeamMode)//if team mode, spawn bots in the respective team spawn points.
			{
				bl_SpawnPointManager.Instance.PossessSpawnPoint(AITeam, AIName, out spawnPoint);
			}
			else
			{
				bl_SpawnPointManager.Instance.PossessSpawnPoint(Team.All, AIName, out spawnPoint);
			}
			debugStep = 2;

			spawnPoint.GetSpawnPosition(out Vector3 spawnPosition, out Quaternion spawnRot);
			// bl_SpawnPointManager.Instance.RandomSpawnPointNearBy(spawnPosition, AIName, out spawnPosition);

			// 새 이름이라면 빈 슬롯이 반드시 있어야 봇 생성 가능.
			bool needNewName = agent == null && !wantBotName.HasContent();
			if (needNewName)
			{
				var playerSlots = team == Team.Team2 ? Team2PlayersSlots : Team1PlayersSlots;
				int index = playerSlots.FindIndex(x => x.Player == string.Empty && x.Bot == string.Empty);
				if (index == Invalid.Index)
				{
					// 생성 불가.
					return null;
				}
			}
			debugStep = 3;

			int botDefNo = GetAvailableBotDefFromPool(AITeam.ToString(), AIName)?.BotDefId ?? Invalid.No;

			debugStep = 4;
			
			var instParam = new BotInstantiationParam(AIName, AITeam, botDefNo, (float)PhotonNetworkEx.TimeEx);

			// BOT 오브젝트 인스턴싱
			GameObject bot = PhotonNetwork.InstantiateRoomObject(AiName, spawnPosition, spawnRot, 0, instParam.ToObjects());

			newAgent = bot.GetComponent<bl_AIShooter>();

			debugStep = 5;

			// 기존봇이 아직 남아있는 경우. 기존 봇 정보를 재사용. 참고로 기존 봇 캐릭터는 곧 제거된다(메서드 호출지점 참고).
			if (agent != null)
			{
				newAgent.AIName = agent.AIName;
				newAgent.AITeam = agent.AITeam;
				photonView.RPC(nameof(SyncBotStat), RpcTarget.Others, newAgent.AIName, bot.GetComponent<PhotonView>().ViewID, (byte)3);
				debugStep = 6;
			}
			else if (wantBotName.HasContent()) // 죽은 봇을 다시 스폰시키는 경우. team정보도 같이 지정되었어야 한다.
			{
				GameException.Verify(team != Team.None, "Logic Error");

				newAgent.AIName = wantBotName;
				newAgent.AITeam = team;
				photonView.RPC(nameof(SyncBotStat), RpcTarget.Others, newAgent.AIName, bot.GetComponent<PhotonView>().ViewID, (byte)3);
				debugStep = 7;
			}
			else // 첫 인스턴싱인 경우
			{
				newAgent.AIName = AIName;
				newAgent.AITeam = team;

				newAgent.LookAtDirection = spawnPoint.transform.forward;

				//insert bot stats
				var bs = new MFPSBotProperties();
				bs.Name = newAgent.AIName;
				bs.Team = team;
				bs.ViewID = bot.GetComponent<PhotonView>().ViewID;
				BotsStatistics.Add(bs);
				//reserve a space in the team for this bot
				VerifyTeamAffiliation(newAgent, team);
				// newAgent.aiSettings = GetBotDef(AIName, team);

				debugStep = 8;
			}
			newAgent.Init(isFirstSpawn ? InitType.FirstSpawn : InitType.Respawn);

			debugStep = 9;
			/*-- 중복됨. Spawn할때 DispatchRemoteActorChange() 호출됨
			//Build Player Data
			MFPSPlayer playerData = new MFPSPlayer()
			{
				Name = newAgent.AIName,
				Team = newAgent.AITeam,
				Actor = newAgent.transform,
				AimPosition = newAgent.AimTarget,
				isRealPlayer = false,
				isAlive = true,
			};

			bl_EventHandler.DispatchRemoteActorChange(new bl_EventHandler.PlayerChangeData()
			{
				PlayerName = newAgent.AIName,
				MFPSActor = playerData,
				IsAlive = true,
				NetworkView = newAgent.GetComponent<PhotonView>()
			});
			--*/
			AllBots.Add(newAgent);
			AllBotsTransforms.Add(newAgent.AimTarget);

			if (isFirstSpawn && !GetGameMode.GetModeInfo().SinglePlayerMode)
			{
				SendBotJoinChatMsg(newAgent.AIName, team);
			}

			DebugEx.Log("[bl_AIManager] SpawnBot() - " + newAgent.AIName);

			debugStep = 10;
		} 
		catch (Exception e)
		{
			//PhotonRoomInstantiate 오류
			if (debugStep == 4)
			{
				CrashlyticsUtil.LogException(new GameException($"SpawnBot error(step:{debugStep}), InstantiateRoomObjectErrorStatus : {PhotonNetwork.InstantiateRoomObjectErrorStatus}" + e.Message));
			}
			else
			{
				CrashlyticsUtil.LogException(new GameException($"SpawnBot error(step:{debugStep}): " + e.Message));
			}
		}
		return newAgent;
	}

	BotRepository.PoolSlotData GetAvailableBotDefFromPool(string aiTeam, string aiName)
	{
		return BotRepository.Instance.GetAvailableBotDef(aiTeam, aiName);
	}

	void SendBotJoinChatMsg(string nickName, Team team)
	{
		RoomChatManager.Instance.SendChat("JoinedTheGame.", nickName, team, RoomChatTarget.All);
	}

	string CreateAIName()
	{
		const int MaxTry = 20;
		for (int i = 0; i < MaxTry; i++)
		{
			int index = Random.Range(0, BotProfileCDB.Instance.GetRecordCnt());
			var botProfileDef = BotProfileCDB.Instance.GetDefByRecordIndex(index);

			if (!IsNameDuplicated(botProfileDef.Name))
			{
				return botProfileDef.Name;
			}
		}

		// 중복이 안걸러지는 경우 방어코드
		{
			int index = Random.Range(0, BotProfileCDB.Instance.GetRecordCnt());
			var botProfileDef = BotProfileCDB.Instance.GetDefByRecordIndex(index);

			return MakeUniqueNameUsingSeqNo(botProfileDef.Name);
		}
	}

	// 시퀀스 번호를 붙이는 방식으로 유니크한 이름을 생성
	string MakeUniqueNameUsingSeqNo(string origName)
	{
		origName = GetNameWithoutSeqNo(origName);
		int seq = 1;
		while (true)
		{
			string name = GetNameWithSeqNo(origName, seq, false);

			if (!IsNameDuplicated(name))
			{
				return name;
			}
			seq += 1;
		}
	}

	static char _nameSeqNoDelim = '.';

	/// <summary>
	/// 시퀀스번호를 붙인 이름을 리턴
	/// </summary>
	static string GetNameWithSeqNo(string name, int seq, bool checkOrigName = true)
	{
		if (checkOrigName)
		{
			name = GetNameWithoutSeqNo(name);
		}
		return $"{name}{_nameSeqNoDelim}{seq}";
	}

	/// <summary>
	/// 만약 시퀀스번호가 붙은 이름인 경우 떼어내고 오리지널 이름을 리턴
	/// </summary>
	static string GetNameWithoutSeqNo(string name)
	{
		int suffixIndex = name.LastIndexOf(_nameSeqNoDelim);
		if (suffixIndex == Invalid.Index)
		{
			return name;
		}

		return name.Substring(0, suffixIndex);
	}
	/// <summary>
	/// Check if the bot is already assigned in a Team slot
	/// </summary>
	/// <returns></returns>
	private bool VerifyTeamAffiliation(bl_AIShooter agent, Team team)
	{
		var playerSlots = team == Team.Team2 ? Team2PlayersSlots : Team1PlayersSlots;
		//check if the bot is assigned in the team
		if (playerSlots.Exists(x => x.Bot == agent.AIName)) return true;
		else
		{
			//if it's not assigned, check if we can add him
			if (hasSpaceInTeamForBot(team))
			{
				//assign the bot to the team
				int index = playerSlots.FindIndex(x => x.Player == string.Empty && x.Bot == string.Empty);
				playerSlots[index].Bot = agent.AIName;
				return true;
			}
			else { return false; }//bot can't be assigned in team
		}
	}

	/// <summary>
	/// Fetch all the available players (alive) in the map.
	/// </summary>
	private void UpdateTargetList()
	{
		if (!PhotonNetworkEx.IsMasterClient || BattleManager.Instance == null) { return; }

		targetsLists.Clear();
		var all = BattleManager.Instance.OtherPlayers;

		for (int i = 0; i < all.Count; i++)
		{
			if (!all[i].IsAliveAndValid)
			{
				continue;
			}
			targetsLists.Add(all[i].Actor.GetComponent<bl_PlayerReferencesCommon>());
		}

		if (bl_MFPS.LocalPlayerReferences != null)
		{
			targetsLists.Add(bl_MFPS.LocalPlayerReferences);
		}

		// Update the targets for each bot
		for (int i = 0; i < AllBots.Count; i++)
		{
			if (AllBots[i] == null) continue;

			AllBots[i].UpdateTargetList(_needForceRefreshTargetList);
		}

		_needForceRefreshTargetList = false;
	}

	private void InitWayPointSettings()
	{
		essentialWayPoints.Clear();
		normalWayPoints.Clear();
		
		essentialWayPoints.AddRange(
			_cpMgr.EssentialWayPointsParent.GetComponentsInChildren<AIWayPoint>()
		);
		
		normalWayPoints.AddRange(
			_cpMgr.NormalWayPointsParent.GetComponentsInChildren<AIWayPoint>()
		);
	}

	public void InitFirstSpawnPaths()
	{
		if (GetGameMode == BattleMode.FFA) { }
		else
		{
			if (GetGameMode.IsOneOf(BattleMode.DM, BattleMode.TDM))
			{
				List<Vector3> points = new List<Vector3>();
			
				List<IGrouping<int, bl_AIShooter>> team1Group = AllBots.FindAll(x => x.AITeam == Team.Team1).GroupBy(x => x.GroupID).ToList();
				foreach (IGrouping<int, bl_AIShooter> groupItem in team1Group)
				{
					AIWayPoint endWayPoint = GetRandomEssentialWayPoint();
					points = GeneratePathStartPointToEndWithEssentialWayPoint(Team.Team1, endWayPoint);
				
					int groupID = groupItem.Key;
					UpdateGroupPath(Team.Team1, groupID, points);
				}
				
				List<IGrouping<int, bl_AIShooter>> team2Group = AllBots.FindAll(x => x.AITeam == Team.Team2).GroupBy(x => x.GroupID).ToList();
				foreach (IGrouping<int, bl_AIShooter> groupItem in team2Group)
				{
					AIWayPoint endWayPoint = GetRandomEssentialWayPoint();
					points = GeneratePathStartPointToEndWithEssentialWayPoint(Team.Team2, endWayPoint);
				
					int groupID = groupItem.Key;
					UpdateGroupPath(Team.Team2, groupID, points);
				}
			}
		}
	}

	private AIWayPoint GetRandomEssentialWayPoint()
	{
		int randomIndex = Random.Range(0, essentialWayPoints.Count);
		AIWayPoint randomWayPoint = essentialWayPoints[randomIndex];
		// Debug.Log(randomWayPoint.name);
		
		return randomWayPoint;
	}

	private AIWayPoint GetRandomStartingEssentialWayPoints(Team team)
	{
		List<AIWayPoint> result = essentialWayPoints
			.FindAll(
				x => x.IsStartPoint && 
				x.StartPointOwnerTeam.ToString().Equals(team.ToString()
			)
		);
		
		int randomIndex = Random.Range(0, result.Count);
		return result[randomIndex];
	}

	private AIWayPoint GetClosestEssentialWayPoint(Vector3 current)
	{
		AIWayPoint closestWayPoint = essentialWayPoints.FirstOrDefault();
		float closestDistance = float.MaxValue;

		foreach (AIWayPoint essentialWayPoint in essentialWayPoints)
		{
			float distanceCurrentToEssentialWayPoint = Vector3.Distance(essentialWayPoint.transform.position, current);
			if (distanceCurrentToEssentialWayPoint < closestDistance)
			{
				closestDistance = distanceCurrentToEssentialWayPoint;
				closestWayPoint = essentialWayPoint;
			}
		}
		
		return closestWayPoint;
	}

	private void GenerateEssentialWayPointsToDestination(Team team, AIWayPoint current, AIWayPoint destination, ref List<AIWayPoint> result, int totalPointNum = 0)
	{
		// Vector3 currentPosition = current.transform.position;
		// Vector3 dirCurrentToDestination = destination - current.transform.position;
		
		List<AIWayPoint> availableWayPoints = new List<AIWayPoint>();
		foreach (AIWayPoint nextPoint in current.nextPoints)
		{
			if(nextPoint == null) continue;
			if(result.Contains(nextPoint)) continue;
			
			// Vector3 dirCurrentToNextWayPoint = (nextPoint.transform.position - currentPosition).normalized;
			// float dotRes = Vector3.Dot(dirCurrentToDestination, dirCurrentToNextWayPoint);

			// Debug.Log("Case 301 " + nextPoint.name + " dotRes : " + dotRes);
			// if (dotRes > 0) { availableWayPoints.Add(nextPoint); }
			
			availableWayPoints.Add(nextPoint);
		}

		foreach (AIWayPoint prevPoint in current.prevPoints)
		{
			if(prevPoint == null) continue;
			if(result.Contains(prevPoint)) continue;
			
			// Vector3 dirCurrentToNextWayPoint = (prevPoint.transform.position - currentPosition).normalized;
			// float dotRes = Vector3.Dot(dirCurrentToDestination, dirCurrentToNextWayPoint);

			// Debug.Log("Case 301 " + prevPoint.name + " dotRes : " + dotRes);
			// if (dotRes > 0) { availableWayPoints.Add(prevPoint); }
			
			availableWayPoints.Add(prevPoint);
		}
		
		if(availableWayPoints.Count == 0 || totalPointNum >= MAX_PATH_POINT_NUM)
		{
			result.Add(destination);
			return;
		}
		
		int rndIndex = Random.Range(0, availableWayPoints.Count);

		current = availableWayPoints[rndIndex];
		
		result.Add(current);

		totalPointNum += 1;
		
		GenerateEssentialWayPointsToDestination(team, current, destination, ref result, totalPointNum);
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="bot"></param>
	/// <returns></returns>
	public void GetTargetsFor(bl_AIShooter bot, ref List<Transform> list)
	{
		list.Clear();
		bl_PlayerReferencesCommon t;
		for (int i = 0; i < targetsLists.Count; i++)
		{
			t = targetsLists[i];
			if (t == null || t.name == bot.AIName) continue;

			if (isOneTeamMode)
			{
				list.Add(t.BotAimTarget);
			}
			else
			{
				if (t.PlayerTeam != Team.None && t.PlayerTeam == bot.AITeam) continue;

				list.Add(t.BotAimTarget);
			}
		}
	}

	/// <summary>
	/// Bot목록에서 제거하고 Respawn목록에 넣는다.
	/// </summary>
	public void OnBotDeath(bl_AIShooter agent, bl_AIShooter killer)
	{
		if (!PhotonNetworkEx.IsMasterClient)
			return;

		AllBots.Remove(agent);
		AllBotsTransforms.Remove(agent.AimTarget);
		for (int i = 0; i < AllBots.Count; i++)
		{
			AllBots[i].CheckTargets();
		}

		AddBotToRespawn(agent);

		UpdateTargetList();
	}


	/// <summary>
	/// Put a bot to the pending list to respawn after the min respawn time.
	/// </summary>
	public void AddBotToRespawn(bl_AIShooter bot)
	{
		//Debug.Log($"ADD BOT TO RESPAWN: " + bot.AIName);
		SpawningBots.Add(bot);

		//automatically spawn the bot after the re-spawn time
		//if (GetGameMode.GetGameModeInfo().OnPlayerDie == OnPlayerDie.SpawnAfterDelay)
		{
			Invoke(nameof(DestroyOrRespawnPendingBot), GameSettings.Instance.PlayerRespawnTime);
		}
	}

	/// <summary>
	/// 
	/// </summary>
	void DestroyOrRespawnPendingBot()
	{
		if (!PhotonNetworkEx.IsMasterClient)
		{ return; }

		if (SpawningBots == null || SpawningBots.Count <= 0) return;
		if (SpawningBots[0] != null)
		{
			if (GetGameMode.GetGameModeInfo().OnPlayerDie == OnPlayerDie.SpawnAfterDelay)
			{
				SpawnBot(SpawningBots[0]);
			}
			//--photonView.RPC(nameof(RpcHideBot), RpcTarget.All, SpawningBots[0].AIName);

			//This fix the issue with the duplicate pv id when a master client re-enter in a room.
			PhotonNetwork.Destroy(SpawningBots[0].gameObject);
			SpawningBots.RemoveAt(0);
		}
	}

	[PunRPC]
	public void RpcHideBot(string nickName)
	{

	}

	public void DestroyAllBots(List<GameObject> needDestroyList)
	{
		if(!PhotonNetwork.IsMasterClient) { return; }

		DebugEx.Log("[AIManager] DestroyAllBots()");

		foreach (var bot in AllBots)
		{
			if (bot != null)
			{
				DebugEx.Log($"[AIManager] Bot Will Destroy.. [{bot.AIName}]");
				
				SetBotDeath(bot.AIName);

				if (needDestroyList == null)
				{
					PhotonNetwork.Destroy(bot.gameObject);
				}
				else
				{
					needDestroyList.Add(bot.gameObject);
				}
			}
		}
		AllBots.Clear();
		AllBotsTransforms.Clear();
		
		foreach (var bot in SpawningBots)
		{
			DebugEx.Log($"[AIManager] Bot Will Destroy.. [{bot.AIName}]");
			
			if (needDestroyList == null)
			{
				PhotonNetwork.Destroy(bot.gameObject);
			}
			else
			{
				needDestroyList.Add(bot.gameObject);
			}
		}
		SpawningBots.Clear();
	}

	/// <summary>
	/// Update the killer bot kills count and sync with everyone
	/// </summary>
	public void SetBotKill(string botName)
	{
		var stats = GetBotStatistics(botName);
		if (stats == null) return;

		photonView.RPC(nameof(SyncBotStat), RpcTarget.All, stats.Name, 0, (byte)0);

		BattleManager.Instance.SetPoint(1, BattleMode.FFA, Team.All);
		BattleManager.Instance.SetPoint(1, BattleMode.TDM, stats.Team);
	}

	/// <summary>
	/// 봇의 Assist 정보 (유저의 Assist정보는 자신이 직접 취합)
	/// </summary>
	/// <param name="botName"></param>
	public void SetBotAssist(string botName)
	{
		var stats = GetBotStatistics(botName);
		if (stats == null) return;

		photonView.RPC(nameof(SyncBotStat), RpcTarget.All, stats.Name, 0, (byte)6);
	}

	/*--
	public void SetBotScore(string botName, int score)
	{
		var stat = GetBotStatistics(botName);
		if (stat == null) return;

		stat.Score += score;
	}
	--*/

	/// <summary>
	/// Called in all clients when a bot die
	/// Update the killed bot death count and sync with everyone.
	/// </summary>
	/// <param name="killed">bot that die</param>
	public void SetBotDeath(string killed)
	{
		//if this bots was already replaced by a real player
		if (lastLifeBots.Contains(killed))
		{
			//this is his last life, so since he die, remove his data
			//last life due he got replace by a player so this bot wont respawn again.
			int bi = BattleManager.Instance.OtherPlayers.FindIndex(x => x.NickName == killed);
			if (bi != -1)
			{
				DebugEx.Log($"[bl_AIManager] Test001 {killed}를 OtherPlayers에서 제거함...");
				
				BattleManager.Instance.OtherPlayers.RemoveAt(bi);
				lastLifeBots.Remove(killed);
				RemoveBotInfo(killed);
				return;
			}
		}
		int index = BotsStatistics.FindIndex(x => x.Name == killed);
		if (index <= -1) return;

		bl_EventHandler.EventBotDeath(killed);

		// Shouldn't this be called by Master client only?
		if (PhotonNetworkEx.IsMasterClient) photonView.RPC(nameof(SyncBotStat), RpcTarget.All, BotsStatistics[index].Name, 0, (byte)1);
	}

	/// <summary>
	/// 
	/// </summary>
	public static void UpdateBotView(bl_AIShooter bot, int viewID)
	{
		var stat = Instance.GetBotStatistics(bot.AIName);
		if (stat == null) return;

		stat.ViewID = viewID;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="bot"></param>
	/// <param name="gameState"></param>
	public static void SetBotGameState(bl_AIShooter bot, BotGameState gameState)
	{
		var stat = Instance.GetBotStatistics(bot.AIName);
		if (stat == null) return;

		stat.GameState = gameState;
	}

	public static void SetHealth(bl_AIShooterAgent agent)
	{
		agent.AIHealth.SetHealth(agent.aiSettings.Health, true);
	}

	public static WeaponDef GetWeaponDef(bl_AIShooterAgent agent)
	{
		int targetGunID = agent.aiSettings.PeekRandomGunID();

		if (targetGunID == -1)
		{
			DebugEx.Error("[bl_AIManager] 해당 무기를 찾을 수 없습니다. error 01");
			return null;
		}
		
		var weaponDef = WeaponCDB.Instance.GetDef(targetGunID);
		return weaponDef;
	}

	public static WeaponDef GetWeaponDef(int weaponCode)
	{
		return WeaponCDB.Instance.GetDef(weaponCode);
	}

	#region Photon Events
	/// <summary>
	/// BattleManager의 OnPlayerEnter보다 먼저 처리해야 하기 때문에 PhotonCallback에 연결하지 않는다.
	/// </summary>
	public void OnPlayerEnter(Player player)
	{
		DebugEx.Log(
			$"[bl_AIManager] OnPlayerEnter({player.NickName})", 
			LogColorType.Orange);

		if (!PhotonNetworkEx.IsMasterClient)
		{ return; }

		// 첫 SyncBot을 하기 전에 새로 입장한 Player는 곧 FirstSync에 의해 받을테니 스킵.
		if (!_firstSyncBotsToOthers)
		{ return; }

		if (player.ActorNumber == PhotonNetworkEx.LocalPlayer.ActorNumber)
		{ return; }

		// Sync를 받지 못한 채로 나로 마스터 전환된 경우.
		// 나도 정상 진행을 못하고 진입한 플레이어도 정상 진행 못함.
		if (!AllBotsStatsSyncDone)
		{
			// 플레이어를 쫒아낸다. 내 자신에 대한 킥은 BattleManager.OnMasterClientSwitch()에서 진행.
			PhotonNetworkEx.KickPlayer(player, KickReason.SyncUnavailable);
			return;
		}

		//cause bots statistics are not sync by Hashtables as player data do we need sync it by RPC
		//so for sync it just one time (after will be update by the local client) we send it when a new player enter (only to the new player)
		//so first we recollect all the stats from the master client and join it in a string line
		string line = GetCompiledBotsData();

		DebugEx.Log($"[bl_AIManager] Will SyncAllBotsStats to ({player.NickName})");

		//and send to the new player so him can have the data and update locally.
		photonView.RPC(nameof(SyncAllBotsStats), player, line, 0);

		//also send the slots data so all player have the same list in case the Master Client leave the game
		line = GetCompiledSlotsData();
		photonView.RPC(nameof(SyncAllBotsStats), player, line, 1);
	}

	/// <summary>
	/// PlayerSlots에서 봇이 차지하고 있는 슬롯을 newPlayer의 정보로 대체한다.
	/// 만약 해당하는 기본 봇 인스턴스가 있는 경우엔 제거한다. 이 로직에서 newPlayer를 스폰시키는 건 아니다.
	/// </summary>
	public void ReplaceBotWithPlayer(Player newPlayer, Team playerTeam)
	{
		if (!PhotonNetworkEx.IsMasterClient) { return; }
		if (!BotsActive) return;

		string replaceBot = string.Empty;
		var slotList = playerTeam.IsOneOf(Team.Team1, Team.All) ? Team1PlayersSlots : Team2PlayersSlots;

		//check if this player was already assigned (maybe just change of team)
		if (slotList.Exists(x => x.Player == newPlayer.NickName)) return;

		int index = Invalid.Index;

		// 해당 팀의 빈 슬롯을 찾는다.
		if (_isCustomRoom)
		{
			// 커스텀 룸은 봇이 유저를 대체하지 않는다.
			index = slotList.FindIndex(x => x.Bot == string.Empty && x.Player == string.Empty);
		}
		else
		{
			index = slotList.FindIndex(x => x.Player == string.Empty);
		}

		if (index != Invalid.Index)
		{
			//replace the bot slot with the new player
			replaceBot = slotList[index].Bot;
			if (replaceBot != string.Empty)
			{
				DebugEx.Log($"[bl_AIManager] Test001 {replaceBot} was replaced by {newPlayer.NickName}", LogColorType.Yellow);
				DeleteBot(replaceBot);
			}

			slotList[index].Player = newPlayer.NickName;
			slotList[index].Bot = string.Empty;

			//sync the slot change with other players
			int teamCmdID = playerTeam == Team.Team2 ? 2 : 1;
			photonView.RPC(nameof(SyncBotStat), RpcTarget.Others, $"{teamCmdID}|{newPlayer.NickName}", index, (byte)4);
		}
		// 이제 BOT은 제거해야 하는데 이전 코드로는 제거하지 못한다. ('newPlayer.IsMasterClient'조건은 왜 있는가?)
		// 따라서 아래에 다시 작성한다.
		/*--
		//remove the bot that the master client replace
		if (newPlayer.IsMasterClient && bl_PhotonNetwork.IsMasterClient && !isMasterAlredyInTeam && !string.IsNullOrEmpty(remplaceBot))
		{
			bl_AIShooter bot = AllBots.Find(x => x.AIName == remplaceBot);
			if (bot != null)
			{
				//Debug.Log($"<color=blue>Bot {bot.AIName} was replaced by master {player.NickName}</color>");
				PhotonView bv = bot.GetComponent<PhotonView>();
				bot.References.shooterHealth.DestroyEntity();//destroy on remote clients
				AllBots.Remove(bot);
				AllBotsTransforms.Remove(bot.AimTarget);
				PhotonNetwork.Destroy(bv.gameObject);
			}
			isMasterAlredyInTeam = true;
		}
		--*/
		if (replaceBot.HasContent())
		{
			bl_AIShooter bot = AllBots.Find(x => x.AIName == replaceBot);
			if (bot != null)
			{
				PhotonView bv = bot.GetComponent<PhotonView>();
				AllBots.Remove(bot);
				AllBotsTransforms.Remove(bot.AimTarget);
				PhotonNetwork.Destroy(bv.gameObject);
				DebugEx.Log($"[bl_AIManager] Replace Bot {replaceBot} by {newPlayer.NickName}", LogColorType.Yellow);
			}
		}
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="newMasterClient"></param>
	public void OnMasterClientSwitched(Player newMasterClient)
	{
		DebugEx.Log(
			$"[bl_AIManager] OnMasterClientSwitched new master : {newMasterClient.NickName}", 
			LogColorType.Orange);
		
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
			// Debug.Log("Bots data has been build in new Master Client");

			_needForceRefreshTargetList = true;
			
			bl_SpawnPointManager.Instance.UnlockAllSpawnPoints();
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public void OnPlayerLeft(Player player)
	{
		if (!BotsActive || BattleManager.Instance.RoundFinish) return;
		if (BattleManager.Instance.GameState == BattleStates.Entering) return;

		//Check if the player was occupying a slot
		Team team = player.GetPlayerTeam();

		// Master가 나가면서 Switch된 경우는 team값이 사라져 있다. 복구해 준다.
		if (team == Team.None)
		{
			if(Team2PlayersSlots.Any(slot => slot.Player == player.NickName))
			{
				team = Team.Team2;
			}
			else if(Team1PlayersSlots.Any(slot => slot.Player == player.NickName))
			{
				team = isOneTeamMode ? Team.All : Team.Team1;
			}
		}
		
		if (team == Team.None)
		{
			return;
		}

		var slotList = team == Team.Team2 ? Team2PlayersSlots : Team1PlayersSlots;
		int index = slotList.FindIndex(x => x.Player == player.NickName);
		//empty the occupied slot
		if (index != -1) { slotList[index].Player = ""; }

		if (BattleManager.Instance.GameState.IsOneOf(BattleStates.CountDown, BattleStates.Playing)
			&& GetGameMode.GetGameModeInfo().OnRoundStartedSpawn == OnRoundStartedSpawn.WaitUntilRoundFinish)
		{
			return;
		}

		// 마스터는 유저를 대체할 봇을 생성한다. (커스텀 룸 제외)
		if (PhotonNetworkEx.IsMasterClient && !_isCustomRoom)
		{
			var newAgent = SpawnBot(null, team);
			if (newAgent == null) return;

			//find the slot id where the bot was assigned
			int botIndex = slotList.FindIndex(x => x.Bot == newAgent.AIName);
			//sync the new slot with all other players
			photonView.RPC(nameof(SyncBotStat), RpcTarget.Others, $"{(int)team}|{newAgent.AIName}|{newAgent.photonView.ViewID}", botIndex, (byte)5);
			//show a notification in all players with the new bot name
			bool teamResSwapped = BattleManager.Instance.GameModeLogic.IsTeamResourceSwapped;
			string teamName = GameSettings.Instance.GetTeamName(team, teamResSwapped);
			SendBotJoinChatMsg(newAgent.AIName, team);
			Debug.Log($"<color=blue>Bot {newAgent.AIName} has replace the player {player.NickName}.</color>");
		}
	}
	#endregion

	/// <summary>
	/// 
	/// </summary>
	/// <param name="data"></param>
	/// <param name="value"></param>
	/// <param name="cmd"></param>
	[PunRPC]
	public void SyncBotStat(string data, int value, byte cmd)
	{
		if(_isBeingDestroyed) { return; }

		// if(_battleManager.GameState != BattleStates.Playing) return;
		
		DebugEx.Log($"[bl_AIManager] SyncBotStat ({data}, {value}, cmd={cmd}", LogColorType.Yellow);

		MFPSBotProperties bs = BotsStatistics.Find(x => x.Name == data);
		if (bs == null && cmd.IsOneOf<byte>(0,1,2,3,6))
		{
			return;
		}
		if (cmd == 0)//add kill
		{
			if(bs.Kills < bl_RoomSettings.Instance.GameGoal) bs.Kills++;
			//-bs.Score += GameSettings.Instance.ScoreReward.ScorePerKill;
		}
		else if (cmd == 1)//death
		{
			bs.Deaths++;
			bs.GameState = BotGameState.Death;
		}
		else if (cmd == 6)//Assists
		{
			bs.Assists++;
		}
		else if (cmd == 2)//remove bot
		{
			RemoveBotInfo(data);
		}
		else if (cmd == 3)//update view id
		{
			bs.ViewID = value;
			OnBotStatUpdate?.Invoke(bs);
		}
		else if (cmd == 4)//replace bot slot with a player
		{
			string[] dataSplit = data.Split('|');
			var list = int.Parse(dataSplit[0]) == 1 ? Team1PlayersSlots : Team2PlayersSlots;
			if (list.Count <= 0) { Debug.LogWarning("Team slots has not been setup yet."); return; }
			list[value].Player = dataSplit[1];
			list[value].Bot = "";
		}
		else if (cmd == 5)//add single new bot
		{
			string[] dataSplit = data.Split('|');
			Team team = (Team)int.Parse(dataSplit[0]);
			var list = team == Team.Team2 ? Team2PlayersSlots : Team1PlayersSlots;
			if (list.Count <= 0) { Debug.LogWarning("Team slots has not been setup yet."); return; }
			list[value].Player = "";
			list[value].Bot = dataSplit[1];

			//add the bot statistic
			bs = new MFPSBotProperties();
			bs.Name = dataSplit[1];
			bs.Team = team;
			bs.ViewID = int.Parse(dataSplit[2]);
			BotsStatistics.Add(bs);
		}

		bl_EventHandler.onBotSyncCmd?.Invoke(cmd);
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="data"></param>
	/// <param name="cmd"></param>
	[PunRPC]
	void SyncAllBotsStats(string data, int cmd)
	{
		if(_isBeingDestroyed) { return; }

		DebugEx.Log($"[bl_AIManager] Test001 SyncAllBotsStats({(cmd == 0 ? "BOTDATA" : "SLOTDATA")}): " + data);
		if (cmd == 0)//bots statistics
		{
			BotsStatistics.Clear();
			string[] split = data.Split("|"[0]);
			for (int i = 0; i < split.Length; i++)
			{
				if (string.IsNullOrEmpty(split[i])) continue;
				string[] info = split[i].Split(","[0]);
				MFPSBotProperties bs = new MFPSBotProperties();
				bs.Name = info[0];
				bs.Kills = int.Parse(info[1]);
				bs.Deaths = int.Parse(info[2]);
				bs.Assists = int.Parse(info[3]);
				bs.Team = (Team)int.Parse(info[4]);
				bs.ViewID = int.Parse(info[5]);
				bs.GameState = (BotGameState)byte.Parse(info[6]);
				BotsStatistics.Add(bs);
				
				if (!BattleManager.Instance.OtherPlayers.Exists(x => x.NickName == bs.Name))
				{
					DebugEx.Log($"[bl_AIManager] Test001 syncAllBotsStats() 봇 추가 {bs.Name}", LogColorType.Yellow);
					BattleManager.Instance.OtherPlayers.Add(new PlayerSeat()
					{
						NickName = bs.Name,
						IsRealPlayer = false,
						Team = bs.Team,
						IsAlive = bs.GameState == BotGameState.Playing,
						IsMonster = false // 몬스터로써의 Bot인지 여부는 아직 오프라인에서만 사용되니 이곳에서는 무시.
					}); ;
				}
				
			}
			OnMaterStatsReceived?.Invoke(BotsStatistics);
			HasMasterInfo = true;
		}
		else if (cmd == 1)//team slots info
		{
			SetUpSlots(false);
			string[] teams = data.Split('&');
			string[] teamInfo = teams[0].Split('|');//get the first team slots
			for (int i = 0; i < teamInfo.Length; i++)
			{
				if (string.IsNullOrEmpty(teamInfo[i])) continue;

				string[] slot = teamInfo[i].Split(',');
				Team1PlayersSlots[i].Player = slot[0];
				Team1PlayersSlots[i].Bot = slot[1];
			}
			if (!isOneTeamMode)
			{
				teamInfo = teams[1].Split('|');//get the second team slots
				for (int i = 0; i < teamInfo.Length; i++)
				{
					if (string.IsNullOrEmpty(teamInfo[i])) continue;

					string[] slot = teamInfo[i].Split(',');
					Team2PlayersSlots[i].Player = slot[0];
					Team2PlayersSlots[i].Bot = slot[1];
				}
			}
			bl_EventHandler.onBotsInitializated?.Invoke();
		}

		if(cmd == 1)
		{
			AllBotsStatsSyncDone = true;
			DebugEx.Log($"[bl_AIManager] AllBotsStatsSyncDone = true, " + GetInstanceID());

			_firstSyncBotsToOthers = true;
		}
	}

	void UpdateBotStatistics(string botName)
	{
		
	}
	
	/// <summary>
	/// 
	/// </summary>
	void RemoveBotInfo(string botName)
	{
		int bi = BotsStatistics.FindIndex(x => x.Name == botName);
		if (bi != -1) BotsStatistics.RemoveAt(bi);

		bi = BattleManager.Instance.OtherPlayers.FindIndex(x => x.NickName == botName);
		if (bi != -1)
		{
			DebugEx.Log($"[bl_AIManager] Test001 removeBotInfo() 봇 제거 {botName}", LogColorType.Yellow);
			BattleManager.Instance.OtherPlayers.RemoveAt(bi);
		}

		lastLifeBots.Add(botName);
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="Name"></param>
	void DeleteBot(string Name)
	{
		if (BotsStatistics.Exists(x => x.Name == Name))
		{
			photonView.RPC(nameof(SyncBotStat), RpcTarget.All, Name, 0, (byte)2);
		}
	}

	/// <summary>
	/// 
	/// </summary>
	private void OnRemotePlayerChange(bl_EventHandler.PlayerChangeData changeData)
	{
		UpdateTargetList();
	}

	/// <summary>
	/// 
	/// </summary>
	private void OnLocalDeath()
	{
		UpdateTargetList();
	}

	/// <summary>
	/// 
	/// </summary>
	private void OnLocalPlayerSpawn()
	{
		UpdateTargetList();
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="team"></param>
	/// <returns></returns>
	public List<PlayerSeat> GetAllBotsInTeam(Team team)
	{
		List<PlayerSeat> list = new List<PlayerSeat>();
		for (int i = 0; i < BattleManager.Instance.OtherPlayers.Count; i++)
		{
			if (BattleManager.Instance.OtherPlayers[i].IsRealPlayer) continue;

			if (BattleManager.Instance.OtherPlayers[i].Team == team)
			{
				list.Add(BattleManager.Instance.OtherPlayers[i]);
			}
		}
		return list;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <returns></returns>
	public List<Transform> GetOtherBots(Transform bot, Team _team)
	{
		List<Transform> all = new List<Transform>();
		if (isOneTeamMode)
		{
			all.AddRange(AllBotsTransforms);
			for (int i = 0; i < all.Count; i++)
			{
				if (all[i] == null) continue;
				if (all[i].transform.root.name.Contains("(die)") || all[i].transform.root == bot.root)
				{
					all.RemoveAt(i);
				}
			}
		}
		else //if TDM game mode
		{
			for (int i = 0; i < AllBotsTransforms.Count; i++)
			{
				if (AllBotsTransforms[i] == null) continue;

				Transform t = AllBotsTransforms[i].root;
				bl_AIShooter asa = t.GetComponent<bl_AIShooter>();
				if (t.name.Contains("(die)") || asa.isDeath) continue;

				if (asa.AITeam != _team && asa.AITeam != Team.None && t != bot.root)
				{
					all.Add(AllBotsTransforms[i]);
				}
			}
		}
		if (all.Contains(bot))
		{
			all.Remove(bot);
		}
		return all;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="botName"></param>
	/// <returns></returns>
	public MFPSBotProperties GetBotStatistics(string botName)
	{
		var id = BotsStatistics.FindIndex(x => x.Name == botName);
		if (id < 0) return null;
		return BotsStatistics[id];
	}

	/// <summary>
	/// Count the empty slots (not occupied by a real player)
	/// </summary>
	/// <returns></returns>
	public int EmptySlotsCount(Team team)
	{
		int count = 0;
		var list = team == Team.Team2 ? Team2PlayersSlots : Team1PlayersSlots;
		for (int i = 0; i < list.Count; i++)
		{
			if (string.IsNullOrEmpty(list[i].Player)) count++;
		}
		return count;
	}

	/// <summary>
	/// 
	/// </summary>
	private bool hasSpaceInTeam(Team team)
	{
		if (team == Team.Team2)
		{
			return Team2PlayersSlots.Exists(x => x.Player == string.Empty);
		}
		else
		{
			return Team1PlayersSlots.Exists(x => x.Player == string.Empty);
		}
	}

	/// <summary>
	/// 
	/// </summary>
	private bool hasSpaceInTeamForBot(Team team)
	{
		if (team == Team.Team2)
		{
			return Team2PlayersSlots.Exists(x => x.Player == string.Empty && x.Bot == string.Empty);
		}
		else
		{
			return Team1PlayersSlots.Exists(x => x.Player == string.Empty && x.Bot == string.Empty);
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public bl_AIShooter GetBot(int viewID)
	{
		foreach (bl_AIShooter agent in AllBots)
		{
			if (agent.photonView.ViewID == viewID)
			{
				return agent;
			}
		}
		return null;
	}

	/// <summary>
	/// 봇 또는 플레이어와 이름 중복이 있는지 여부
	/// </summary>
	public bool IsNameDuplicated(string name, Player exceptPlayer = null)
	{
		foreach (var agent in AllBots)
		{
			if (agent.AIName == name)
			{
				return true;
			}
		}
		foreach (var user in PhotonNetwork.PlayerList)
		{
			if(exceptPlayer != null && exceptPlayer == user)
			{
				continue;
			}

			if (user.NickName == name)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// 개발자 테스트 용
	/// </summary>
	public void AddDummyBotStats(string nickName, Team team, int k, int d, int a)
	{
		var bs = new MFPSBotProperties();
		bs.Name = nickName;
		bs.Team = team;
		bs.Kills = k;
		bs.Deaths = d;
		bs.Assists = a;
		BotsStatistics.Add(bs);
	}
	
	//폭탄 모드, 팀 데스인 경우 Key : Team1, Team2
	//Value의 Id는 GroupId
	MainGroupPath GroupPaths = new();
	GroupType SelectedGroupType = GroupType.ALL_ONE;
	
	public enum GroupType
	{
		TWO_Three = 0,		//2:3 비율
		TWO_TWO_ONE = 1,	//2:2:1 비율
		ONE_FOUR = 2,		//1:4 비율
		ALL_ONE = 3			//1:1:1:1:1 비율
	}
	
	public enum InitType
	{
		FirstSpawn = 0,
		Respawn = 1,
		MasterClientChanged = 2
	}
	
	//그룹을 어떻게 나눌지 결정한다.
	//DecideBotGroupIDs 함수를 호출하기 전에 호출
	private void DecideBotGroups()
	{
		int resultGroupIndex = _cpMgr.AiGroupPathSettings.GetRandomGroupIndex();
		SelectedGroupType = (GroupType)resultGroupIndex;
	}

	//결정된 그룹(SelectedGroupType)별로 봇을 할당한다. [GroupID 부여]
	//시작 전 봇이 모두 소환된 시점에 호출
	public void DecideBotGroupIDs()
	{
		DecideBotGroups();

		int[] groupMaxNums = Array.Empty<int>();
		switch (SelectedGroupType)
		{
			case GroupType.TWO_Three:
				groupMaxNums = new int[] { 2, 3 };
				break;
			case GroupType.TWO_TWO_ONE:
				groupMaxNums = new int[] { 2, 2, 1 };
				break;
			case GroupType.ONE_FOUR:
				groupMaxNums = new int[] { 1, 4 };
				break;
			case GroupType.ALL_ONE:
				groupMaxNums = new int[] { 1, 1, 1, 1, 1 };
				break;
		}
		
		if (GetGameMode == BattleMode.FFA)
		{
			for (int i = 0; i < AllBots.Count; i++) { AllBots[i].GroupID = -1; }
		}
		else
		{
			List<bl_AIShooter> team1Bots = AllBots.FindAll(x => x.AITeam == Team.Team1);
			List<bl_AIShooter> team2Bots = AllBots.FindAll(x => x.AITeam == Team.Team2);

			{
				int groupIndex = 0;
				int groupMaxNumArrLength = groupMaxNums.Length;
				int groupBotNum = 0;
				for (int i = 0; i < team1Bots.Count; i++)
				{
					if (groupIndex < groupMaxNumArrLength - 1)
					{
						if (groupBotNum == groupMaxNums[groupIndex])
						{
							groupBotNum = 0;
							groupIndex++;
						}
						team1Bots[i].GroupID = groupIndex;
					}
					//남은 봇을 모두 마지막 그룹에 넣는다.
					else
					{
						team1Bots[i].GroupID = groupIndex;
					}

					groupBotNum++;
				}
			}
		
			{
				int groupIndex = 0;
				int groupMaxNumArrLength = groupMaxNums.Length;
				int groupBotNum = 0;
				
				for (int i = 0; i < team2Bots.Count; i++)
				{
					if (groupIndex < groupMaxNumArrLength - 1)
					{
						if (groupBotNum == groupMaxNums[groupIndex])
						{
							groupBotNum = 0;
							groupIndex++;
						}
						team2Bots[i].GroupID = groupIndex;
					}
					//남은 봇을 모두 마지막 그룹에 넣는다.
					else
					{
						team2Bots[i].GroupID = groupIndex;
					}
					
					groupBotNum++;
				}
			}	
		}
	}

	public void UpdateBombAssignerPath(Team team, int groupID, Vector3 begin, Vector3 end)
	{
		List<Vector3> points = GenerateBombAssignerWayPoint(begin, end);
		UpdateGroupPath(team, groupID, points);
	}

	public void SetAllBotsToBombArea(Vector3 bombPosition)
	{
		foreach (bl_AIShooter terroristBot in AllBots)
		{
			bl_AIShooterAgent shooterAgent = (bl_AIShooterAgent)terroristBot;
			shooterAgent.CurrentState = new TargetAreaSearching(shooterAgent);
			
			List<Vector3> newWayPoints = new List<Vector3>();
			
			newWayPoints.Add(bombPosition);
			shooterAgent.UpdateWayPoints(newWayPoints);
		}
	}

	public bl_AIShooter GetClosestBotFromBombInstalledZone(Team team)
	{
		var allBotsInTeam = AllBots.FindAll(x => x.AITeam == team);
		float closestDistance = float.MaxValue;
		if(DemolitionMode.Instance.PlantingZone == null) return null;
		
		Vector3 bombZoneLocation = DemolitionMode.Instance.PlantingZone.transform.position;
		bl_AIShooter targetBot = allBotsInTeam.FirstOrDefault();
		
		foreach (bl_AIShooter botInTeam in allBotsInTeam)
		{
			if (!botInTeam.isDeath)
			{
				float distanceToBombZone = Vector3.Distance(bombZoneLocation, botInTeam.transform.position);
				if (distanceToBombZone < closestDistance)
				{
					targetBot = botInTeam;
				}
			}
		}

		return targetBot;
	}

	public bool IsBombAssignerGroup(bl_AIShooterAgent shooterAgent)
	{
		if (bombAssignedShooter == null) { return false; }
		return (bombAssignedShooter.GroupID == shooterAgent.GroupID) && 
		       (bombAssignedShooter.AITeam == shooterAgent.AITeam);
	}

	public void UpdateBombAssignerGroupPath(bl_AIShooter shooter)
	{
		DebugEx.Log($"[bl_AIManager] UpdateBombAssignerGroupPath AllBots Number : {AllBots.Count}");
		bombAssignedShooter = shooter;

		List<bl_AIShooter> allTeamBots = AllBots.FindAll(x => x.AITeam == shooter.AITeam);
		List<bl_AIShooter> bombAssignedGroup = allTeamBots.FindAll(x => x.GroupID == bombAssignedShooter.GroupID);
		
		foreach (bl_AIShooter targetBot in bombAssignedGroup)
		{
			bl_AIShooterAgent shooterAgent = (bl_AIShooterAgent)targetBot;
			shooterAgent.CurrentState = new TargetAreaSearching(shooterAgent);
			shooterAgent.UpdateWayPoints(GetGroupPath(bombAssignedShooter.AITeam, bombAssignedShooter.GroupID));
		}
	}

	public bool IsBombAssignedGroup(bl_AIShooter shooter)
	{
		if(bombAssignedShooter == null) { return false; }
		return bombAssignedShooter.GroupID == shooter.GroupID;
	}

	public DemolitionBombZone GetRandomDemolitionZone()
	{
		DemolitionBombManager demolitionBombManager = DemolitionBombManager.Instance;

		DemolitionBombZone[] demolitionBombZones = demolitionBombManager.GetAllDemolitionZones();
		if (demolitionBombZones == null)
		{
			DebugEx.AILog("[bl_AIManager] GetRandomDemolitionZone() demolitionBombZones을 찾을 수 없습니다!", LogColorType.Red);
			return null;
		}

		int rndIndex = Random.Range(0, demolitionBombZones.Length);
		
		//test code
		// rndIndex = 1;
		
		return demolitionBombZones[rndIndex];
	}

	public void SetTargetBombZone(DemolitionBombZone targetBombZone)
	{
		this.targetBombZone = targetBombZone;
	}

	public Vector3? GetTargetBombZone()
	{
		if(targetBombZone == null) { return null; }
		return targetBombZone.transform.position;
	}

	public bool IsCloseToBombInstall(Vector3 current)
	{
		if(targetBombZone == null) { return false; }

		float bombZoneRadius = targetBombZone.GetComponent<SphereCollider>().radius;
		float distanceCurrentToBomb = Vector3.Distance(current, targetBombZone.transform.position);
		// Debug.Log("distanceCurrentToBomb : " + distanceCurrentToBomb);
		return distanceCurrentToBomb < bombZoneRadius;
	}
	
	//그룹의 경로를 갱신한다.
	//GeneratePathBeginToEndWithEssentialWayPoint 함수 등을 통해 경로를 만들어 전달하면 된다.
	public void UpdateGroupPath(Team team, int groupID, List<Vector3> points)
	{
		string key = team.ToString();
		if (!GroupPaths.Data.ContainsKey(key)) { GroupPaths.Data.Add(key, new SubGroupPath()); }
		GroupPaths.Data[key].Data[groupID] = points;
	}

	public List<Vector3> GetMyPath(Team team, int groupID, Vector3 begin)
	{
		List<Vector3> resultPoints = new List<Vector3>();

		List<Vector3> groupPoints = GetGroupPath(team, groupID);
		if (groupPoints == null)
		{
			groupPoints = new List<Vector3>();
			groupPoints.Add(begin);
		}
		else { resultPoints.AddRange(groupPoints); }
		
		return resultPoints;
	}
	
	//내 위치에서 역주행을 하지 않으면서 현재 위치에서 Group 경로를 따라가는 길을 찾는다.
	public List<Vector3> GetGroupPath(Team team, int groupID)
	{
		string key = team.ToString();
		if (GroupPaths.Data.ContainsKey(key) && GroupPaths.Data[key].Data.ContainsKey(groupID))
		{
			return GroupPaths.Data[key].Data[groupID];
		}
		return null;
	}

	public List<Vector3> GeneratePathStartPointToEndWithEssentialWayPoint(Team team, AIWayPoint endWayPoint)
	{
		List<Vector3> path = new List<Vector3>();

		AIWayPoint beginWayPoint = GetRandomStartingEssentialWayPoints(team);
		
		List<AIWayPoint> middlePaths = new List<AIWayPoint>();
		middlePaths.Add(beginWayPoint);
		
		GenerateEssentialWayPointsToDestination(team, beginWayPoint, endWayPoint, ref middlePaths);
		
		// Debug.Log("Case 301 begin.........");
		foreach (AIWayPoint middlePath in middlePaths)
		{
			// Debug.Log("Case 301 : " + middlePath.name);
			path.Add(middlePath.transform.position);
		}
		// Debug.Log("Case 301 end.........");
		return path;
	}

	public List<Vector3> GenerateBombAssignerWayPoint(Vector3 begin, Vector3 end)
	{
		List<Vector3> path = new List<Vector3>();
		path.Add(begin);
		path.Add(end);
		
		return path;
	}

	//Essential WayPoint들을 활용하여 현재 위치(begin)부터 목적지(end)까지 가는 경로 만들기
	public List<Vector3> GeneratePathBeginToEndWithEssentialWayPoint(Vector3 begin, Vector3 end)
	{
		List<Vector3> path = new List<Vector3>();
		path.Add(begin);
		
		//현재 위치에서 가장 가까운 WayPoint를 찾는다.
		AIWayPoint closestWayPoint = GetClosestEssentialWayPoint(begin);
		AIWayPoint closestEndWayPoint = GetClosestEssentialWayPoint(end);
		
		List<AIWayPoint> middlePaths = new List<AIWayPoint>();
		middlePaths.Add(closestWayPoint);
		
		GenerateEssentialWayPointsToDestination(Team.None, closestWayPoint, closestEndWayPoint, ref middlePaths);
		
		foreach (AIWayPoint middlePath in middlePaths)
		{
			path.Add(middlePath.transform.position);
		}
		
		path.Add(end);
		
		return path;
	}

	public AIWayPoint GetRandomNormalWayPoint()
	{
		if(normalWayPoints.Count == 0) return null;
		int randIndex = Random.Range(0, normalWayPoints.Count);

		return normalWayPoints[randIndex];
	}

	/*-- CHECKIT(25.7.30) 안쓰는 경우 제거
	// 이름 재사용을 위해 안쓰고 있는 봇 이름을 찾는다.
	string FindReusableBotName(Team team)
	{
		var slotList = team == Team.Team2 ? Team2PlayersSlots : Team1PlayersSlots;

		foreach (var botStat in BotsStatistics)
		{
			if(botStat.Team != team) { continue; }

			bool isUsing = slotList.Any(slot => slot.Bot == botStat.Name);
			if(!isUsing)
			{
				return botStat.Name;
			}
		}
		return null;
	}
	--*/

	#region SubClasses

	public class MainGroupPath
	{
		public Dictionary<string, SubGroupPath> Data = new();
	}
	
	public class SubGroupPath
	{
		public Dictionary<int, List<Vector3>> Data = new();
	}
	
	/// <summary>
	/// 
	/// </summary>
	/// <returns></returns>
	public MFPSBotProperties GetBotWithMoreKills()
	{
		if (BotsStatistics == null || BotsStatistics.Count <= 0)
		{
			MFPSBotProperties bs = new MFPSBotProperties()
			{
				Name = "None",
				Kills = 0,
				Team = Team.None,
				//--Score = 0,
			};
			return bs;
		}
		int high = 0;
		int id = 0;
		for (int i = 0; i < BotsStatistics.Count; i++)
		{
			if (BotsStatistics[i].Kills > high)
			{
				high = BotsStatistics[i].Kills;
				id = i;
			}
		}
		return BotsStatistics[id];
	}

	[System.Serializable]
	public class PlayersSlots
	{
		public string Player;
		public string Bot;
	}
	#endregion

	private static bl_AIManager _instance;
	public static bl_AIManager Instance
	{
		get
		{
			if (_instance == null) { _instance = FindFirstObjectByType<bl_AIManager>(); }
			return _instance;
		}
	}
}