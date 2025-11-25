using Framework;
using MFPS.Runtime.AI;
using UnityEngine;
using UnityEngine.AI;

namespace Game.View.AI.State
{
	/// <summary>
	/// Bot이 공격하는 행동 패턴 계열
	/// </summary>
	public class Attacking : IAIState
	{
		public Attacking(bl_AIShooterAgent shooterAgent) : base(shooterAgent)
		{
			if(shooterAgent.IsCrouch) shooterAgent.SetCrouch(false);
		}
		
		public override void Update()
		{
			base.Update();

			if (shooterAgent.IsAlive()) { CheckFiring(); }
		}

		/// <summary>
		/// 사격 조건이 부합한다면, 사격한다.
		/// </summary>
		void CheckFiring()
		{
			Transform agentTarget = shooterAgent.Target;
			if (agentTarget !=null)
			{
				bool isDie = agentTarget.name.Contains("die");
				
				if (IsVisibleTarget() && IsCloseToFire() && !isDie)
				{
					float velocityMagnitude = shooterAgent.Agent.velocity.magnitude;
					bl_AIShooterAttackBase.FireReason fireReason = velocityMagnitude > 0.2f ? bl_AIShooterAttackBase.FireReason.OnMove : bl_AIShooterAttackBase.FireReason.Normal;

					// 시야선에 연막탄, 섬광탄이 존재하면 탄퍼짐을 더 증가시킨다.
					shooterAgent.References.shooterWeapon.FiringInaccuracyOffset = 
						IsBarrierAreaFront(agentTarget) ? 2.0f : 1.0f;
					
					if (IsFront(agentTarget)) { shooterAgent.TriggerFire(fireReason); }
					
					shooterAgent.SetLookAtState(AILookAt.Target);
				}
			}
			else
			{
				ToggleToSearchingState();
			}
		}
		
		void ToggleToSearchingState()
		{
			shooterAgent.SetLookAtState(AILookAt.PathToTarget);
			
			nextState = new Searching(shooterAgent);
			Exit();
		}

		protected void SetCrouchOrStand()
		{
			int rndIndex = Random.Range(0, 2);
			bool isCrouch = rndIndex == 1;
			shooterAgent.SetCrouch(isCrouch);
		}
	}

	/// <summary>
	/// 적에게 초근접하는 공격 형태
	/// </summary>
	public class AggressiveAttacking : Attacking
	{
		private float _agressiveTime = 5.0f;		//회피 공격을 유지하는 시간(초)
		private float _currentAggresiveTime = 0.0f;
		
		public AggressiveAttacking(bl_AIShooterAgent shooterAgent) : base(shooterAgent) { }

		public override void Enter()
		{
			base.Enter();

			_agressiveTime = shooterAgent.aiSettings.AttackBehaviorRemainTimes[0];
		}

		public override void Update()
		{
			if (_currentAggresiveTime > _agressiveTime)
			{
				nextState = IsVisibleTarget(shooterAgent.Target) ? 
					GetRandomAttackState() : 
					new Searching(shooterAgent);
				
				Exit();
			}
			
			_currentAggresiveTime += Time.deltaTime;
		}
	}

	/// <summary>
	/// [회피] 현재 지역에서 벗어남 - 뒷걸음질 치면서 사격하다가 적당한 시점에 Crouch 하기도 한다.
	/// </summary>
	public class AvoidAttacking : Attacking
	{
		private Vector3 lastHitDirection = Vector3.zero;
		private float _avoidTime = 5.0f;		//회피 공격을 유지하는 시간(초)
		private float _currentAvoidTime = 0.0f;
		
		public AvoidAttacking(bl_AIShooterAgent shooterAgent, Vector3 lastHitDirection) : base(shooterAgent) 
		{
			this.lastHitDirection = lastHitDirection.normalized;
		}

		public override void Enter()
		{
			base.Enter();

			//총알이 날아온 방향 (추측)
			Vector3 targetCoverPoint = GetCloseCoverPoint(lastHitDirection * 10.0f);
			
			shooterAgent.SetLookAtState(AILookAt.Target);
			shooterAgent.SetDestination(targetCoverPoint);

			_currentAvoidTime = 0.0f;
			
			SetCrouchOrStand();
		}

		public override void Update()
		{
			base.Update();

			//은폐 지역 근처에 다다르거나 회피 공격 유지시간을 초과하면 다시 탐색 상태로 전환한다.
			if (_currentAvoidTime > _avoidTime)
			{
				nextState = IsVisibleTarget(shooterAgent.Target) ? 
					GetRandomAttackState() : 
					new Searching(shooterAgent);
				
				Exit();
			}
			
			_currentAvoidTime += Time.deltaTime;
		}

		private Vector3 GetCloseCoverPoint(Vector3 moveDir)
		{
			return bl_AICoverPointManager.Instance.GetCoverOnRadius(shooterAgent.transform, 10, moveDir);
		}
	}

