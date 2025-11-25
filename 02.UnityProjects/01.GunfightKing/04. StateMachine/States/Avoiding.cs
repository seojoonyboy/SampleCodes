using UnityEngine;

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
		
		public Avoiding(bl_AIShooterAgent shooterAgent, Vector3 avoidTargetPos) : base(shooterAgent)
		{
			if(shooterAgent.IsCrouch) shooterAgent.SetCrouch(false);
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
		public SmokeAreaAvoiding(bl_AIShooterAgent shooterAgent, Vector3 avoidTargetPos) : base(shooterAgent, avoidTargetPos) { }
		
		public override void Enter()
		{
			base.Enter();
			
			avoidTime = 3.0f;
			shooterAgent.SetDestination(_avoidTargetPos);
		}

		protected override bool IsVisibleTarget(Transform target)
		{
			return false;
		}
	}

	public class FlashAreaAvoiding : Avoiding
	{
		public FlashAreaAvoiding(bl_AIShooterAgent shooterAgent, Vector3 avoidTargetPos) : base(shooterAgent, avoidTargetPos) { }
		
		public override void Enter()
		{
			base.Enter();
			
			avoidTime = 3.0f;
			shooterAgent.SetDestination(_avoidTargetPos);
		}

		protected override bool IsVisibleTarget(Transform target)
		{
			return false;
		}
	}
}