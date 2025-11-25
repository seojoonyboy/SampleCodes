using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;
using MFPS.Runtime.AI;
using Game.View.BattleSystem;
using Framework;
using Framework.UI;
using Game.Data;
using Compressor = Game.View.BattleSystem.NetworkDataCompressor;
using Game.View;


public sealed class bl_AIShooterNetwork : bl_MonoBehaviour, IPunObservable
{
	public NavMeshAgent Agent { get; set; }
	public Vector3 Velocity { get; set; }
	public Transform AimTarget { get; set; }
	
	Transform m_Transform;
	int receivePackages = 0;
	Vector3 correctPlayerPos = Vector3.zero; // We lerp towards this
	Quaternion correctPlayerRot = Quaternion.identity; // We lerp towards this
	Vector3 networkLookAtPosition = Vector3.zero;
	float _networkLookAtPitch;
	MFPSBotProperties BotStat = null;

	/// <summary>
	/// 
	/// </summary>
	protected override void Awake()
	{
		base.Awake();
		m_Transform = transform;
		Agent = References.Agent;

		bl_AIManager.OnMaterStatsReceived += OnMasterStatsReceived;
		bl_AIManager.OnBotStatUpdate += OnBotStatUpdate;
		bl_EventHandler.onLocalPlayerSpawn += OnLocalPlayerSpawn;

		GetEssentialData();
		if (photonView.IsMine)
		{
			OnAwakeByLocal();
		}
		else
		{
			OnAwakeByRemote();
		}
	}

	/// <summary>
	/// 
	/// </summary>
	protected override void OnEnable()
	{
		base.OnEnable();
		// BOT은 Master에서 죽은 상태라도 Slave는 스폰된다. 
		// 일단 Alive상태로 등록한다.(이후 Stats정보 갱신시 사망상태로 갱신될 것이다)
		RegisterPlayerSpawn(true);
	}

	/// <summary>
	/// 
	/// </summary>
	protected override void OnDisable()
	{
		base.OnDisable();
		bl_AIManager.OnMaterStatsReceived -= OnMasterStatsReceived;
		bl_AIManager.OnBotStatUpdate -= OnBotStatUpdate;
		bl_EventHandler.onLocalPlayerSpawn -= OnLocalPlayerSpawn;
	}

	/// <summary>
	/// 
	/// </summary>
	void OnAwakeByLocal()
	{

	}

	/// <summary>
	/// 
	/// </summary>
	void OnAwakeByRemote()
	{
		References.aiShooter.Init(bl_AIManager.InitType.FirstSpawn);
	}

