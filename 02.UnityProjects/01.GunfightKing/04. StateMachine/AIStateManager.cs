using System.Collections.Generic;
using Framework;
using Game.View.AI.State;

namespace Game.View
{
    public class AIStateManager : bl_MonoBehaviour
	{
		private static AIStateManager _instance;
		public static AIStateManager Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = FindFirstObjectByType<AIStateManager>();
				}
				return _instance;
			}
		}

		private List<bl_AIShooterAgent> aiAgents = new List<bl_AIShooterAgent>();

		public void RegisterAgent(bl_AIShooterAgent agent)
		{
			if (!aiAgents.Contains(agent))
			{
				aiAgents.Add(agent);
			}
		}

		public void UnregisterAgent(bl_AIShooterAgent agent)
		{
			if (aiAgents.Contains(agent))
			{
				DebugEx.Log("Case 100 Bot Die : " + agent.name);
				aiAgents.Remove(agent);
			}
		}

		public List<bl_AIShooterAgent> GetAgentsInState<T>() where T : IAIState
		{
			List<bl_AIShooterAgent> agentsInState = new List<bl_AIShooterAgent>();

			aiAgents.RemoveAll(x => x == null);
			
			foreach (var agent in aiAgents)
			{
				if (agent.CurrentState != null && agent.CurrentState.GetType() == typeof(T))
				{
					agentsInState.Add(agent);
				}
			}

			return agentsInState;
		}

		protected override void OnDisable()
		{
			base.OnDisable();

			Reset();
		}

		public void Reset()
		{
			aiAgents.Clear();
		}
	}
}
