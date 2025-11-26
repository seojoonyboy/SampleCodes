using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;
using Photon.Pun;
using Photon.Realtime;
using NetHashTable = ExitGames.Client.Photon.Hashtable;
using MFPS.Runtime.AI;
using MFPSEditor;
using Game.View.BattleSystem;
using Game.View;
using Framework;
using Framework.UI;
using Game.View.AI.State;
using static Game.View.BattleSystem.DemolitionMode;
using Game.Data;
using System;
using System.Threading;
using UnityEditor;
using Random = UnityEngine.Random;

[RequireComponent(typeof(NavMeshAgent))]
public class bl_AIShooterAgent : bl_AIShooter
{
	#region Public Members
	[Space(5)]
	[ScriptableDrawer] public bl_AIBehaviorSettings behaviorSettings;
	
	[Header("Others")]
	public LayerMask ObstaclesLayer;

	[Header("References")]
	public Transform aimTarget;
	public Transform gunObjectRoot;
	public bl_Footstep footstep;
	[SerializeField] Transform chest;
	#endregion

	#region Public Properties
	public override Transform AimTarget
	{
		get => aimTarget;
	}
	public bool playerInFront { get; set; }
	public bool ObstacleBetweenTarget { get; set; }
	public bl_AIShooterAttackBase AIWeapon { get; set; }
	public bl_PlayerHealthManagerBase AIHealth { get; set; }
	public override float CachedTargetDistance { get; set; } = 0;
	
	public bl_NetworkGun NetworkGun { get; set; }
	public bl_Gun BlGun { get; set; }
	public bool IsCrouch { get; set; } = false;
	public bool ForcelyDie = false;
	
	/// <summary>
	/// 사격 가능하게 되었을 때 사격 지연 타이머 (인지 시간을 흉내)
	/// </summary>
	public float ShootWaitTimer { get; set; }
	
	/// <summary>
	/// 최초에 세팅된 사격 지연 시간
	/// </summary>
	public float OriginDelayTime { get; set; }

	/// <summary>
	/// 사격 지연 시간
	/// </summary>
	public float ShootDelayTime { get; private set; }

	/// <summary>
	/// AI 상태
	/// </summary>
	public IAIState CurrentState { get; set; }

	public bool IsCarrier { get; set; }

	public float SpreadAngle => GetSpreadAngle;

	public float Speed => Agent.velocity.magnitude;
	const float MAX_SPEED = 4.0f;

	public float NormalSpeed
	{
		get
		{
			float normalSpeed = Mathf.Clamp(Speed / MAX_SPEED, 0, 1);
			
			return normalSpeed;
		}
	}
	
	float GetSpreadAngle
	{
		get
		{
			if (BlGun == null) return 10.0f;		//방어 코드
			
			float finalSpreadAngle = BlGun.DefaultSpreadAngle;
			
			if (IsCrouch)
			{
				finalSpreadAngle *= BlGun.SpreadSitMultiply;
			}
			
			float speedMultiply = Mathf.Lerp(1.0f, BlGun.SpreadRunMultiply, NormalSpeed);
			finalSpreadAngle *= speedMultiply;
			
			//DebugEx.Log($"[Bot] {AIName} NormalSpeed : {NormalSpeed}, finalSpreadAngle : {finalSpreadAngle}");
			
			return finalSpreadAngle;
		}
	}

	#endregion

	#region Private members
	private Animator Anim;
	private Vector3 finalPosition;
	private Vector3 lastHitDirection;
	private float lookTime;
	private BattleMode m_GameMode;
	private float time, delta = 0;
	private Transform m_Transform;
	private bool isGameStarted = false;
	private Vector3 targetDirection = Vector3.zero;
	private RaycastHit obsRay;
	private float lastDestinationTime = 0;
	private float velocityMagnitud = 0;
	private int[] animationHash = new int[] { 0 };
	private bool wasTargetInSight = false;
	public const string RPC_NAME = "RPCShooterBot";
	private List<Transform> availableTargets = new List<Transform>();
	private float lastCrouchTime = 0;

	private int prevTargetViewID = -1;
	private Vector3 prevCachedTargetDirection = Vector3.zero;

	private float _lastSpreadUpdateTime = 0;		//탄퍼짐에 기반하여 실제 타겟 지점 결정 이후 흐른 시간
	private float _spreadUpdateInterval = 3.0f;		//실제 타겟 지점 결정 갱신 주기(초)
	#endregion

	/// <summary>
	/// 
	/// </summary>
	protected override void Awake()
	{
		base.Awake();
		m_Transform = transform;
		bl_PhotonCallbacks.PlayerEnteredRoom += OnPhotonPlayerConnected;
		Agent = References.Agent;
		AIHealth = References.shooterHealth;
		AIWeapon = References.shooterWeapon;
		Anim = References.PlayerAnimator;
		
		References.aiAnimation?.AddFootStepEventListener(OnFootStepAnimEvent);
		
		ObstacleBetweenTarget = false;
		m_GameMode = GetGameMode;
		Agent.updateRotation = false;
		animationHash[0] = Animator.StringToHash("IsCrouch");

		var botParam = photonView.InstantiationData.ToBotInstantiationParam();
		AIName = botParam.NickName;

		bl_AIManager.SetBotGameState(this, BotGameState.Playing);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		DebugEx.Log($"[bl_AIShooterAgent] OnDestroy {AIName}");

		{
			if (moveDecelerationToken != null && !moveDecelerationToken.IsCancellationRequested)
			{
				moveDecelerationToken?.Cancel();
				moveDecelerationToken?.Dispose();
				isStopping = false;
			}
		}

		//if (FrameworkApp.IsApplicationQuiting || SceneManager.IsSceneDestroying()) { return; }
		//if (BattleManager.Instance.GetGameMode == BattleMode.DM)
		//{
		//	DemolitionBombManager.Instance.TryDetechBombFromPlayer(AIName);
		//}

		DOTween.Kill(this);
		
		References.aiAnimation?.RemoveFootStepEventListener(OnFootStepAnimEvent);
	}

