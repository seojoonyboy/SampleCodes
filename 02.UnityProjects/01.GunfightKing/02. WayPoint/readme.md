<img width="1874" height="953" alt="Waypoint" src="https://github.com/user-attachments/assets/80d09eef-c9c8-4eaa-8b4b-8f637b7533dd" />

빨간색 포인트 : AI Bot이 팀전인 경우 (팀데스 모드, 폭탄모드) 탐색 상태에서 반드시 거쳐야 하는 주요 거점들. 전투가 일어나기 전까지는 이어진 인접 빨간색 포인트들을 경로로 삼아 이동하게 된다.
분기점이 있는 경우에는 무작위로 하나를 선택하게 된다.

노란색 포인트 : AI Bot이 개인전인 경우 탐색 상태에서 무작위 노란색 포인트를 다음 목적지로 삼아 이동한다.

<img width="598" height="399" alt="image" src="https://github.com/user-attachments/assets/07d2019b-2026-4daf-99f0-75fdfaa572ab" />

커스텀 Editor 스크립트, AIWayPointEditor를 통해 해당 빨간색 포인트에서 갈 수 있는 다음 거점들을 이용자(기획자)가 연결하게 된다. 
<pre>
  <code>
    public override void OnInspectorGUI()
	{
		AIWayPoint script = (AIWayPoint)target;
		
		base.OnInspectorGUI();
		
		GUILayout.BeginVertical("box");
		if (script.type == AIWayPoint.Type.ESSENTIAL)
		{
			prevPointsProp = serializedObject.FindProperty("prevPoints");
			nextPointsProp = serializedObject.FindProperty("nextPoints");
			
			GUI.enabled = false;
			EditorGUILayout.PropertyField(prevPointsProp, new GUIContent("Previous Points"), true);
			
			GUI.enabled = true;
			EditorGUILayout.PropertyField(nextPointsProp, new GUIContent("Next Points"), true);
		}
		GUILayout.EndVertical();

		if (script.type == AIWayPoint.Type.ESSENTIAL)
		{
			int w = ((int)EditorGUIUtility.currentViewWidth / 3) - 25;
		
			GUILayout.BeginVertical("box");
			EditorGUILayout.Space();
			
			script.IsStartPoint = EditorGUILayout.ToggleLeft("IsStartPoint", script.IsStartPoint, GUILayout.Width(w));
			if (script.IsStartPoint)
			{
				script.StartPointOwnerTeam = (AIWayPoint.EssentialPointTeam)EditorGUILayout
					.EnumPopup("StartPointOwnerTeam", script.StartPointOwnerTeam, EditorStyles.toolbarPopup);
			}
			
			EditorGUILayout.EndVertical();
		}
		
		serializedObject.ApplyModifiedProperties();
		
		HandleAutoBackLink(script);
		
		if (GUI.changed) { EditorUtility.SetDirty(script); }
	}
  </code>
</pre>

역으로 가는 연결 경로는 내부적으로 계산한다. 빨간색 포인트 간의 연결은 이용자가 단방향으로만 지정하면, 역방향은 내부적으로 처리한다.
<pre>
  <code>
    private void HandleAutoBackLink(AIWayPoint script)
	{
		// 현재 nextPoints와 비교
		foreach (AIWayPoint newPoint in script.nextPoints)
		{
			if (newPoint == null) continue;
			if (!previousNextPoints.Contains(newPoint))
			{
				// 새로 추가된 경우
				if (!newPoint.prevPoints.Contains(script))
				{
					Undo.RecordObject(newPoint, "Add Prev Point");
					newPoint.prevPoints.Add(script);
				}
			}
			
			EditorUtility.SetDirty(newPoint);
		}

		//prev에서 실제로 next로 이 WayPoint를 참조하고 있는지 재확인
		foreach (AIWayPoint prevPoint in script.prevPoints.ToList())
		{
			if (prevPoint == null) continue;
			if (!prevPoint.nextPoints.Contains(script))
			{
				script.prevPoints.Remove(prevPoint);
			}
		}
		
		// 최신 상태 저장
		StoreCurrentNextPoints();
	}
  </code>
</pre>
