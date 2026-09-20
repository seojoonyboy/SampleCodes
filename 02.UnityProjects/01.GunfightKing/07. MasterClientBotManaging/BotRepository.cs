using Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

namespace Game.Data
{
	/// <summary>
	/// 게임 단위 Bot Pool 관리 저장소
	/// Note. bl_AIManager와 혼재되어 있는데, 추후에 Repository로 옮길 수 있는건 옮길 예정
	/// </summary>
	public class BotRepository : EntityManager<BotRepository>
	{
		//현재 게임의 Bot Pool 목록
		List<PoolSlotData> poolSlots1 = new List<PoolSlotData>();
		List<PoolSlotData> poolSlots2 = new List<PoolSlotData>();

		WeaponCodeDef[] _weaponCodeDefs;
		
		public void LoadAllAvailableWeaponSkins()
		{
			_weaponCodeDefs = WeaponCodeCDB.Instance.GetAllDefs();
		}

		public WeaponCodeDef GetRandomWeaponCodeDef(int weaponDefNo)
		{
			if (_weaponCodeDefs == null) return null;

			var availableList = _weaponCodeDefs
				.FindAll(x => x.WeaponNo == weaponDefNo && x.No != weaponDefNo)	
				.ToList();
			if (availableList.Count < 1) return null;

			int rndIndex = Random.Range(0, availableList.Count);
			return availableList[rndIndex];
		}

		//이번 게임의 Bot Pool을 결정한다.
		public void DecideBotDefPool(string miscKey, bool isOneTeamMode, int maxBotNumber, int roomSeed)
		{
			poolSlots1.Clear();
			poolSlots2.Clear();

			MiscCDB miscCDB = MiscCDB.Instance;
			int[][] botDistData;

			bool isMultiMode = miscKey.Contains("Multi");
			
			switch (miscKey)
			{
				case "BotMultiPersonal":
					botDistData = miscCDB.BotMultiPersonal;
					break;
				
				case "BotMultiParty":
					botDistData = miscCDB.BotMultiTeam;
					break;
				
				case "BotOfflinePersonalEasy":
					botDistData = miscCDB.BotOfflinePersonalEasy;
					break;
				
				case "BotOfflinePersonalNormal":
					botDistData = miscCDB.BotOfflinePersonalNormal;
					break;
				
				case "BotOfflinePersonalHard":
					botDistData = miscCDB.BotOfflinePersonalHard;
					break;
				
				case "BotOfflineTeamEasy":
					botDistData = miscCDB.BotOfflineTeamEasy;
					break;
				
				case "BotOfflineTeamNormal":
					botDistData = miscCDB.BotOfflineTeamNormal;
					break;
				
				case "BotOfflineTeamHard":
					botDistData = miscCDB.BotOfflineTeamHard;
					break;
				
				default:
					botDistData = miscCDB.BotMultiTeam;
					break;
			}
			
			List<BotDef> list = GenerateBotDefs(isMultiMode, botDistData, maxBotNumber);

			foreach (BotDef botDef in list)
			{
				PoolSlotData slotData = new PoolSlotData(botDef.No);
				poolSlots1.Add(slotData);
			}
			
			if (!isOneTeamMode)
			{
				poolSlots2.AddRange(poolSlots1);
			}
			
			List<BotDef> GenerateBotDefs(bool isMultiMode, int[][] botDistData, int maxBotNumber)
			{
				List<BotDef> result = new List<BotDef>();
				int prevBotNumber = 0;

				System.Random rand = new System.Random(roomSeed);
				
				for (int i = 0; i < botDistData.Length; i++)
				{
					if(prevBotNumber >= maxBotNumber) break;
					
					int botNum = botDistData[i][0] != -1
						? rand.Next(botDistData[i][0], botDistData[i][1])
						: maxBotNumber - prevBotNumber;
					
					DebugEx.Log($"Generated bot [{(AIDifficulty)i}] number {botNum}");
					
					AIDifficulty difficulty = (AIDifficulty)i;
					List<BotDef> botSettings = BotCDB.Instance.GetAllDefs().FindAll(x => 
						x.Difficulty == difficulty && 
						x.MultiMode == isMultiMode).ToList();
					
					for (int j = 0; j < botNum; j++)
					{
						int rndIndex = Random.Range(0, botSettings.Count);
						BotDef rndBotDef = botSettings[rndIndex];
						
						DebugEx.Log($"Generated bot settings Def no : {rndBotDef.No}");
						result.Add(rndBotDef);
					}

					prevBotNumber += botNum;
				}

				return result;
			}
		}

		public PoolSlotData GetAvailableBotDef(string botTeam, string botName)
		{
			if (botTeam.Equals("All"))
			{
				for (int i = 0; i < poolSlots1.Count; i++)
				{
					if(!poolSlots1[i].IsAvailable) continue;

					poolSlots1[i].SetPossesed(botName);;
					return poolSlots1[i];
				}
			}
			else
			{
				List<PoolSlotData> targetList = botTeam.Equals("Team1") ? poolSlots1 : poolSlots2;
				
				for (int i = 0; i < targetList.Count; i++)
				{
					if(!targetList[i].IsAvailable) continue;

					targetList[i].SetPossesed(botName);;
					return targetList[i];
				}
			}
			

			return GetDefaultBotDef();
		}

		public PoolSlotData GetDefaultBotDef()
		{
			return this.poolSlots1.First();
		}

		public void UnpossesBotDef(string aiTeam, int botDefId)
		{
			PoolSlotData poolSlotData;
			if (aiTeam.Equals("All") || aiTeam.Equals("Team1"))
			{
				poolSlotData = poolSlots1.Find(x => x.BotDefId == botDefId);
			}
			else
			{
				poolSlotData = poolSlots2.Find(x => x.BotDefId == botDefId);
			}
			
			if (poolSlotData != null) { poolSlotData.SetUnpossesed(); }
		}

		[Serializable]
		public class PoolSlotData
		{
			public int BotDefId;
			
			public string AIName;	//점유한 Bot viewId
			public bool IsAvailable; //사용가능 여부 [최초 Bot 생성시 점유한다.]

			public PoolSlotData(int botDefId)
			{
				this.IsAvailable = true;
				this.BotDefId = botDefId;
			}
			
			public void SetPossesed(string aiName)
			{
				this.AIName = aiName;
				this.IsAvailable = true;
			}

			public void SetUnpossesed()
			{
				this.AIName = string.Empty;
				this.IsAvailable = true;
			}
		}
	}
}