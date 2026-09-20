using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Framework;
using Newtonsoft.Json;
using System.Linq;

namespace Game.Data
{
	/// <summary>
	/// Bot 관련 설정 Def.
	/// </summary>
	public class BotDef : IDef
	{
		public int Version => 1;
		
		public int No { get; private set; }
		
		public AIDifficulty Difficulty { get; private set; }
		
		public bool MultiMode { get; private set; }
		
		public float[] AttackBehaviorPercentages { get; private set; }
		
		public float[] AttackBehaviorRemainTimes { get; private set; }
		
		public int MinimumDistanceForGrenades { get; private set; }
		public int GrenadeNum { get; private set; }
		
		public int Health { get; private set; }
		
		public float WalkSpeed { get; private set; }
		public float CrouchSpeed { get; private set; }
		public float RunSpeed { get; private set; }
		public float RotationSmoothing { get; private set; }
		
		public float MinRange { get; private set; }
		public float StopDistance { get; private set; }
		public float FiringDistance { get; private set; }
		public float ViewAngle { get; private set; }
		
		public float ReactSpeed { get; private set; }
		
		public float DetectNoiseRange { get; private set; }
		
		public bool EnableShootDelay { get; private set; }
		public float ShootDelayMinTime { get; private set; }	//적을 인지하고 사격까지 지연 시간 최솟값(초)
		public float ShootDelayMaxTime { get; private set; }	//적을 인지하고 사격까지 지연 시간 최댓값(초)
		public float ImmatureUserShootDelayMinTime { get; private set; }	//KD 값이 낮은 유저 적을 인지하고 사격까지 지연 시간 최솟값(초)
		public float ImmatureUserShootDelayMaxTime { get; private set; }	//KD 값이 낮은 유저 적을 인지하고 사격까지 지연 시간 최댓값(초)
		
		public float[] AttackerBombAssignedBehaviorPercentages { get; private set; }
		public float[] AttackerBombInstallBehaviorPercentages { get; private set; }
		
		public string AR_GunList;
		public string SMG_GunList;
		public string SG_GunList;
		public string SR_GunList;
		
		public bool ARAvailable;
		public bool SMGAvailable;
		public bool SGAvailable;
		public bool SRAvailable;
		
		public int ARPercentage;
		public int SMGPercentage;
		public int SGPercentage;
		public int SRPercentage;
		
		public void OnLoad(ref DefLoader loader)
		{
			No = loader.ReadInt();
			
			Difficulty = (AIDifficulty)loader.ReadInt();

			MultiMode = loader.ReadInt() == 1;
			
			AttackBehaviorPercentages = JsonConvert.DeserializeObject<float[]>(loader.ReadString());
			AttackBehaviorRemainTimes = JsonConvert.DeserializeObject<float[]>(loader.ReadString());
			
			MinimumDistanceForGrenades = loader.ReadInt();
			GrenadeNum = loader.ReadInt();
			Health = loader.ReadInt();
			WalkSpeed = loader.ReadFloat();
			CrouchSpeed = loader.ReadFloat();
			RunSpeed = loader.ReadFloat();
			RotationSmoothing = loader.ReadFloat();
			StopDistance = loader.ReadFloat();
			MinRange = loader.ReadFloat();
			FiringDistance = loader.ReadFloat();
			ViewAngle = loader.ReadFloat();
			ReactSpeed = loader.ReadFloat();
			DetectNoiseRange = loader.ReadFloat();
			EnableShootDelay = loader.ReadInt() == 1;
			
			ShootDelayMinTime = loader.ReadFloat();
			ShootDelayMaxTime = loader.ReadFloat();
			ImmatureUserShootDelayMinTime = loader.ReadFloat();
			ImmatureUserShootDelayMaxTime = loader.ReadFloat();
			
			AttackerBombAssignedBehaviorPercentages = JsonConvert.DeserializeObject<float[]>(loader.ReadString());
			AttackerBombInstallBehaviorPercentages = JsonConvert.DeserializeObject<float[]>(loader.ReadString());
			AR_GunList = loader.ReadString();
			SMG_GunList = loader.ReadString();
			SG_GunList = loader.ReadString();
			SR_GunList = loader.ReadString();
			ARAvailable = loader.ReadInt() == 1;
			SMGAvailable = loader.ReadInt() == 1;
			SGAvailable = loader.ReadInt() == 1;
			SRAvailable = loader.ReadInt() == 1;
			ARPercentage = loader.ReadInt();
			SMGPercentage = loader.ReadInt();
			SGPercentage = loader.ReadInt();
			SRPercentage = loader.ReadInt();
		}
		
