using Framework;
using Game.Data;
using Game.View.BattleSystem;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Game.View.AI.State
{
	/// <summary>
	/// Bot이 탐색을 하는 패턴 계열
	/// </summary>
    public class Searching : IAIState
    {
		public Searching(bl_AIShooterAgent shooterAgent) : base(shooterAgent)
		{
			if(shooterAgent.IsCrouch) shooterAgent.SetCrouch(false);
			wayPoints = new List<Vector3>();
		}

		public override void Enter()
		{
			base.Enter();

			if (wayPoints.Count > 0) 
			{
				shooterAgent.Agent.SetDestination(wayPoints[0]);
			}
			
			DebugEx.AILog("Enter Searching State...");
		}

		public override void Exit()
		{
			base.Exit();

			DebugEx.AILog("Exit Searching State...");
		}

		protected override void UpdateMoving()
		{
			//적이 감지됨
			if (detactedTargets.Count > 0)
			{
				foreach (Transform detactedTarget in detactedTargets)
				{
					if(detactedTarget == null) continue;
					
					//감지된 적들 중 보이는 적이 있는 경우
					if (IsVisibleTarget(detactedTarget))
					{
						nextState = new HoldingPositionAttacking(shooterAgent);
						
						shooterAgent.SetTarget(detactedTarget);
						shooterAgent.Agent.SetDestination(detactedTarget.transform.position);
						Exit();
						
						break;
					}
				}

				if (IsDMMode)
				{
					DemolitionBombManager demolitionBombManager = DemolitionBombManager.Instance;
					DemolitionBomb bomb = demolitionBombManager.Bomb;

					if (bomb.bombStatus == BombStatus.Actived)
					{
						Vector3 nearBombLoc = GetRndLocationNearBomb();
						List<Vector3> newPoints = new List<Vector3>();
						newPoints.Add(nearBombLoc);
						MakeWayPoints(newPoints);
					}
				}
				//감지는 되었지만 보이는 적은 없는 경우 목적지만 감지된 지역으로 변경한다.
				Transform target = GetClosestDetactedTarget();
				if (target != null && shooterAgent.Target != target)
				{
					shooterAgent.SetTarget(target);
					shooterAgent.Agent.SetDestination(target.transform.position);
					
					return;
				}
			}
			
			//감지된 적이 없는 경우
			if (shooterAgent.Agent.remainingDistance <= shooterAgent.Agent.stoppingDistance + 0.5f)
			{
				//자신이 폭탄을 가지고 있는 경우
				if (IsDMMode && shooterAgent.IsCarrier && DemolitionBombManager.Instance.Bomb.bombStatus != BombStatus.Actived)
				{
					nextState = new DemolitionAreaSearching(shooterAgent);
					Exit();
				}
				
				if (!IsLastWayPoint())
				{
					MoveToNextWayPoint();	
				}
				else
				{
					DecideRandomMove();
				}
			}
		}
		
		private void DecideRandomMove()
		{
			bl_AIManager aiManager = bl_AIManager.Instance;

			var gameMode = BattleManager.Instance.GetGameMode;
			if (gameMode == BattleMode.DM)
			{
				//수비조인 경우, 무작위 설치 지역으로 이동한다.
				if (!IsTerrorlistTeam)
				{
					DemolitionBombZone randomDemolitionZone = aiManager.GetRandomDemolitionZone();
					Vector3 begin = shooterAgent.transform.position;
					Vector3 end = randomDemolitionZone.transform.position;

					List<Vector3> newPoints = aiManager.GeneratePathBeginToEndWithEssentialWayPoint(begin, end);
					MakeWayPoints(newPoints);
				}
				//공격조인 경우, 무작위 NormalPoints 지역으로 이동한다.
				else
				{
					List<Vector3> newPoints = new List<Vector3>();
					Vector3 newPoint = aiManager.GetRandomNormalWayPoint().transform.position;
					newPoints.Add(newPoint);
					MakeWayPoints(newPoints);
				}
			}
			else
			{
				List<Vector3> newPoints = new List<Vector3>();
				Vector3 newPoint = aiManager.GetRandomNormalWayPoint().transform.position;
				newPoints.Add(newPoint);
				MakeWayPoints(newPoints);
			}
		}
		
		private Vector3 GetRndLocationNearBomb()
		{
			Vector3 bombLocation = GetBombLocation();
			Vector3 rndLocationNearBomb = bombLocation + Random.insideUnitSphere * 2.0f;
			
			bool isFound = NavMesh.SamplePosition(rndLocationNearBomb, out NavMeshHit hit, 2.0f, NavMesh.AllAreas);
			return isFound ? hit.position : bombLocation;
		}
    }

	public class TargetAreaSearching : Searching
	{
		public TargetAreaSearching(bl_AIShooterAgent shooterAgent) : base(shooterAgent)
		{
			repathInterval = 2.0f;
		}

		protected override void UpdateMoving()
		{
			//적이 감지됨
			if (detactedTargets.Count > 0)
			{
				foreach (Transform detactedTarget in detactedTargets)
				{
					//감지된 적들 중 보이는 적이 있는 경우
					if (IsVisibleTarget(detactedTarget))
					{
						//공격, 이동, 은폐 행동 중 하나를 결정한다.
						IAIState state = GetRandomAttackState();
						if (state == null) return;

						if (state.GetType() == typeof(HoldingPositionAttacking)) nextState = (HoldingPositionAttacking)state;
						else if (state.GetType() == typeof(MovingAttacking)) nextState = (MovingAttacking)state;
						else if (state.GetType() == typeof(AvoidAttacking)) nextState = (AvoidAttacking)state;
						else if (state.GetType() == typeof(AggressiveAttacking)) nextState = (AggressiveAttacking)state;
						else if (state.GetType() == typeof(CoveringAttacking)) nextState = (CoveringAttacking)state;
						else { return; }
						
						shooterAgent.SetTarget(detactedTarget);
						shooterAgent.Agent.SetDestination(detactedTarget.transform.position);
						Exit();
						
						break;
					}
				}
			}
			
			//감지된 적이 없는 경우
			if (shooterAgent.Agent.remainingDistance <= shooterAgent.Agent.stoppingDistance + 0.5f)
			{
				if (!IsLastWayPoint())
				{
					MoveToNextWayPoint();	
				}
				else
				{
					if (currentRepathTime >= repathInterval)
					{
						DecideRandomMoveNearBy();
						currentRepathTime = 0;
					}
					
					currentRepathTime += Time.deltaTime;
				}
			}
		}

		void DecideRandomMoveNearBy()
		{
			Vector3 rndLocationNearBy = shooterAgent.transform.position + Random.insideUnitSphere * 2.0f;
			bool isFound = NavMesh.SamplePosition(rndLocationNearBy, out NavMeshHit hit, 2.0f, NavMesh.AllAreas);
			
			shooterAgent.Agent.SetDestination(isFound ? rndLocationNearBy : shooterAgent.transform.position);
		}
	}

	/// <summary>
	/// 공격조 드랍 된 폭탄 찾으러 가는 행동
	/// </summary>
	public class BombSearching : Searching
	{
		public BombSearching(bl_AIShooterAgent shooterAgent) : base(shooterAgent) { }

		public override void Enter()
		{
			base.Enter();
			
			//폭탄 위치로 이동하기 시작한다.
			UpdateDestinationToBomb();
		}

		public void UpdateDestinationToBomb()
		{
			if (DemolitionBombManager.Instance.Bomb == null) return;
			Vector3 targetPosition = DemolitionBombManager.Instance.Bomb.transform.position;

			shooterAgent.SetDestination(targetPosition);
		}

		public override void Update()
		{
			DemolitionBomb demolitionBomb = DemolitionBombManager.Instance.Bomb;
			
			//폭탄이 더이상 Droped 상태가 아니면 Searching으로 빠져나간다.
			if (demolitionBomb.bombStatus != BombStatus.Droped)
			{
				nextState = new Searching(shooterAgent);
				Exit();
			}
		}
	}

	/// <summary>
	/// 소지한 폭탄을 설치하러 가는 행동
	/// </summary>
	public class DemolitionAreaSearching : Searching
	{
		public DemolitionAreaSearching(bl_AIShooterAgent shooterAgent) : base(shooterAgent) {	}

		public override void Enter()
		{
			base.Enter();
			
			Team team = shooterAgent.AITeam;
			int groupID = shooterAgent.References.aiShooter.GroupID;
			List<Vector3> points = bl_AIManager.Instance.GetMyPath(team, groupID, shooterAgent.transform.position);
			MakeWayPoints(points);

			DebugEx.AILog("Enter DemolitionAreaSearching State...");
		}

		public override void MakeWayPoints(List<Vector3> _wayPoints)
		{
			currentWayPointIndex = 0;
			wayPoints = _wayPoints;
			if(wayPoints.Count > 0) shooterAgent.Agent.SetDestination(wayPoints[0]);
		}

		protected override void UpdateMoving()
		{
			//적 감지하는 경우
			if (visableTargets.Count > 0)
			{
				if (shooterAgent.Target != null && IsVisibleTarget(shooterAgent.Target))
				{
					nextState = GetRandomAttackState();
					Exit();
				}
			}
			else
			{
				if (!shooterAgent.IsCarrier) return;

				if (bl_AIManager.Instance.IsCloseToBombInstall(shooterAgent.transform.position))
				{
					//폭탄 설치 가능 지역까지 도달한 경우
					DemolitionBombManager demolitionBombManager = DemolitionBombManager.Instance;

					bool isAvailableToPlant = demolitionBombManager.Bomb.isAvailableToPlant;
					bool isBombActivated = demolitionBombManager.Bomb.bombStatus == BombStatus.Actived;

					if (shooterAgent.IsCarrier && !isBombActivated && isAvailableToPlant)
					{
						nextState = new Demolitioning(shooterAgent);
						Exit();
					}
				}
			}
			
			if (shooterAgent.Agent.remainingDistance <= shooterAgent.Agent.stoppingDistance + 0.5f)
			{
				MoveToNextWayPoint();
			}
		}

		protected override void MoveToNextWayPoint()
		{
			if (wayPoints == null || wayPoints.Count < 1) return;
			if (currentWayPointIndex + 1 > wayPoints.Count)
			{
				Vector3? targetBombZoneLocation = bl_AIManager.Instance.GetTargetBombZone();
				if (targetBombZoneLocation != null)
				{
					shooterAgent.Agent.SetDestination(targetBombZoneLocation.Value);
				}
				
				return;
			}
			
			if (currentWayPointIndex < wayPoints.Count)
			{
				shooterAgent.Agent.SetDestination(wayPoints[currentWayPointIndex]);
			}
			
			currentWayPointIndex++;
		}

		public override void Exit() 
		{ 
			base.Exit();

			DebugEx.AILog("Exit BombSearching State...");
		}

		//무작위 공격 행동을 결정한다.
		public override IAIState GetRandomAttackState()
		{
			float[] percentageRange = new float[shooterAgent.aiSettings.AttackerBombAssignedBehaviorPercentages.Length];
			shooterAgent.aiSettings.AttackerBombAssignedBehaviorPercentages.CopyTo(percentageRange, 0);

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
					if (i == 0) return new AggressiveAttacking(shooterAgent);
					else if (i == 1) return new CoveringAttacking(shooterAgent);
				}
			}

			//default attack 상태
			return new CoveringAttacking(shooterAgent);
		}
	}
}
