using System.Collections.Generic;
using UnityEngine;
using Game.View.BattleSystem;
using MFPSEditor;
using Game.View;



#if UNITY_EDITOR
using UnityEditor;
#endif

public class bl_AICoverPointManager : MonoBehaviour
{
	[SerializeField] public float MaxDistance = 50;
	[SerializeField] public float UsageTime = 10;
	[SerializeField] public float maxNeighbordDistance = 25;
	[LovattoToogle]
	[SerializeField] public bool onlyAllowOneBotPerCover = true;
	[Space(5)]
	[SerializeField] AIGroupPathSettings _aiGroupPathSettings;
	[SerializeField] Transform _essentialWayPointsParent;
	[SerializeField] Transform _normalWayPointsParent;
	[SerializeField] Transform _testBegin;
	[SerializeField] Transform _testEnd;

	public static List<bl_AICoverPoint> AllCovers = new List<bl_AICoverPoint>();

	public AIGroupPathSettings AiGroupPathSettings => _aiGroupPathSettings;
	public Transform EssentialWayPointsParent => _essentialWayPointsParent;
	public Transform NormalWayPointsParent => _normalWayPointsParent;

	/// <summary>
	/// 
	/// </summary>
	private void OnDestroy()
	{
		// Since it's a static property, make sure to clean up after unload the scene.
		AllCovers.Clear();
	}