		public int PeekRandomGunID()
		{
			string rndCategory = GetRandomCategory();
			if(string.IsNullOrEmpty(rndCategory)) { return -1; }
			
			return GetRandomGunID(rndCategory);
		}
		
		public string GetRandomCategory()
		{
			Dictionary<string, int> percentages = GetGunCategoryPercentageSet();
			if(percentages.Count == 0) return string.Empty;
		
			int totalWeight = 0;
			foreach (var kvp in percentages)
				totalWeight += kvp.Value;

			string selectedKey = percentages.FirstOrDefault().Key;

			// 0 ~ 100 사이의 랜덤 값 생성
			int randomValue = Random.Range(0, totalWeight);

			// 누적 확률과 비교하여 Key 선택
			int cumulativeSum = 0;
			foreach (var kvp in percentages)
			{
				cumulativeSum += kvp.Value;
				if (randomValue < cumulativeSum)
				{
					selectedKey = kvp.Key;
					break;
				}
			}

			return selectedKey;
		}
		
		public Dictionary<string, int> GetGunCategoryPercentageSet()
		{
			Dictionary<string, int> percentages = new Dictionary<string, int>();

			if (ARAvailable && (ARPercentage > 0)) { percentages.Add("AR", ARPercentage); }
			if (SMGAvailable && SMGPercentage > 0) { percentages.Add("SMG", SMGPercentage); }
			if (SGAvailable && (SGPercentage > 0)) { percentages.Add("SG", SGPercentage); }
			if (SRAvailable && (SRPercentage > 0)) { percentages.Add("SR", SRPercentage); }
			
			return percentages;
		}

		public int GetRandomGunID(string category)
		{
			var subCategory = GetGunSubCategorySet(category);
			if (subCategory.Length == 0) { return -1; }

			subCategory = subCategory.Select(x => x).Where(x => x.value > 0).ToArray();
			int totalWeight = 0;
			foreach (MiscCDB.GunData gunData in subCategory)
				totalWeight += gunData.value;

			int selectedKey = subCategory.First().id;

			// 0 ~ 100 사이의 랜덤 값 생성
			int randomValue = Random.Range(0, totalWeight);

			// 누적 확률과 비교하여 Key 선택
			int cumulativeSum = 0;
			foreach (MiscCDB.GunData gunData in subCategory)
			{
				cumulativeSum += gunData.value;
				if (randomValue < cumulativeSum)
				{
					selectedKey = gunData.id;
					break;
				}
			}

			return selectedKey;
		}
		
		public MiscCDB.GunData[] GetGunSubCategorySet(string category)
		{
			switch (category)
			{
				case "AR":
					return GetARGunList();
				case "SMG":
					return GetSMGGunList();
				case "SG":
					return GetSGGunList();
				case "SR":
					return GetSRGunList();
			}

			return null;
		}

		public MiscCDB.GunData[] GetARGunList(int id = 1)
		{
			string GunDataKey = $"{BotCDB.arBaseKey}{id}";
			return MiscCDB.Instance.GetGunTable(GunDataKey);
		}
		
		public MiscCDB.GunData[] GetSMGGunList(int id = 1)
		{
			string GunDataKey = $"{BotCDB.smgBaseKey}{id}";
			return MiscCDB.Instance.GetGunTable(GunDataKey);
		}
		
		public MiscCDB.GunData[] GetSGGunList(int id = 1)
		{
			string GunDataKey = $"{BotCDB.sgBaseKey}{id}";
			return MiscCDB.Instance.GetGunTable(GunDataKey);
		}
		
		public MiscCDB.GunData[] GetSRGunList(int id = 1)
		{
			string GunDataKey = $"{BotCDB.srBaseKey}{id}";
			return MiscCDB.Instance.GetGunTable(GunDataKey);
		}
	}
}