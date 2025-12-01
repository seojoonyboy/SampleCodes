using System.Collections;
using UnityEngine;
using Photon.Pun;
using HashTable = ExitGames.Client.Photon.Hashtable;
using Game.View.UI;
using static Game.View.BattleSystem.DemolitionMode;
using Framework;
using Photon.Realtime;
using System.Text;
using System.Collections.Generic;
using System;
using Game.Data;
using Framework.UI;
using System.Linq;
using UnityEngine.Serialization;

namespace Game.View.BattleSystem
{
	public class DemolitionBombManager : bl_PhotonHelper
	{
		[Header("References")]
		[FormerlySerializedAs("Bomb")]
		[SerializeField] DemolitionBomb _bomb;
		[SerializeField] GameObject ExplosionPrefab;

		DemolitionBomb _bombInstance;
		int _bombInstantiatedCount = 0;
		Coroutine _plantOrDefuseCR;

		public DemolitionBomb Bomb
		{
			get
			{
				if (_bombInstance == null)
				{
					if (_bombInstantiatedCount > 100)
					{
						throw new GameException("Bomb too many instantiated.");
					}

					if (_bombInstantiatedCount > 0)
					{
						CrashlyticsUtil.LogException(new GameException("Bomb is null. Recreating count: " + _bombInstantiatedCount));
					}
					_bombInstantiatedCount++;
					_bombInstance = Instantiate(_bomb);
					_bombInstance.SetActiveGo(true);
				}

				return _bombInstance;
			}
		}

		private DemolitionBombZone _installedBombZone;

		public DemolitionBombZone BombInstalledZone
		{
			get
			{
				if (_installedBombZone != null)
				{
					return _installedBombZone;
				}

				DebugEx.Log($"폭탄 설치가 되지 않았는데 호출 되었습니다!!!", LogColorType.Red);
				return GetRandomDemolitionZone();
			}
		}

		private static DemolitionBombManager _instance;
		public static DemolitionBombManager Instance
		{
			get
			{
				if (_instance == null) { _instance = FindFirstObjectByType<DemolitionBombManager>(); }
				return _instance;
			}
		}

		public bool isLocalPlayerTheCarrier => Carrier == BattleManager.Instance.LocalPlayerSeat;

		public bool IsLocalTeamTheCarrier => Carrier?.Team == BattleManager.Instance.LocalPlayerSeat.Team;

		public bool canLocalPlant { get { return DemolitionMode.Instance.IsLocalInZone && isLocalPlayerTheCarrier; } }

		public float getFinalCountPercentage { get { if (detonationTime > 0) { return detonationTime / DemolitionMode.Instance.DetonationTime; } else { return 0; } } }


		//public int carrierActorNumber { get; set; }

		// BOT과 함께 테스트를 위해 ActorNumer대신 NickName을 사용.
		public string CarrierNickName => (Carrier != null) ? Carrier.NickName : null;

		public PlayerSeat Carrier { get; private set; }

		public bool isPlating = false;
		public bool isDefusing = false;

		public float detonationTime { get; set; }

		public enum CancelOpCause
		{
			PlayerDeath,
			PlayerInput,
			GameReset
		}

		void Awake()
		{
			_bomb.SetActiveGo(false);
		}

		private void OnEnable()
		{
			PhotonNetworkEx.Instance.AddCallback(PropertiesKeys.DMBombEvent, this.OnEventReceived);
		}

		private void OnDisable()
		{
			PhotonNetworkEx.Instance.ncop()?.RemoveCallback(this.OnEventReceived);
		}
		/// <summary>
		/// 
		/// </summary>
		private void Update()
		{
			InputControl();
		}

		public void SyncBombStatus()
		{
			if (BattleManager.Instance.GameState == BattleStates.Playing)
			{
				LoadBombInfoFromRoom();
			}
		}

		public void SetCarrier(PlayerSeat player)
		{
			Carrier = player;
		}
		public void ClearCarrier()
		{
			Carrier = null;
		}

		public DemolitionBombZone[] GetAllDemolitionZones()
		{
			return DemolitionMode.Instance.BombZoneParent
				.GetComponentsInChildren<DemolitionBombZone>()
				.Where(x => x.gameObject.activeSelf)
				.ToArray();
		}

