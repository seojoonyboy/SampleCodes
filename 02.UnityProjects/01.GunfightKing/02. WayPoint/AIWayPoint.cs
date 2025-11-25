using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Game.View
{
	[Serializable]
    public class AIWayPoint : MonoBehaviour
    {
	    [Tooltip("필수로 지나야 하는 경유지인 경우 ESSENTIAL. 일반적인 경유지인 경우 NORMAL로 지정")]
	    [SerializeField] public Type type;
	    
	    [HideInInspector] public EssentialPointTeam StartPointOwnerTeam;
	    [HideInInspector] public bool IsStartPoint;
	    
	    [HideInInspector] public List<AIWayPoint> prevPoints;
	    [HideInInspector] public List<AIWayPoint> nextPoints;
	    
        public enum Type
        {
	        NORMAL = 0,
	        ESSENTIAL = 1
        }
        
        public enum EssentialPointTeam
        {
	        Team1 = 0,
	        Team2 = 1
        }

        private void OnDrawGizmos()
        {
			if (PlayerPrefs.GetInt("AICoverPointTool.ShowWayPoints", 1) != 1)
			{
				return;
			}

			if (type == Type.NORMAL)
	        {
		        Gizmos.color = Color.yellow;
	        }
	        else if (type == Type.ESSENTIAL)
	        {
		        Gizmos.color = Color.red;
	        }
	        
	        Gizmos.DrawWireSphere(transform.position, 1);
	        Gizmos.DrawCube(transform.position, new Vector3(1, 0.1f, 1));

	        if (nextPoints != null && nextPoints.Count > 0)
	        {
		        foreach (AIWayPoint nextPoint in nextPoints)
		        {
			        if(nextPoint == null) continue;
			        
			        Gizmos.color = Color.red;
			        Gizmos.DrawLine(transform.position, nextPoint.transform.position);
		        }
	        }
        }
    }
}
