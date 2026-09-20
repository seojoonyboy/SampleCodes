using Framework;
using Game.View.BattleSystem;
using MFPS.Runtime.AI;
using System;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Game.View.AI.State
{
	/// <summary>
	/// Bot이 공격하는 행동 패턴 계열
	/// </summary>
	public class Attacking : IAIState
	{
		protected float MAX_MOVE_BACKWARD_TIME = 1.0f;		//몇초 동안 근접했을 때 뒤로 물러나는 수행을 할것인가
		protected float _moveBackwardTime = 0.0f;		//현재 뒤로 물러나는 수행을 진행한 시간
		protected bool _flagMoveBackward;
		
		public Attacking(bl_AIShooterAgent shooterAgent) : base(shooterAgent)
		{
			if(shooterAgent.IsCrouch) shooterAgent.SetCrouch(false);
			
			//상태가 공격상태로 새롭게 전환되면 사격 Delay를 초기화한다.
			shooterAgent.ResetShootDelayTimer(this.shooterAgent.Target);
			
			IsVisibleTarget(shooterAgent.Target);

			_flagMoveBackward = false;
			
			//DebugEx.Log($"[ShootDelay] 공격 상태 전환에 의한 Delay 초기화 [case002]");
			//DebugEx.Log($"{base.shooterAgent.AIName} 공격 상태 전환 {GetType().Name}");
		}
		
		public override void Update()
		{
			base.Update();

			if (!shooterAgent.IsAlive()) return;
			
			CheckFiring();
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
					
					DecideRandomSpeed();
					
					shooterAgent.TriggerFire(fireReason);
					
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

		protected void MoveBackwardIfTooCloseToTarget()
		{
			if(_moveBackwardTime > MAX_MOVE_BACKWARD_TIME) return;
			_moveBackwardTime += Time.deltaTime;
			
			if (shooterAgent.Target == null) return;
			
			float distToTarget = bl_UtilityHelper.Distance2D(
				shooterAgent.Target.position,
				shooterAgent.transform.position);

			float stopDistance = shooterAgent.aiSettings.StopDistance * 0.5f;

			// 너무 가까우면 뒤로 빠질 목적지 계산
			if (distToTarget < stopDistance && !_flagMoveBackward)
			{
				if(shooterAgent.Agent.isStopped) shooterAgent.ToggleMovable(true);
				
				_isBackingOff = true;
				
				shooterAgent.Agent.speed = 2.0f;
				// 타겟에서 나 쪽 방향 → 반대로(타겟으로부터 멀어지는 방향)
				Vector3 awayDir = (shooterAgent.transform.position - shooterAgent.Target.position);
				awayDir.y = 0;
				awayDir.Normalize();

				// stopDistance보다 좀 더 떨어진 위치를 목표로
				float desiredDistance = stopDistance;
				Vector3 desiredPos = shooterAgent.Target.position + awayDir * desiredDistance;

				// NavMesh에 투영
				if (NavMesh.SamplePosition(desiredPos, out NavMeshHit hit, 1.0f, NavMesh.AllAreas))
				{
					shooterAgent.SetDestination(hit.position);
					shooterAgent.SetLookAtState(AILookAt.Target);
				}

				// 뒤로 빠지는 동안 재경로 시간 초기화
				currentRepathTime = 0f;
				_flagMoveBackward = true;
			}
			else
			{
				_isBackingOff = false;
			}
		}

		protected void SetCrouchOrStand()
		{
			int rndIndex = Random.Range(0, 2);
			bool isCrouch = rndIndex == 1;

			if (isCrouch)
			{
				shooterAgent.Agent.speed = shooterAgent.aiSettings.CrouchSpeed;
			}
			
			shooterAgent.SetCrouch(isCrouch);
		}

		protected override void DecideRandomSpeed()
		{
			bool toWalk = Random.Range(0, 100) < 90;
			shooterAgent.Agent.speed = toWalk ? 
				shooterAgent.aiSettings.WalkSpeed : 
				shooterAgent.aiSettings.RunSpeed;
		}
	}

	/// <summary>
	/// 적에게 초근접하는 공격 형태
	/// </summary>
	public class AggressiveAttacking : Attacking
	{
		private float _agressiveTime = 3.0f;		//저돌적 공격을 유지하는 시간
		private float _currentAggresiveTime = 0.0f;
		
		public AggressiveAttacking(bl_AIShooterAgent shooterAgent) : base(shooterAgent) { }

		public override void Enter()
		{
			base.Enter();

			_agressiveTime = shooterAgent.aiSettings.AttackBehaviorRemainTimes[0];
			_moveBackwardTime = 0.0f;
			
			shooterAgent.SetLookAtState(AILookAt.Target);
		}

		public override void Update()
		{
			if (_currentAggresiveTime > _agressiveTime)
			{
				nextState = shooterAgent.Target != null ? 
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
			
			DebugEx.Log($"{shooterAgent.AIName} target cover point: {targetCoverPoint}");
			
			shooterAgent.SetLookAtState(AILookAt.Target);
			shooterAgent.SetDestination(targetCoverPoint);

			_currentAvoidTime = 0.0f;
			_moveBackwardTime = 0.0f;
			
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
			_moveBackwardTime = 0.0f;
			
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
		float _holdingPositionTime = 5.0f;
		float _holdingPassTime = 0.0f;
		
		public HoldingPositionAttacking(bl_AIShooterAgent shooterAgent) : base(shooterAgent) { }
		
		public override void Enter()
		{
			base.Enter();
			
			shooterAgent.ToggleMovable(false);
			_holdingPositionTime = shooterAgent.aiSettings.AttackBehaviorRemainTimes[1];
			_holdingPassTime = 0.0f;
			
			_moveBackwardTime = 0.0f;
		}

		public override void Update()
		{
			base.Update();
			
			_holdingPassTime += Time.deltaTime;

			if (!IsVisibleTarget())
			{
				nextState = new Searching(shooterAgent);
				Exit();
			}
			else
			{
				if (_holdingPassTime >= _holdingPositionTime)
				{
					nextState = GetRandomAttackState();
					Exit();
				}
				else
				{
					MoveBackwardIfTooCloseToTarget();
				}
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
			_moveBackwardTime = 0.0f;
			
			shooterAgent.SetLookAtState(AILookAt.Target);
			
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
