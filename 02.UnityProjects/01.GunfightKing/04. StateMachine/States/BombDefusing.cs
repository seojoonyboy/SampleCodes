using Framework;
using Game.View.BattleSystem;
using MFPS.Runtime.AI;
using UnityEngine;

namespace Game.View.AI.State
{
	/// <summary>
	/// 폭탄 해체 시도 상태
	/// </summary>
	public class BombDefusing : IAIState
	{
		public BombDefusing(bl_AIShooterAgent shooterAgent) : base(shooterAgent) { }

		public override void Enter()
		{
			base.Enter();

			DebugEx.Log("On BomDefusing State Enter " + shooterAgent.name);
			SetDestinationTargetToDefuse();
		}

		public override void SlowUpdate()
		{
			base.SlowUpdate();

			UpdateMoving();
			UpdateTargeting();
		}

		private void SetDestinationTargetToDefuse()
		{
			DemolitionBombManager demolitionBombManager = DemolitionBombManager.Instance;
			if (demolitionBombManager.Bomb == null) return;

			DemolitionBomb bomb = demolitionBombManager.Bomb;
			if (bomb == null) return;

			shooterAgent.SetDestination(bomb.transform.position);
			shooterAgent.Agent.stoppingDistance = 0.25f;
		}

		protected override void UpdateMoving()
		{
			if (!shooterAgent.IsCrouch) { shooterAgent.IsCrouch = false; }

			//폭탄 설치 가능 지역까지 도달한 경우
			DemolitionBombManager demolitionBombManager = DemolitionBombManager.Instance;
			if (demolitionBombManager.Bomb == null) return;

			DemolitionBomb bomb = demolitionBombManager.Bomb;
			if (bomb == null) return;

			bool isBombActivated = bomb.bombStatus == BombStatus.Actived;
			if (!isBombActivated)
			{
				nextState = new Searching(shooterAgent);
				Exit();
			}

			CheckFiring();
			
			bool isCloseToBomb = bl_AIManager.Instance.IsCloseToBombInstall(shooterAgent.transform.position);

			// DebugEx.Log("distanceToBomb : " + distanceToBomb);

			if (isCloseToBomb)
			{
				if (!shooterAgent.IsCrouch) { shooterAgent.SetCrouch(true); }

				demolitionBombManager.BotDefuseBomb(shooterAgent.BotMFPSActor);
				DebugEx.Log("[Bomb] Try Defusing by " + shooterAgent.name);
			}
		}

		void UpdateTargeting()
		{
			Transform agentTarget = shooterAgent.Target;
			if (agentTarget != null)
			{
				if (IsVisibleTarget() && IsCloseToFire())
				{
					DebugEx.AILog(string.Format("<color=yellow>[AvoidAttacking] 타겟 감지됨....from : {0} to : {1}</color>", shooterAgent.name, agentTarget.name));

					shooterAgent.SetLookAtState(AILookAt.Target);

					float velocityMagnitude = shooterAgent.Agent.velocity.magnitude;
					bl_AIShooterAttackBase.FireReason fireReason = velocityMagnitude > 0.2f ? bl_AIShooterAttackBase.FireReason.OnMove : bl_AIShooterAttackBase.FireReason.Normal;

					if (IsFront(agentTarget))
					{
						shooterAgent.TriggerFire(fireReason);
					}
				}
				else
				{
					shooterAgent.SetLookAtState(AILookAt.PathToTarget);
				}
			}
		}
		
		void CheckFiring()
		{
			Transform agentTarget = shooterAgent.Target;
			if (agentTarget != null)
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
		}
	}
}