	/// <summary>
	/// BattleManager의 Start에 호출된다.
	/// </summary>
	public override void Init(bl_AIManager.InitType initType)
	{
		bool isRespawn = initType == bl_AIManager.InitType.Respawn;
		
		isGameStarted = BattleManager.Instance.GameState == BattleStates.Playing;
		
		References.shooterNetwork.CheckNamePlate();
		References.Agent.enabled = PhotonNetworkEx.IsMasterClient;

		aiSettings = GameSettings.Instance.GetRandomAISetting();

		bl_AIManager.SetHealth(this);
		
		ResetShootDelayTimer(null, true);
		//DebugEx.Log($"[ShootDelay] 첫 소환에 의한 Delay 초기화 [case001]");
		
		InitGun();
		
		if (initType != bl_AIManager.InitType.MasterClientChanged)
		{
			bl_AIShooterHealth shooterHealth = References.shooterHealth as bl_AIShooterHealth;
			if (shooterHealth != null) { shooterHealth.Init(); }

			CheckSpawnProtection();
		}
		
		if (PhotonNetworkEx.IsMasterClient) { UpdateWayPointsAfterGameBegin(isRespawn).Forget(); }
		InitStateMachine();
		
		m_Transform.rotation = Quaternion.LookRotation(LookAtDirection);

		CheckUAVDetect();
	}
	
	void OnFootStepAnimEvent()
	{
		References.footstep.DetectAndPlaySurface();
	}

	// 스폰 보호 시간이라면 깜박임 이펙트와 방패 이미지
	void CheckSpawnProtection()
	{
		var initParam = References.InstParam;
		float protectedTimeLeft = bl_UtilityHelper.DetermineProtectedTime(initParam.SpawnTime);

		if (protectedTimeLeft == 0)
		{
			return;
		}

		// 방패 이미지
		References.shooterNetwork.ActiveShieldIconInNamePlate(protectedTimeLeft);

		// 몸체 깜박이기
		StartCoroutine(SpawnBlickEffectUtil.SpawnBlickRoutine(protectedTimeLeft, AITeam, () =>
		{
			return References.playerShaderHandler.Renderers;
		}));

		// 무기 깜박이기
		StartCoroutine(SpawnBlickEffectUtil.SpawnBlickRoutine(protectedTimeLeft, AITeam, () =>
		{
			return NetworkGun.AllRenderers;
		}));
	}

	// UAV 탐지 대상이라면 탐지 목록에 추가된다.
	void CheckUAVDetect()
	{
		PlayerSeat playerSeat = BattleManager.Instance.FindMFPSActor(photonView.ViewID);
		PlayerDetectionManager.Instance.TryExposePlayer(playerSeat, DetectionType.UavVision);
	}

	CancellationTokenSource _updateWayPointsTaskTokenSource;
	async UniTask UpdateWayPointsAfterGameBegin(bool isRespawn = false)
	{
		if (_updateWayPointsTaskTokenSource != null)
		{
			_updateWayPointsTaskTokenSource.Cancel();
			_updateWayPointsTaskTokenSource.Dispose();
			_updateWayPointsTaskTokenSource = null;
		}
		
		var destroyToken = this.GetCancellationTokenOnDestroy();
		_updateWayPointsTaskTokenSource = CancellationTokenSource.CreateLinkedTokenSource(destroyToken);
		var token = _updateWayPointsTaskTokenSource.Token;
		
		try
		{
			await UniTask.WaitUntil(() => isGameStarted, cancellationToken: token);
			
			List<Vector3> wayPoints = new List<Vector3>();
			if (GetGameMode == BattleMode.Defense)
			{
				CurrentState = new Searching(this);
			
				Team TargetTeam = BattleManager.Instance.LocalPlayerSeat.Team;
				var spawnPointTF = bl_SpawnPointManager.Instance.GetSequentialSpawnPoint(TargetTeam).transform;
				wayPoints = new List<Vector3> { spawnPointTF.transform.position };
			
				UpdateWayPoints(wayPoints);
			}
			else
			{
				//리스폰인 경우는 그룹 경로를 가져오지 않고, 각자 무작위 경로 배정
				if (isRespawn)
				{
					CurrentState = new Searching(this);
				
					AIWayPoint randomWayPoint = bl_AIManager.Instance.GetRandomNormalWayPoint();
					wayPoints = new List<Vector3> { randomWayPoint.transform.position };
				
					UpdateWayPoints(wayPoints);
				}
				else
				{
					if (GetGameMode == BattleMode.DM)
					{
						bool isBombAssignerGroup = bl_AIManager.Instance.IsBombAssignerGroup(this);
						if (!isBombAssignerGroup)
						{
							CurrentState = new Searching(this);
						
							bl_AIManager aiManager = bl_AIManager.Instance;
							Team team = AITeam;
							GroupID = References.aiShooter.GroupID;
							UpdateWayPoints(aiManager.GetMyPath(team, GroupID, transform.position));
						}
						else
						{
							//폭탄이 할당되기 전에 관전자가 Master로 바뀌는 경우 
							//폭탄을 소지한 그룹의 경로를 재갱신 시킨다.
							bl_AIManager.Instance.UpdateBombAssignerGroupPath(this);
						}
					}
					else
					{
						CurrentState = new Searching(this);
					
						bl_AIManager aiManager = bl_AIManager.Instance;
						Team team = AITeam;
						GroupID = References.aiShooter.GroupID;

						// string msg = string.Format("Team : {0} / Agent Name : {1} / Group ID : {2}", team.ToString(), AIName, groupID);
						// Debug.Log(msg);
					
						UpdateWayPoints(aiManager.GetMyPath(team, GroupID, transform.position));
					}
				}
			}
		}
		catch (OperationCanceledException) { }
	}

	void InitStateMachine()
	{
		AIStateManager.Instance.RegisterAgent(this);
		CurrentState = new Idle(this);
	}

