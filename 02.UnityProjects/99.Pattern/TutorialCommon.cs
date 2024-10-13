using System;
using System.Collections.Generic;
using Snowballs.Sheets;
using Snowballs.Util;

namespace Snowballs.Sheets.Data
{
	[Serializable]
	public class TutorialCommon
	{
		public Int32 Code;
		public Int32 TutorialType;
		public Int32 TutorialText;
		public Boolean TutoVoiceActive;
		public Int32 TutoVoiceResource;
		public Boolean TutorialSkip;
		public Int32 NextTutorial;
		public Boolean TutorialReward;
		public Int32 TutorialRewardBundleCode;
		public Int32 TutorialPopupResource;
		public Int32 TextYPos;
		public Int32 TargetObject;
		public Int32 PortraitXPos;
		public Int32 PortraitYPos;
		public Int32 TUTOPopup;
		public Boolean TUTOType;
		public Int32 LimitTime;
		public Int32 LocaleTime;
		public Int32 RhythmIngameTime;

		public TutorialType GetTutorialTypeByTutorialType()
		{
			if (TutorialType == default) return null;
			return SBDataSheet.Instance.TutorialType[TutorialType];
		}
		public TutorialLocale GetTutorialLocaleByTutorialText()
		{
			if (TutorialText == default) return null;
			return SBDataSheet.Instance.TutorialLocale[TutorialText];
		}
		public List<ArtistVoice> GetTutoVoiceResource()
		{
			if (TutoVoiceResource == default) return null;
			return SBDataSheet.Instance.GetArtistVoiceListByBundle(TutoVoiceResource);
		}
		public TutorialCommon GetTutorialCommonByNextTutorial()
		{
			if (NextTutorial == default) return null;
			return SBDataSheet.Instance.TutorialCommon[NextTutorial];
		}
		public List<ItemBox> GetTutorialRewardBundleCode()
		{
			if (TutorialRewardBundleCode == default) return null;
			return SBDataSheet.Instance.GetItemBoxListByBundle(TutorialRewardBundleCode);
		}
		public TutorialResource GetTutorialResourceByTutorialPopupResource()
		{
			if (TutorialPopupResource == default) return null;
			return SBDataSheet.Instance.TutorialResource[TutorialPopupResource];
		}
		public Target GetTargetByTargetObject()
		{
			if (TargetObject == default) return null;
			return SBDataSheet.Instance.Target[TargetObject];
		}
	}
}
