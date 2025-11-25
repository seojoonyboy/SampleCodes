using System.Collections.Generic;
using System.Linq;
using Framework;
using Game.Data;
using Game.View.CustomSeraizeDictionary;
using Game.View.UI;
using UnityEngine;

namespace Game.View.BattleSystem
{
	public class TrainingMarkManager : MonoBehaviour
	{
		[SerializeField] 
		List<TrainingMark> traingMarks;

		private static TrainingMarkManager _instance;
		
		[SerializeField] 
		SerializableDictionary<int, TrainingMarkGroup> _trainingMarkMap;		//전체 과녁 프리팹 참조맵
		
		public static TrainingMarkManager Instance
		{
			get
			{
				if (_instance == null) { _instance = FindFirstObjectByType<TrainingMarkManager>(); }
				return _instance;
			}
		}

		//자유 연습장 모드 활성화
		public void SetFreeShootingMode()
		{
			if (!BattleManager.IsStandAloneMode)
			{
				var gunManager = bl_MFPS.LocalPlayerReferences.gunManager;
				gunManager.EquipWeapons[3].bulletsLeft = 1;
				gunManager.EquipWeapons[4].bulletsLeft = 1;
			}

			var tacticalConsume = MobileControlsUi.Instance.TacticalBt.GetComponent<ConsumableWeaponButtonCtl>();
			tacticalConsume.UpdateAmmoUIs();
			
			var grenadeConsume = MobileControlsUi.Instance.GrenadeBt.GetComponent<ConsumableWeaponButtonCtl>();
			grenadeConsume.UpdateAmmoUIs();
			
			foreach (KeyValuePair<int, TrainingMarkGroup> keyValuePair in _trainingMarkMap)
			{
				foreach (TrainingMark defaultMark in keyValuePair.Value.GetAllMarks())
				{
					TrainingMark.TradingMarkParams tradingMarkParams = new();
					tradingMarkParams.ActiveTime = 3.0f;
					defaultMark.InitContent(tradingMarkParams, true);
				}
			}
		}

		//훈련(레벨) 모드 활성화
		public void SetShootingMode(PracticeModeDef selectedPracticeModeDef)
		{
			foreach (KeyValuePair<int, TrainingMarkGroup> keyValuePair in _trainingMarkMap)
			{
				keyValuePair.Value.OffAllMarks();
			}

			if (selectedPracticeModeDef == null)
			{
				DebugEx.Log("Cannot find selectedDef!!", LogColorType.Red);
				return;
			}
			
			foreach (TrainingMarkData activeMark in selectedPracticeModeDef.ActiveMarks)
			{
				int markIndex = activeMark.MarkNo;
				int currentGroupId = activeMark.GroupNo;

				bool afterPrevGroup = activeMark.AfterPrevGroup;
				
				int targetRow = markIndex / GetColumnCount();
				int targetColumn = markIndex % GetColumnCount();
			
				TrainingMark trainingMark = _trainingMarkMap[targetRow].GetMark(targetColumn);
				var targetPatternDef = PracticeModeMarkPatternCDB.Instance.GetDef(activeMark.MarkPatternKey);
			
				TrainingMark.TradingMarkParams tradingMarkParams = new()
				{
					MoveLeft = targetPatternDef.MoveLeft,
					MoveRight = targetPatternDef.MoveRight,
					
					MoveForward = targetPatternDef.MoveForward,
					MoveBackward =  targetPatternDef.MoveBackward,
					
					MoveSpeed = targetPatternDef.MoveSpeed,
					
					ActiveTime = activeMark.MarkBeginTime,
					DeactiveTime = activeMark.MarkEndTime,
					
					OnHit = OnHitTrainingMark
				};

				if (afterPrevGroup && (currentGroupId > 1))
				{
					tradingMarkParams.PrevGroupMarks = GetTrainingMarksByGroupID(currentGroupId - 1);
				}
				
				trainingMark.InitContent(tradingMarkParams, false);
			}
		}

		List<TrainingMark> GetTrainingMarksByGroupID(int groupId)
		{
			PracticeModeDef selectedDef = PracticeModeRepository.Instance.SelectedPracticeModeDef;
			List<TrainingMarkData> selectedDefActiveMarks = selectedDef.ActiveMarks;
			List<TrainingMarkData> targetGroupMarkData = selectedDefActiveMarks.FindAll(x => x.GroupNo == groupId);
			
			List<TrainingMark> result = new();
			foreach (TrainingMarkData markData in targetGroupMarkData)
			{
				int targetRow = markData.MarkNo / GetColumnCount();
				int targetColumn = markData.MarkNo % GetColumnCount();
			
				TrainingMark trainingMark = _trainingMarkMap[targetRow].GetMark(targetColumn);
				result.Add(trainingMark);
			}
			
			return result;
		}
		
		int GetRowCount()
		{
			return _trainingMarkMap.Values.Count;
		}

		int GetColumnCount()
		{
			return _trainingMarkMap.First().Value.GetMarkCount();
		}
		
		void OnHitTrainingMark(bool isSuccess)
		{
			BattleMainUi battleMainUi = BattleMainUi.Instance;
			if(battleMainUi == null) return;
			
			if(isSuccess) battleMainUi.PracticeModeUi.AddSuccess();
			else battleMainUi.PracticeModeUi.AddFail();
		}
	}
}