	void InitGun()
	{
		WeaponDef weaponDef = bl_AIManager.GetWeaponDef(this);
		if (weaponDef == null)
		{
			DebugEx.Error("[bl_AIShooterAgent] AI용 총기 Data를 찾을 수 없습니다!");
			return;
		}
		
		var resSettings = weaponDef.GetResSettings();
		if (resSettings == null)
		{
			DebugEx.Error("[bl_AIShooterAgent] AI Resource Settings를 찾을 수 없습니다!");
			return;
		}
		
		GameObject instance = GameObject.Instantiate(resSettings.RemoteWeaponPrefab.gameObject, gunObjectRoot);
		NetworkGun = instance.GetComponent<bl_NetworkGun>();
		BlGun = resSettings.LocalWeaponPrefab;
		var blNetworkGunInst = instance.GetComponent<bl_NetworkGun>();
		
		if (NetworkGun == null)
		{
			DebugEx.Error("AI용 총기 bl_NetworkGun 컴포넌트를 확인할 수 없습니다!");
			return;
		}
		
		AIWeapon.Init(weaponDef.No);

		WeaponCodeDef targetWeaponCodeDefs = WeaponUtil.GetRandomWeaponCodeDefByWeaponDefNo(weaponDef.No);
		
		if (targetWeaponCodeDefs != null && blNetworkGunInst != null)
		{
			WeaponUtil.ApplySkin(blNetworkGunInst.SkinRenderGroups, targetWeaponCodeDefs);
		}
		
		
		RegisterMainWeaponInThePlayerSeat();
	}
	
	/// <summary>
	/// 
	/// </summary>
	public override void OnUpdate()
	{  
		time = Time.time;
		delta = Time.deltaTime;
		if (isDeath) return;

		UpdateState();
		UpdateLookAt();
		UpdateAnimatorPitch();
		
		// DebugEx.Log($"{AIName} Speed : {Agent.velocity.magnitude}");
		
		if(ForcelyDie)
		{
			AIHealth.ForceKill();
		}
		
		ShootWaitTimer += Time.deltaTime;
	}

	public void UpdateState()
	{
		if (!isGameStarted) return;

		if (PhotonNetworkEx.IsMasterClient && CurrentState != null) { CurrentState.Process(); }
	}

	/// <summary>
	/// this is called one time each second instead of each frame
	/// </summary>
	public override void OnSlowUpdate()
	{
		if (isDeath) return;
		if (BattleManager.Instance.IsTimeUp() || !isGameStarted)
		{
			return;
		}

		velocityMagnitud = Agent.velocity.magnitude;

		if (PhotonNetworkEx.IsMasterClient) { CheckVision(); }

		FootStep();
	}

	public override void RegisterMainWeaponInThePlayerSeat()
	{
		if (References.PlayerSeat != null)
		{
			WeaponCode mainWeapon = (WeaponCode)(AIWeapon.Weapon?.No ?? BattleConstants.DefultWeaponNo);
			References.PlayerSeat.MainWeapon = mainWeapon;
		}
	}
	/// <summary>
	/// 
	/// </summary>
	void OnAgentStateChanged(AIAgentState from, AIAgentState to)
	{

	}
	
	//void OnUAVActive(Hashtable data)
	//{
	//	string logMsg =$"OnUAVActive Bot : {transform.name}";
	//	DebugEx.Log(logMsg);
		
	//	CheckUAVDetect();
	//}

	void OnDMEventReceived(NetHashTable data)
	{
		if (!PhotonNetworkEx.IsMasterClient) { return; }
		
		BombStatus getStatus = (BombStatus)(int)data["type"];
		bool isTerrorlistTeam = AITeam == DemolitionMode.Instance.AttackTeam;

		switch (getStatus)
		{
			case BombStatus.Droped:
				if (isTerrorlistTeam)
				{
					CurrentState.UpdateAttackerBombDroppedBehavior();
				}
				break;
			case BombStatus.Actived:
				Vector3 bombPosition = (Vector3)data["position"];
				bl_AIManager.Instance.SetAllBotsToBombArea(bombPosition);
				break;

			case BombStatus.Carried:
				int viewID = (int)data["viewID"];
				IsCarrier = photonView.ViewID == viewID;
				if (IsCarrier)
				{
					DebugEx.Log($"[bl_AIShooterAgent] {AIName} OnDMEventReceived.Carried event occured");
					CurrentState.UpdateAttackerBombAssignedBehavior();
				}
				break;
		}
	}

	private void OnDemolitionEventReceived(NetHashTable data)
	{
		if(!PhotonNetworkEx.IsMasterClient) { return; }
		
		DemolitionEventType t = (DemolitionEventType)data["type"];
		switch (t)
		{
			case DemolitionEventType.CarrierAssign:
				if (photonView == null) break;

				int viewID = (int)data["viewID"];
				IsCarrier = photonView.ViewID == viewID;
				if (IsCarrier)
				{
					bl_AIManager.Instance.DecideBotGroupIDs();
					bl_AIManager.Instance.InitFirstSpawnPaths();
					
					CurrentState?.UpdateAttackerBombAssignedBehavior();
				}
				break;
		}
	}

	/// <summary>
	/// 
	/// </summary>
	private void OnTargetChanged(Transform from, Transform to)
	{
		ResetShootDelayTimer(to);
		//DebugEx.Log($"[ShootDelay] 타겟 변경에 의한 Delay 초기화 [case005]");
	}

	/// <summary>
	/// Called when the bot not direct vision to the target -> have direct vision to the target and vice versa
	/// </summary>
	/// <param name="from">seeing?</param>
	/// <param name="to">seeing?</param>
	private void OnTargetLineOfSightChanged(bool from, bool to)
	{
		if (from == true)//the player lost the line of vision with the target
		{
			if (HasATarget && TargetDistance > 5)//he lost the target but not because it is death.
				Invoke(nameof(CorrectLookAt), 3);//if after 3 second of loss the target, still now found it -> don't look at it (trough walls for example)
		}
		else//player now has direct vision to the target
		{
			if (AgentState == AIAgentState.Following)
				CancelInvoke(nameof(CorrectLookAt));
		}
	}