		public DemolitionBombZone GetClosestDemolitionZone(Vector3 pos)
		{
			return GetAllDemolitionZones()
				.OrderBy(zone => Vector3.SqrMagnitude(zone.transform.position - pos))
				.FirstOrDefault();
		}

		public DemolitionBombZone GetRandomDemolitionZone()
		{
			DemolitionBombZone[] allZones = GetAllDemolitionZones();
			if (allZones.Length == 0) { return null; }
			
			int rndIndex = UnityEngine.Random.Range(0, allZones.Length);
			return allZones[rndIndex];
		}

		public string SetBombInstalledZone(Vector3 installedPosition)
		{
			var targetZone = GetClosestDemolitionZone(installedPosition);
			if (targetZone == null) { return String.Empty; }

			_installedBombZone = targetZone;
			return targetZone.ZoneName;
		}

		public bool IsBombVisibleToLocalPlayer()
		{
			var myTeam = bl_MFPS.LocalPlayer.Team;
			if (myTeam == Team.None)
			{
				return false;
			}

			bool isVisible = Bomb.bombStatus switch
			{
				BombStatus.Initial => false,
				BombStatus.Droped => true, // 드랍시 모든 팀에게.
				BombStatus.Carried => IsLocalTeamTheCarrier && !isLocalPlayerTheCarrier, // 나 아닌 아군이 소지시.
				BombStatus.Actived => myTeam == DemolitionMode.Instance.AttackTeam, // 설치된 경우 공격팀에게만.
				BombStatus.Defused or _ => false,
			};

			return isVisible;
		}


