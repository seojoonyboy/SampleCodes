using Framework;
using Game.Data;
using Game.View;
using Game.View.BattleSystem;
using Game.View.UI;
using MFPS.Audio;
using MFPS.Runtime.AI;
using Photon.Pun;
using System;
using UnityEngine;

/// <summary>
/// Handle all relating to the bot health
/// This script has not direct references, you can replace with your own script
/// Simply make sure to inherit your script from <see cref="bl_PlayerHealthManagerBase"/>
/// </summary>
public class bl_AIShooterHealth : bl_PlayerHealthManagerBase
{
	[Range(10, 500)] public int Health = 100;

	#region Private members
	bl_AIShooter m_AIShooter;
	int LastActorEnemy = -1;
	RepetingDamageInfo repetingDamageInfo;
	bl_AIShooterReferences references;
	bl_AIShooter shooterAgent;
	bl_AIShooterAttack _shooterAttack;
	#endregion

	/// <summary>
	/// 스폰 직후의 무적타임 중인가
	/// </summary>
	public bool IsProtectionEnable;

	/// <summary>
	/// 
	/// </summary>
	protected override void Awake()
	{
		base.Awake();
		references = GetComponent<bl_AIShooterReferences>();
		m_AIShooter = references.aiShooter;
		shooterAgent = GetComponent<bl_AIShooter>();
		_shooterAttack = GetComponent<bl_AIShooterAttack>();
		CrashlyticsUtil.AddActionLog($"[Battle] Bot Awake. ({gameObject.name})");
	}

