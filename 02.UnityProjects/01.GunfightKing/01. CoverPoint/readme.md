<img width="1878" height="956" alt="Coverpoint" src="https://github.com/user-attachments/assets/bda63db4-f382-499e-a9a9-a1c2d70a7848" />

**[1단계] 배치** 
해당 맵마다 CoverPoint 컴포넌트를 원하는 곳에 배치한다. 배치된 CoverPoint는 기즈모를 통해 이용자가 쉽게 파악할 수 있네 보강한다.
<pre>
  <code>
    private void OnDrawGizmos()
	{
		if(bl_AICoverPointManager.Instance == null)
		{
			return;
		}
		
		if(PlayerPrefs.GetInt("AICoverPointTool.ShowPoints", 1) == 1)
		{ 
			Gizmos.color = Color.blue;
			Gizmos.DrawWireSphere(transform.position, 1);
			Gizmos.DrawCube(transform.position, new Vector3(1, 0.1f, 1));
		}

		if (PlayerPrefs.GetInt("AICoverPointTool.ShowNeighbors", 1) == 1)
		{
			Gizmos.color = Color.gray;
			if (NeighbordPoints.Count > 0)
			{
				for (int i = 0; i < NeighbordPoints.Count; i++)
				{
					if (NeighbordPoints[i] == null) continue;
					Gizmos.DrawLine(transform.position, NeighbordPoints[i].transform.position);
				}
			}
		}
	}
  </code>  
</pre>

해당 CoverPoint의 주변을 탐색하여 인접한 다른 CoverPoint를 찾는다.
<pre>
  <script>
    public bl_AICoverPoint TryGetAvailableNeighbord()
	{
		if (NeighbordPoints == null || NeighbordPoints.Count <= 0) return null;

		for (int i = 0; i < NeighbordPoints.Count; i++)
		{
			if (NeighbordPoints[i] == null) continue;

			if (NeighbordPoints[i].IsAvailable(bl_AICoverPointManager.Instance.UsageTime))
				return NeighbordPoints[i];
		}
		return null;
	}
  </script>
</pre>

**[2단계] 관리** 
전체 CoverPoihnt에 대한 관리는 bl_AICoverPointManager에서 담당한다. 
실제 CoverPoint를 활용하고자 하는 경우 CoverPointManager에게 요청하여 결과를 반환받는다. 그 예로 현재 위치에서 가장 가까운 CoverPoint들을 찾는 함수
<pre>
  <script>
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
  </script>
</pre>
