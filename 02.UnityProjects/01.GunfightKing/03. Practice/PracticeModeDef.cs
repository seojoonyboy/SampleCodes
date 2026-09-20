using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Framework;
using Newtonsoft.Json;
using System;
using System.Linq;

namespace Game.Data
{
	public class PracticeModeDef : IDef
	{
		public int Version => 1;
		
		public int No { get; private set; }
		public int Level { get; private set; }
		public int Enable { get; private set; }
		public int TimeLimit { get; private set; }
		public int LimitBulletNum { get; private set; }
		public Quantity[] Rewards { get; private set; }
		
		const int REWARD_COUNT_PER_LEVEL = 3;
		
		//실제 게임 내에서 사용 가능한 전체 과녁 목록
		public List<TrainingMarkData> ActiveMarks { get; private set; }

		const int ACTIVE_MARK_MAX_COUNT = 30;

		public void OnLoad(ref DefLoader loader)
		{
			No = loader.ReadInt();
			
			Level = loader.ReadInt();
			Enable = loader.ReadInt();
			TimeLimit = loader.ReadInt();
			
			LimitBulletNum = loader.ReadInt();
			
			Rewards = new Quantity[REWARD_COUNT_PER_LEVEL];
			for (int i = 0; i < Rewards.Length; i++)
			{
				QuantityType rewardType = (QuantityType)loader.ReadInt();
				int rewardNo = loader.ReadInt();
				int rewardCount = loader.ReadInt();
				Rewards[i] = new Quantity(rewardType, rewardNo, rewardCount);
			}
			
			// 비어있는 것은 제거
			Rewards = Rewards.Where(r => r.Type != QuantityType.None).ToArray();
			
			// Mark1 ~ Mark30T 컬럼까지
			ActiveMarks = new List<TrainingMarkData>();
			for (int i = 0; i < ACTIVE_MARK_MAX_COUNT; i++)
			{
				int markNo = loader.ReadInt();			//1. Mark{N}
				int groupNo = loader.ReadInt();			//2. Mark{N}G
				
				int markPatternKey = loader.ReadInt();		//3. Mark{N}P

				try
				{
					string timeArrStr = loader.ReadString();			//4. Mark{N}T
					bool afterPrevGroup = loader.ReadInt() == 1;		//5. Mark{N}PrevGroup
					
					//빈칸인 경우 skip 한다.
					if(markPatternKey == 0 || !timeArrStr.HasContent()) continue;
					
					int[] markTimeArr = JsonConvert.DeserializeObject<int[]>(timeArrStr);
					
					TrainingMarkData markData = new TrainingMarkData()
					{
						MarkNo = markNo,
						GroupNo = groupNo,
						
						MarkPatternKey = markPatternKey,
						
						MarkBeginTime = markTimeArr[0],
						MarkEndTime = markTimeArr[1],
						
						AfterPrevGroup = afterPrevGroup
					};
					
					ActiveMarks.Add(markData);
				}
				catch (Exception ex)
				{
					DebugEx.Log($"{i + 1}번째 타겟 정보를 읽어오는데 실패했습니다! {ex.Message}", LogColorType.Red);
				}
			}

			ActiveMarks = ActiveMarks.OrderBy(x => x.GroupNo).ToList();
		}
	}

	public class PracticeModeMarkPatternDef : IDef
	{
		public int Version => 1;
		
		public int No { get; private set; }
		
		public string Desc { get; private set; }
		
		public int MoveForward { get; private set; }	//앞으로 움직일지 여부 [0 또는 1]
		public int MoveBackward { get; private set; }	//뒤로 움직일지 여부 [0 또는 1]
		
		public int MoveRight { get; private set; }		//우측으로 움직일지 여부 [0 또는 1]
		public int MoveLeft { get; private set; }		//좌측으로 움직일지 여부 [0 또는 1]
		
		public float MoveSpeed { get; private set; }
		
		public void OnLoad(ref DefLoader loader)
		{
			No = loader.ReadInt();
			
			Desc = loader.ReadString();
			
			MoveForward = loader.ReadInt();
			MoveBackward = loader.ReadInt();
			
			MoveRight = loader.ReadInt();
			MoveLeft = loader.ReadInt();
			
			MoveSpeed = loader.ReadFloat();
		}
	}
}