using Framework;
using Game.View.AI.State;
using Game.View.BattleSystem;
using MFPS.Runtime.AI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Game.View
{
    public class CoveringDefusing : IAIState
    {
	    float waitNextMoveTime = 0.0f;
	    float randomMoveInterval = 3.0f;
	    
	    public CoveringDefusing(bl_AIShooterAgent shooterAgent) : base(shooterAgent) { }

	    public override void SlowUpdate()
	    {
		    base.SlowUpdate();
		    
		    if (!shooterAgent.IsCrouch) { shooterAgent.IsCrouch = false; }
		    
		    //적이 감지된 경우
		    if (detactedTargets.Count == 0) 
            {
	            if (IsDMMode)
	            {
		            DemolitionBombManager demolitionBombManager = DemolitionBombManager.Instance;
		            DemolitionBomb bomb = demolitionBombManager.Bomb;
		            bool isBombActivated = bomb.bombStatus == BombStatus.Actived;
				    
		            if (isBombActivated)
		            {
			            if (!IsTerrorlistTeam)
			            {
				            AIStateManager aiStateManager = AIStateManager.Instance;
				            List<bl_AIShooterAgent> otherUnits = aiStateManager.GetAgentsInState<BombDefusing>();
				            if ((otherUnits != null) && (otherUnits.Count == 0))
				            {
					            nextState = new BombDefusing(shooterAgent);	
					            Exit();
				            }
			            }
		            }
	            }
            }
	    }

	    public override void Update()
	    {
		    UpdateMoving();
	    }

	    protected override void UpdateMoving()
	    {
		    if (shooterAgent.Agent.remainingDistance < 1.0f)
		    {
			    waitNextMoveTime += Time.deltaTime;

			    if (waitNextMoveTime > randomMoveInterval)
			    {
				    DebugEx.Log("[" + shooterAgent.Agent.name + "] Case 100 DecideRandomMove in TargetAreaSearching...");

				    Vector3 rndTargetMovePosition = DecideRandomMove();
				    shooterAgent.SetDestination(rndTargetMovePosition);

				    waitNextMoveTime = 0.0f;
			    }
		    }
		    
		    CheckFiring();
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
	    
	    private Vector3 DecideRandomMove()
	    {
		    Vector3 randomPoint = shooterAgent.transform.position + Random.insideUnitSphere * 4.0f;
		    
		    bool isFound = NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 4.0f, NavMesh.AllAreas);

		    return isFound ? hit.position : shooterAgent.transform.position;
	    }
    }
}
