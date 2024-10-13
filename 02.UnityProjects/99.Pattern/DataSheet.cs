using ICSharpCode.SharpZipLib.GZip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Snowballs.Sheets.Data;
using Snowballs.Util;
using UnityEngine;

namespace Snowballs.Sheets
{
	public class SheetInfo<T>
	{
		public string name;
		public List<T> rows;
	}
	public class SheetData<T>
	{
		private Dictionary<Int32, T> dict;
		public T this[Int32 Code] {
			get {
				T target;
				dict.TryGetValue(Code, out target);
				return target;
			}
		}
		public Dictionary<Int32, T>.ValueCollection Values { get { return dict.Values; } }

		public SheetData(Dictionary<Int32, T> _dict)
		{
			dict = _dict;
		}

		public bool ContainsCode(Int32 Code)
		{
			return dict.ContainsKey(Code);
		}
	}

	public class SBDataSheet
	{
		private static SBDataSheet instance;
		public static SBDataSheet Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new SBDataSheet();
				}
				return instance;
			}
		}

		private SheetData<ActiveItemInfo> _ActiveItemInfo;
		public SheetData<ActiveItemInfo> ActiveItemInfo { get { return _ActiveItemInfo; } }

		private SheetData<Advertisement> _Advertisement;
		public SheetData<Advertisement> Advertisement { get { return _Advertisement; } }

		private SheetData<ArtistInfoSon> _ArtistInfoSon;
		public SheetData<ArtistInfoSon> ArtistInfoSon { get { return _ArtistInfoSon; } }

		private SheetData<ArtistLevelUpInfoSon> _ArtistLevelUpInfoSon;
		public SheetData<ArtistLevelUpInfoSon> ArtistLevelUpInfoSon { get { return _ArtistLevelUpInfoSon; } }
		public List<ArtistLevelUpInfoSon> GetArtistLevelUpInfoSonListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, ArtistLevelUpInfoSon>();
			foreach (ArtistLevelUpInfoSon data in ArtistLevelUpInfoSon.Values)
			{
				if (data.Bundle == key)
				{
					if (ArtistLevelUpInfoSon.ContainsCode(data.Code))
						list.Add(data.Code, ArtistLevelUpInfoSon[data.Code]);
				}
			}
			return new List<ArtistLevelUpInfoSon>(list.Values);
		}

		private SheetData<ArtistLocaleSon> _ArtistLocaleSon;
		public SheetData<ArtistLocaleSon> ArtistLocaleSon { get { return _ArtistLocaleSon; } }

		private SheetData<ArtistResourceSon> _ArtistResourceSon;
		public SheetData<ArtistResourceSon> ArtistResourceSon { get { return _ArtistResourceSon; } }

		private SheetData<ArtistVoice> _ArtistVoice;
		public SheetData<ArtistVoice> ArtistVoice { get { return _ArtistVoice; } }
		public List<ArtistVoice> GetArtistVoiceListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, ArtistVoice>();
			foreach (ArtistVoice data in ArtistVoice.Values)
			{
				if (data.Bundle == key)
				{
					if (ArtistVoice.ContainsCode(data.Code))
						list.Add(data.Code, ArtistVoice[data.Code]);
				}
			}
			return new List<ArtistVoice>(list.Values);
		}

		private SheetData<BannerResource> _BannerResource;
		public SheetData<BannerResource> BannerResource { get { return _BannerResource; } }

		private SheetData<BlockInfo> _BlockInfo;
		public SheetData<BlockInfo> BlockInfo { get { return _BlockInfo; } }

		private SheetData<Buff> _Buff;
		public SheetData<Buff> Buff { get { return _Buff; } }

		private SheetData<CardEnhanceItemInfoSon> _CardEnhanceItemInfoSon;
		public SheetData<CardEnhanceItemInfoSon> CardEnhanceItemInfoSon { get { return _CardEnhanceItemInfoSon; } }

		private SheetData<CardExpSon> _CardExpSon;
		public SheetData<CardExpSon> CardExpSon { get { return _CardExpSon; } }

		private SheetData<CardExtraResource> _CardExtraResource;
		public SheetData<CardExtraResource> CardExtraResource { get { return _CardExtraResource; } }

		private SheetData<CardFrameBundle> _CardFrameBundle;
		public SheetData<CardFrameBundle> CardFrameBundle { get { return _CardFrameBundle; } }
		public List<CardFrameBundle> GetCardFrameBundleListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, CardFrameBundle>();
			foreach (CardFrameBundle data in CardFrameBundle.Values)
			{
				if (data.Bundle == key)
				{
					if (CardFrameBundle.ContainsCode(data.Code))
						list.Add(data.Code, CardFrameBundle[data.Code]);
				}
			}
			return new List<CardFrameBundle>(list.Values);
		}

		private SheetData<CardFrameBundleSon> _CardFrameBundleSon;
		public SheetData<CardFrameBundleSon> CardFrameBundleSon { get { return _CardFrameBundleSon; } }
		public List<CardFrameBundleSon> GetCardFrameBundleSonListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, CardFrameBundleSon>();
			foreach (CardFrameBundleSon data in CardFrameBundleSon.Values)
			{
				if (data.Bundle == key)
				{
					if (CardFrameBundleSon.ContainsCode(data.Code))
						list.Add(data.Code, CardFrameBundleSon[data.Code]);
				}
			}
			return new List<CardFrameBundleSon>(list.Values);
		}

		private SheetData<CardFrameGacha> _CardFrameGacha;
		public SheetData<CardFrameGacha> CardFrameGacha { get { return _CardFrameGacha; } }

		private SheetData<CardFrameGachaSon> _CardFrameGachaSon;
		public SheetData<CardFrameGachaSon> CardFrameGachaSon { get { return _CardFrameGachaSon; } }

		private SheetData<CardFrameLocale> _CardFrameLocale;
		public SheetData<CardFrameLocale> CardFrameLocale { get { return _CardFrameLocale; } }

		private SheetData<CardFramePackage> _CardFramePackage;
		public SheetData<CardFramePackage> CardFramePackage { get { return _CardFramePackage; } }
		public List<CardFramePackage> GetCardFramePackageListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, CardFramePackage>();
			foreach (CardFramePackage data in CardFramePackage.Values)
			{
				if (data.Bundle == key)
				{
					if (CardFramePackage.ContainsCode(data.Code))
						list.Add(data.Code, CardFramePackage[data.Code]);
				}
			}
			return new List<CardFramePackage>(list.Values);
		}

		private SheetData<CardFramePackageSon> _CardFramePackageSon;
		public SheetData<CardFramePackageSon> CardFramePackageSon { get { return _CardFramePackageSon; } }
		public List<CardFramePackageSon> GetCardFramePackageSonListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, CardFramePackageSon>();
			foreach (CardFramePackageSon data in CardFramePackageSon.Values)
			{
				if (data.Bundle == key)
				{
					if (CardFramePackageSon.ContainsCode(data.Code))
						list.Add(data.Code, CardFramePackageSon[data.Code]);
				}
			}
			return new List<CardFramePackageSon>(list.Values);
		}

		private SheetData<CardFrameParts> _CardFrameParts;
		public SheetData<CardFrameParts> CardFrameParts { get { return _CardFrameParts; } }

		private SheetData<CardFramePartsSon> _CardFramePartsSon;
		public SheetData<CardFramePartsSon> CardFramePartsSon { get { return _CardFramePartsSon; } }

		private SheetData<CardFrameResource> _CardFrameResource;
		public SheetData<CardFrameResource> CardFrameResource { get { return _CardFrameResource; } }

		private SheetData<CardFrameSet> _CardFrameSet;
		public SheetData<CardFrameSet> CardFrameSet { get { return _CardFrameSet; } }

		private SheetData<CardFrameSetSon> _CardFrameSetSon;
		public SheetData<CardFrameSetSon> CardFrameSetSon { get { return _CardFrameSetSon; } }

		private SheetData<CardGacha> _CardGacha;
		public SheetData<CardGacha> CardGacha { get { return _CardGacha; } }

		private SheetData<CardGachaEventSon> _CardGachaEventSon;
		public SheetData<CardGachaEventSon> CardGachaEventSon { get { return _CardGachaEventSon; } }
		public List<CardGachaEventSon> GetCardGachaEventSonListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, CardGachaEventSon>();
			foreach (CardGachaEventSon data in CardGachaEventSon.Values)
			{
				if (data.Bundle == key)
				{
					if (CardGachaEventSon.ContainsCode(data.Code))
						list.Add(data.Code, CardGachaEventSon[data.Code]);
				}
			}
			return new List<CardGachaEventSon>(list.Values);
		}

		private SheetData<CardGachaInfo> _CardGachaInfo;
		public SheetData<CardGachaInfo> CardGachaInfo { get { return _CardGachaInfo; } }
		public List<CardGachaInfo> GetCardGachaInfoListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, CardGachaInfo>();
			foreach (CardGachaInfo data in CardGachaInfo.Values)
			{
				if (data.Bundle == key)
				{
					if (CardGachaInfo.ContainsCode(data.Code))
						list.Add(data.Code, CardGachaInfo[data.Code]);
				}
			}
			return new List<CardGachaInfo>(list.Values);
		}

		private SheetData<CardGachaLvUpPackageSon> _CardGachaLvUpPackageSon;
		public SheetData<CardGachaLvUpPackageSon> CardGachaLvUpPackageSon { get { return _CardGachaLvUpPackageSon; } }
		public List<CardGachaLvUpPackageSon> GetCardGachaLvUpPackageSonListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, CardGachaLvUpPackageSon>();
			foreach (CardGachaLvUpPackageSon data in CardGachaLvUpPackageSon.Values)
			{
				if (data.Bundle == key)
				{
					if (CardGachaLvUpPackageSon.ContainsCode(data.Code))
						list.Add(data.Code, CardGachaLvUpPackageSon[data.Code]);
				}
			}
			return new List<CardGachaLvUpPackageSon>(list.Values);
		}

		private SheetData<CardGachaPackage> _CardGachaPackage;
		public SheetData<CardGachaPackage> CardGachaPackage { get { return _CardGachaPackage; } }
		public List<CardGachaPackage> GetCardGachaPackageListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, CardGachaPackage>();
			foreach (CardGachaPackage data in CardGachaPackage.Values)
			{
				if (data.Bundle == key)
				{
					if (CardGachaPackage.ContainsCode(data.Code))
						list.Add(data.Code, CardGachaPackage[data.Code]);
				}
			}
			return new List<CardGachaPackage>(list.Values);
		}

		private SheetData<CardGachaPackageSon> _CardGachaPackageSon;
		public SheetData<CardGachaPackageSon> CardGachaPackageSon { get { return _CardGachaPackageSon; } }
		public List<CardGachaPackageSon> GetCardGachaPackageSonListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, CardGachaPackageSon>();
			foreach (CardGachaPackageSon data in CardGachaPackageSon.Values)
			{
				if (data.Bundle == key)
				{
					if (CardGachaPackageSon.ContainsCode(data.Code))
						list.Add(data.Code, CardGachaPackageSon[data.Code]);
				}
			}
			return new List<CardGachaPackageSon>(list.Values);
		}

		private SheetData<CardGachaPrice> _CardGachaPrice;
		public SheetData<CardGachaPrice> CardGachaPrice { get { return _CardGachaPrice; } }
		public List<CardGachaPrice> GetCardGachaPriceListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, CardGachaPrice>();
			foreach (CardGachaPrice data in CardGachaPrice.Values)
			{
				if (data.Bundle == key)
				{
					if (CardGachaPrice.ContainsCode(data.Code))
						list.Add(data.Code, CardGachaPrice[data.Code]);
				}
			}
			return new List<CardGachaPrice>(list.Values);
		}

		private SheetData<CardGachaSon> _CardGachaSon;
		public SheetData<CardGachaSon> CardGachaSon { get { return _CardGachaSon; } }

		private SheetData<CardGrade> _CardGrade;
		public SheetData<CardGrade> CardGrade { get { return _CardGrade; } }

		private SheetData<CardGradeReward> _CardGradeReward;
		public SheetData<CardGradeReward> CardGradeReward { get { return _CardGradeReward; } }
		public List<CardGradeReward> GetCardGradeRewardListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, CardGradeReward>();
			foreach (CardGradeReward data in CardGradeReward.Values)
			{
				if (data.Bundle == key)
				{
					if (CardGradeReward.ContainsCode(data.Code))
						list.Add(data.Code, CardGradeReward[data.Code]);
				}
			}
			return new List<CardGradeReward>(list.Values);
		}

		private SheetData<CardGradeRewardSon> _CardGradeRewardSon;
		public SheetData<CardGradeRewardSon> CardGradeRewardSon { get { return _CardGradeRewardSon; } }
		public List<CardGradeRewardSon> GetCardGradeRewardSonListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, CardGradeRewardSon>();
			foreach (CardGradeRewardSon data in CardGradeRewardSon.Values)
			{
				if (data.Bundle == key)
				{
					if (CardGradeRewardSon.ContainsCode(data.Code))
						list.Add(data.Code, CardGradeRewardSon[data.Code]);
				}
			}
			return new List<CardGradeRewardSon>(list.Values);
		}

		private SheetData<CardGradeSon> _CardGradeSon;
		public SheetData<CardGradeSon> CardGradeSon { get { return _CardGradeSon; } }
		public List<CardGradeSon> GetCardGradeSonListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, CardGradeSon>();
			foreach (CardGradeSon data in CardGradeSon.Values)
			{
				if (data.Bundle == key)
				{
					if (CardGradeSon.ContainsCode(data.Code))
						list.Add(data.Code, CardGradeSon[data.Code]);
				}
			}
			return new List<CardGradeSon>(list.Values);
		}

		private SheetData<CardInfo> _CardInfo;
		public SheetData<CardInfo> CardInfo { get { return _CardInfo; } }

		private SheetData<CardInfoSon> _CardInfoSon;
		public SheetData<CardInfoSon> CardInfoSon { get { return _CardInfoSon; } }
		public List<CardInfoSon> GetCardInfoSonListByThemeCode(Int32 key)
		{
			var list = new Dictionary<Int32, CardInfoSon>();
			foreach (CardInfoSon data in CardInfoSon.Values)
			{
				if (data.ThemeCode == key)
				{
					if (CardInfoSon.ContainsCode(data.Code))
						list.Add(data.Code, CardInfoSon[data.Code]);
				}
			}
			return new List<CardInfoSon>(list.Values);
		}

		private SheetData<CardLvGradeSon> _CardLvGradeSon;
		public SheetData<CardLvGradeSon> CardLvGradeSon { get { return _CardLvGradeSon; } }

		private SheetData<CardLvUpItemInfoSon> _CardLvUpItemInfoSon;
		public SheetData<CardLvUpItemInfoSon> CardLvUpItemInfoSon { get { return _CardLvUpItemInfoSon; } }

		private SheetData<CardPartsInfo> _CardPartsInfo;
		public SheetData<CardPartsInfo> CardPartsInfo { get { return _CardPartsInfo; } }

		private SheetData<CardPartsInfoSon> _CardPartsInfoSon;
		public SheetData<CardPartsInfoSon> CardPartsInfoSon { get { return _CardPartsInfoSon; } }

		private SheetData<CardProfileLocale> _CardProfileLocale;
		public SheetData<CardProfileLocale> CardProfileLocale { get { return _CardProfileLocale; } }

		private SheetData<CardResource> _CardResource;
		public SheetData<CardResource> CardResource { get { return _CardResource; } }

		private SheetData<CardSkill> _CardSkill;
		public SheetData<CardSkill> CardSkill { get { return _CardSkill; } }

		private SheetData<CardSkillSet> _CardSkillSet;
		public SheetData<CardSkillSet> CardSkillSet { get { return _CardSkillSet; } }
		public List<CardSkillSet> GetCardSkillSetListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, CardSkillSet>();
			foreach (CardSkillSet data in CardSkillSet.Values)
			{
				if (data.Bundle == key)
				{
					if (CardSkillSet.ContainsCode(data.Code))
						list.Add(data.Code, CardSkillSet[data.Code]);
				}
			}
			return new List<CardSkillSet>(list.Values);
		}

		private SheetData<CardSkillSetSon> _CardSkillSetSon;
		public SheetData<CardSkillSetSon> CardSkillSetSon { get { return _CardSkillSetSon; } }
		public List<CardSkillSetSon> GetCardSkillSetSonListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, CardSkillSetSon>();
			foreach (CardSkillSetSon data in CardSkillSetSon.Values)
			{
				if (data.Bundle == key)
				{
					if (CardSkillSetSon.ContainsCode(data.Code))
						list.Add(data.Code, CardSkillSetSon[data.Code]);
				}
			}
			return new List<CardSkillSetSon>(list.Values);
		}

		private SheetData<CardVoiceBundle> _CardVoiceBundle;
		public SheetData<CardVoiceBundle> CardVoiceBundle { get { return _CardVoiceBundle; } }
		public List<CardVoiceBundle> GetCardVoiceBundleListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, CardVoiceBundle>();
			foreach (CardVoiceBundle data in CardVoiceBundle.Values)
			{
				if (data.Bundle == key)
				{
					if (CardVoiceBundle.ContainsCode(data.Code))
						list.Add(data.Code, CardVoiceBundle[data.Code]);
				}
			}
			return new List<CardVoiceBundle>(list.Values);
		}

		private SheetData<ChallengeStage> _ChallengeStage;
		public SheetData<ChallengeStage> ChallengeStage { get { return _ChallengeStage; } }

		private SheetData<DailyBonusInfo> _DailyBonusInfo;
		public SheetData<DailyBonusInfo> DailyBonusInfo { get { return _DailyBonusInfo; } }

		private SheetData<DailyCumReward> _DailyCumReward;
		public SheetData<DailyCumReward> DailyCumReward { get { return _DailyCumReward; } }
		public List<DailyCumReward> GetDailyCumRewardListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, DailyCumReward>();
			foreach (DailyCumReward data in DailyCumReward.Values)
			{
				if (data.Bundle == key)
				{
					if (DailyCumReward.ContainsCode(data.Code))
						list.Add(data.Code, DailyCumReward[data.Code]);
				}
			}
			return new List<DailyCumReward>(list.Values);
		}

		private SheetData<DailyReward> _DailyReward;
		public SheetData<DailyReward> DailyReward { get { return _DailyReward; } }
		public List<DailyReward> GetDailyRewardListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, DailyReward>();
			foreach (DailyReward data in DailyReward.Values)
			{
				if (data.Bundle == key)
				{
					if (DailyReward.ContainsCode(data.Code))
						list.Add(data.Code, DailyReward[data.Code]);
				}
			}
			return new List<DailyReward>(list.Values);
		}

		private SheetData<DefaultItemInfo> _DefaultItemInfo;
		public SheetData<DefaultItemInfo> DefaultItemInfo { get { return _DefaultItemInfo; } }

		private SheetData<DiaEvent> _DiaEvent;
		public SheetData<DiaEvent> DiaEvent { get { return _DiaEvent; } }

		private SheetData<DiaStore> _DiaStore;
		public SheetData<DiaStore> DiaStore { get { return _DiaStore; } }

		private SheetData<EventLocale> _EventLocale;
		public SheetData<EventLocale> EventLocale { get { return _EventLocale; } }

		private SheetData<EventResource> _EventResource;
		public SheetData<EventResource> EventResource { get { return _EventResource; } }

		private SheetData<ExtraLocale> _ExtraLocale;
		public SheetData<ExtraLocale> ExtraLocale { get { return _ExtraLocale; } }

		private SheetData<FrameGachaPrice> _FrameGachaPrice;
		public SheetData<FrameGachaPrice> FrameGachaPrice { get { return _FrameGachaPrice; } }
		public List<FrameGachaPrice> GetFrameGachaPriceListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, FrameGachaPrice>();
			foreach (FrameGachaPrice data in FrameGachaPrice.Values)
			{
				if (data.Bundle == key)
				{
					if (FrameGachaPrice.ContainsCode(data.Code))
						list.Add(data.Code, FrameGachaPrice[data.Code]);
				}
			}
			return new List<FrameGachaPrice>(list.Values);
		}

		private SheetData<GachaResource> _GachaResource;
		public SheetData<GachaResource> GachaResource { get { return _GachaResource; } }

		private SheetData<GameConfig> _GameConfig;
		public SheetData<GameConfig> GameConfig { get { return _GameConfig; } }

		private SheetData<GoldStore> _GoldStore;
		public SheetData<GoldStore> GoldStore { get { return _GoldStore; } }

		private SheetData<IDCardInfoSon> _IDCardInfoSon;
		public SheetData<IDCardInfoSon> IDCardInfoSon { get { return _IDCardInfoSon; } }

		private SheetData<IDCardResourceSon> _IDCardResourceSon;
		public SheetData<IDCardResourceSon> IDCardResourceSon { get { return _IDCardResourceSon; } }

		private SheetData<IndicatorInfoSon> _IndicatorInfoSon;
		public SheetData<IndicatorInfoSon> IndicatorInfoSon { get { return _IndicatorInfoSon; } }

		private SheetData<ItemBox> _ItemBox;
		public SheetData<ItemBox> ItemBox { get { return _ItemBox; } }
		public List<ItemBox> GetItemBoxListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, ItemBox>();
			foreach (ItemBox data in ItemBox.Values)
			{
				if (data.Bundle == key)
				{
					if (ItemBox.ContainsCode(data.Code))
						list.Add(data.Code, ItemBox[data.Code]);
				}
			}
			return new List<ItemBox>(list.Values);
		}

		private SheetData<ItemLocale> _ItemLocale;
		public SheetData<ItemLocale> ItemLocale { get { return _ItemLocale; } }

		private SheetData<ItemProduction> _ItemProduction;
		public SheetData<ItemProduction> ItemProduction { get { return _ItemProduction; } }

		private SheetData<ItemRandomBox> _ItemRandomBox;
		public SheetData<ItemRandomBox> ItemRandomBox { get { return _ItemRandomBox; } }
		public List<ItemRandomBox> GetItemRandomBoxListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, ItemRandomBox>();
			foreach (ItemRandomBox data in ItemRandomBox.Values)
			{
				if (data.Bundle == key)
				{
					if (ItemRandomBox.ContainsCode(data.Code))
						list.Add(data.Code, ItemRandomBox[data.Code]);
				}
			}
			return new List<ItemRandomBox>(list.Values);
		}

		private SheetData<ItemResource> _ItemResource;
		public SheetData<ItemResource> ItemResource { get { return _ItemResource; } }

		private SheetData<ItemType> _ItemType;
		public SheetData<ItemType> ItemType { get { return _ItemType; } }

		private SheetData<LoadingImageInfo> _LoadingImageInfo;
		public SheetData<LoadingImageInfo> LoadingImageInfo { get { return _LoadingImageInfo; } }

		private SheetData<LobbyBanner> _LobbyBanner;
		public SheetData<LobbyBanner> LobbyBanner { get { return _LobbyBanner; } }

		private SheetData<LobbyIcon> _LobbyIcon;
		public SheetData<LobbyIcon> LobbyIcon { get { return _LobbyIcon; } }

		private SheetData<LobbyIconResource> _LobbyIconResource;
		public SheetData<LobbyIconResource> LobbyIconResource { get { return _LobbyIconResource; } }

		private SheetData<MagazineLocale> _MagazineLocale;
		public SheetData<MagazineLocale> MagazineLocale { get { return _MagazineLocale; } }

		private SheetData<MagazineResource> _MagazineResource;
		public SheetData<MagazineResource> MagazineResource { get { return _MagazineResource; } }

		private SheetData<MembershipCumReward> _MembershipCumReward;
		public SheetData<MembershipCumReward> MembershipCumReward { get { return _MembershipCumReward; } }
		public List<MembershipCumReward> GetMembershipCumRewardListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, MembershipCumReward>();
			foreach (MembershipCumReward data in MembershipCumReward.Values)
			{
				if (data.Bundle == key)
				{
					if (MembershipCumReward.ContainsCode(data.Code))
						list.Add(data.Code, MembershipCumReward[data.Code]);
				}
			}
			return new List<MembershipCumReward>(list.Values);
		}

		private SheetData<MembershipInfo> _MembershipInfo;
		public SheetData<MembershipInfo> MembershipInfo { get { return _MembershipInfo; } }
		public List<MembershipInfo> GetMembershipInfoListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, MembershipInfo>();
			foreach (MembershipInfo data in MembershipInfo.Values)
			{
				if (data.Bundle == key)
				{
					if (MembershipInfo.ContainsCode(data.Code))
						list.Add(data.Code, MembershipInfo[data.Code]);
				}
			}
			return new List<MembershipInfo>(list.Values);
		}

		private SheetData<MileageStore> _MileageStore;
		public SheetData<MileageStore> MileageStore { get { return _MileageStore; } }
		public List<MileageStore> GetMileageStoreListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, MileageStore>();
			foreach (MileageStore data in MileageStore.Values)
			{
				if (data.Bundle == key)
				{
					if (MileageStore.ContainsCode(data.Code))
						list.Add(data.Code, MileageStore[data.Code]);
				}
			}
			return new List<MileageStore>(list.Values);
		}

		private SheetData<MileageStoreInfo> _MileageStoreInfo;
		public SheetData<MileageStoreInfo> MileageStoreInfo { get { return _MileageStoreInfo; } }

		private SheetData<MissionBundle> _MissionBundle;
		public SheetData<MissionBundle> MissionBundle { get { return _MissionBundle; } }
		public List<MissionBundle> GetMissionBundleListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, MissionBundle>();
			foreach (MissionBundle data in MissionBundle.Values)
			{
				if (data.Bundle == key)
				{
					if (MissionBundle.ContainsCode(data.Code))
						list.Add(data.Code, MissionBundle[data.Code]);
				}
			}
			return new List<MissionBundle>(list.Values);
		}

		private SheetData<MissionCondition> _MissionCondition;
		public SheetData<MissionCondition> MissionCondition { get { return _MissionCondition; } }

		private SheetData<MissionConditionBundle> _MissionConditionBundle;
		public SheetData<MissionConditionBundle> MissionConditionBundle { get { return _MissionConditionBundle; } }
		public List<MissionConditionBundle> GetMissionConditionBundleListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, MissionConditionBundle>();
			foreach (MissionConditionBundle data in MissionConditionBundle.Values)
			{
				if (data.Bundle == key)
				{
					if (MissionConditionBundle.ContainsCode(data.Code))
						list.Add(data.Code, MissionConditionBundle[data.Code]);
				}
			}
			return new List<MissionConditionBundle>(list.Values);
		}

		private SheetData<MissionInfo> _MissionInfo;
		public SheetData<MissionInfo> MissionInfo { get { return _MissionInfo; } }

		private SheetData<MissionLocale> _MissionLocale;
		public SheetData<MissionLocale> MissionLocale { get { return _MissionLocale; } }

		private SheetData<MissionResource> _MissionResource;
		public SheetData<MissionResource> MissionResource { get { return _MissionResource; } }

		private SheetData<MissionType> _MissionType;
		public SheetData<MissionType> MissionType { get { return _MissionType; } }

		private SheetData<PackageBundle> _PackageBundle;
		public SheetData<PackageBundle> PackageBundle { get { return _PackageBundle; } }
		public List<PackageBundle> GetPackageBundleListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, PackageBundle>();
			foreach (PackageBundle data in PackageBundle.Values)
			{
				if (data.Bundle == key)
				{
					if (PackageBundle.ContainsCode(data.Code))
						list.Add(data.Code, PackageBundle[data.Code]);
				}
			}
			return new List<PackageBundle>(list.Values);
		}

		private SheetData<PackagePopUp> _PackagePopUp;
		public SheetData<PackagePopUp> PackagePopUp { get { return _PackagePopUp; } }
		public List<PackagePopUp> GetPackagePopUpListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, PackagePopUp>();
			foreach (PackagePopUp data in PackagePopUp.Values)
			{
				if (data.Bundle == key)
				{
					if (PackagePopUp.ContainsCode(data.Code))
						list.Add(data.Code, PackagePopUp[data.Code]);
				}
			}
			return new List<PackagePopUp>(list.Values);
		}

		private SheetData<PassInfo> _PassInfo;
		public SheetData<PassInfo> PassInfo { get { return _PassInfo; } }

		private SheetData<PassReward> _PassReward;
		public SheetData<PassReward> PassReward { get { return _PassReward; } }
		public List<PassReward> GetPassRewardListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, PassReward>();
			foreach (PassReward data in PassReward.Values)
			{
				if (data.Bundle == key)
				{
					if (PassReward.ContainsCode(data.Code))
						list.Add(data.Code, PassReward[data.Code]);
				}
			}
			return new List<PassReward>(list.Values);
		}

		private SheetData<PopUpStore> _PopUpStore;
		public SheetData<PopUpStore> PopUpStore { get { return _PopUpStore; } }

		private SheetData<PopUpStoreType> _PopUpStoreType;
		public SheetData<PopUpStoreType> PopUpStoreType { get { return _PopUpStoreType; } }

		private SheetData<PopupDes> _PopupDes;
		public SheetData<PopupDes> PopupDes { get { return _PopupDes; } }
		public List<PopupDes> GetPopupDesListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, PopupDes>();
			foreach (PopupDes data in PopupDes.Values)
			{
				if (data.Bundle == key)
				{
					if (PopupDes.ContainsCode(data.Code))
						list.Add(data.Code, PopupDes[data.Code]);
				}
			}
			return new List<PopupDes>(list.Values);
		}

		private SheetData<PopupInfo> _PopupInfo;
		public SheetData<PopupInfo> PopupInfo { get { return _PopupInfo; } }

		private SheetData<ProductLocale> _ProductLocale;
		public SheetData<ProductLocale> ProductLocale { get { return _ProductLocale; } }

		private SheetData<Profile> _Profile;
		public SheetData<Profile> Profile { get { return _Profile; } }

		private SheetData<ProfileResource> _ProfileResource;
		public SheetData<ProfileResource> ProfileResource { get { return _ProfileResource; } }

		private SheetData<ProfileSelect> _ProfileSelect;
		public SheetData<ProfileSelect> ProfileSelect { get { return _ProfileSelect; } }

		private SheetData<ProfileSon> _ProfileSon;
		public SheetData<ProfileSon> ProfileSon { get { return _ProfileSon; } }

		private SheetData<RecommendStore> _RecommendStore;
		public SheetData<RecommendStore> RecommendStore { get { return _RecommendStore; } }

		private SheetData<RhythmAlbumCoverResource> _RhythmAlbumCoverResource;
		public SheetData<RhythmAlbumCoverResource> RhythmAlbumCoverResource { get { return _RhythmAlbumCoverResource; } }

		private SheetData<RhythmAlbumLocale> _RhythmAlbumLocale;
		public SheetData<RhythmAlbumLocale> RhythmAlbumLocale { get { return _RhythmAlbumLocale; } }

		private SheetData<RhythmAlbumTheme> _RhythmAlbumTheme;
		public SheetData<RhythmAlbumTheme> RhythmAlbumTheme { get { return _RhythmAlbumTheme; } }

		private SheetData<RhythmBackgroundBundle> _RhythmBackgroundBundle;
		public SheetData<RhythmBackgroundBundle> RhythmBackgroundBundle { get { return _RhythmBackgroundBundle; } }
		public List<RhythmBackgroundBundle> GetRhythmBackgroundBundleListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, RhythmBackgroundBundle>();
			foreach (RhythmBackgroundBundle data in RhythmBackgroundBundle.Values)
			{
				if (data.Bundle == key)
				{
					if (RhythmBackgroundBundle.ContainsCode(data.Code))
						list.Add(data.Code, RhythmBackgroundBundle[data.Code]);
				}
			}
			return new List<RhythmBackgroundBundle>(list.Values);
		}

		private SheetData<RhythmBackgroundType> _RhythmBackgroundType;
		public SheetData<RhythmBackgroundType> RhythmBackgroundType { get { return _RhythmBackgroundType; } }

		private SheetData<RhythmCardDeckCnt> _RhythmCardDeckCnt;
		public SheetData<RhythmCardDeckCnt> RhythmCardDeckCnt { get { return _RhythmCardDeckCnt; } }

		private SheetData<RhythmComboScore> _RhythmComboScore;
		public SheetData<RhythmComboScore> RhythmComboScore { get { return _RhythmComboScore; } }

		private SheetData<RhythmHPInfo> _RhythmHPInfo;
		public SheetData<RhythmHPInfo> RhythmHPInfo { get { return _RhythmHPInfo; } }

		private SheetData<RhythmHit> _RhythmHit;
		public SheetData<RhythmHit> RhythmHit { get { return _RhythmHit; } }

		private SheetData<RhythmLevelType> _RhythmLevelType;
		public SheetData<RhythmLevelType> RhythmLevelType { get { return _RhythmLevelType; } }

		private SheetData<RhythmNoteHitInfo> _RhythmNoteHitInfo;
		public SheetData<RhythmNoteHitInfo> RhythmNoteHitInfo { get { return _RhythmNoteHitInfo; } }

		private SheetData<RhythmNoteInfo> _RhythmNoteInfo;
		public SheetData<RhythmNoteInfo> RhythmNoteInfo { get { return _RhythmNoteInfo; } }

		private SheetData<RhythmNoteSpeedInfo> _RhythmNoteSpeedInfo;
		public SheetData<RhythmNoteSpeedInfo> RhythmNoteSpeedInfo { get { return _RhythmNoteSpeedInfo; } }

		private SheetData<RhythmPlayBackgoundResource> _RhythmPlayBackgoundResource;
		public SheetData<RhythmPlayBackgoundResource> RhythmPlayBackgoundResource { get { return _RhythmPlayBackgoundResource; } }

		private SheetData<RhythmPlayPreviewMusic> _RhythmPlayPreviewMusic;
		public SheetData<RhythmPlayPreviewMusic> RhythmPlayPreviewMusic { get { return _RhythmPlayPreviewMusic; } }

		private SheetData<RhythmRankingInfo> _RhythmRankingInfo;
		public SheetData<RhythmRankingInfo> RhythmRankingInfo { get { return _RhythmRankingInfo; } }
		public List<RhythmRankingInfo> GetRhythmRankingInfoListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, RhythmRankingInfo>();
			foreach (RhythmRankingInfo data in RhythmRankingInfo.Values)
			{
				if (data.Bundle == key)
				{
					if (RhythmRankingInfo.ContainsCode(data.Code))
						list.Add(data.Code, RhythmRankingInfo[data.Code]);
				}
			}
			return new List<RhythmRankingInfo>(list.Values);
		}

		private SheetData<RhythmRankingReward> _RhythmRankingReward;
		public SheetData<RhythmRankingReward> RhythmRankingReward { get { return _RhythmRankingReward; } }
		public List<RhythmRankingReward> GetRhythmRankingRewardListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, RhythmRankingReward>();
			foreach (RhythmRankingReward data in RhythmRankingReward.Values)
			{
				if (data.Bundle == key)
				{
					if (RhythmRankingReward.ContainsCode(data.Code))
						list.Add(data.Code, RhythmRankingReward[data.Code]);
				}
			}
			return new List<RhythmRankingReward>(list.Values);
		}

		private SheetData<RhythmScoreRank> _RhythmScoreRank;
		public SheetData<RhythmScoreRank> RhythmScoreRank { get { return _RhythmScoreRank; } }

		private SheetData<RhythmSongEvent> _RhythmSongEvent;
		public SheetData<RhythmSongEvent> RhythmSongEvent { get { return _RhythmSongEvent; } }

		private SheetData<RhythmSongList> _RhythmSongList;
		public SheetData<RhythmSongList> RhythmSongList { get { return _RhythmSongList; } }

		private SheetData<RhythmSongLocale> _RhythmSongLocale;
		public SheetData<RhythmSongLocale> RhythmSongLocale { get { return _RhythmSongLocale; } }

		private SheetData<RhythmUseItemInfo> _RhythmUseItemInfo;
		public SheetData<RhythmUseItemInfo> RhythmUseItemInfo { get { return _RhythmUseItemInfo; } }

		private SheetData<ScoreBuffCardSon> _ScoreBuffCardSon;
		public SheetData<ScoreBuffCardSon> ScoreBuffCardSon { get { return _ScoreBuffCardSon; } }
		public List<ScoreBuffCardSon> GetScoreBuffCardSonListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, ScoreBuffCardSon>();
			foreach (ScoreBuffCardSon data in ScoreBuffCardSon.Values)
			{
				if (data.Bundle == key)
				{
					if (ScoreBuffCardSon.ContainsCode(data.Code))
						list.Add(data.Code, ScoreBuffCardSon[data.Code]);
				}
			}
			return new List<ScoreBuffCardSon>(list.Values);
		}

		private SheetData<ScoreModeFaleImageSon> _ScoreModeFaleImageSon;
		public SheetData<ScoreModeFaleImageSon> ScoreModeFaleImageSon { get { return _ScoreModeFaleImageSon; } }
		public List<ScoreModeFaleImageSon> GetScoreModeFaleImageSonListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, ScoreModeFaleImageSon>();
			foreach (ScoreModeFaleImageSon data in ScoreModeFaleImageSon.Values)
			{
				if (data.Bundle == key)
				{
					if (ScoreModeFaleImageSon.ContainsCode(data.Code))
						list.Add(data.Code, ScoreModeFaleImageSon[data.Code]);
				}
			}
			return new List<ScoreModeFaleImageSon>(list.Values);
		}

		private SheetData<ScoreModeInfoSon> _ScoreModeInfoSon;
		public SheetData<ScoreModeInfoSon> ScoreModeInfoSon { get { return _ScoreModeInfoSon; } }

		private SheetData<ScoreModeStageSon> _ScoreModeStageSon;
		public SheetData<ScoreModeStageSon> ScoreModeStageSon { get { return _ScoreModeStageSon; } }
		public List<ScoreModeStageSon> GetScoreModeStageSonListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, ScoreModeStageSon>();
			foreach (ScoreModeStageSon data in ScoreModeStageSon.Values)
			{
				if (data.Bundle == key)
				{
					if (ScoreModeStageSon.ContainsCode(data.Code))
						list.Add(data.Code, ScoreModeStageSon[data.Code]);
				}
			}
			return new List<ScoreModeStageSon>(list.Values);
		}

		private SheetData<ScorePtimeInfoSon> _ScorePtimeInfoSon;
		public SheetData<ScorePtimeInfoSon> ScorePtimeInfoSon { get { return _ScorePtimeInfoSon; } }

		private SheetData<ScoreRewardSon> _ScoreRewardSon;
		public SheetData<ScoreRewardSon> ScoreRewardSon { get { return _ScoreRewardSon; } }
		public List<ScoreRewardSon> GetScoreRewardSonListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, ScoreRewardSon>();
			foreach (ScoreRewardSon data in ScoreRewardSon.Values)
			{
				if (data.Bundle == key)
				{
					if (ScoreRewardSon.ContainsCode(data.Code))
						list.Add(data.Code, ScoreRewardSon[data.Code]);
				}
			}
			return new List<ScoreRewardSon>(list.Values);
		}

		private SheetData<SlotItem> _SlotItem;
		public SheetData<SlotItem> SlotItem { get { return _SlotItem; } }

		private SheetData<SpecialID> _SpecialID;
		public SheetData<SpecialID> SpecialID { get { return _SpecialID; } }

		private SheetData<SpecialIDLocale> _SpecialIDLocale;
		public SheetData<SpecialIDLocale> SpecialIDLocale { get { return _SpecialIDLocale; } }

		private SheetData<SpecialIDResource> _SpecialIDResource;
		public SheetData<SpecialIDResource> SpecialIDResource { get { return _SpecialIDResource; } }

		private SheetData<SpecialMagazineInfo> _SpecialMagazineInfo;
		public SheetData<SpecialMagazineInfo> SpecialMagazineInfo { get { return _SpecialMagazineInfo; } }

		private SheetData<SpecialMagazinePicture> _SpecialMagazinePicture;
		public SheetData<SpecialMagazinePicture> SpecialMagazinePicture { get { return _SpecialMagazinePicture; } }
		public List<SpecialMagazinePicture> GetSpecialMagazinePictureListByGroup(Int32 key)
		{
			var list = new Dictionary<Int32, SpecialMagazinePicture>();
			foreach (SpecialMagazinePicture data in SpecialMagazinePicture.Values)
			{
				if (data.Group == key)
				{
					if (SpecialMagazinePicture.ContainsCode(data.Code))
						list.Add(data.Code, SpecialMagazinePicture[data.Code]);
				}
			}
			return new List<SpecialMagazinePicture>(list.Values);
		}

		private SheetData<StageInfo> _StageInfo;
		public SheetData<StageInfo> StageInfo { get { return _StageInfo; } }

		private SheetData<StageMagazineInfo> _StageMagazineInfo;
		public SheetData<StageMagazineInfo> StageMagazineInfo { get { return _StageMagazineInfo; } }

		private SheetData<StageMagazinePicture> _StageMagazinePicture;
		public SheetData<StageMagazinePicture> StageMagazinePicture { get { return _StageMagazinePicture; } }
		public List<StageMagazinePicture> GetStageMagazinePictureListByGroup(Int32 key)
		{
			var list = new Dictionary<Int32, StageMagazinePicture>();
			foreach (StageMagazinePicture data in StageMagazinePicture.Values)
			{
				if (data.Group == key)
				{
					if (StageMagazinePicture.ContainsCode(data.Code))
						list.Add(data.Code, StageMagazinePicture[data.Code]);
				}
			}
			return new List<StageMagazinePicture>(list.Values);
		}

		private SheetData<StagePurchaseInfo> _StagePurchaseInfo;
		public SheetData<StagePurchaseInfo> StagePurchaseInfo { get { return _StagePurchaseInfo; } }

		private SheetData<StageResource> _StageResource;
		public SheetData<StageResource> StageResource { get { return _StageResource; } }

		private SheetData<StageRewardInfo> _StageRewardInfo;
		public SheetData<StageRewardInfo> StageRewardInfo { get { return _StageRewardInfo; } }

		private SheetData<StoreLocale> _StoreLocale;
		public SheetData<StoreLocale> StoreLocale { get { return _StoreLocale; } }

		private SheetData<StoreResource> _StoreResource;
		public SheetData<StoreResource> StoreResource { get { return _StoreResource; } }

		private SheetData<SubscribeBuff> _SubscribeBuff;
		public SheetData<SubscribeBuff> SubscribeBuff { get { return _SubscribeBuff; } }

		private SheetData<SystemLocale> _SystemLocale;
		public SheetData<SystemLocale> SystemLocale { get { return _SystemLocale; } }

		private SheetData<SystemResource> _SystemResource;
		public SheetData<SystemResource> SystemResource { get { return _SystemResource; } }

		private SheetData<Target> _Target;
		public SheetData<Target> Target { get { return _Target; } }

		private SheetData<ThemeBonusSon> _ThemeBonusSon;
		public SheetData<ThemeBonusSon> ThemeBonusSon { get { return _ThemeBonusSon; } }
		public List<ThemeBonusSon> GetThemeBonusSonListByBundle(Int32 key)
		{
			var list = new Dictionary<Int32, ThemeBonusSon>();
			foreach (ThemeBonusSon data in ThemeBonusSon.Values)
			{
				if (data.Bundle == key)
				{
					if (ThemeBonusSon.ContainsCode(data.Code))
						list.Add(data.Code, ThemeBonusSon[data.Code]);
				}
			}
			return new List<ThemeBonusSon>(list.Values);
		}

		private SheetData<ThemeInfoSon> _ThemeInfoSon;
		public SheetData<ThemeInfoSon> ThemeInfoSon { get { return _ThemeInfoSon; } }

		private SheetData<ThemeLocaleSon> _ThemeLocaleSon;
		public SheetData<ThemeLocaleSon> ThemeLocaleSon { get { return _ThemeLocaleSon; } }

		private SheetData<TimeChargeItemInfo> _TimeChargeItemInfo;
		public SheetData<TimeChargeItemInfo> TimeChargeItemInfo { get { return _TimeChargeItemInfo; } }

		private SheetData<ToastMsgInfo> _ToastMsgInfo;
		public SheetData<ToastMsgInfo> ToastMsgInfo { get { return _ToastMsgInfo; } }

		private SheetData<TutorialCommon> _TutorialCommon;
		public SheetData<TutorialCommon> TutorialCommon { get { return _TutorialCommon; } }

		private SheetData<TutorialLocale> _TutorialLocale;
		public SheetData<TutorialLocale> TutorialLocale { get { return _TutorialLocale; } }

		private SheetData<TutorialResource> _TutorialResource;
		public SheetData<TutorialResource> TutorialResource { get { return _TutorialResource; } }

		private SheetData<TutorialStageInfo> _TutorialStageInfo;
		public SheetData<TutorialStageInfo> TutorialStageInfo { get { return _TutorialStageInfo; } }

		private SheetData<TutorialType> _TutorialType;
		public SheetData<TutorialType> TutorialType { get { return _TutorialType; } }

		private SheetData<UsableItemInfo> _UsableItemInfo;
		public SheetData<UsableItemInfo> UsableItemInfo { get { return _UsableItemInfo; } }

		private SheetData<VoiceResource> _VoiceResource;
		public SheetData<VoiceResource> VoiceResource { get { return _VoiceResource; } }

		private SheetData<WishPointInfoSon> _WishPointInfoSon;
		public SheetData<WishPointInfoSon> WishPointInfoSon { get { return _WishPointInfoSon; } }

		public void SetData(String name, byte[] data, bool isDecryptData = true)
		{
			string json = string.Empty;

			if (isDecryptData)
			{
				byte[] decrypted = SBCrypto.DecryptData(data);
				json = DecompressData(decrypted);
			}
			else
			{
				data = data.Reverse().ToArray();
				json = DecompressData(data);
			}
			
			switch (name) {
				case "ActiveItemInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<ActiveItemInfo>>(json);
					_ActiveItemInfo = new SheetData<ActiveItemInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "Advertisement": {
					var sheet = JsonUtility.FromJson<SheetInfo<Advertisement>>(json);
					_Advertisement = new SheetData<Advertisement>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ArtistInfoSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<ArtistInfoSon>>(json);
					_ArtistInfoSon = new SheetData<ArtistInfoSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ArtistLevelUpInfoSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<ArtistLevelUpInfoSon>>(json);
					_ArtistLevelUpInfoSon = new SheetData<ArtistLevelUpInfoSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ArtistLocaleSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<ArtistLocaleSon>>(json);
					_ArtistLocaleSon = new SheetData<ArtistLocaleSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ArtistResourceSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<ArtistResourceSon>>(json);
					_ArtistResourceSon = new SheetData<ArtistResourceSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ArtistVoice": {
					var sheet = JsonUtility.FromJson<SheetInfo<ArtistVoice>>(json);
					_ArtistVoice = new SheetData<ArtistVoice>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "BannerResource": {
					var sheet = JsonUtility.FromJson<SheetInfo<BannerResource>>(json);
					_BannerResource = new SheetData<BannerResource>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "BlockInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<BlockInfo>>(json);
					_BlockInfo = new SheetData<BlockInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "Buff": {
					var sheet = JsonUtility.FromJson<SheetInfo<Buff>>(json);
					_Buff = new SheetData<Buff>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardEnhanceItemInfoSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardEnhanceItemInfoSon>>(json);
					_CardEnhanceItemInfoSon = new SheetData<CardEnhanceItemInfoSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardExpSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardExpSon>>(json);
					_CardExpSon = new SheetData<CardExpSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardExtraResource": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardExtraResource>>(json);
					_CardExtraResource = new SheetData<CardExtraResource>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardFrameBundle": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardFrameBundle>>(json);
					_CardFrameBundle = new SheetData<CardFrameBundle>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardFrameBundleSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardFrameBundleSon>>(json);
					_CardFrameBundleSon = new SheetData<CardFrameBundleSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardFrameGacha": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardFrameGacha>>(json);
					_CardFrameGacha = new SheetData<CardFrameGacha>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardFrameGachaSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardFrameGachaSon>>(json);
					_CardFrameGachaSon = new SheetData<CardFrameGachaSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardFrameLocale": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardFrameLocale>>(json);
					_CardFrameLocale = new SheetData<CardFrameLocale>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardFramePackage": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardFramePackage>>(json);
					_CardFramePackage = new SheetData<CardFramePackage>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardFramePackageSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardFramePackageSon>>(json);
					_CardFramePackageSon = new SheetData<CardFramePackageSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardFrameParts": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardFrameParts>>(json);
					_CardFrameParts = new SheetData<CardFrameParts>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardFramePartsSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardFramePartsSon>>(json);
					_CardFramePartsSon = new SheetData<CardFramePartsSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardFrameResource": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardFrameResource>>(json);
					_CardFrameResource = new SheetData<CardFrameResource>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardFrameSet": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardFrameSet>>(json);
					_CardFrameSet = new SheetData<CardFrameSet>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardFrameSetSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardFrameSetSon>>(json);
					_CardFrameSetSon = new SheetData<CardFrameSetSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardGacha": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardGacha>>(json);
					_CardGacha = new SheetData<CardGacha>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardGachaEventSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardGachaEventSon>>(json);
					_CardGachaEventSon = new SheetData<CardGachaEventSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardGachaInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardGachaInfo>>(json);
					_CardGachaInfo = new SheetData<CardGachaInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardGachaLvUpPackageSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardGachaLvUpPackageSon>>(json);
					_CardGachaLvUpPackageSon = new SheetData<CardGachaLvUpPackageSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardGachaPackage": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardGachaPackage>>(json);
					_CardGachaPackage = new SheetData<CardGachaPackage>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardGachaPackageSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardGachaPackageSon>>(json);
					_CardGachaPackageSon = new SheetData<CardGachaPackageSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardGachaPrice": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardGachaPrice>>(json);
					_CardGachaPrice = new SheetData<CardGachaPrice>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardGachaSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardGachaSon>>(json);
					_CardGachaSon = new SheetData<CardGachaSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardGrade": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardGrade>>(json);
					_CardGrade = new SheetData<CardGrade>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardGradeReward": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardGradeReward>>(json);
					_CardGradeReward = new SheetData<CardGradeReward>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardGradeRewardSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardGradeRewardSon>>(json);
					_CardGradeRewardSon = new SheetData<CardGradeRewardSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardGradeSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardGradeSon>>(json);
					_CardGradeSon = new SheetData<CardGradeSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardInfo>>(json);
					_CardInfo = new SheetData<CardInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardInfoSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardInfoSon>>(json);
					_CardInfoSon = new SheetData<CardInfoSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardLvGradeSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardLvGradeSon>>(json);
					_CardLvGradeSon = new SheetData<CardLvGradeSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardLvUpItemInfoSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardLvUpItemInfoSon>>(json);
					_CardLvUpItemInfoSon = new SheetData<CardLvUpItemInfoSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardPartsInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardPartsInfo>>(json);
					_CardPartsInfo = new SheetData<CardPartsInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardPartsInfoSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardPartsInfoSon>>(json);
					_CardPartsInfoSon = new SheetData<CardPartsInfoSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardProfileLocale": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardProfileLocale>>(json);
					_CardProfileLocale = new SheetData<CardProfileLocale>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardResource": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardResource>>(json);
					_CardResource = new SheetData<CardResource>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardSkill": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardSkill>>(json);
					_CardSkill = new SheetData<CardSkill>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardSkillSet": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardSkillSet>>(json);
					_CardSkillSet = new SheetData<CardSkillSet>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardSkillSetSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardSkillSetSon>>(json);
					_CardSkillSetSon = new SheetData<CardSkillSetSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "CardVoiceBundle": {
					var sheet = JsonUtility.FromJson<SheetInfo<CardVoiceBundle>>(json);
					_CardVoiceBundle = new SheetData<CardVoiceBundle>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ChallengeStage": {
					var sheet = JsonUtility.FromJson<SheetInfo<ChallengeStage>>(json);
					_ChallengeStage = new SheetData<ChallengeStage>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "DailyBonusInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<DailyBonusInfo>>(json);
					_DailyBonusInfo = new SheetData<DailyBonusInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "DailyCumReward": {
					var sheet = JsonUtility.FromJson<SheetInfo<DailyCumReward>>(json);
					_DailyCumReward = new SheetData<DailyCumReward>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "DailyReward": {
					var sheet = JsonUtility.FromJson<SheetInfo<DailyReward>>(json);
					_DailyReward = new SheetData<DailyReward>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "DefaultItemInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<DefaultItemInfo>>(json);
					_DefaultItemInfo = new SheetData<DefaultItemInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "DiaEvent": {
					var sheet = JsonUtility.FromJson<SheetInfo<DiaEvent>>(json);
					_DiaEvent = new SheetData<DiaEvent>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "DiaStore": {
					var sheet = JsonUtility.FromJson<SheetInfo<DiaStore>>(json);
					_DiaStore = new SheetData<DiaStore>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "EventLocale": {
					var sheet = JsonUtility.FromJson<SheetInfo<EventLocale>>(json);
					_EventLocale = new SheetData<EventLocale>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "EventResource": {
					var sheet = JsonUtility.FromJson<SheetInfo<EventResource>>(json);
					_EventResource = new SheetData<EventResource>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ExtraLocale": {
					var sheet = JsonUtility.FromJson<SheetInfo<ExtraLocale>>(json);
					_ExtraLocale = new SheetData<ExtraLocale>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "FrameGachaPrice": {
					var sheet = JsonUtility.FromJson<SheetInfo<FrameGachaPrice>>(json);
					_FrameGachaPrice = new SheetData<FrameGachaPrice>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "GachaResource": {
					var sheet = JsonUtility.FromJson<SheetInfo<GachaResource>>(json);
					_GachaResource = new SheetData<GachaResource>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "GameConfig": {
					var sheet = JsonUtility.FromJson<SheetInfo<GameConfig>>(json);
					_GameConfig = new SheetData<GameConfig>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "GoldStore": {
					var sheet = JsonUtility.FromJson<SheetInfo<GoldStore>>(json);
					_GoldStore = new SheetData<GoldStore>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "IDCardInfoSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<IDCardInfoSon>>(json);
					_IDCardInfoSon = new SheetData<IDCardInfoSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "IDCardResourceSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<IDCardResourceSon>>(json);
					_IDCardResourceSon = new SheetData<IDCardResourceSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "IndicatorInfoSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<IndicatorInfoSon>>(json);
					_IndicatorInfoSon = new SheetData<IndicatorInfoSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ItemBox": {
					var sheet = JsonUtility.FromJson<SheetInfo<ItemBox>>(json);
					_ItemBox = new SheetData<ItemBox>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ItemLocale": {
					var sheet = JsonUtility.FromJson<SheetInfo<ItemLocale>>(json);
					_ItemLocale = new SheetData<ItemLocale>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ItemProduction": {
					var sheet = JsonUtility.FromJson<SheetInfo<ItemProduction>>(json);
					_ItemProduction = new SheetData<ItemProduction>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ItemRandomBox": {
					var sheet = JsonUtility.FromJson<SheetInfo<ItemRandomBox>>(json);
					_ItemRandomBox = new SheetData<ItemRandomBox>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ItemResource": {
					var sheet = JsonUtility.FromJson<SheetInfo<ItemResource>>(json);
					_ItemResource = new SheetData<ItemResource>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ItemType": {
					var sheet = JsonUtility.FromJson<SheetInfo<ItemType>>(json);
					_ItemType = new SheetData<ItemType>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "LoadingImageInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<LoadingImageInfo>>(json);
					_LoadingImageInfo = new SheetData<LoadingImageInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "LobbyBanner": {
					var sheet = JsonUtility.FromJson<SheetInfo<LobbyBanner>>(json);
					_LobbyBanner = new SheetData<LobbyBanner>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "LobbyIcon": {
					var sheet = JsonUtility.FromJson<SheetInfo<LobbyIcon>>(json);
					_LobbyIcon = new SheetData<LobbyIcon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "LobbyIconResource": {
					var sheet = JsonUtility.FromJson<SheetInfo<LobbyIconResource>>(json);
					_LobbyIconResource = new SheetData<LobbyIconResource>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "MagazineLocale": {
					var sheet = JsonUtility.FromJson<SheetInfo<MagazineLocale>>(json);
					_MagazineLocale = new SheetData<MagazineLocale>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "MagazineResource": {
					var sheet = JsonUtility.FromJson<SheetInfo<MagazineResource>>(json);
					_MagazineResource = new SheetData<MagazineResource>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "MembershipCumReward": {
					var sheet = JsonUtility.FromJson<SheetInfo<MembershipCumReward>>(json);
					_MembershipCumReward = new SheetData<MembershipCumReward>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "MembershipInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<MembershipInfo>>(json);
					_MembershipInfo = new SheetData<MembershipInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "MileageStore": {
					var sheet = JsonUtility.FromJson<SheetInfo<MileageStore>>(json);
					_MileageStore = new SheetData<MileageStore>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "MileageStoreInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<MileageStoreInfo>>(json);
					_MileageStoreInfo = new SheetData<MileageStoreInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "MissionBundle": {
					var sheet = JsonUtility.FromJson<SheetInfo<MissionBundle>>(json);
					_MissionBundle = new SheetData<MissionBundle>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "MissionCondition": {
					var sheet = JsonUtility.FromJson<SheetInfo<MissionCondition>>(json);
					_MissionCondition = new SheetData<MissionCondition>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "MissionConditionBundle": {
					var sheet = JsonUtility.FromJson<SheetInfo<MissionConditionBundle>>(json);
					_MissionConditionBundle = new SheetData<MissionConditionBundle>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "MissionInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<MissionInfo>>(json);
					_MissionInfo = new SheetData<MissionInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "MissionLocale": {
					var sheet = JsonUtility.FromJson<SheetInfo<MissionLocale>>(json);
					_MissionLocale = new SheetData<MissionLocale>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "MissionResource": {
					var sheet = JsonUtility.FromJson<SheetInfo<MissionResource>>(json);
					_MissionResource = new SheetData<MissionResource>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "MissionType": {
					var sheet = JsonUtility.FromJson<SheetInfo<MissionType>>(json);
					_MissionType = new SheetData<MissionType>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "PackageBundle": {
					var sheet = JsonUtility.FromJson<SheetInfo<PackageBundle>>(json);
					_PackageBundle = new SheetData<PackageBundle>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "PackagePopUp": {
					var sheet = JsonUtility.FromJson<SheetInfo<PackagePopUp>>(json);
					_PackagePopUp = new SheetData<PackagePopUp>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "PassInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<PassInfo>>(json);
					_PassInfo = new SheetData<PassInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "PassReward": {
					var sheet = JsonUtility.FromJson<SheetInfo<PassReward>>(json);
					_PassReward = new SheetData<PassReward>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "PopUpStore": {
					var sheet = JsonUtility.FromJson<SheetInfo<PopUpStore>>(json);
					_PopUpStore = new SheetData<PopUpStore>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "PopUpStoreType": {
					var sheet = JsonUtility.FromJson<SheetInfo<PopUpStoreType>>(json);
					_PopUpStoreType = new SheetData<PopUpStoreType>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "PopupDes": {
					var sheet = JsonUtility.FromJson<SheetInfo<PopupDes>>(json);
					_PopupDes = new SheetData<PopupDes>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "PopupInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<PopupInfo>>(json);
					_PopupInfo = new SheetData<PopupInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ProductLocale": {
					var sheet = JsonUtility.FromJson<SheetInfo<ProductLocale>>(json);
					_ProductLocale = new SheetData<ProductLocale>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "Profile": {
					var sheet = JsonUtility.FromJson<SheetInfo<Profile>>(json);
					_Profile = new SheetData<Profile>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ProfileResource": {
					var sheet = JsonUtility.FromJson<SheetInfo<ProfileResource>>(json);
					_ProfileResource = new SheetData<ProfileResource>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ProfileSelect": {
					var sheet = JsonUtility.FromJson<SheetInfo<ProfileSelect>>(json);
					_ProfileSelect = new SheetData<ProfileSelect>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ProfileSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<ProfileSon>>(json);
					_ProfileSon = new SheetData<ProfileSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RecommendStore": {
					var sheet = JsonUtility.FromJson<SheetInfo<RecommendStore>>(json);
					_RecommendStore = new SheetData<RecommendStore>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmAlbumCoverResource": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmAlbumCoverResource>>(json);
					_RhythmAlbumCoverResource = new SheetData<RhythmAlbumCoverResource>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmAlbumLocale": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmAlbumLocale>>(json);
					_RhythmAlbumLocale = new SheetData<RhythmAlbumLocale>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmAlbumTheme": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmAlbumTheme>>(json);
					_RhythmAlbumTheme = new SheetData<RhythmAlbumTheme>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmBackgroundBundle": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmBackgroundBundle>>(json);
					_RhythmBackgroundBundle = new SheetData<RhythmBackgroundBundle>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmBackgroundType": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmBackgroundType>>(json);
					_RhythmBackgroundType = new SheetData<RhythmBackgroundType>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmCardDeckCnt": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmCardDeckCnt>>(json);
					_RhythmCardDeckCnt = new SheetData<RhythmCardDeckCnt>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmComboScore": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmComboScore>>(json);
					_RhythmComboScore = new SheetData<RhythmComboScore>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmHPInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmHPInfo>>(json);
					_RhythmHPInfo = new SheetData<RhythmHPInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmHit": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmHit>>(json);
					_RhythmHit = new SheetData<RhythmHit>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmLevelType": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmLevelType>>(json);
					_RhythmLevelType = new SheetData<RhythmLevelType>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmNoteHitInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmNoteHitInfo>>(json);
					_RhythmNoteHitInfo = new SheetData<RhythmNoteHitInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmNoteInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmNoteInfo>>(json);
					_RhythmNoteInfo = new SheetData<RhythmNoteInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmNoteSpeedInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmNoteSpeedInfo>>(json);
					_RhythmNoteSpeedInfo = new SheetData<RhythmNoteSpeedInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmPlayBackgoundResource": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmPlayBackgoundResource>>(json);
					_RhythmPlayBackgoundResource = new SheetData<RhythmPlayBackgoundResource>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmPlayPreviewMusic": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmPlayPreviewMusic>>(json);
					_RhythmPlayPreviewMusic = new SheetData<RhythmPlayPreviewMusic>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmRankingInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmRankingInfo>>(json);
					_RhythmRankingInfo = new SheetData<RhythmRankingInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmRankingReward": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmRankingReward>>(json);
					_RhythmRankingReward = new SheetData<RhythmRankingReward>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmScoreRank": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmScoreRank>>(json);
					_RhythmScoreRank = new SheetData<RhythmScoreRank>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmSongEvent": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmSongEvent>>(json);
					_RhythmSongEvent = new SheetData<RhythmSongEvent>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmSongList": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmSongList>>(json);
					_RhythmSongList = new SheetData<RhythmSongList>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmSongLocale": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmSongLocale>>(json);
					_RhythmSongLocale = new SheetData<RhythmSongLocale>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "RhythmUseItemInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<RhythmUseItemInfo>>(json);
					_RhythmUseItemInfo = new SheetData<RhythmUseItemInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ScoreBuffCardSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<ScoreBuffCardSon>>(json);
					_ScoreBuffCardSon = new SheetData<ScoreBuffCardSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ScoreModeFaleImageSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<ScoreModeFaleImageSon>>(json);
					_ScoreModeFaleImageSon = new SheetData<ScoreModeFaleImageSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ScoreModeInfoSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<ScoreModeInfoSon>>(json);
					_ScoreModeInfoSon = new SheetData<ScoreModeInfoSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ScoreModeStageSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<ScoreModeStageSon>>(json);
					_ScoreModeStageSon = new SheetData<ScoreModeStageSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ScorePtimeInfoSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<ScorePtimeInfoSon>>(json);
					_ScorePtimeInfoSon = new SheetData<ScorePtimeInfoSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ScoreRewardSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<ScoreRewardSon>>(json);
					_ScoreRewardSon = new SheetData<ScoreRewardSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "SlotItem": {
					var sheet = JsonUtility.FromJson<SheetInfo<SlotItem>>(json);
					_SlotItem = new SheetData<SlotItem>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "SpecialID": {
					var sheet = JsonUtility.FromJson<SheetInfo<SpecialID>>(json);
					_SpecialID = new SheetData<SpecialID>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "SpecialIDLocale": {
					var sheet = JsonUtility.FromJson<SheetInfo<SpecialIDLocale>>(json);
					_SpecialIDLocale = new SheetData<SpecialIDLocale>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "SpecialIDResource": {
					var sheet = JsonUtility.FromJson<SheetInfo<SpecialIDResource>>(json);
					_SpecialIDResource = new SheetData<SpecialIDResource>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "SpecialMagazineInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<SpecialMagazineInfo>>(json);
					_SpecialMagazineInfo = new SheetData<SpecialMagazineInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "SpecialMagazinePicture": {
					var sheet = JsonUtility.FromJson<SheetInfo<SpecialMagazinePicture>>(json);
					_SpecialMagazinePicture = new SheetData<SpecialMagazinePicture>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "StageInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<StageInfo>>(json);
					_StageInfo = new SheetData<StageInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "StageMagazineInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<StageMagazineInfo>>(json);
					_StageMagazineInfo = new SheetData<StageMagazineInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "StageMagazinePicture": {
					var sheet = JsonUtility.FromJson<SheetInfo<StageMagazinePicture>>(json);
					_StageMagazinePicture = new SheetData<StageMagazinePicture>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "StagePurchaseInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<StagePurchaseInfo>>(json);
					_StagePurchaseInfo = new SheetData<StagePurchaseInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "StageResource": {
					var sheet = JsonUtility.FromJson<SheetInfo<StageResource>>(json);
					_StageResource = new SheetData<StageResource>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "StageRewardInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<StageRewardInfo>>(json);
					_StageRewardInfo = new SheetData<StageRewardInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "StoreLocale": {
					var sheet = JsonUtility.FromJson<SheetInfo<StoreLocale>>(json);
					_StoreLocale = new SheetData<StoreLocale>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "StoreResource": {
					var sheet = JsonUtility.FromJson<SheetInfo<StoreResource>>(json);
					_StoreResource = new SheetData<StoreResource>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "SubscribeBuff": {
					var sheet = JsonUtility.FromJson<SheetInfo<SubscribeBuff>>(json);
					_SubscribeBuff = new SheetData<SubscribeBuff>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "SystemLocale": {
					var sheet = JsonUtility.FromJson<SheetInfo<SystemLocale>>(json);
					_SystemLocale = new SheetData<SystemLocale>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "SystemResource": {
					var sheet = JsonUtility.FromJson<SheetInfo<SystemResource>>(json);
					_SystemResource = new SheetData<SystemResource>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "Target": {
					var sheet = JsonUtility.FromJson<SheetInfo<Target>>(json);
					_Target = new SheetData<Target>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ThemeBonusSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<ThemeBonusSon>>(json);
					_ThemeBonusSon = new SheetData<ThemeBonusSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ThemeInfoSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<ThemeInfoSon>>(json);
					_ThemeInfoSon = new SheetData<ThemeInfoSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ThemeLocaleSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<ThemeLocaleSon>>(json);
					_ThemeLocaleSon = new SheetData<ThemeLocaleSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "TimeChargeItemInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<TimeChargeItemInfo>>(json);
					_TimeChargeItemInfo = new SheetData<TimeChargeItemInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "ToastMsgInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<ToastMsgInfo>>(json);
					_ToastMsgInfo = new SheetData<ToastMsgInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "TutorialCommon": {
					var sheet = JsonUtility.FromJson<SheetInfo<TutorialCommon>>(json);
					_TutorialCommon = new SheetData<TutorialCommon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "TutorialLocale": {
					var sheet = JsonUtility.FromJson<SheetInfo<TutorialLocale>>(json);
					_TutorialLocale = new SheetData<TutorialLocale>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "TutorialResource": {
					var sheet = JsonUtility.FromJson<SheetInfo<TutorialResource>>(json);
					_TutorialResource = new SheetData<TutorialResource>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "TutorialStageInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<TutorialStageInfo>>(json);
					_TutorialStageInfo = new SheetData<TutorialStageInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "TutorialType": {
					var sheet = JsonUtility.FromJson<SheetInfo<TutorialType>>(json);
					_TutorialType = new SheetData<TutorialType>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "UsableItemInfo": {
					var sheet = JsonUtility.FromJson<SheetInfo<UsableItemInfo>>(json);
					_UsableItemInfo = new SheetData<UsableItemInfo>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "VoiceResource": {
					var sheet = JsonUtility.FromJson<SheetInfo<VoiceResource>>(json);
					_VoiceResource = new SheetData<VoiceResource>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
				case "WishPointInfoSon": {
					var sheet = JsonUtility.FromJson<SheetInfo<WishPointInfoSon>>(json);
					_WishPointInfoSon = new SheetData<WishPointInfoSon>(sheet.rows.ToDictionary(m => m.Code));
					break;
				}
			}
		}

		public string DecompressData(byte[] compressed)
		{
			MemoryStream inputMS = new MemoryStream(compressed);
			MemoryStream outputMS = new MemoryStream();
			var gzipIS = new GZipInputStream(inputMS);
			gzipIS.CopyTo(outputMS);
			byte[] outputBytes = outputMS.ToArray();
			return Encoding.UTF8.GetString(outputBytes);
		}
	}
}
