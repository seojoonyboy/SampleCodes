using Framework;
using Game.View.BattleSystem;
using MFPS.Runtime.AI;
using UnityEngine;

namespace Game.View.AI.State
{
	/// <summary>
	/// 폭탄을 설치하는 행동
	/// </summary>
	public class Demolitioning : IAIState
	{
		public Demolitioning(bl_AIShooterAgent shooterAgent) : base(shooterAgent) {	}

		public override void Enter()
		{
			base.Enter();
			
			shooterAgent.SetCrouch(true);
		}

		public override void SlowUpdate()
		{
			base.SlowUpdate();
			
			//주변에 적이 없고, 폭탄 설치가 가능한 경우
			DemolitionBombManager demolitionBombManager = DemolitionBombManager.Instance;
			if (!IsVeryCloseEnemyExist() && demolitionBombManager.CanPlantBomb())
			{
				shooterAgent.ToggleMovable(false);

				DebugEx.Log($"[Bomb] {shooterAgent.name} 폭탄 설치중");
				demolitionBombManager.BotPlantBomb(shooterAgent.BotMFPSActor, OnBombPlantFinished);
			}
		}

		private void OnBombPlantFinished()
		{
			nextState = new TargetAreaSearching(shooterAgent);
			Exit();
		}

		public override void Update() { }

		//무작위 공격 행동을 결정한다.
		public override IAIState GetRandomAttackState()
		{
			float[] percentageRange = new float[shooterAgent.aiSettings.AttackerBombInstallBehaviorPercentages.Length];
			shooterAgent.aiSettings.AttackerBombInstallBehaviorPercentages.CopyTo(percentageRange, 0);
			
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