		/// <summary>
		/// 
		/// </summary>
		void InputControl()
		{
			if (bl_UtilityHelper.UseMobileControl) return;

			//if player is inside of a bomb zone and carries the bomb
			if (canLocalPlant && Bomb.isAvailableToPlant)
			{
				if (bl_GameInput.Interact(GameInputType.Hold))
				{
					bl_MFPS.LocalPlayerReferences.firstPersonController.SetCrouch(true);
				}

				//and him press the plant button
				if (bl_GameInput.Interact())
				{
					PlantBomb();
				}
				else if (bl_GameInput.Interact(GameInputType.Up))//if for some reason him is not keep pressing the plant button
				{
					bl_MFPS.LocalPlayerReferences.firstPersonController.SetCrouch(false);
					CancelPlant(CancelOpCause.PlayerInput);
				}
			}
			
			if (DemolitionMode.Instance.IsPlayerLookingAtBomb && Bomb.isAvailableToDefuse)
			{
				if (bl_GameInput.Interact())
				{
					DefuseBomb();
				}
				else if (bl_GameInput.Interact(GameInputType.Up))//if for some reason him is not keep pressing the plant button
				{
					CancelDefuse(CancelOpCause.PlayerInput);
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public void OnPlantButton(bool press)
		{
			if (!canLocalPlant || !Bomb.isAvailableToPlant) return;

			if (press) PlantBomb();
			else CancelPlant(CancelOpCause.PlayerInput);
		}

		/// <summary>
		/// 
		/// </summary>
		public void OnDefuseButton(bool press)
		{
			if (!DemolitionMode.Instance.IsPlayerLookingAtBomb || !Bomb.isAvailableToDefuse) return;

			if (press) DefuseBomb();
			else CancelDefuse(CancelOpCause.PlayerInput);
		}

		/// <summary>
		/// 
		/// </summary>
		public void StopAll()
		{
			StopAllCoroutines();
			CancelInvoke();
			
			CancelDefuse(CancelOpCause.GameReset);
			CancelPlant(CancelOpCause.GameReset);

			if (Bomb != null)
			{
				Bomb.bombEffects.SetActive(false);
				ResetBomb();
			}
			else
			{
				DebugEx.Log($"[Bomb] Logic Error!!! Bomb Instance is null");
			}
		}

		public void ResetBomb()
		{
			Bomb.ResetToInit();

			isPlating = false;
			isDefusing = false;

			if (PhotonNetwork.IsMasterClient)
			{
				SaveBombInfoAtRoom(null);
			}
		}

		public bool CanPlantBomb()
		{
			if (isPlating) return false;
			if (Bomb.bombStatus == BombStatus.Actived) return false;
			return true;
		}

		/// <param name="player">폭탄 설치를 하려는 Player</param>
		public void BotPlantBomb(PlayerSeat player, Action onPlantFinished)
		{
			if (Bomb.bombStatus == BombStatus.Actived) return;
			if (isPlating) return;

			StartCoroutine(DoBotPlant(player, onPlantFinished));
		}

		/// <summary>
		/// 
		/// </summary>
		public void PlantBomb()
		{
			DebugEx.Log($"[DemolitionBombManager] PlantBomb()");
			CrashlyticsUtil.AddActionLog($"[Battle] PlantBomb");
			_plantOrDefuseCR = StartCoroutine(nameof(DoPlant));
			DemolitionModeUi.Instance.UpdateProgress(0);
			DemolitionModeUi.Instance.ProgressUi.SetActive(true);

			//here you can replace the BlockAllWeapons() with your custom code in order to show a bomb activation hand animation instead of just hide the weapons.	  
			bl_MFPS.LocalPlayerReferences.gunManager.BlockAllWeapons();
			bl_MFPS.LocalPlayerReferences.firstPersonController.isControlable = false;
		}

		public void BotDefuseBomb(PlayerSeat playerSeat)
		{
			if (isDefusing) return;

			StartCoroutine(DoBotDefuse(playerSeat));
		}

		/// <summary>
		/// 
		/// </summary>
		public void DefuseBomb()
		{
			DebugEx.Log($"[DemolitionBombManager] DefuseBomb()");
			CrashlyticsUtil.AddActionLog($"[Battle] DefuseBomb");

			_plantOrDefuseCR = StartCoroutine(nameof(DoDefuse));
			DemolitionModeUi.Instance.UpdateProgress(1);
			DemolitionModeUi.Instance.ProgressUi.SetActive(true);

			//here you can replace the BlockAllWeapons() with your custom code in order to show a bomb activation hand animation instead of just hide the weapons.
			BattleManager.Instance.LocalActor.GetComponent<bl_PlayerReferences>().gunManager.BlockAllWeapons();
			bl_MFPS.LocalPlayerReferences.firstPersonController.isControlable = false;
		}

		/// <summary>
		/// 
		/// </summary>
		public void CancelPlant(CancelOpCause cancelOpCause)
		{
			StopCoroutine(nameof(DoPlant));
			_plantOrDefuseCR = null;
			DemolitionModeUi.Instance.ProgressUi.SetActive(false);

			if (cancelOpCause != CancelOpCause.PlayerDeath)
			{
				if (cancelOpCause != CancelOpCause.GameReset && bl_MFPS.LocalPlayerReferences != null)
				{
					bl_MFPS.LocalPlayerReferences.gunManager.ReleaseWeapons(true);
					bl_MFPS.LocalPlayerReferences.firstPersonController.isControlable = true;
				}

				if (cancelOpCause != CancelOpCause.GameReset && DemolitionMode.Instance.IsLocalInZone)
				{
					DemolitionModeUi.Instance.ShowPlantGuide(true);
				}
			}
		}

		//폭탄 설치를 취소한다.
		public void BotCancelPlantBomb(PlayerSeat player)
		{
			CrashlyticsUtil.AddActionLog($"[Battle] BotCancelPlantBomb");
			StopCoroutine(nameof(BotPlantBomb));
			isPlating = false;
		}

		//폭탄 해체를 취소한다.
		public void BotCancelDefuseBomb()
		{
			CrashlyticsUtil.AddActionLog($"[Battle] BotCancelDefuseBomb");
			StopCoroutine(nameof(BotDefuseBomb));
			isDefusing = false;
		}

		/// <summary>
		/// 
		/// </summary>
		public void CancelDefuse(CancelOpCause cancelOpCause)
		{
			CrashlyticsUtil.AddActionLog($"[Battle] CancelDefuse({cancelOpCause})");
			StopCoroutine(nameof(DoDefuse));
			_plantOrDefuseCR = null;

			DemolitionModeUi.Instance.ProgressUi.SetActive(false);
			DemolitionModeUi.Instance.ShowDefuseGuide(false);
			if (cancelOpCause != CancelOpCause.PlayerDeath)
			{
				if (cancelOpCause != CancelOpCause.GameReset && bl_MFPS.LocalPlayerReferences != null)
				{
					bl_MFPS.LocalPlayerReferences.gunManager.ReleaseWeapons(true);
					bl_MFPS.LocalPlayerReferences.firstPersonController.isControlable = true;
				}
				if (DemolitionMode.Instance.IsPlayerLookingAtBomb) DemolitionModeUi.Instance.ShowDefuseGuide(true);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public void StartDetonationCountDown(bool start)
		{
			if (start) { StartCoroutine(nameof(DoDetonationCountDown)); }
			else { StopCoroutine(nameof(DoDetonationCountDown)); }
		}

		IEnumerator DoBotPlant(PlayerSeat player, Action callback)
		{
			isPlating = true;
			Vector3 botPos = player.Actor.position;

			float d = 0;
			while (d < 1)
			{
				//Bot 이 설치 도중 사망하는 경우 설치 취소
				if (!player.IsAlive)
				{
					BotCancelPlantBomb(player);
					yield break;
				}

				d += Time.deltaTime / DemolitionMode.Instance.plantDuration;
				yield return null;
			}

			callback?.Invoke();

			string zoneName = SetBombInstalledZone(botPos);
			Bomb.OnBotPlant(botPos, zoneName);

			isPlating = false;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		IEnumerator DoPlant()
		{
			isPlating = true;

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

			string bombZoneName = SetBombInstalledZone(
				bl_MFPS.LocalPlayerReferences.transform.position);
			
			Bomb.OnLocalPlant(bombZoneName);
			
			isPlating = false;
			_plantOrDefuseCR = null;
		}

		IEnumerator DoBotDefuse(PlayerSeat player)
		{
			CrashlyticsUtil.AddActionLog($"[Battle] Bot Dufuse ({player.NickName}");

			isDefusing = true;

			float d = 0;
			while (d < 1)
			{
				if (!player.IsAlive)
				{
					BotCancelDefuseBomb();
					yield break;
				}
				d += Time.deltaTime / DemolitionMode.Instance.defuseDuration;
				yield return null;
			}

			Bomb.OnBotDefuse(player);

			isDefusing = false;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		IEnumerator DoDefuse()
		{
			isDefusing = true;

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
			ResetBomb();
			DemolitionModeUi.Instance.ProgressUi.SetActive(false);
			DemolitionModeUi.Instance.ShowPlantGuide(false);
			bl_MFPS.LocalPlayerReferences.firstPersonController.isControlable = true;
			bl_MFPS.LocalPlayerReferences.gunManager.ReleaseWeapons(true);
			//send defuse event
			Bomb.OnLocalDefuse();

			isDefusing = false;
			_plantOrDefuseCR = null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public IEnumerator DoDetonationCountDown()
		{
			while (true)
			{
				detonationTime = DemolitionMode.Instance.DetonationTime - ((float)PhotonNetworkEx.TimeEx - Bomb.plantedTime);
				if (detonationTime <= 0)
				{
					Bomb.bombEffects.SetActive(false);
					DemolitionModeUi.Instance.OnPlantUi.SetActive(false);

					StartCoroutine(ExplosionSequence(Bomb.transform.position));

					ResetBomb();

					yield return new WaitForSeconds(1);

					if (PhotonNetworkEx.IsMasterClient)
					{
						//send the round finish event and let others know who win
						PhotonNetworkEx.Instance.SendDataOverNetwork(PropertiesKeys.DemolitionEvent, new HashTable()
						{
						{"type", DemolitionMode.DemolitionEventType.RoundFinish },
						{"cause", DMRoundFinishCauses.BombPlant },
						{"winner",DemolitionMode.Instance.AttackTeam }
						});
					}
					//
					yield break;
				}
				yield return null;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		IEnumerator ExplosionSequence(Vector3 position)
		{
			//change the 3 for the number of explosion that you want
			for (int i = 0; i < 3; i++)
			{
				//select a random position around the bomb to instance the explosion effect
				Vector3 rp = position + (UnityEngine.Random.insideUnitSphere * 5);
				rp.y = position.y;
				Instantiate(ExplosionPrefab, rp, Quaternion.identity);
				yield return new WaitForSeconds(0.5f);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public void OnSelectPlayerToCarrieBomb(HashTable data)
		{
			Bomb.PickUp(data);

			if (PhotonNetwork.IsMasterClient)
			{
				HashTable copiedData = new HashTable(data.Count);
				foreach (var item in data) 
				{
					copiedData.Add(item.Key, item.Value);
				}

				copiedData["type"] = (int)BombStatus.Carried;
				SaveBombInfoAtRoom(copiedData);
			}
		}

		public void OnBotDeath(string nickName)
		{
			if (CarrierNickName == nickName)
			{
				var bot = BattleManager.Instance.FindMFPSPlayerByNickname(nickName);
				if (bot != null && bot.Actor)
				{
					DropBomb(bot.Actor.transform.position);
				}
			}
		}

		/// <summary>
		/// 각 클라이언트의 Local Player 자신이 사망할 때. 
		/// 직접 폭탄을 바닥에 내려 놓는다.
		/// </summary>
		public void OnLocalDeath()
		{
			if(_plantOrDefuseCR != null)
			{
				StopCoroutine(_plantOrDefuseCR);
				_plantOrDefuseCR = null;
			}

			if (CarrierNickName == PhotonNetworkEx.LocalPlayer.NickName)
			{
				Vector3 playerPos = BattleManager.Instance.LocalActor.transform.position;
				DropBomb(playerPos);

			}
		}

		public void OnOtherPlayerEnter(Player newPlayer)
		{
		}

		public void OnOtherPlayerLeave(Player player)
		{
			if (CarrierNickName == player.NickName)
			{
				var mfpsplayer = BattleManager.Instance.FindMFPSPlayerByNickname(player.NickName);
				if (mfpsplayer != null && mfpsplayer.Actor)
				{
					ClearCarrier();
					DropBomb(mfpsplayer.Actor.transform.position);
				}
			}
		}

		/// <summary>
		/// 안전장치. 플레이어나 봇이 Disable될 때 Bomb을 소지하고 있다면 바닥에 Drop한다.
		/// </summary>
		public void TryRecoverBomb(GameObject actor)
		{
			if (FrameworkApp.IsApplicationQuiting || SceneManager.IsSceneDestroying()) { return; }
			if(!enabled) { return; }

			if (IsBombParent(actor))
			{
				DebugEx.Log($"RecoverBomb '{actor.name}'");
				ClearCarrier();
				DropBomb(actor.transform.position);
			}
		}

		// 예외적인 상황을 트래킹하기 위함. 절대 호출되면 안된다. 
		// Player프리팹이 파괴될 때 Bomb을 지니고 있으면 Bomb도 같이 삭제되는 불상사를 미리 방지하기 위한 체크.
		public void TryDetechBombFromPlayer(string nickName)
		{
			if (CarrierNickName != nickName)
			{
				return; // 문제없음.
			}

			DebugEx.Error($"Logic Error! Bomb almost destroyed with player '{nickName}'");

			// 일단 분리.
			if (CarrierNickName == nickName)
			{
				Bomb.transform.parent = null;
			}

			var player = BattleManager.Instance.FindMFPSPlayerByNickname(nickName);
			if (player != null && player.Actor)
			{
				DropBomb(player.Actor.transform.position);
			}
		}

		// 플레이어가 지니고 있는 폭탄을 드랍시킨다.
		void DropBomb(Vector3 playerPos)
		{
			DebugEx.Log($"[DemolitionBombManager] DropBomb()");

			Vector3 dropPosition = playerPos;
			Quaternion rot = Quaternion.identity;
			RaycastHit hit;
			if (Physics.Raycast(playerPos, Vector3.down * 5, out hit, 5, Bomb.layerMask, QueryTriggerInteraction.Ignore))
			{
				dropPosition = hit.point;
				rot = Quaternion.LookRotation(hit.normal, Vector3.up);
			}
			//drop the bomb activator
			var data = bl_UtilityHelper.CreatePhotonHashTable();
			data.Add("type", (int)BombStatus.Droped);
			data.Add("position", dropPosition);
			data.Add("rotation", rot);
			PhotonNetworkEx.Instance.SendDataOverNetwork(PropertiesKeys.DMBombEvent, data);
			CancelPlant(CancelOpCause.PlayerDeath);
			CancelDefuse(CancelOpCause.PlayerDeath);
			Bomb.transform.parent = null;
		}

		public void OnEventReceived(ExitGames.Client.Photon.Hashtable data)
		{
			BombStatus getStatus = (BombStatus)(int)data["type"];
			DebugEx.Log($"[DemolitionBombManager] OnEventReceived() - bombStatus:{getStatus}");

			if (getStatus == BombStatus.TryPickup)
			{
				// Pickup신호는 동시 다발적으로 발생할 수 있으므로 Master가 최종 결정해서 다시 Broadcast한다.
				if (PhotonNetwork.IsMasterClient && Bomb.bombStatus == BombStatus.Droped)
				{
					Bomb.PickUp(data);

					HashTable copiedData = new HashTable(data.Count);
					foreach (var item in data)
					{
						copiedData.Add(item.Key, item.Value);
					}
					copiedData["type"] = (int)BombStatus.Carried;
					PhotonNetworkEx.Instance.SendDataOverNetwork(PropertiesKeys.DMBombEvent, copiedData);
				}
			}
			else if (getStatus == BombStatus.Carried)
			{
				if (Bomb.bombStatus != BombStatus.Carried)
				{
					Bomb.PickUp(data);
				}
			}
			else if (getStatus == BombStatus.Droped)
			{
				Bomb.Drop(data);
			}
			else if (getStatus == BombStatus.Actived)
			{
				Bomb.Plant(data);
			}
			else if (getStatus == BombStatus.Defused)
			{
				Bomb.Defuse(data);
			}

			if (PhotonNetwork.IsMasterClient)
			{
				SaveBombInfoAtRoom(data);
			}
		}

		void LoadBombInfoFromRoom()
		{
			string value = PhotonNetworkEx.GetRoomCustomProperty(PropertiesKeys.BombInfo, "");
			if(!value.HasContent()) { return; }
			DebugEx.Log("[DemolitionBombManager] LoadBombInfoFromRoom() - " + value);

			var data = new ExitGames.Client.Photon.Hashtable();

			string[] args = value.Split("|");
			for (int i = 0; i < args.Length - 1; i += 2)
			{
				string key = args[i];
				string valueS = args[i + 1];

				// Value값이 비어있는 경우를 찾기 위한 Assert.
				if (valueS == string.Empty)
				{
					string erroMsg = "Wrong BombInfo: " + value;
					if (ApplicationInfo.IsDebug) { Navigator.ErrorToast(erroMsg); }
					CrashlyticsUtil.LogException(new Exception(erroMsg));
				}

				object valueO = key switch
				{
					"type" => valueO = int.Parse(valueS),
					"carrierID" or "viewID" => int.Parse(valueS),
					"nickName" => valueS,
					"position" => StringToVector3(valueS),
					"rotation" => StringToQuaternion(valueS),
					"time" => float.Parse(valueS),
					_ => valueS,
				};
				data.Add(key, valueO);
			}

			OnEventReceived(data);
		}

		void SaveBombInfoAtRoom(ExitGames.Client.Photon.Hashtable data)
		{
			if (!PhotonNetwork.IsMasterClient) { return; }

			StringBuilder sb = new StringBuilder();
			if (data?.HasContent() == true)
			{
				foreach (var kv in data)
				{
					string valueS = kv.Key switch {
						"position" => Vector3ToString((Vector3)kv.Value),
						"rotation" => QuaternionToString((Quaternion)kv.Value),
						_ => kv.Value.ToString(),
					};
					sb.Append($"{kv.Key}|{valueS}|");
				}
			}

			PhotonNetworkEx.SetRoomCustomProperty(PropertiesKeys.BombInfo, sb.ToString());


		}

		string Vector3ToString(Vector3 v)
		{
			return $"{v.x},{v.y},{v.z}";
		}
		string QuaternionToString(Quaternion v)
		{
			return $"{v.x},{v.y},{v.z},{v.w}";
		}
		Vector3 StringToVector3(string s)
		{
			string[] args = s.Split(',');

			return new Vector3(
				float.Parse(args[0]),
				float.Parse(args[1]),
				float.Parse(args[2]));
		}
		Quaternion StringToQuaternion(string s)
		{
			string[] args = s.Split(',');

			return new Quaternion(
				float.Parse(args[0]),
				float.Parse(args[1]),
				float.Parse(args[2]),
				float.Parse(args[3]));
		}

		bool IsBombParent(GameObject go)
		{
			if (go == null || Bomb == null) { return false; }

			Transform t = Bomb.transform.parent;
			while (t != null)
			{
				if (t.gameObject == go)
				{
					return true;
				}
				t = t.parent;
			}
			return false;
		}
	}
}