using Game.Data;
using UnityEngine;
using UnityEngine.AI;

namespace Game.View.AI.State
{
	/// <summary>
	/// Bot이 공격하지 않고 회피하는 패턴 계열
	/// </summary>
	public class Avoiding : IAIState
	{
		protected float avoidTime = 3.0f;		//연막탄을 벗어나기 위해 피하는 시간
		protected float currentAvoidTime = 0.0f;
		
		protected Vector3 _avoidTargetPos;
		
		public Avoiding(bl_AIShooterAgent shooterAgent, Vector3 avoidTargetPos, float duration) : base(shooterAgent)
		{
			if(shooterAgent.IsCrouch) shooterAgent.SetCrouch(false);
			shooterAgent.Agent.speed = shooterAgent.aiSettings.RunSpeed;
			
			avoidTime = duration;
			_avoidTargetPos = avoidTargetPos;
		}

		public override void Enter()
		{
			base.Enter();
			
			currentAvoidTime = 0.0f;
		}

		protected override void UpdateMoving()
		{
			if (currentAvoidTime > avoidTime)
			{
				nextState = new Searching(shooterAgent);
				Exit();
			}
			
			currentAvoidTime += Time.deltaTime;
		}
	}

	public class SmokeAreaAvoiding : Avoiding
	{

		public SmokeAreaAvoiding(bl_AIShooterAgent shooterAgent, Vector3 avoidTargetPos, float duration) : base(shooterAgent, avoidTargetPos, duration) { }
		
		public override void Enter()
		{
			base.Enter();
			
			shooterAgent.SetDestination(_avoidTargetPos);
		}

		protected override bool IsVisibleTarget(Transform target)
		{
			return false;
		}

		protected override void UpdateMoving()
		{
			//목적지 [연막탄을 벗어나는 위치] 도달시 즉시 해당 행동을 Exit 한다.
			if (shooterAgent.Agent.remainingDistance <= 0.1f)
			{
				nextState = new Searching(shooterAgent);
				Exit();
			}
		}
	}

	public class FlashAreaAvoiding : Avoiding
	{
		public FlashAreaAvoiding(bl_AIShooterAgent shooterAgent, Vector3 avoidTargetPos, float duration) : base(shooterAgent, avoidTargetPos, duration) { }
		
		public override void Enter()
		{
			base.Enter();
			
			shooterAgent.SetDestination(_avoidTargetPos);
		}

		protected override bool IsVisibleTarget(Transform target)
		{
			return false;
		}
	}
}