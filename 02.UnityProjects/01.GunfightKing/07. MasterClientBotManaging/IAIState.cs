using Framework;
using Game.Data;
using Game.View.BattleSystem;
using MFPS.Runtime.AI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Game.View.AI.State
{
	public enum EVENT
	{
		ENTER,
		UPDATE,
		EXIT
	}

	/// AI에 대한 FSM에 사용되는 State의 원형으로서, 상속받아 실제 상태를 정의한다.
	public abstract class IAIState
    {
		protected bl_AIShooterAgent shooterAgent;
		protected EVENT stage;
		protected IAIState nextState;
		protected float currentTime = 0;

		protected float reactionTime = 0;       //반응 소요 시간

		protected List<Transform> detactedTargets = new List<Transform>();       //시야에 보이는 적
        protected List<Transform> visableTargets = new List<Transform>();
        
        protected List<Vector3> wayPoints = new List<Vector3>();
        protected int currentWayPointIndex = 0;
        
        protected float repathInterval = 1.0f;
        protected float currentRepathTime = 0.0f;

        protected bool _isBackingOff;
        
		private bool _isTerrorlistTeam;
		protected bool IsTerrorlistTeam
		{
			get
			{
				return _isTerrorlistTeam;
			}
			set
			{
				_isTerrorlistTeam = value;
			}
		}

		private bool _isDMMode;
		protected bool IsDMMode
		{
			get
			{
				return _isDMMode;
			}
			set
			{
				_isDMMode = value;
			}
		}

		public virtual void Enter() 
		{
			shooterAgent.CurrentState = this;
			shooterAgent.ToggleMovable(true);

			DecideAgentSpeed();

			IsDMMode = BattleManager.Instance.GetGameMode == BattleMode.DM;
			if (IsDMMode) 
			{
				IsTerrorlistTeam = shooterAgent.AITeam == DemolitionMode.Instance.AttackTeam;
			}

			reactionTime = shooterAgent.aiSettings.ReactSpeed;

			stage = EVENT.UPDATE;
		}

		public virtual void Update() 
		{
			stage = EVENT.UPDATE;
			
			UpdateMoving();
		}

		public void DecideAgentSpeed()
		{
			if (GetType().IsSubclassOf(typeof(Searching)))
			{
				shooterAgent.Agent.speed = shooterAgent.aiSettings.RunSpeed;
			}
			else if (GetType().IsSubclassOf(typeof(Attacking)))
			{
				shooterAgent.Agent.speed = shooterAgent.aiSettings.WalkSpeed;
			}
			else
			{
				shooterAgent.Agent.speed = shooterAgent.aiSettings.RunSpeed;
			}
		}

		//폭탄 할당 받은 이후 행동 결정
		public void UpdateAttackerBombAssignedBehavior()
		{
			if (!shooterAgent.IsCarrier) return;
			if (DemolitionBombManager.Instance.Bomb.bombStatus == BombStatus.Actived) return;
            if (!CanChangeStateInBombEvent()) return;
            
            DebugEx.Log($"[Bomb] {shooterAgent.AIName} [공격조] UpdateAttackerBombAssignedBehavior", LogColorType.Red);

            bl_AIManager aiManager = bl_AIManager.Instance; 
            DemolitionBombZone targetBombZone = aiManager.SetRandomTargetBombZone();
            Vector3 begin = shooterAgent.transform.position;
            Vector3 end = targetBombZone.transform.position;
			
            bl_AIManager.Instance.UpdateBombAssignerPath(shooterAgent.AITeam, shooterAgent.References.aiShooter.GroupID, begin, end);
            bl_AIManager.Instance.UpdateBombAssignerGroupPath(shooterAgent);
            
            nextState = new DemolitionAreaSearching(shooterAgent);
            Exit();
		}

		//드랍된 폭탄 회수 행동 할지 여부 결정
        public void UpdateAttackerBombDroppedBehavior()
        {
            if (!CanChangeStateInBombEvent()) return;

			DemolitionBombManager demolitionBombManager = DemolitionBombManager.Instance;
			if (demolitionBombManager.Bomb == null) return;

			DemolitionBomb bomb = demolitionBombManager.Bomb;
			if (bomb == null) return;

			bool isBombActivated = bomb.bombStatus == BombStatus.Actived;
			if (isBombActivated) return;

			AIStateManager aiStateManager = AIStateManager.Instance;

			//이미 드랍된 폭탄 회수 행동을 하고 있는 AI 가 있다면 pass
			List<bl_AIShooterAgent> otherUnits = aiStateManager.GetAgentsInState<BombSearching>();
			if (otherUnits.Count > 0)
			{
				foreach (bl_AIShooterAgent otherUnit in otherUnits)
				{
					if (otherUnit.CurrentState is BombSearching bombSearching) { bombSearching.UpdateDestinationToBomb(); }
				}
				return;
			}

			DebugEx.Log("[Bomb] [" + shooterAgent.name + "] " + "[공격조] UpdateAttackerBombDroppedBehavior called...", LogColorType.Red);
			nextState = new BombSearching(shooterAgent);
			Exit();
		}

		//폭탄 해체 행동 할지 여부 결정
        public void UpdateDefenderBombActivatedBehavior()
        {
	        var bombDefusingBots = AIStateManager.Instance.GetAgentsInState<BombDefusing>();
	        if (bombDefusingBots.Count > 0) { return; }
	        
			bl_AIShooter targetBot = bl_AIManager.Instance.GetClosestBotFromBombInstalledZone(DemolitionMode.Instance.AttackTeam.OppositeTeam());
			if(targetBot == null) return;

			bl_AIShooterAgent targetBotAgent = (bl_AIShooterAgent)targetBot;
			if (targetBotAgent == this.shooterAgent)
			{
				if (this.shooterAgent.CurrentState.GetType() != typeof(BombDefusing))
				{
					DebugEx.Log($"[Bomb] {shooterAgent.name} 폭탄 해체 임무 배정받음...{shooterAgent.CurrentState}", LogColorType.Red);
					
					nextState = new BombDefusing(shooterAgent);
					Exit();	
				}
			}
			else
			{
				if (GetType() != typeof(CoveringDefusing))
				{
					Vector3 bombLocation = GetBombLocation();

					Vector3 randomPoint = bombLocation + Random.insideUnitSphere * 2.0f;
					bool isFound = NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2.0f, NavMesh.AllAreas);
				
					shooterAgent.SetDestination(isFound ? hit.position : bombLocation);
					
					nextState = new CoveringDefusing(shooterAgent);
					Exit();
				}
			}
        }

        public virtual IAIState GetRandomAttackState()
        {
	        //begin test code
	        if (EditorLocalConfig.Instance.TestCodeOwner == "joonwon")
	        {
		        // return new CoveringAttacking(this);
	        }
	        //end test code

	        //init percentage
	        float[] percentageRange = new float[shooterAgent.aiSettings.AttackBehaviorPercentages.Length];
	        shooterAgent.aiSettings.AttackBehaviorPercentages.CopyTo(percentageRange, 0);

	        float sum = 0;

	        for (int i = 0; i < percentageRange.Length; i++)
	        {
		        sum += percentageRange[i];
		        percentageRange[i] = sum;
	        }

	        float randomValue = Random.Range(0, 100);

	        for (int i = 0; i < percentageRange.Length; i++) 
	        {
		        if(randomValue < percentageRange[i])
		        {
			        switch (i)
			        {
				        case 0:
					        return new AggressiveAttacking(shooterAgent);
				        case 1:
					        return new HoldingPositionAttacking(shooterAgent);
				        case 2:
					        return new MovingAttacking(shooterAgent);
				        case 3:
					        return new CoveringAttacking(shooterAgent);
			        }
		        }
	        }

	        //default attack 상태
	        return new AggressiveAttacking(shooterAgent);
        }

        public IAIState GetRandomDamagedState(Vector3 lastHitDirection)
        {
	        float[] percentageRange = new float[shooterAgent.aiSettings.AttackBehaviorPercentages.Length];
	        shooterAgent.aiSettings.AttackBehaviorPercentages.CopyTo(percentageRange, 0);

	        float sum = 0;

	        for (int i = 0; i < percentageRange.Length; i++)
	        {
		        sum += percentageRange[i];
		        percentageRange[i] = sum;
	        }

	        float randomValue = Random.Range(0, 100);

	        for (int i = 0; i < percentageRange.Length; i++)
	        {
		        if (randomValue < percentageRange[i])
		        {
			        if (i == 0) return new AggressiveAttacking(shooterAgent);	//저돌적 공격
			        else if (i == 1) return new HoldingPositionAttacking(shooterAgent);	//제자리 공격
			        else if (i == 2) return new MovingAttacking(shooterAgent);		//이동
			        else if (i == 3) return new AvoidAttacking(shooterAgent, lastHitDirection);		//회피
		        }
	        }

	        //default attack 상태
	        return new AggressiveAttacking(shooterAgent);
        }

        public virtual void OnSmokeBombArea(Vector3 smokeOrigin, WeaponDef weaponDef)
        {
	        //연막탄 중심에서 벗어나는 방향으로 이동한다. [반경을 벗어나는 위치를 목적지로 삼는다.]
	        Vector3 myPos = shooterAgent.transform.position;
	        Vector3 dirToAwayFromSmoke = (myPos - smokeOrigin).normalized;
	        
	        //반경보다는 조금 더 벗어나게 한다.
	        Vector3 targetPos = myPos + dirToAwayFromSmoke * weaponDef.Range * 1.5f;
	        
	        nextState = new SmokeAreaAvoiding(shooterAgent, targetPos, weaponDef.DamageDuration);
	        Exit();
        }

        public virtual void OnFlashBombArea(Vector3 flashBombOrigin, WeaponDef weaponDef)
        {
	        //무작위 위치로 이동한다. [앞이 안보이는 상황이므로]
	        Vector3 myPos = shooterAgent.transform.position;
	        Vector3 randomPoint = myPos + shooterAgent.transform.forward * weaponDef.Range;

	        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
	        {
		        randomPoint = hit.position;
	        }
	        
	        Vector3 dirToMove = (randomPoint - myPos).normalized;
	        Vector3 targetPos = myPos + dirToMove * weaponDef.Range * 1.5f;

	        nextState = new FlashAreaAvoiding(shooterAgent, targetPos, weaponDef.DamageDuration);
	        Exit();
        }

        private bool CanChangeStateInBombEvent()
        {
            if (this.GetType() == typeof(DemolitionAreaSearching)) return false;
            if (this.GetType() == typeof(Demolitioning)) return false;

            return true;
        }

		/// <summary>
		/// 낮은 주기의 Update 호출
		/// </summary>
		public virtual void SlowUpdate()
		{
			PlayerDetectionManager.Instance.UpdateBotVision(shooterAgent.photonView.ViewID);
			UpdateDetactedTargets(shooterAgent.aiSettings);

			if (IsDMMode)
			{
				DemolitionBombManager demolitionBombManager = DemolitionBombManager.Instance;
				DemolitionBomb bomb = demolitionBombManager.Bomb;
				
				switch (bomb.bombStatus)
				{
					case BombStatus.Droped:
						if (IsTerrorlistTeam) { UpdateAttackerBombDroppedBehavior(); }
						break;
					
					case BombStatus.Actived:
						if(!IsTerrorlistTeam){ UpdateDefenderBombActivatedBehavior(); }
						break;
				}
			}
		}

		public virtual void Exit() 
		{
			// canReact = false;

			stage = EVENT.EXIT;
			shooterAgent.CurrentState = nextState;

			if (shooterAgent.IsCrouch) { shooterAgent.SetCrouch(false); }
		}

		public IAIState(bl_AIShooterAgent shooterAgent)
		{
			this.shooterAgent = shooterAgent;
		}

		public IAIState Process()
		{
			if (BattleManager.Instance != null && BattleManager.Instance.GameState != BattleStates.Playing) return null;
			
			if (stage == EVENT.ENTER) Enter();
			if (stage == EVENT.UPDATE)
			{
				currentTime += Time.deltaTime;
				if(currentTime > reactionTime)
				{
					SlowUpdate();
					currentTime = 0;
				}

				currentTime += Time.deltaTime;

				Update();
			}
			if (stage == EVENT.EXIT)
			{
				Exit();
				return nextState;
			}
			return this;
		}
		
		//Update 문에서 주기적으로 호출된다.
		protected virtual void UpdateMoving()
		{
			if(_isBackingOff) return;
			
			//Target(적)이 지정되어 있으면 그곳으로 이동한다.
			Transform agentTarget = shooterAgent.Target;
			if ((agentTarget != null) && (currentRepathTime >= repathInterval))
			{
				bool isDie = agentTarget.name.Contains("die");
				if (!isDie)
				{
					shooterAgent.Agent.SetDestination(agentTarget.position);
					currentRepathTime = 0.0f;
				}
			}
			
			currentRepathTime += Time.deltaTime;

			if (shooterAgent.IsCrouch)
			{
				shooterAgent.Agent.speed = shooterAgent.aiSettings.CrouchSpeed;
			}
		}

		protected Vector3 GetBombLocation()
		{
			DemolitionBombManager demolitionBombManager = DemolitionBombManager.Instance;
			return demolitionBombManager.Bomb.transform.position;
		}

		protected void UpdateDetactedTargets(BotDef settings)
		{
			detactedTargets.Clear();

			DetectionGroup detectedPlayers = PlayerDetectionManager
				.Instance
				.GetDetectionGroup(shooterAgent);

			foreach (var detectionStatus in detectedPlayers.GetAllStatuses())
			{
				var playerSeat = detectionStatus.Player;
				if (playerSeat.Actor == null) { continue; }

				if (playerSeat.IsAliveAndValid)
				{
					detactedTargets.Add(playerSeat.Actor);	
				}
			}
		}

		protected Transform GetClosestDetactedTarget()
		{
			if(detactedTargets.Count == 0) return null;

			float minDistance = int.MaxValue;
			Transform resultVisibleTarget = null;
			foreach (Transform visibleTarget in detactedTargets)
			{
				if(visibleTarget == shooterAgent.Agent.transform) continue;
				if(visibleTarget == null) continue;

				float distance = Vector3.Distance(shooterAgent.transform.position, visibleTarget.position);
				if(distance < minDistance)
				{
					minDistance = distance;
					resultVisibleTarget = visibleTarget;
				}
			}

			return resultVisibleTarget;
		}

		protected bool UpdateVeryCloseEnemy()
		{
			Transform closestTarget = GetClosestDetactedTarget();
			if (closestTarget == null) return false;

			float distance = bl_UtilityHelper.Distance(shooterAgent.transform.position, closestTarget.position);
			
			//적을 설치지역에서 감지하더라도 폭탄설치 시도만 하고 공격을 하지 않는 경우 부자연스러워지므로
			//minRange 를 통한 대응이 필요하다
			if(distance <= shooterAgent.aiSettings.MinRange && IsVisibleTarget(closestTarget)) 
			{
				shooterAgent.SetTarget(closestTarget);
				shooterAgent.SetDestination(closestTarget.position);

				shooterAgent.SetLookAtState(AILookAt.Target);
				return true;
			}

			return false;
		}

		protected bool IsVeryCloseEnemyExist()
		{
			Transform closestTarget = GetClosestDetactedTarget();
			if (closestTarget == null) return false;

			float distance = bl_UtilityHelper.Distance(shooterAgent.transform.position, closestTarget.position);
			if (distance <= shooterAgent.aiSettings.MinRange && IsVisibleTarget(closestTarget))
			{
				return true;
			}

			return false;
		}

		protected bool IsBarrierAreaFront(Transform target)
		{
			RaycastHit raycastHit;
			LayerMask grenadeBarrier = 1 << 16;
			if (Physics.Linecast(shooterAgent.AIWeapon.GetFirePosition(), target.position, out raycastHit, grenadeBarrier))
			{
				return true;
			}

			return false;
		}

		protected virtual bool IsVisibleTarget()
		{
			if(shooterAgent == null) return false;
			if(shooterAgent.Target == null) return false;

			return !shooterAgent.ObstacleBetweenTarget;
		}

		protected virtual bool IsVisibleTarget(Transform target)
		{
			RaycastHit obsRay;
			int playerLayer = 9;
			if (shooterAgent.AIWeapon == null || target == null) return false;
			
			if (Physics.Linecast(shooterAgent.AIWeapon.GetFirePosition(), target.position, out obsRay))
			{
				bool isObstacleExist = obsRay.transform.gameObject.layer != playerLayer;
				return !isObstacleExist;
			}

			return true;
		}

		protected virtual bool IsFront(Transform target)
		{
			Vector3 relative = shooterAgent.transform.InverseTransformPoint(target.position);
			return (relative.x < 2f && relative.x > -2f) || (relative.x > -2f && relative.x < 2f);
		}

		protected virtual bool IsCloseToFire()
		{
			if (shooterAgent == null) return false;

			Vector3 myPos = shooterAgent.Agent.transform.position;

			float distanceToTarget = bl_UtilityHelper.Distance2D(myPos, shooterAgent.TargetDirection);
			bool isCloseToFire = distanceToTarget <= shooterAgent.aiSettings.FiringDistance;
			
			// DebugEx.Log($"SJW 300 distanceToTarget : {distanceToTarget}");
			return isCloseToFire;
		}

		public virtual void MakeWayPoints(List<Vector3> _wayPoints)
		{
			currentWayPointIndex = 0;
			wayPoints.Clear();
			
			foreach (Vector3 point in _wayPoints)
			{
				Vector3 randomPoint = point + Random.insideUnitSphere * 2.0f;
				
				bool isFound = NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2.0f, NavMesh.AllAreas);
				wayPoints.Add(isFound ? hit.position : point);
			}
			
			if(wayPoints.Count > 0) shooterAgent.Agent.SetDestination(wayPoints[0]);
		}

		protected virtual void MoveToNextWayPoint()
		{
			//목적지 도착 이후 무작위 NormalPoint 이동을 원하지 않는 경우 MoveToNextWayPoint 함수 override
			if (wayPoints == null || wayPoints.Count < 1 || (currentWayPointIndex + 1 > wayPoints.Count))
			{
				bl_AIManager aiManager = bl_AIManager.Instance;
				List<Vector3> newPoints = new List<Vector3>();
				Vector3 newPoint = aiManager.GetRandomNormalWayPoint().transform.position;
				newPoints.Add(newPoint);
				MakeWayPoints(newPoints);
				
				return;
			}

			currentWayPointIndex++;

			if (currentWayPointIndex < wayPoints.Count)
			{
				shooterAgent.Agent.SetDestination(wayPoints[currentWayPointIndex]);
			}
		}

		protected bool IsLastWayPoint()
		{
			return currentWayPointIndex >= wayPoints.Count - 1;
		}

		protected virtual void DecideRandomSpeed()
		{
			bool toWalk = Random.Range(0, 100) < 50;
			shooterAgent.Agent.speed = toWalk ? 
				shooterAgent.aiSettings.WalkSpeed : 
				shooterAgent.aiSettings.RunSpeed;
		}
	}
}
