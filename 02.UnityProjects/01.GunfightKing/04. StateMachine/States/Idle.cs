using UnityEngine;

namespace Game.View.AI.State
{
	/// <summary>
	/// 첫 진입 상태 [Entry State]
	/// </summary>
    public class Idle : IAIState
	{
		public Idle(bl_AIShooterAgent shooterAgent) : base(shooterAgent) { }
	}
}