	/// <summary>
	/// 
	/// </summary>
	public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
	{
		if(_isBeingDestroyed) {  return; }

		if (stream.IsWriting)
		{
			if (GVConstants.UsePhotonStreamCompress)
			{
				stream.SendNext(Compressor.CompressPosXZ(m_Transform.localPosition));
				stream.SendNext(Compressor.CompressPosY(m_Transform.localPosition));
				stream.SendNext(Compressor.CompressAngle(m_Transform.localRotation.eulerAngles.y));
				stream.SendNext(Compressor.CompressVelocityXZ(Agent.velocity));
				stream.SendNext(Compressor.CompressVelocityY(Agent.velocity));
				stream.SendNext(Compressor.CompressBotLook(References.aiShooter.LookAtPosition, References.aiShooter.LookAtPitch));
			}
			else
			{
				stream.SendNext(m_Transform.localPosition);
				stream.SendNext(m_Transform.localRotation);
				stream.SendNext(Agent.velocity);
				stream.SendNext(References.aiShooter.LookAtPosition);
				stream.SendNext(References.aiShooter.LookAtPitch);
			}
		}
		else
		{
			if (GVConstants.UsePhotonStreamCompress)
			{
				int posXZ = (int)stream.ReceiveNext();
				short posY = (short)stream.ReceiveNext();
				byte rotationYaw = (byte)stream.ReceiveNext();
				int velXZ = (int)stream.ReceiveNext();
				short velY = (short)stream.ReceiveNext();
				long botLook = (long)stream.ReceiveNext();

				correctPlayerPos = Compressor.DecompressPos(posXZ, posY);
				correctPlayerRot = Quaternion.Euler(0, Compressor.DecompressAngle(rotationYaw), 0);
				Velocity = Compressor.DecompressVelocity(velXZ, velY);
				Compressor.DecompressBotLook(botLook, out networkLookAtPosition, out _networkLookAtPitch);
			}
			else
			{
				correctPlayerPos = (Vector3)stream.ReceiveNext();
				correctPlayerRot = (Quaternion)stream.ReceiveNext();
				Velocity = (Vector3)stream.ReceiveNext();
				networkLookAtPosition = (Vector3)stream.ReceiveNext();
				_networkLookAtPitch = (float)stream.ReceiveNext();
			}
			//Fix the translation effect on remote clients
			if (receivePackages < 5)
			{
				m_Transform.localPosition = correctPlayerPos;
				m_Transform.localRotation = correctPlayerRot;
				receivePackages++;
			}
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public override void OnUpdate()
	{
		if (!PhotonNetworkEx.IsMasterClient)//if not master client, then get position from server
		{
			m_Transform.localPosition = Vector3.Lerp(m_Transform.localPosition, correctPlayerPos, Time.deltaTime * 7);
			m_Transform.localRotation = Quaternion.Lerp(m_Transform.localRotation, correctPlayerRot, Time.deltaTime * 7);
			References.aiShooter.LookAtPosition = Vector3.Lerp(References.aiShooter.LookAtPosition, networkLookAtPosition, Time.deltaTime * 5);
			References.aiShooter.LookAtPitch = Mathf.Lerp(References.aiShooter.LookAtPitch, _networkLookAtPitch, Time.deltaTime * 5);
		}
		else
		{
			Velocity = Agent.velocity;
			if (BattleManager.Instance.IsTimeUp())
			{
				if(Agent.enabled) Agent.isStopped = true;
			}
		}
	}

	/// <summary>
	/// 
	/// </summary>
	void GetEssentialData()
	{
		//var botParam = photonView.InstantiationData.ToBotInstantiationParam();
		var botParam = ((BotInstantiationParam)References.InstParam);

		AIName = botParam.NickName;
		AITeam = botParam.Team;
		//--References.aiShooter.IsMonster = botParam.IsMonster;
		BotDefID = botParam.BotDefNo;

		gameObject.name = AIName;
		CheckNamePlate();
		//since Non master client doesn't update the view ID when bots are created, lets do it on Start
		if (!PhotonNetworkEx.IsMasterClient)
		{
			bl_AIManager.UpdateBotView(References.aiShooter, photonView.ViewID);
		}
	}

	/// <summary>
	/// 
	/// </summary>
	void OnMasterStatsReceived(List<MFPSBotProperties> stats)
	{
		ApplyMasterInfo(stats);
	}

	/// <summary>
	/// 
	/// </summary>
	void ApplyMasterInfo(List<MFPSBotProperties> stats)
	{
		int viewID = photonView.ViewID;
		MFPSBotProperties bs = stats.Find(x => x.ViewID == viewID);
		if (bs != null)
		{
			AIName = bs.Name;
			AITeam = bs.Team;
			gameObject.name = AIName;
			BotStat = new MFPSBotProperties();
			BotStat.Name = AIName;
			BotStat.Assists = bs.Assists;
			BotStat.Kills = bs.Kills;
			BotStat.Deaths = bs.Deaths;
			BotStat.ViewID = bs.ViewID;
			BotStat.GameState = bs.GameState;

			bool isAlive = BotStat.GameState != BotGameState.Death;
			References.aiShooter.isDeath = !isAlive;

			RegisterPlayerSpawn(isAlive);

			CheckNamePlate();

			if (!isAlive)
			{
				// 사망한 채로 스폰해야 하는데 방법이 없다 (사망 상태 애니메이션도 없고)
				// 일단 오브젝트 자체를 비활성화 해본다.
				gameObject.SetActive(false);
			}
		}
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="stat"></param>
	void OnBotStatUpdate(MFPSBotProperties stat)
	{
		if (stat.ViewID != photonView.ViewID) return;

		BotStat = stat;
		AIName = stat.Name;
		AITeam = BotStat.Team;
		gameObject.name = AIName;

		bool isAlive = stat.GameState != BotGameState.Death;
		RegisterPlayerSpawn(isAlive);
	}

	/// <summary>
	/// 
	/// </summary>
	private void RegisterPlayerSpawn(bool isAlive)
	{
		DebugEx.Log($"[AIShooterNetwork] RegisterPlayerSpawn (live:{isAlive})");

		string name = AIName;

		if (!name.HasContent())
		{
			name = photonView.InstantiationData.ToBotInstantiationParam().NickName;
		}

		var changeData = new bl_EventHandler.PlayerChangeData()
		{
			PlayerName = name,
			MFPSActor = BuildPlayer(),
			IsAlive = isAlive,
			NetworkView = photonView
		};
		changeData.OnRegistered = seat =>
		{
			References.PlayerSeat = seat;
			References.PlayerSeat.Parts = References.FPSCharacter.CharParts;
		};
		bl_EventHandler.DispatchRemoteActorChange(changeData);
	}

	/// <summary>
	/// 
	/// </summary>
	/// <returns></returns>
	private PlayerSeat BuildPlayer()
	{
		PlayerSeat player = new PlayerSeat()
		{
			NickName = AIName,
			PhotonView = photonView,
			IsRealPlayer = false,
			Actor = transform,
			AimPosition = AimTarget,
			Team = AITeam,
			IsAlive = true,
			IsMonster = References.aiShooter.IsMonster,
		};
		return player;
	}

	/// <summary>
	/// 
	/// </summary>
	protected override void OnDestroy()
	{
		base.OnDestroy();

		if (FrameworkApp.IsApplicationQuiting || SceneManager.IsSceneDestroying())
		{ return; }

		bl_EventHandler.DispatchRemoteActorChange(new bl_EventHandler.PlayerChangeData()
		{
			PlayerName = AIName,
			MFPSActor = BuildPlayer(),
			IsAlive = false,
			NetworkView = photonView
		});
		
		BotRepository.Instance.UnpossesBotDef(AITeam.ToString(), BotDefID);
	}

	/// <summary>
	/// 
	/// </summary>
	public void CheckNamePlate()
	{
		//DebugEx.Log($"[AIShooterNetwork] CheckNamePlate() {AIName}");
		References.namePlateDrawer.SetProfile(AIName);
		References.namePlateDrawer.BindPlayer();



		/// LocalPlayer가 없어도 항상 출력하게 해본다 <see cref="NamePlateAlways"/>
		
		References.namePlateDrawer.SetActive(true);
		/*--
		if (!isOneTeamMode && BattleManager.Instance.LocalPlayer != null && !References.aiShooter.isDeath)
		{
			// 적군도 출력.
			References.namePlateDrawer.SetActive(true);
		}
		else
		{
			References.namePlateDrawer.SetActive(false);
		}
		--*/
	}

	public void ToggleShieldIconInNamePlate(bool isOn)
	{
		References.namePlateDrawer.ToggleShieldIcon(isOn);
	}

	public void ActiveShieldIconInNamePlate(float durationTime)
	{
		var token = this.GetCancellationTokenOnDestroy();
		
		ToggleShieldIconInNamePlate(true);
		
		if (durationTime > 0)
		{
			DoTask(() =>
			{
				ToggleShieldIconInNamePlate(false);
			}).Forget();
		}
		return;

		async UniTaskVoid DoTask(Action onFinished)
		{
			await UniTask.WaitForSeconds(durationTime, cancellationToken: token);
			onFinished?.Invoke();
		}
	}

	void OnLocalPlayerSpawn()
	{
		CheckNamePlate();
	}

	private string AIName { get => References.aiShooter.AIName; set => References.aiShooter.AIName = value; }
	private Team AITeam { get => References.aiShooter.AITeam; set => References.aiShooter.AITeam = value; }
	private int BotDefID { get => References.aiShooter.BotDefID; set => References.aiShooter.BotDefID = value; }

	private bl_AIShooterReferences _References;
	public bl_AIShooterReferences References
	{
		get
		{
			if (_References == null) _References = GetComponent<bl_AIShooterReferences>();
			return _References;
		}
	}
}