	/// <summary>
	/// 
	/// </summary>
	void CorrectLookAt()
	{
		SetLookAtState(AILookAt.PathToTarget);
	}

	/// <summary>
	/// pos : 타격 받은 위치
	/// </summary>
	public override void OnGetHit(Vector3 hitDirection)
	{
		lastHitDirection = hitDirection;
		
		SetLookAtState(AILookAt.Target);
		IAIState state = CurrentState.GetRandomDamagedState(lastHitDirection);

		//test code
		//state = new HoldingPositionAttacking(this);
		//state = new MovingAttacking(this);
		//end test code

		if (state == null) return;

		if (state.GetType() == typeof(HoldingPositionAttacking)) CurrentState = (HoldingPositionAttacking)state;
		else if (state.GetType() == typeof(MovingAttacking)) CurrentState = (MovingAttacking)state;
		else if (state.GetType() == typeof(AvoidAttacking)) CurrentState = (AvoidAttacking)state;
		else if (state.GetType() == typeof(AggressiveAttacking)) CurrentState = (AggressiveAttacking)state;
	}

	/// <summary>
	/// 
	/// </summary>
	void CheckVision()
	{
		if (!HasATarget || !PhotonNetworkEx.IsMasterClient)
		{
			ShootWaitTimer = 0;
			ObstacleBetweenTarget = false;
			return;
		}

		Vector3 relative = m_Transform.InverseTransformPoint(this.TargetDirection);
		playerInFront = (relative.x < 2f && relative.x > -2f) || (relative.x > -2f && relative.x < 2f);

		//TargetDirection은 보정값을 준 가상의 위치이므로 그 위치까지의 라인에는 장애물이 있을 수 있다. 따라서, 원래 위치로 라인을
		//쏘아야 한다.

		var targetPhotonView = Target.GetComponent<PhotonView>();
		if(targetPhotonView == null) return;
		
		if (Physics.Linecast(References.VisionCheckStartPoint.position, GetTargetPositionFromPlayerTransform(targetPhotonView), out obsRay, ObstaclesLayer, QueryTriggerInteraction.Ignore))
		{
			ObstacleBetweenTarget = obsRay.transform.root.CompareTag(bl_MFPS.LOCAL_PLAYER_TAG) == false;
		}
		else { ObstacleBetweenTarget = false; }

		bool nowTargetInSight = !ObstacleBetweenTarget;

		if (wasTargetInSight != nowTargetInSight)
		{
			OnTargetLineOfSightChanged(wasTargetInSight, nowTargetInSight);
			wasTargetInSight = nowTargetInSight;
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public void KillTheTarget()
	{
		if (!HasATarget) return;

		SetTarget(null);
		var data = bl_UtilityHelper.CreatePhotonHashTable();
		data.Add("type", AIRemoteCallType.SyncTarget);
		data.Add("viewID", -1);

		photonView.RPC(RPC_NAME, RpcTarget.Others, data);
	}

	/// <summary>
	/// 
	/// </summary>
	private void UpdateLookAt()
	{
		if (!PhotonNetworkEx.IsMasterClient) return;
		if (!Agent.isOnNavMesh) return;		//NavMesh를 아직 불러오기 전이라면 (예외상황) Return 한다.
		
		if (LookingAt != AILookAt.Path && !HasATarget)
		{
			LookingAt = AILookAt.Path;
		}
			
		switch (LookingAt)
		{
			case AILookAt.Path:
			case AILookAt.PathToTarget:
				int cID = Agent.path.corners.Length > 1 ? 1 : 0;
				var v = Agent.path.corners[cID];
				v.y = m_Transform.localPosition.y;
				LookAtPosition = v;
				v = LookAtPosition - transform.position;
				LookAtDirection = v;
				break;
			case AILookAt.Target:
				var tp = transform.position;
				tp.y = chest.transform.position.y;
				LookAtDirection = this.TargetDirection - tp;
				break;
		}

		LookAtPitch = -Vector3.SignedAngle(transform.forward, LookAtDirection, transform.right);
		
		AIAnimation aiAnimation = References.aiAnimation as AIAnimation;
		Vector3 flatDirection = new Vector3(LookAtDirection.x, 0f, LookAtDirection.z);	//수평방향 벡터
		LookAtYaw = Vector3.SignedAngle(transform.forward.normalized, flatDirection.normalized, Vector3.up);
		
		if (aiAnimation != null && !aiAnimation.IsRagdolling && flatDirection.sqrMagnitude > 0.001f)
		{
			Quaternion targetYRotation = Quaternion.LookRotation(flatDirection, Vector3.up);
			m_Transform.rotation = Quaternion.Slerp(m_Transform.rotation, targetYRotation, Time.deltaTime * 10.0f);
		}
	}

	public void UpdateAnimatorPitch()
	{
		if(References.aiAnimation == null) return;

		//Note. 앉아있는 경우 Pitch 0 (정면) 이 Animation Crouch Aim 상으로 살짝 숙여진 모션으로 
		//보여지는 문제가 있어, Pitch 값 최소값을 15도로 고정했음
		float clampMin = IsCrouch ? 15.0f : -90.0f;
		var clampedPitch = Mathf.Clamp(LookAtPitch, clampMin, 90f);
		float normalizedPitch = (clampedPitch + 90.0f) / 180.0f;
		float prevPitch = References.aiAnimation.GetFloatParameter("VerticalLook");
		
		float lerpPitch = Mathf.Lerp(prevPitch, normalizedPitch, Time.deltaTime);
		References.aiAnimation.SetFloatParameter("VerticalLook", lerpPitch);
	}

	public void TriggerFire(bl_AIShooterAttackBase.FireReason reason = bl_AIShooterAttackBase.FireReason.Normal)
	{
		if (LookingAt == AILookAt.Path) SetLookAtState(AILookAt.Target);

		if (Mathf.Abs(LookAtYaw) < 15.0f)
		{
			AIWeapon.Fire(reason);
		}
	}

	bool isStopping = false;
	CancellationTokenSource moveDecelerationToken;
	public void ToggleMovable(bool canMove)
	{
		Agent.isStopped = !canMove;

		if (!canMove)
		{
			Agent.speed = Mathf.Lerp(Agent.speed, 0.0f, Time.deltaTime * 10.0f);

			if (!isStopping)
			{
				moveDecelerationToken = new CancellationTokenSource();
				DecelerateToZeroAsync(moveDecelerationToken.Token).Forget();
			}
		}
		else
		{
			if (moveDecelerationToken != null && !moveDecelerationToken.IsCancellationRequested)
			{
				moveDecelerationToken?.Cancel();
				moveDecelerationToken?.Dispose();
				isStopping = false;
			}
		}
		
		async UniTask DecelerateToZeroAsync(CancellationToken token)
		{
			isStopping = true;
			
			float start = Agent.speed;
			float t = 0f;
			
			float duration = 0.25f;
			
			// 경로 이동을 계속 유지하려면 isStopped는 건드리지 말고 speed만 감소
			// 완전히 멈추게 하려면 마지막에 ResetPath/velocity zero 처리
			while (t < duration)
			{
				token.ThrowIfCancellationRequested();

				t += Time.deltaTime;
				float u = Mathf.Clamp01(t / duration);

				// EaseOutCubic: 1 - (1 - u)^3
				float eased = 1f - Mathf.Pow(1f - u, 3f);

				Agent.speed = Mathf.Lerp(start, 0f, eased);

				await UniTask.Yield(PlayerLoopTiming.Update, token);
			}

			// 마무리: 완전 0 고정 + 관성 제거
			Agent.speed = 0f;
			isStopping = false;
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public void SetState(AIAgentState newState)
	{
		if (newState == AgentState) return;

		OnAgentStateChanged(AgentState, newState);
		AgentState = newState;
	}

	public bool IsAlive()
	{
		PlayerSeat player = BattleManager.Instance.FindPlayerSeat(photonView.ViewID);
		if (player == null) return false;

		return player.IsAliveAndValid;
	}

	/// <summary>
	/// 
	/// </summary>
	public void SetTarget(Transform newTarget)
	{
		if (Target == newTarget) return;

		OnTargetChanged(Target, newTarget);
		Target = newTarget;

		if (!PhotonNetworkEx.IsMasterClient) return;

		try
		{
			// sync the bot target
			var data = bl_UtilityHelper.CreatePhotonHashTable();
			data.Add("type", AIRemoteCallType.SyncTarget);

			if (Target == null)
			{
				data.Add("viewID", -1);
				photonView.RPC(RPC_NAME, RpcTarget.Others, data);
			}
			else
			{
				PhotonView view = GetPhotonView(Target.root.gameObject);
				if (view != null)
				{
					data.Add("viewID", view.ViewID);
					photonView.RPC(RPC_NAME, RpcTarget.Others, data);
				}
				else
				{
					DebugEx.Log($"[bl_AIShooterAgent] {AIName}'s Target " + Target.name + "no have photonview");
				}
			}
		}
		catch(Exception e)
		{
			CrashlyticsUtil.LogException(new GameException("SetTarget error. " + e.Message));
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public void SetLookAtState(AILookAt newLookAt)
	{
		if (LookingAt == newLookAt) return;
		if (LookingAt == AILookAt.PathToTarget && newLookAt == AILookAt.Target)
		{
			if (ObstacleBetweenTarget) return;
		}

		LookingAt = newLookAt;
		//--forceUpdateRotation = true;
	}

	public void SetDestination(Vector3 position, bool checkRate = false)
	{
		if (!Agent.isOnNavMesh) return;
		if (checkRate && (time - lastDestinationTime) < 2) return;

		Agent.SetDestination(position);
		lastDestinationTime = time;
	}

	public void SetCrouch(bool crouch)
	{
		if (IsCrouch == crouch || time - lastCrouchTime < 0.5f) return;
		lastCrouchTime = time;
		if (crouch && (AgentState == AIAgentState.Following || AgentState == AIAgentState.Looking))
		{
			crouch = false;
		}

		References.aiAnimation?.SetBoolParameter("IsCrouch", crouch);
		// Speed = crouch ? aiSettings.CrouchSpeed : aiSettings.WalkSpeed;
		if (IsCrouch != crouch)
		{
			var data = bl_UtilityHelper.CreatePhotonHashTable();
			data.Add("type", AIRemoteCallType.CrouchState);
			data.Add("state", crouch);

			photonView.RPC(RPC_NAME, RpcTarget.Others, data);
			IsCrouch = crouch;
		}
	}

	public override void UpdateTargetList(bool force = false)
	{
		if (Target != null && !force)
		{
			return;
		}

		bl_AIManager.Instance.GetTargetsFor(this, ref availableTargets);
		AimTarget.name = AIName;
	}

	public override void CheckTargets()
	{
		if (Target != null && Target.name.Contains("(die)"))
		{
			SetTarget(null);
		}
	}

	public override void OnSmokeBombArea(Vector3 smokeOrigin, WeaponDef weaponDef)
	{
		if(CurrentState == null) return;
		if(CurrentState.GetType() == typeof(Avoiding)) return;
		if(CurrentState.GetType().IsSubclassOf(typeof(Avoiding))) return;
		
		CurrentState.OnSmokeBombArea(smokeOrigin, weaponDef);
	}

	public override void OnFlashBombArea(Vector3 flashOrigin, WeaponDef weaponDef)
	{
		if(CurrentState == null) return;
		if(CurrentState.GetType() == typeof(Avoiding)) return;
		if(CurrentState.GetType().IsSubclassOf(typeof(Avoiding))) return;
		
		CurrentState.OnFlashBombArea(flashOrigin, weaponDef);
	}

	/// <summary>
	/// This is called when a forced bot respawn is called and this bot is still alive
	/// </summary>
	public override void Respawn()
	{
		AIHealth.ClearBotDamageHint();

		var data = bl_UtilityHelper.CreatePhotonHashTable();
		data.Add("type", AIRemoteCallType.Respawn);
		photonView.RPC(RPC_NAME, RpcTarget.All, data);
	}

	/// <summary>
	/// 
	/// </summary>
	public void FootStep()
	{
		if (velocityMagnitud > 0.2f) { }
	}

	[System.Serializable]
	public class NetworkMessagesCount
	{
		public AIRemoteCallType CallType;
		public int Count;
	}
	public List<NetworkMessagesCount> networkMessagesCounts = new List<NetworkMessagesCount>();

	[PunRPC]
	public void RPCShooterBot(NetHashTable data, PhotonMessageInfo info)
	{
		if(_isBeingDestroyed) { return; }

		var callType = (AIRemoteCallType)data["type"];

		var cid = networkMessagesCounts.FindIndex(x => x.CallType == callType);
		if(cid == -1)
		{
			networkMessagesCounts.Add(new NetworkMessagesCount()
			{
				CallType = callType,
				Count = 1
			});
		}
		else
		{
			networkMessagesCounts[cid].Count++;
		}

		switch (callType)
		{
			case AIRemoteCallType.DestroyBot:
				DestroyBot(data, info);
				break;
			case AIRemoteCallType.SyncTarget:
				SyncTargetAI(data);
				break;
			case AIRemoteCallType.CrouchState:
				Anim.SetBool(animationHash[0], (bool)data["state"]);
				break;
			case AIRemoteCallType.Respawn:
				DoAliveRespawn();
				break;
		}
	}

	/// <summary>
	/// Do a respawn without destroying the bot instance
	/// </summary>
	private void DoAliveRespawn()
	{
		DebugEx.Log($"[AIShooterAgent] DoAliveRespawn() name:{AIName}");
		if (PhotonNetworkEx.IsMasterClient)
		{
			ToggleMovable(true);
		}
		AIHealth.SetHealth(100, true);
		isGameStarted = false;
		Target = null;
		/*--
		this.InvokeAfter(2, () => 
		{
			isGameStarted = BattleManager.Instance.GameState == GameStates.Playing;
		});
		--*/
		if (PhotonNetworkEx.IsMasterClient)
		{
			var spawn = bl_SpawnPointManager.Instance.GetSequentialSpawnPoint(AITeam).transform;
			Agent.Warp(spawn.position);
			m_Transform.position = spawn.position;
			m_Transform.rotation = spawn.rotation;
		}	  
	}

	/// <summary>
	/// Called from Master Client on all clients when a bot die
	/// </summary>
	public void DestroyBot(NetHashTable data, PhotonMessageInfo info)
	{
		if (data.ContainsKey("instant"))
		{
			if (PhotonNetworkEx.IsMasterClient) PhotonNetworkEx.Destroy(gameObject);
			return;
		}

		Vector3 position = (Vector3)data["direction"];
		References.aiAnimation?.Ragdolled(position, data.ContainsKey("explosion"));

		DOTween.Kill(this);

		//if (GetGameMode.GetGameModeInfo().OnRoundStartedSpawn == OnRoundStartedSpawn.WaitUntilRoundFinish)
		//{
		//	this.InvokeAfter(bl_GameData.Instance.PlayerRespawnTime, () =>
		//	{
		//		if (bl_PhotonNetwork.IsMasterClient && BotMFPSActor?.isAlive == false)
		//		{
		//			bl_PhotonNetwork.Destroy(gameObject);
		//		}
		//	});
		//}
	}

	/// <summary>
	/// 
	/// </summary>
	void SyncTargetAI(NetHashTable data)
	{
		var view = (int)data["viewID"];
		if (view == -1)
		{
			SetTarget(null);
			return;
		}

		GameObject pr = FindPlayerRoot(view);
		if (pr == null)
		{
			DebugEx.AILog($"[bl_AIShooterAgent] {AIName} Couldn't find target's network view: {view}");
			return;
		}

		Transform t = pr.transform;
		if (t != null)
		{
			SetTarget(t);
		}
	}

	/// <summary>
	/// 
	/// </summary>
	void OnLocalSpawn()
	{
		if (!isOneTeamMode && bl_MFPS.LocalPlayer.Team == AITeam)
		{
			References.namePlateDrawer.enabled = true;
		}
	}

	/// <summary>
	/// When a new player joins in the room
	/// </summary>
	/// <param name="newPlayer"></param>
	public void OnPhotonPlayerConnected(Player newPlayer)
	{
		if (PhotonNetworkEx.IsMasterClient && newPlayer.ActorNumber != PhotonNetworkEx.LocalPlayer.ActorNumber)
		{
			// received in bl_AIShooterHealth
			photonView.RPC("RpcSyncHealth", newPlayer, AIHealth.GetHealth());
		}
	}

	/// <summary>
	/// 
	/// </summary>
	protected override void OnEnable()
	{
		base.OnEnable();
		
		DebugEx.Log($"[bl_AIShooterAgent] {AIName} OnEnable()");
		
		bl_EventHandler.onLocalPlayerSpawn += OnLocalSpawn;
		bl_EventHandler.onRoundStart += OnRoundStart;
		bl_EventHandler.onGameStateChanged += OnGameStateChange;
		bl_EventHandler.onPlayerDebuff += OnPlayerDebuff;
		
		if (BattleManager.Instance.GetGameMode == BattleMode.DM)
		{
			PhotonNetworkEx.Instance.AddCallback(PropertiesKeys.DMBombEvent, OnDMEventReceived);
			PhotonNetworkEx.Instance.AddCallback(PropertiesKeys.DemolitionEvent, OnDemolitionEventReceived);
		}
		
		//PhotonNetworkEx.Instance.AddCallback(PropertiesKeys.UAVActiveEvent, OnUAVActive);
	}

	/// <summary>
	/// 
	/// </summary>
	protected override void OnDisable()
	{
		base.OnDisable();
		
		bl_EventHandler.onLocalPlayerSpawn -= OnLocalSpawn;
		bl_PhotonCallbacks.PlayerEnteredRoom -= OnPhotonPlayerConnected;
		bl_EventHandler.onRoundStart -= OnRoundStart;
		bl_EventHandler.onGameStateChanged -= OnGameStateChange;
		bl_EventHandler.onPlayerDebuff -= OnPlayerDebuff;

		if (PhotonNetworkEx.Instance != null)
		{
			PhotonNetworkEx.Instance.RemoveCallback(OnDMEventReceived);
			PhotonNetworkEx.Instance.RemoveCallback(OnDemolitionEventReceived);
			//PhotonNetworkEx.Instance.RemoveCallback(OnUAVActive);
		}

		if (FrameworkApp.IsApplicationQuiting || SceneManager.IsSceneDestroying()) { return; }

		if (BattleManager.Instance.GetGameMode == BattleMode.DM)
		{
			if (DemolitionBombManager.Instance)
			{
				DemolitionBombManager.Instance.TryRecoverBomb(gameObject);
			}
		}
	}

	/// <summary>
	/// 
	/// </summary>
	void OnGameStateChange(BattleStates state)
	{
		isGameStarted = (state == BattleStates.Playing);
	}
	
	void OnPlayerDebuff(bl_EventHandler.PlayerDebuffData data)
	{
		if(data.ViewID != photonView.ViewID) return;
		
		switch (data.DebuffType)
		{
			case DebuffType.Flash:
				{
					OnFlashBombArea(data.OriginPos, data.WeaponDef);
					ActiveDizzyIconInNamePlate(data.WeaponDef.DamageDuration);
				}
				break;
			
			case DebuffType.Smoke:
				{
					OnSmokeBombArea(data.OriginPos, data.WeaponDef);
				}
				break;
		}
	}
	
	public void ActiveDizzyIconInNamePlate(float disableAfterTime)
	{
		var token = this.GetCancellationTokenOnDestroy();
		
		ToggleDizzyIconInNamePlate(true);
		
		if (disableAfterTime > 0)
		{
			DoTask(() =>
			{
				ToggleDizzyIconInNamePlate(false);
			}).Forget();
		}
		return;

		async UniTaskVoid DoTask(Action onFinished)
		{
			await UniTask.WaitForSeconds(disableAfterTime, cancellationToken: token);
			onFinished?.Invoke();
		}
	}
	
	public void ToggleDizzyIconInNamePlate(bool isOn)
	{
		References.namePlateDrawer.ToggleDizzyIcon(isOn);
		References.BlindEffect.SetActiveGo(isOn);
	}

	/// <summary>
	/// 
	/// </summary>
	void OnRoundStart()
	{
		isGameStarted = BattleManager.Instance.GameState == BattleStates.Playing;
		UpdateTargetList();
	}

	public override void OnDeath() 
	{
		ToggleDizzyIconInNamePlate(false);
		AIStateManager.Instance.UnregisterAgent(this);
		CancelInvoke(); 
	}

	public override Vector3 TargetDirection
	{
		get
		{
			if (Target != null) 
			{
				PhotonView view = GetPhotonView(Target.root.gameObject);
				if (view == null) return Vector3.zero;

				Vector3 originTarget = GetTargetPositionFromPlayerTransform(view);
				
				if (view.ViewID != prevTargetViewID)
				{
					prevCachedTargetDirection = GetDirToTarget(originTarget);
					
					prevTargetViewID = view.ViewID;
				}
				else
				{
					_lastSpreadUpdateTime += Time.deltaTime;

					if (_lastSpreadUpdateTime > _spreadUpdateInterval)
					{
						prevCachedTargetDirection = GetDirToTarget(originTarget);
						
						_lastSpreadUpdateTime = 0;
					}
					
					// DebugEx.Log($"[AIShooter] LastSpreadUpdateTime : {_lastSpreadUpdateTime}");
				}
				
				return originTarget + prevCachedTargetDirection;
			}
			
			//팀전이고, 타겟이 없을 때 [아군이 감지한 적을 타겟으로 정한다.]
			if (!isOneTeamMode && availableTargets.Count > 0)
			{
				Transform t = GetNearestPlayer();

				Vector3 originTarget = Vector3.zero;
				if (t != null)
				{
					PhotonView pV = t.GetComponent<PhotonView>();
				
					if (pV != null)
					{
						originTarget = GetTargetPositionFromPlayerTransform(pV);
						
						if (pV.ViewID != prevTargetViewID)
						{
							prevCachedTargetDirection = GetDirToTarget(originTarget);

							prevTargetViewID = pV.ViewID;
						}
						else
						{
							_lastSpreadUpdateTime += Time.deltaTime;
						
							if (_lastSpreadUpdateTime > _spreadUpdateInterval)
							{
								prevCachedTargetDirection = GetDirToTarget(originTarget);
						
								_lastSpreadUpdateTime = 0;
							}
						}
					}
				}
				
				return originTarget + prevCachedTargetDirection;
			}
			
			return Vector3.zero;
		}
	}

	Vector3 GetDirToTarget(Vector3 originTarget)
	{
		Vector3 direction = originTarget - transform.position;
		
		if (Target == null) return direction.normalized;
		
		float distanceToTarget = bl_UtilityHelper.Distance2D(originTarget, transform.position);
		if(distanceToTarget < 4.0f) return direction.normalized;
		
		Vector3 normalizedTargetDirection = DecideTargetPosition(direction, GetSpreadAngle);
		return normalizedTargetDirection;
	}

	//감지된 대상의 Tranform으로 부터 실제 타겟 기준점을 잡는다.
	Vector3 GetTargetPositionFromPlayerTransform(PhotonView view)
	{
		Vector3 targetPosition = Vector3.zero;
		
		bool isBot = view.GetComponent<bl_AIShooterAgent>() != null;
		//Bot인 경우
		if (isBot)
		{
			targetPosition = view.GetComponent<bl_AIShooterAgent>().aimTarget.position;
			return targetPosition;
		}
		//유저인 경우
		else
		{
			bl_PlayerReferences playerReferences = view.GetComponent<bl_PlayerReferences>();
			if (playerReferences != null)
			{
				targetPosition = playerReferences.weaponCamera.transform.position + Vector3.down * 0.4f;
				return targetPosition;
			}
		}
		
		return view.transform.position;
	}

	//실제 타겟팅 지점을 보정을 줘서 지정한다.
	Vector3 DecideTargetPosition(Vector3 direction, float spreadAngle)
	{
		float distance = direction.magnitude;
		direction = direction.normalized;

		float distanceFactor = 1f + (distance * 0.5f);    

		// spreadAngle 확장
		float scaledSpread = spreadAngle * distanceFactor;
		float halfSpread = scaledSpread * 0.5f;

		// 방향 기준 회전
		Quaternion baseRot = Quaternion.LookRotation(direction);

		// distance 에 비례해 퍼짐각 증가
		float randomYaw = Random.Range(-halfSpread, halfSpread);

		Quaternion randRot = Quaternion.Euler(0f, randomYaw, 0f);

		Vector3 spreadDir = baseRot * randRot * Vector3.forward;
		return spreadDir.normalized;
	}
	
	
	
	/// <summary>
	/// 
	/// </summary>
	/// <returns></returns>
	public Transform GetNearestPlayer()
	{
		if (availableTargets.Count > 0)
		{
			Transform t = null;
			float d = 1000;
			for (int i = 0; i < availableTargets.Count; i++)
			{
				if (availableTargets[i] == null || availableTargets[i].name.Contains("(die)")) continue;
				float dis = bl_UtilityHelper.Distance(m_Transform.localPosition, availableTargets[i].position);
				if (dis < d)
				{
					d = dis;
					t = availableTargets[i];
				}
			}
			return t;
		}
		else { return null; }
	}

	private PlayerSeat m_MFPSActor;

	/// <summary>
	/// 주의: Null일 수 있음.
	/// </summary>
	public PlayerSeat BotMFPSActor
	{
		get
		{
			if (m_MFPSActor == null) { m_MFPSActor = BattleManager.Instance.FindMFPSPlayerByNickname(AIName); }
			return m_MFPSActor;
		}
	}
	public float TargetDistance { get { return bl_UtilityHelper.Distance(m_Transform.position, this.TargetDirection); } }
	public bool HasATarget { get => Target != null; }
	
	/// <summary>
	/// 사격 지연 시간 초기화
	/// </summary>
	/// <param name="force"></param>
	public void ResetShootDelayTimer(Transform target, bool isInit = false)
	{
		ShootWaitTimer = 0;

		if (aiSettings != null)
		{
			if (isInit)
			{
				OriginDelayTime = ShootDelayTime = aiSettings.EnableShootDelay ? 
					Random.Range(aiSettings.ShootDelayMinTime, aiSettings.ShootDelayMaxTime) : 0;
				
				//DebugEx.Log($"[Bot] BotCDB ID : {aiSettings.No}, 최초 ShootDelayTime 결정 : {ShootDelayTime}");
			}
			else
			{
				if (target == null)
				{
					ShootDelayTime = OriginDelayTime;
					return;
				}
				
				//Note. 초보로 추정되는(KD 값으로 대조) 실제 플레이어가 Target이 되는 경우, 사격 지연 시간을 재조정한다.
				PhotonView view = target.GetComponent<PhotonView>();
				if (view != null)
				{
					float? kdrValue = BattleManager.Instance.GetKDValueIfRealPlayer(view.ViewID);
					float[] immatureKDRange = MiscCDB.Instance.ImmatureUserKD;

					/*
					if (kdrValue.HasValue)
					{
						DebugEx.Log($"[Bot] BotCDB ID : {aiSettings.No}, 타겟의 KDR 값 {kdrValue.Value}");	
					}
					*/

					if (kdrValue.HasValue)
					{
						var clampedKdr = Mathf.Clamp(
							kdrValue.Value, 
							immatureKDRange[0], 
							immatureKDRange[1]);

						//test code
						//clampedKdr = 6.0f;
						//end test code 
						
						//immatureKDRange 에서 정규화된 kdr값의 위치를 찾고
						float t = Mathf.InverseLerp(
							immatureKDRange[0], 
							immatureKDRange[1], 
							clampedKdr);
						
						float min = Mathf.Lerp(
							aiSettings.ShootDelayMinTime, 
							aiSettings.ImmatureUserShootDelayMinTime, 
							1 - t);

						float max = Mathf.Lerp(
							aiSettings.ShootDelayMaxTime, 
							aiSettings.ImmatureUserShootDelayMaxTime,
							1 - t);
						
						ShootDelayTime = Random.Range(min, max);
						
						//DebugEx.Log($"[Bot] BotCDB ID : {aiSettings.No}, KDR : {kdrValue.Value}, Min : {min}, Max : {max}");
					}
					else
					{
						OriginDelayTime = ShootDelayTime = aiSettings.EnableShootDelay ? 
							Random.Range(aiSettings.ShootDelayMinTime, aiSettings.ShootDelayMaxTime) : 0;
					}
				}
				// 그 외에는 최초 지정된 ShootDelayTime 으로 유지한다.
				else
				{
					ShootDelayTime = OriginDelayTime;
					
					//DebugEx.Log($"[Bot] BotCDB ID : {aiSettings.No}, 원래값으로 ShootDelayTime 재조정 : {ShootDelayTime}");
				}
			}
		}
	}

	public void UpdateWayPoints(List<Vector3> newWayPoints)
	{
		CurrentState.MakeWayPoints(newWayPoints);
	}

#if UNITY_EDITOR
	private void OnDrawGizmos()
	{
		if (isDeath) return;
		if (Agent != null && this.Target != null)
		{
			Gizmos.color = Color.white;
			Gizmos.DrawWireCube(References.VisionCheckStartPoint.position, Vector3.one * 0.3f);
			Gizmos.DrawWireCube(GetTargetPositionFromPlayerTransform(Target.GetComponent<PhotonView>()), Vector3.one * 0.3f);
		}
	}
#endif
}