	protected override void OnDestroy()
	{
		CrashlyticsUtil.AddActionLog($"[Battle] Bot Destroy. ({gameObject.name})");
		base.OnDestroy();
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="dmgData"></param>
	public override void DoDamage(DamageData dmgData)
	{
		if (dmgData.MFPSActor == null)
		{
			Debug.Log($"DamageData - player not found. maybe left the match? (from:'{dmgData.From}' weaponCode:'{dmgData.WeaponCode}')");
			return;
		}

		if (this.IsProtectionEnable) return;

		DoDamage(dmgData.Damage, dmgData.WeaponCode, dmgData.Position, dmgData.Direction, dmgData.ActorViewID, !dmgData.MFPSActor.IsRealPlayer, dmgData.MFPSActor.Team, dmgData.isHeadShot);
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="damage"></param>
	public override void DoFallDamage(int damage)
	{
	}

	/// <summary>
	/// 
	/// </summary>
	public void DoDamage(int damage, WeaponCode weaponCode, Vector3 position, Vector3 direction, int viewID, bool fromBot, Team team, bool ishead)
	{
		if (m_AIShooter.isDeath)
			return;

		if (!isOneTeamMode)
		{
			if (/*--!bl_RoomSettings.Instance.CurrentRoomInfo.friendlyFire &&--*/ team == m_AIShooter.AITeam) return;
		}

		BattleManager battleManager = BattleManager.Instance;
#if DEBUG
		// 무적 모드
		var devFlags = battleManager.DevFlags;

		if ((devFlags.HasFlag(RoomDevFlags.AllyTeamInvul) && shooterAgent.IsAllyTeam())
			|| devFlags.HasFlag(RoomDevFlags.EnemyTeamInvul) && !shooterAgent.IsAllyTeam())
		{
			damage = 0;
		}
#endif
		if (battleManager.GameState == BattleStates.Playing)
		{
			photonView.RPC(nameof(RpcDoDamage), RpcTarget.All, damage, (int)weaponCode, direction, viewID, fromBot, ishead);	
		}
	}

	public void Init()
	{
		float protectedTimeLeft = bl_UtilityHelper.DetermineProtectedTime(references.InstParam.SpawnTime);
		
		if (protectedTimeLeft > 0)
		{
			IsProtectionEnable = true;
			Invoke(nameof(DisableProtection), protectedTimeLeft);
		}
	}
	
	void DisableProtection()
	{
		IsProtectionEnable = false;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="damage"></param>
	/// <param name="weaponName"></param>
	/// <param name="direction"></param>
	/// <param name="enemyViewID"></param>
	/// <param name="fromBot"></param>
	/// <param name="ishead"></param>
	[PunRPC]
	void RpcDoDamage(int damage, int weaponCodeInt, Vector3 hitDirection, int enemyViewID, bool fromBot, bool ishead)
	{
		if (_isBeingDestroyed) { return; }

		if (m_AIShooter.isDeath)
			return;

		Health -= damage;
		if (LastActorEnemy != enemyViewID)
		{
			if (shooterAgent != null)
				shooterAgent.FocusOnSingleTarget = false;
		}
		LastActorEnemy = enemyViewID;

		if (PhotonNetworkEx.IsMasterClient)
		{
			if (fromBot)
			{
				PhotonView p = PhotonView.Find(enemyViewID);
				if (p != null)
				{
					AddBotDamageHint(enemyViewID, damage);
				}
			}
			shooterAgent?.OnGetHit(hitDirection);
		}
		if (enemyViewID == BattleManager.LocalPlayerViewID)//if was me that make damage
		{
			CrosshairCtl.Instance.OnHit();
			bl_AudioController.Instance.PlayClip("body-hit");
			bl_EventHandler.DispatchLocalPlayerHitEnemy(new MFPSHitData()
			{
				HitTransform = transform,
				HitPosition = transform.position,
				Damage = damage,
				HitName = gameObject.name,
				IsHeadshot = ishead,
			});
		}

		if (Health > 0)
		{
			PlayerSeat seat = BattleManager.Instance.FindPlayerSeat(enemyViewID);
			Transform actor = seat?.Actor;
			if (actor != null && !seat.Actor.name.Contains("(die)"))
			{
				if (m_AIShooter.Target == null)
				{
					if (shooterAgent != null)
						shooterAgent.FocusOnSingleTarget = true;

					if (seat.PhotonViewID != m_AIShooter.photonView.ViewID)
					{
						m_AIShooter.Target = actor;
					}
				}
				else
				{
					//자기 자신은 타겟이 되지 않게 처리
					if (seat.PhotonViewID != m_AIShooter.photonView.ViewID)
					{
						float cd = bl_UtilityHelper.Distance(transform.position, m_AIShooter.Target.position);
						float od = bl_UtilityHelper.Distance(transform.position, actor.position);
						if (od < cd && (cd - od) > 7)
						{
							if (shooterAgent != null)
								shooterAgent.FocusOnSingleTarget = true;
							m_AIShooter.Target = actor;
						}
					}
				}
			}
			references.aiAnimation.OnGetHit();
		}
		else
		{
			Die(enemyViewID, fromBot, ishead, (WeaponCode)weaponCodeInt, hitDirection);
		}
	}

	/// <summary>
	/// 
	/// </summary>
	void Die(int enemyViewID, bool fromBot, bool ishead, WeaponCode weaponCode, Vector3 position)
	{
		int debugStep = 0;
		try
		{
			CrashlyticsUtil.AddActionLog($"[Battle] Bot die. ({gameObject.name})");

			debugStep = 1;

			//Debug.Log($"{gameObject.name} die with {weaponName} from viewID {viewID} Bot?= {fromBot}");
			if (weaponCode == WeaponCode.Invalid)
			{
				weaponCode = (WeaponCode)BattleConstants.DefultWeaponNo;
			}
			WeaponDef weaponDef = WeaponCDB.Instance.GetDef(weaponCode.WeaponNo);

			debugStep = 2;
			m_AIShooter.isDeath = true;
			m_AIShooter.OnDeath();
			gameObject.name += " (die)";
			m_AIShooter.AimTarget.name += " (die)";
			references.shooterWeapon.OnDeath();
			m_AIShooter.enabled = false;
			references.onDie?.Invoke();

			debugStep = 3;

			if (PhotonNetworkEx.IsMasterClient && references.Agent.enabled)
			{
				references.Agent.isStopped = true;
			}
			debugStep = 4;
			GetComponent<NamePlate>().SetActive(false);
			debugStep = 5;
			//update the MFPSPlayer data
			PlayerSeat player = BattleManager.Instance.FindMFPSPlayerByNickname(m_AIShooter.AIName);
			if (player != null)
			{
				player.IsAlive = false;
			}

			bl_AIShooter killerBot = null;
			//if was local player who terminated this bot
			if (enemyViewID == BattleManager.LocalPlayerViewID && !fromBot)
			{
				debugStep = 6;
				Team team = PhotonNetworkEx.LocalPlayer.GetPlayerTeam();

				debugStep = 7;
				//send kill feed message
				var feed = new KillFeedNetwork.FeedData()
				{
					LeftText = LocalName,
					RightText = m_AIShooter.AIName,
					Team = team
				};
				feed.AddData("weaponCode", (int)weaponCode);
				feed.AddData("headshot", ishead);

				KillFeedNetwork.Instance.SendKillMessageEvent(feed);

				debugStep = 8;

				var killConsideration = GameSettings.Instance.howConsiderBotsEliminations;
				if (killConsideration != BotKillConsideration.DoNotCountAtAll)
				{
					if (isOneTeamMode)
					{
						PhotonNetworkEx.LocalPlayer.PostKills(1);
						bl_RoomSettings.IncreaseMatchPersistData("bot-kills", 1);
					}
					else if (shooterAgent.AITeam != Team.All && shooterAgent.AITeam != bl_MFPS.LocalPlayer.Team)
					{
						PhotonNetworkEx.LocalPlayer.PostKills(1);
						bl_RoomSettings.IncreaseMatchPersistData("bot-kills", 1);
					}
				}

				debugStep = 9;

				// only grant score to the kill player if bot eliminations counts as real players.
				if (killConsideration == BotKillConsideration.SameAsRealPlayers)
				{
					int score;
					//If headshot will give you double experience
					if (ishead)
					{
						BattleManager.Instance.HeadshotKills++;
						score = GameSettings.Instance.ScoreReward.ScorePerKill + GameSettings.Instance.ScoreReward.ScorePerHeadShot;
					}
					else
					{
						score = GameSettings.Instance.ScoreReward.ScorePerKill;
					}
					debugStep = 10;
					if (weaponDef.SlotType == WeaponSlotType.Melee)
					{
						BattleManager.Instance.MeleeKills++;
					}
					else if (weaponDef.SlotType == WeaponSlotType.Grenade)
					{
						BattleManager.Instance.GrenadeKills++;
					}

					if (isOneTeamMode) PhotonNetworkEx.LocalPlayer.PostScore(score);
					else if (shooterAgent.AITeam != Team.All && shooterAgent.AITeam != bl_MFPS.LocalPlayer.Team)
					{
						PhotonNetworkEx.LocalPlayer.PostScore(score);
					}
				}
				debugStep = 11;
				//show an local notification for the kill
				var localKillInfo = new KillInfo();
				localKillInfo.Killer = PhotonNetworkEx.LocalPlayer.NickName;
				localKillInfo.Killed = string.IsNullOrEmpty(m_AIShooter.AIName) ? gameObject.name.Replace("(die)", "") : m_AIShooter.AIName;
				localKillInfo.byHeadShot = ishead;
				localKillInfo.WeaponCode = weaponCode;
				bl_EventHandler.DispatchLocalKillEvent(localKillInfo);

				//update team score
				BattleManager.Instance.SetPoint(1, BattleMode.TDM, team);
				debugStep = 12;

#if GR
			if (GetGameMode == GameMode.GR)
			{
				bl_GunRace.Instance?.GetNextGun();
			}
#endif
			}
			//if was killed by another bot
			else if (fromBot)
			{
				debugStep = 100;
				//make Master handle as the owner
				if (PhotonNetworkEx.IsMasterClient)
				{
					//find the killer in the scene
					PhotonView p = PhotonView.Find(enemyViewID);
					bl_AIShooter bot = null;
					string killer = "Unknown";
					if (p != null)
					{
						bot = p.GetComponent<bl_AIShooter>();//killer bot
						killer = bot.AIName;
						if (string.IsNullOrEmpty(killer)) { killer = p.gameObject.name.Replace(" (die)", ""); }
						//update bot stats
						bl_AIManager.Instance.SetBotKill(killer);
					}
					debugStep = 101;
					// BOT의 Assist 처리. 일정 데미지를 입혔던 AI들의 Assist 카운트를 올린다.
					if (BotDamageHint != null)
					{
						int refDamage = KillFeedSettings.Instance.AssistRefDamage;
						foreach (var kv in BotDamageHint)
						{
							if (kv.Value >= refDamage)
							{
								var botStats = bl_AIManager.Instance.GetBotStatistics(kv.Key);
								if (botStats != null)
								{
									bl_AIManager.Instance.SetBotAssist(botStats.Name);
								}
							}
						}
					}
					debugStep = 102;
					//send kill feed message

					var feed = new KillFeedNetwork.FeedData()
					{
						LeftText = killer,
						RightText = m_AIShooter.AIName,
						Team = bot.AITeam
					};
					debugStep = 103;
					feed.AddData("weaponCode", (int)weaponCode);
					feed.AddData("headshot", ishead);

					KillFeedNetwork.Instance.SendKillMessageEvent(feed);

					if (bot != null)
					{
						killerBot = bot;
					}
					else
					{
						Debug.Log("Bot who kill this bot can't be found");
					}
					debugStep = 104;
				}
			}//else, (if other player kill this bot) -> do nothing.

			if (GameSettings.Instance.showDeathIcons && !isOneTeamMode)
			{
				if (m_AIShooter.AITeam == PhotonNetworkEx.LocalPlayer.GetPlayerTeam())
				{
					GameObject di = bl_ObjectPoolingBase.Instance.Instantiate("deathicon", transform.position, transform.rotation);
				}
			}

			//if (GetGameMode == GameMode.DM) { DemolitionMode.Instance.OnLocalDeath(); }

			var mplayer = new PlayerSeat(photonView, false, false);
			bl_EventHandler.DispatchOtherPlayerDeath(mplayer);

			//update the bot deaths count.
			bl_AIManager.Instance.SetBotDeath(m_AIShooter.AIName);

			if (PhotonNetworkEx.IsMasterClient)
			{
				debugStep = 200;

				//respawn management here
				//only master client called it since the spawn will be sync by PhotonNetwork.Instantiate()
				bl_AIManager.Instance.OnBotDeath(m_AIShooter, killerBot);

				debugStep = 201;

				//Only Master client should send the RPC
				var deathData = bl_UtilityHelper.CreatePhotonHashTable();
				deathData.Add("type", AIRemoteCallType.DestroyBot);
				deathData.Add("direction", position);
				if (weaponDef.WeaponType == WeaponType.Grenade)
				{
					deathData.Add("explosion", true);
				}
				debugStep = 202;

				//Should buffer this RPC?
				this.photonView.RPC(bl_AIShooterAgent.RPC_NAME, RpcTarget.All, deathData);//callback is in bl_AIShooterAgent.cs -> DestroyBot(...)

				//--DropAmmoItem();
			}

			ClearBotDamageHint();

			debugStep = 203;

			if (GameSettings.Instance.DropGunOnDeath)
			{
				_shooterAttack.ThrowCurrentWeapon();
			}
			debugStep = 204;
		}
		catch (Exception e)
		{
			CrashlyticsUtil.LogException(new GameException($"AIShooterHeath.Die() DebugStep({debugStep}) - " + e.Message));
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public override void DestroyEntity()
	{
		var deathData = bl_UtilityHelper.CreatePhotonHashTable();
		deathData.Add("type", AIRemoteCallType.DestroyBot);
		deathData.Add("instant", true);
		this.photonView.RPC(bl_AIShooterAgent.RPC_NAME, RpcTarget.AllBuffered, deathData);//callback is in bl_AIShooterAgent.cs
	}

	/// <summary>
	/// 
	/// </summary>
	public override void DoRepetingDamage(RepetingDamageInfo info)
	{
		repetingDamageInfo = info;
		InvokeRepeating(nameof(MakeDamageRepeting), 0, info.Rate);
	}

	/// <summary>
	/// 
	/// </summary>
	void MakeDamageRepeting()
	{
		if (repetingDamageInfo == null)
		{
			CancelRepetingDamage();
			return;
		}

		var damageinfo = repetingDamageInfo.DamageData;
		if (damageinfo == null)
		{
			damageinfo = new DamageData();
			damageinfo.Position = Vector3.zero;
			damageinfo.Cause = DamageCause.Map;
			damageinfo.Direction = Vector3.one;
		}
		damageinfo.Damage = repetingDamageInfo.Damage;

		// CHECKIT(25.7.30) 현재는 이 타입은 화염병밖에 없으니 임시로 하드코딩.
		// 정식으로는 PlayerNetwork.GrenadeFire() 등에서 bulletData.MFPSActor에 PlayerSeat를 연결하는 것부터 필요하다.
		// 다시 말해서 투척무기들의 bulletData에 'FromWho'정보과 Weapon정보를 제대로 지정해야 할 것이다.
		damageinfo.WeaponCode = (WeaponCode)BattleConstants.MolotovWeaponNo;

		DoDamage(damageinfo.Damage, damageinfo.WeaponCode, damageinfo.Position, damageinfo.Direction, BattleManager.LocalPlayerViewID, false, PhotonNetworkEx.LocalPlayer.GetPlayerTeam(), false);
	}

	/// <summary>
	/// 
	/// </summary>
	public override void CancelRepetingDamage()
	{
		CancelInvoke(nameof(MakeDamageRepeting));
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="healthToAdd"></param>
	public override void SetHealth(int healthToAdd, bool overrideHealth)
	{
		if (overrideHealth) Health = healthToAdd;
		else Health += healthToAdd;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <returns></returns>
	public override bool Suicide()
	{
		return false;
	}

	public override void ForceKill()
	{
		photonView.RPC(nameof(RpcDoDamage), RpcTarget.AllBuffered, 200, 1, Vector3.zero, BattleManager.LocalPlayerViewID, false, false);
	}

	/// <summary>
	/// 
	/// </summary>
	/// <returns></returns>
	public override int GetHealth() => Health;

	/// <summary>
	/// 
	/// </summary>
	/// <returns></returns>
	public override int GetMaxHealth() => 100;

	/// <summary>
	/// 
	/// </summary>
	/// <returns></returns>
	public override bool IsDeath()
	{
		return m_AIShooter.isDeath;
	}

	[PunRPC]
	void RpcSyncHealth(int _health)
	{
		if (_isBeingDestroyed) { return; }

		Health = _health;
	}

	/// <summary>
	/// (사망시) 탄창 드롭
	/// </summary>
	void DropAmmoItem()
	{
		bl_AIShooterAgent agent = (bl_AIShooterAgent)shooterAgent;
		var throwData = new AmmoDropData()
		{
			Origin = agent.gunObjectRoot.position,
			Direction = transform.forward,
			ClipCount = 1,
		};
		AmmoPickupManager.Instance.DropAmmo(throwData);
	}
}