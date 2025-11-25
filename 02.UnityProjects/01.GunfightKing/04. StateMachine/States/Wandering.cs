using Framework;
using UnityEngine;
using UnityEngine.AI;

/// 주변을 탐색하는 State
/// 적을 조우하고, 시야에서 놓친 경우 주변 탐색을 하는 행동
namespace Game.View.AI.State
{
	public class Wandering : IAIState
	{
		float wanderintTime = 0.0f;
		private const float maxWanderingSecond = 5.0f;

		public Wandering(bl_AIShooterAgent shooterAgent) : base(shooterAgent)
		{

		}

		public override void Enter()
		{
			DebugEx.AILog("Enter Wandering State...");
			base.Enter();

			wanderintTime = 0.0f;

			Vector2 targetDestination = DecideRandomMove();

			shooterAgent.SetDestination(targetDestination);
		}

		public override void Update()
		{
			wanderintTime += Time.deltaTime;
			if (wanderintTime > maxWanderingSecond) 
			{
				nextState = new Searching(shooterAgent);
				wanderintTime = 0.0f;

				Exit();
			}
		}

		public override void Exit() 
		{
			DebugEx.AILog("Exit Wandering State...");
			base.Exit(); 
		}

		private Vector3 DecideRandomMove()
		{
			Vector3 randomPoint = shooterAgent.transform.position + Random.insideUnitSphere * 4.0f;
			NavMeshHit hit;
			NavMesh.SamplePosition(randomPoint, out hit, 4.0f, NavMesh.AllAreas);

			return hit.position;
		}
	}
}