	/// <summary>
	/// 
	/// </summary>
	public static void Register(bl_AICoverPoint co)
	{
		AllCovers.Add(co);
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="target"></param>
	/// <returns></returns>
	public bl_AICoverPoint GetCloseCover(Transform target)
	{
		if (AllCovers == null || AllCovers.Count <= 0)
		{
			Debug.LogWarning("There is no Cover Points for bots in this scene, bots behave will be limited.");
			return null;
		}

		bl_AICoverPoint cover = null;
		float d = MaxDistance;
		for (int i = 0; i < AllCovers.Count; i++)
		{
			float dis = bl_UtilityHelper.Distance(target.localPosition, AllCovers[i].Position);
			if (dis < MaxDistance && dis < d)
			{
				d = dis;
				cover = AllCovers[i];
			}
		}
		cover = CheckCoverUsage(cover);
		return cover;
	}

	public bl_AICoverPoint GetCoverOnRadius(bl_AICoverPoint except, Transform target, float radius)
	{
		if (AllCovers == null || AllCovers.Count <= 0)
		{
			Debug.LogWarning("There is no Cover Points for bots in this scene, bots behave will be limited.");
			return null;
		}
		
		List<bl_AICoverPoint> list = new List<bl_AICoverPoint>();
		for (int i = 0; i < AllCovers.Count; i++)
		{
			float dis = bl_UtilityHelper.Distance(target.localPosition, AllCovers[i].Position);
			if (dis <= radius)
			{
				list.Add(AllCovers[i]);
			}
		}

		list.Remove(except);
		
		bl_AICoverPoint cp = null;
		if (list.Count > 0)
		{
			cp = list[Random.Range(0, list.Count)];
		}
		if (cp == null) { cp = AllCovers[Random.Range(0, AllCovers.Count)]; }

		return cp;
	}

	///radius 반경 내의 무작위 CoverPoint를 찾아 반환
	public bl_AICoverPoint GetCoverOnRadius(Transform target, float radius)
	{
		if (AllCovers == null || AllCovers.Count <= 0)
		{
			Debug.LogWarning("There is no Cover Points for bots in this scene, bots behave will be limited.");
			return null;
		}

		List<bl_AICoverPoint> list = new List<bl_AICoverPoint>();
		for (int i = 0; i < AllCovers.Count; i++)
		{
			float dis = bl_UtilityHelper.Distance(target.localPosition, AllCovers[i].Position);
			if (dis <= radius)
			{
				list.Add(AllCovers[i]);
			}
		}
		bl_AICoverPoint cp = null;
		if (list.Count > 0)
		{
			cp = list[Random.Range(0, list.Count)];
		}
		if (cp == null) { cp = AllCovers[Random.Range(0, AllCovers.Count)]; }

		return cp;
	}

	///radius 반경 내의 targetDir 방향으로 존재하는 coverPoint 들 중 무작위 하나의 position을 찾아 반환
	public Vector3 GetCoverOnRadius(Transform target, float radius, Vector3 targetDir)
	{
		if (AllCovers == null || AllCovers.Count <= 0)
		{
			Debug.LogWarning("There is no Cover Points for bots in this scene, bots behave will be limited.");
			return target.position + targetDir * radius;
		}

		List<bl_AICoverPoint> list = new List<bl_AICoverPoint>();
		for (int i = 0; i < AllCovers.Count; i++)
		{
			Vector3 dirTargetToCover = (AllCovers[i].Position - target.position).normalized;
			float dotResult = Vector3.Dot(targetDir, dirTargetToCover);

			float dis = bl_UtilityHelper.Distance(target.localPosition, AllCovers[i].Position);
			if ((dotResult > 0) && (dis <= radius))
			{
				list.Add(AllCovers[i]);
			}
		}

		if (list.Count > 0)
		{
			return list[Random.Range(0, list.Count)].Position;
		}

		return target.position + targetDir * radius;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="target"></param>
	/// <returns></returns>
	public bl_AICoverPoint GetCloseCoverForced(Transform target)
	{
		if (AllCovers == null || AllCovers.Count <= 0)
		{
			Debug.LogWarning("There is no Cover Points for bots in this scene, bots behave will be limited.");
			return null;
		}

		bl_AICoverPoint cover = null;
		float d = 100000;
		for (int i = 0; i < AllCovers.Count; i++)
		{
			float dis = bl_UtilityHelper.Distance(target.localPosition, AllCovers[i].Position);
			if (dis < d)
			{
				d = dis;
				cover = AllCovers[i];
			}
		}
		cover = CheckCoverUsage(cover, true);
		return cover;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="target"></param>
	/// <param name="overrdidePoint"></param>
	/// <returns></returns>
	public bl_AICoverPoint GetCloseCover(Transform target, bl_AICoverPoint overrdidePoint)
	{
		bl_AICoverPoint cover = null;
		float d = MaxDistance;
		for (int i = 0; i < AllCovers.Count; i++)
		{
			float dis = bl_UtilityHelper.Distance(target.localPosition, AllCovers[i].Position);
			if (dis < MaxDistance && dis < d && AllCovers[i] != overrdidePoint)
			{
				d = dis;
				cover = AllCovers[i];
			}
		}
		cover = CheckCoverUsage(cover);
		return cover;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="coverSource"></param>
	/// <returns></returns>
	public bl_AICoverPoint CheckCoverUsage(bl_AICoverPoint coverSource, bool forceAvaliable = false)
	{
		if (coverSource == null)
		{
			return null;
		}

		// If this cover has been used recently, try to find another one
		if ((Time.time - coverSource.lastUseTime) <= UsageTime)
		{
			if (coverSource.HasNeighbords())
			{
				var neighbord = coverSource.TryGetAvailableNeighbord();
				if (neighbord == null)
					neighbord = coverSource.NeighbordPoints[Random.Range(0, coverSource.NeighbordPoints.Count)];

				coverSource = neighbord;
			}
			else
			{
				if (onlyAllowOneBotPerCover && !forceAvaliable)
				{
					coverSource = null;
				}
			}
		}

		if (coverSource == null)
		{
			return null;
		}

		coverSource.lastUseTime = Time.time;
		return coverSource;
	}

	private static bl_AICoverPointManager _instance;
	public static bl_AICoverPointManager Instance
	{
		get
		{
			if (_instance == null) { _instance = FindFirstObjectByType<bl_AICoverPointManager>(); }
			return _instance;
		}
	}

#if UNITY_EDITOR
	[ContextMenu("Fix Points")]
	public void FixedFloorPos()
	{
		bl_AICoverPoint[] sp = FindObjectsByType<bl_AICoverPoint>(FindObjectsSortMode.None);
		RaycastHit r;
		for (int i = 0; i < sp.Length; i++)
		{
			Transform t = sp[i].transform;
			Ray ray = new Ray(t.position + t.up, Vector3.down);
			if (Physics.Raycast(ray, out r, 100))
			{
				t.position = r.point;
			}
		}
	}

	[ContextMenu("Calculate Neighbors")]
	public void CalcuNeighbords()
	{
		bl_AICoverPoint[] allCovers = FindObjectsByType<bl_AICoverPoint>(FindObjectsSortMode.None);
		for (int i = 0; i < allCovers.Length; i++)
		{
			Transform t = allCovers[i].transform;
			allCovers[i].NeighbordPoints.Clear();
			for (int e = 0; e < allCovers.Length; e++)
			{
				if (allCovers[i] == allCovers[e]) continue;

				var posA = allCovers[i].transform.position;
				var posB = allCovers[e].transform.position;

				if (Vector3.Distance(posA, posB) <= maxNeighbordDistance)
				{
					//if(HasNavigationPath(posA, posB))
					allCovers[i].NeighbordPoints.Add(allCovers[e]);
				}
			}
			UnityEditor.EditorUtility.SetDirty(allCovers[i]);
		}
	}
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(bl_AICoverPointManager))]
public class bl_AICovertPointManagerEditor : Editor
{
	bl_AICoverPointManager script;

	private void OnEnable()
	{
		script = (bl_AICoverPointManager)target;
	}

	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();
		
		GizmoToggle("Show CoverPoints", "AICoverPointTool.ShowPoints");
		GizmoToggle("Show CoverPoint Neighbors", "AICoverPointTool.ShowNeighbors");
		GizmoToggle("Show WayPoints", "AICoverPointTool.ShowWayPoints");

		GUILayout.Space(10);

		if (GUILayout.Button("Bake Neighbors points"))
		{
			script.CalcuNeighbords();
		}
		if (GUILayout.Button("Align points to floors"))
		{
			script.FixedFloorPos();
		}
	}

	void GizmoToggle(string caption, string prefName)
	{
		bool oldValue = PlayerPrefs.GetInt(prefName, 1) == 1;

		Rect r = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(EditorGUIUtility.singleLineHeight));
		bool newValue = MFPSEditorStyles.FeatureToogle(r, oldValue, caption);

		if (oldValue != newValue)
		{
			PlayerPrefs.SetInt(prefName, newValue ? 1 : 0);
			SceneView.RepaintAll();
		}
	}
}
#endif