	/// <summary>
	/// 전방/후방/좌/우 이동하면서 또는 앉아서 공격하는 형태
	/// </summary>
	public class MovingAttacking : Attacking
	{
		private float _movingAttackTime = 5.0f;		//공격을 유지하는 시간(초)
		private float _currentMovingAttackTime = 0.0f;
		
		public MovingAttacking(bl_AIShooterAgent shooterAgent) : base(shooterAgent) { }

		public override void Enter()
		{
			base.Enter();

			Vector3 rndTargetMovePosition = DecideRandomMove();
			shooterAgent.SetDestination(rndTargetMovePosition);
			_movingAttackTime = shooterAgent.aiSettings.AttackBehaviorRemainTimes[2];

			SetCrouchOrStand();
		}

		private Vector3 DecideRandomMove()
		{
			if (shooterAgent.Target == null) return shooterAgent.transform.position;

			Vector3 randomPoint = shooterAgent.transform.position + Random.insideUnitSphere * 4.0f;
			
			bool isFound = NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 4.0f, NavMesh.AllAreas);
			return isFound ? hit.position : shooterAgent.transform.position;
		}

		public override void Update()
		{
			if (_currentMovingAttackTime > _movingAttackTime)
			{
				nextState = IsVisibleTarget(shooterAgent.Target) ? 
					GetRandomAttackState() : 
					new Searching(shooterAgent);
				
				Exit();
			}
			
			_currentMovingAttackTime += Time.deltaTime;
		}
	}

	/// <summary>
	/// 제자리에서 공격하는 형태
	/// </summary>
	public class HoldingPositionAttacking : Attacking
	{
		float holdingPositionTime = 5.0f;
		float passTime = 0.0f;
		
		public HoldingPositionAttacking(bl_AIShooterAgent shooterAgent) : base(shooterAgent) { }
		
		public override void Enter()
		{
			base.Enter();
			
			shooterAgent.ToggleMovable(false);
			holdingPositionTime = shooterAgent.aiSettings.AttackBehaviorRemainTimes[1];
			
			passTime = 0.0f;
		}

		public override void Update()
		{
			base.Update();
			
			passTime += Time.deltaTime;

			if (passTime >= holdingPositionTime)
			{
				nextState = GetRandomAttackState();
				Exit();
			}
		}
	}

	/// <summary>
	/// [은폐] 근처 CoverPoint로 이동하여 공격하는 형태
	/// </summary>
	public class CoveringAttacking : Attacking
	{
		float coveringTime = 5.0f;
		float passTime = 0.0f;
		
		bl_AICoverPoint currentCoverPoint;

		public CoveringAttacking(bl_AIShooterAgent shooterAgent) : base(shooterAgent) { }

		public override void Enter()
		{
			base.Enter();

			coveringTime = shooterAgent.aiSettings.AttackBehaviorRemainTimes[3];
			MoveToNextCoverPoint();
		}

		/// <summary>
		/// from에서 가장 가까운 무작위 Cover Point를 찾는다,
		/// </summary>
		private bl_AICoverPoint GetCloseCoverPoint(Transform from, bl_AICoverPoint except = null)
		{
			if (except != null)
			{
				return bl_AICoverPointManager.Instance.GetCoverOnRadius(except, from, 10);
			}
			
			return bl_AICoverPointManager.Instance.GetCoverOnRadius(from, 10);
		}

		/// <summary>
		/// 현재 내 위치에서 가장 가까운 무작위 Cover Point를 찾는다.
		/// </summary>
		private bl_AICoverPoint GetCloseCoverPoint(bl_AICoverPoint except = null)
		{
			if (except != null)
			{
				return bl_AICoverPointManager.Instance.GetCoverOnRadius(except, shooterAgent.transform, 10);
			}
			
			return bl_AICoverPointManager.Instance.GetCoverOnRadius(shooterAgent.transform, 10);
		}

		protected override void UpdateMoving()
		{
			Transform agentTarget = shooterAgent.Target;
			if (agentTarget != null)
			{
				if (passTime >= coveringTime)
				{
					nextState = IsVisibleTarget(shooterAgent.Target) ? 
						GetRandomAttackState() : 
						new Searching(shooterAgent);
					
					Exit();
				}
			}
			
			passTime += Time.deltaTime; 
		}

		void MoveToNextCoverPoint(Transform from = null)
		{
			SetCrouchOrStand();
			
			if (from == null)
			{
				currentCoverPoint = GetCloseCoverPoint(currentCoverPoint);
			}
			else
			{
				currentCoverPoint = GetCloseCoverPoint(from, currentCoverPoint);
			}

			if (currentCoverPoint != null)
			{
				shooterAgent.SetDestination(currentCoverPoint.Position);
			}
		}
	}
}
