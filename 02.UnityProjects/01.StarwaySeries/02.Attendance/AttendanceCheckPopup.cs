using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using com.snowballs.SWHJ.client.model;
using com.snowballs.SWHJ.client.view;
using com.snowballs.SWHJ.Ext.Event;
using com.snowballs.SWHJ.presenter;
using com.snowballs.SWHJ.type;
using Snowballs.Network.API;
using Snowballs.Network.Dto;
using Snowballs.Sheets;
using Snowballs.Sheets.Data;
using Snowballs.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = System.Random;

public class AttendanceCheckPopup : Popup
{
    [SerializeField] private AttendanceItem[] gridItems;
    [SerializeField] private TextMeshProUGUI remainingTime;
    [SerializeField] private Image background;
    [SerializeField] private AccumulateRewardItemView[] accumulateRewardItemViews;

    [SerializeField] private TextMeshProUGUI totalRewardReceivedCount;
    [SerializeField] private TextMeshProUGUI bottomHeaderText, bottomContext;

    [SerializeField] private AttendanceRewardDescriptionModal descriptionModal;
    [SerializeField] private GameObject rewardPopupDimmedObj;
    //현재 팝업 상태
    private STATE _currentState = STATE.None;
    public STATE CurrentState => this._currentState;

    private int eventCode;

    private DailyDto dailyDto;
    private List<DailyReward> dailyRewards;
    private List<DailyCumReward> dailyCumRewards;
    
    private int totalAccumulateCount;
    private int todayReceivedReward;

    public int lobbyIconCode;
    
    public override void OnOpen()
    {
        base.OnOpen();
        this.LoadingData();
        
        Input.multiTouchEnabled = false;
    }

    public new class Params : Popup.Params
    {
        public int lobbyIconCode;
        public int eventCode;

        public DailyDto dailyDto;
        public DailyBonusInfo dailyBonusInfoData;               //DailyBonusInfo 시트 데이터
        public List<DailyReward> dailyRewardData;               //DailyReward 시트 데이터
        public List<DailyCumReward> dailyCumRewardData;         //DailyCumReward 시트 데이터

        public bool isTimerExist = true;
        
        public Int32? notReceiveRewardCommentLocale;
        public Int32? alreadyReceiveRewardCommentLocale;
        public Int32? bottomContextLocale;
        public Int32? bottomHeaderLocale;
    }
    
    private void LoadingData()
    {
        this.SetState(STATE.LoadingData);
    }

    //Note. 하루가 지나면 9칸 모두 초기화 된다. (다시 9개 중 무작위 1개를 선택하게 됨)
    private void Init()
    {
        Params pB = this.paramBuffer as Params;
        this.eventCode = pB.eventCode;
        this.dailyDto = pB.dailyDto;
        this.lobbyIconCode = pB.lobbyIconCode;
        this.dailyRewards = pB.dailyRewardData;
        this.dailyCumRewards = pB.dailyCumRewardData;
        
        SBDebug.Log(string.Format("DailyEvent Popup opened. code : {0}", this.eventCode));
        SBDebug.Log(string.Format("DailyEvent Popup opened. beginAt : {0}", pB.dailyBonusInfoData.DT_StartAt));
        SBDebug.Log(string.Format("DailyEvent Popup opened. endAt : {0}", pB.dailyBonusInfoData.DT_EndAt));
        
        this.SetWeightDict(pB.dailyRewardData);
        
        DailyDto dailyDto = pB.dailyDto;
        
        int[] receivedList = dailyDto.receivedCumRewards;
        this.totalAccumulateCount = receivedList.Length;
        
        int bgIndex = pB.dailyBonusInfoData.BGRes;
        int cardFrontImageIndex = pB.dailyBonusInfoData.CardFrontRes;
        int cardBackImageIndex = pB.dailyBonusInfoData.CardBackRes;
        
        string bgLocaleAddress = string.Empty;
        string cardFrontImageLocaleAddress = string.Empty;
        string cardBackImageLocaleAddress = string.Empty;
        
        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
        if (playerStorage.Locale == "ko")
        {
            bgLocaleAddress = SBDataSheet.Instance.EventResource[bgIndex].KoKRAddress;
            cardFrontImageLocaleAddress = SBDataSheet.Instance.EventResource[cardFrontImageIndex].KoKRAddress;
            cardBackImageLocaleAddress = SBDataSheet.Instance.EventResource[cardBackImageIndex].KoKRAddress;
        }
        else if (playerStorage.Locale == "en")
        {
            bgLocaleAddress = SBDataSheet.Instance.EventResource[bgIndex].EnUSAddress;
            cardFrontImageLocaleAddress = SBDataSheet.Instance.EventResource[cardFrontImageIndex].EnUSAddress;
            cardBackImageLocaleAddress = SBDataSheet.Instance.EventResource[cardBackImageIndex].EnUSAddress;
        }
        else
        {
            bgLocaleAddress = SBDataSheet.Instance.EventResource[bgIndex].KoKRAddress;
            cardFrontImageLocaleAddress = SBDataSheet.Instance.EventResource[cardFrontImageIndex].KoKRAddress;
            cardBackImageLocaleAddress = SBDataSheet.Instance.EventResource[cardBackImageIndex].KoKRAddress;
        }

        this.InitBackground(bgLocaleAddress);
        this.InitAccumulateRewardImage(pB.dailyCumRewardData, playerStorage.Locale);

        //Note. Server에서 하루가 지나면 todayReceivedPos 값, todayReceivedReward 값이 초기화 될 것이다.
        int receivedPos = dailyDto.todayReceivedPos;
        this.todayReceivedReward = dailyDto.todayReceivedReward;
        
        var signal = new CancellableSignal(() => { return this == null; });

        if(pB.isTimerExist)
            CoroutineTaskManager.AddTask(RemainingTimeTask(signal, pB.dailyBonusInfoData.DT_EndAt));
        else
        {
            this.remainingTime.text = string.Empty;
            CoroutineTaskManager.AddTask(RemainingTimeTask(signal));
        }
        
        this.SetLocales();
        
        this.SetState(STATE.LoadingDataDone);

        string cardFrontImagePath = AssetPathController.PATH_FOLDER_ASSETS + cardFrontImageLocaleAddress;
        string cardBackImagePath = AssetPathController.PATH_FOLDER_ASSETS + cardBackImageLocaleAddress;
        
        for (int i = 0; i < this.gridItems.Length; i++)
        {
            AttendanceItem attendanceItem = this.gridItems[i];
            var copiedIndex = i + 1;
            CoroutineTaskManager.AddTask(
                DownloadCardImages(
                    (cardFrontTexture, cardBackTexture) =>
                    {
                        AttendanceItem.Param itemParam = new AttendanceItem.Param();
                        itemParam.frontCardImageTexture = cardFrontTexture;
                        itemParam.backCardImageTexture = cardBackTexture;
                        itemParam.itemIndex = copiedIndex;

                        if (copiedIndex == receivedPos)
                        {
                            itemParam.isOpened = true;
                            
                            if(this.todayReceivedReward != 0)
                                itemParam.todayReceivedReward = this.todayReceivedReward;
                        }
                        else
                        {
                            itemParam.isOpened = false;
                        }
                        
                        attendanceItem.Init(itemParam);
                    }, cardFrontImagePath, cardBackImagePath
                )
            );
        }
        
        //오늘 이미 보상을 받은 상태인 경우 하루가 지나기 전까지는 보상 요청을 할 수 없음
        if (todayReceivedReward != 0)
        {
            foreach (AttendanceItem attendanceItem in this.gridItems)
            {
                attendanceItem.canOpen = false;
            }
        }
        else
        {
            foreach (AttendanceItem attendanceItem in this.gridItems)
            {
                attendanceItem.canOpen = true;
            }
        }
        
        // RectTransform closeBtnRect = this.closeBtn.GetComponent<RectTransform>();
        // float posY = closeBtnRect.localPosition.y;
        // closeBtnRect.localPosition = new Vector3(470f, 760f);

        // this.OpenDummyAccumReceivedPopup();
        // this.OnDayChanged();

        IEnumerator DownloadCardImages(Action<Texture2D, Texture2D> cb, string cardFrontPath, string cardBackPath)
        {
            bool isFinished = false;

            Texture2D cardFrontTexture = null;
            Texture2D cardBackTexture = null;
            
            WWWFile.DownloadPath cardFrontDownloadPath = new WWWFile.DownloadPath(
                WWWFile.TYPE.Bytes,
                String.Empty, 
                cardFrontPath
            );
            
            TextureController.GetTexture(cardFrontDownloadPath, false, (isSuccess, texture) =>
            {
                cardFrontTexture = texture;
                isFinished = true;
            });

            yield return new WaitUntil(() => isFinished);

            isFinished = false;

            WWWFile.DownloadPath cardBackDownloadPath = new WWWFile.DownloadPath(
                    WWWFile.TYPE.Bytes, 
                    String.Empty, 
                    cardBackPath
            );
            
            TextureController.GetTexture(cardBackDownloadPath, false, (isSuccess, texture) =>
            {
                cardBackTexture = texture;
                isFinished = true;
            });
            
            yield return new WaitUntil(() => isFinished);
            
            cb?.Invoke(cardFrontTexture, cardBackTexture);
        }
    }

    // private void Update()
    // {
    //     if (Input.GetKeyDown(KeyCode.L))
    //     {
    //         SBDebug.Log("Test Day Changed");
    //             
    //         EventStorage eventStorage = GameStorage.Instance.GetStorage<EventStorage>();
    //         var dailyDto = eventStorage.GetDailyEventData(this.eventCode).dailyDto;
    //         dailyDto.todayReceivedPos = 0;
    //         dailyDto.todayReceivedReward = 0;
    //             
    //         PopupRoot.Instance.CloseForcedAllPopup(this.gameObject, this);
    //         this.rewardPopupDimmedObj.SetActive(false);
    //             
    //         this.OnDayChanged();
    //     }
    // }

    private Func<DateTime> timeFunc = () => { return SBTime.Instance.ServerTime; };

    IEnumerator RemainingTimeTask(CancellableSignal signal, DateTime endTime)
    {
        var wfef = new WaitForEndOfFrame();

        DateTime now;
        DateTime beginTime = this.timeFunc();
        TimeSpan remainingTime;
        do
        {
            now = this.timeFunc();

            //날짜 변경됨 (팝업 다시 오픈)
            if (!string.IsNullOrEmpty(GameStorage.DailyDtoUpdatedTime.ToString()))
            {
                if (GameStorage.DailyDtoUpdatedTime.Day < now.Day)
                {
                    SBDebug.Log("Day Changed");
                
                    EventStorage eventStorage = GameStorage.Instance.GetStorage<EventStorage>();
                    var dailyDto = eventStorage.GetDailyEventData(this.eventCode).dailyDto;
                    dailyDto.todayReceivedPos = 0;
                    dailyDto.todayReceivedReward = 0;
                             
                    this.OnDayChanged();
                    yield break;
                }
            }
            
            if (now <= endTime)
            {
                remainingTime = endTime - now;
                this.UpdateSeasonRemainingTime(remainingTime);
            }
            else
            {
                //이벤트 시간 만료
                this.OnEndTime();
            }
            yield return wfef;
            if (CancellableSignal.IsCanceled(signal)) { yield break; }
        }
        while (now <= endTime);
    }

    IEnumerator RemainingTimeTask(CancellableSignal signal)
    {
        var wfef = new WaitForEndOfFrame();

        DateTime now;
        DateTime beginTime = this.timeFunc();
        TimeSpan remainingTime;
        
        do
        {
            now = this.timeFunc();

            //날짜 변경됨 (팝업 다시 오픈)
            if (GameStorage.DailyDtoUpdatedTime.Day < now.Day)
            {
                SBDebug.Log("Day Changed");
                
                EventStorage eventStorage = GameStorage.Instance.GetStorage<EventStorage>();
                var dailyDto = eventStorage.GetDailyEventData(this.eventCode).dailyDto;
                dailyDto.todayReceivedPos = 0;
                dailyDto.todayReceivedReward = 0;

                Params pB = this.paramBuffer as Params;
                bool repeatBool = pB.dailyBonusInfoData.RepeatBool;
                if (repeatBool)
                {
                    //최대 누적 보상 수령 이후 다시 0으로 초기화 시킨다.
                    if (dailyDto.receivedCumRewards.Length == this.dailyCumRewards.Count)
                    {
                        dailyDto.receivedCumRewards = new int[0];

                        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
                        EventLocale countLocale = SBDataSheet.Instance.EventLocale[9999];
                        if (countLocale != null)
                        {
                            if (playerStorage.Locale == "ko")
                            {
                                this.totalRewardReceivedCount.text = 0 + countLocale.KoKR;
                            }
                            else
                            {
                                this.totalRewardReceivedCount.text = 0 + countLocale.EnUS;
                            }
                        }
                        else
                            this.totalRewardReceivedCount.text = 0 + "";
                    }
                }

                this.OnDayChanged();
                
                yield break;
            }
            
            yield return wfef;
            if (CancellableSignal.IsCanceled(signal)) { yield break; }
        }
        while (true);
        
    }

    private void OnDayChanged()
    {
        BroadcastTunnel<string, int>.Notify("com.snowballs.SWHJ.DayChanged", 0);
        StopAllCoroutines();
        
        ConfirmPopup.Params popup = new ConfirmPopup.Params();
        
        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
        if (playerStorage.Locale == "ko")
        {
            popup.dummyHeaderText = SBDataSheet.Instance.EventLocale[9993] != null
                ? SBDataSheet.Instance.EventLocale[9993].KoKR
                : "안내";

            popup.dummyContext = SBDataSheet.Instance.EventLocale[9994] != null
                ? SBDataSheet.Instance.EventLocale[9994].KoKR
                : "밤 12시가 지나 화면이 새로고침 됩니다.";
        }
        else
        {
            popup.dummyHeaderText = SBDataSheet.Instance.EventLocale[9993] != null
                ? SBDataSheet.Instance.EventLocale[9993].EnUS
                : "Notice";
            
            popup.dummyContext = SBDataSheet.Instance.EventLocale[9994] != null
                ? SBDataSheet.Instance.EventLocale[9994].EnUS
                : "Midnight KST has passed. Screen is being refreshed.";
        }

        popup.isCloseBtnNeed = true;
        
        Popup.Load("System/ConfirmPopup", popup, (pop, result) =>
        {
            base.result.args = true;
            base.OnTriggerX();
        });
        
        this.rewardPopupDimmedObj.SetActive(false);
    }

    public override void OnTriggerX()
    {
        base.OnTriggerX();

        Input.multiTouchEnabled = true;
    }

    private void OnEndTime()
    {
        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
        ConfirmPopup.Params popup = new ConfirmPopup.Params();

        if (playerStorage.Locale == "ko")
        {
            popup.dummyHeaderText = SBDataSheet.Instance.EventLocale[9993] != null
                ? SBDataSheet.Instance.EventLocale[9993].KoKR
                : "안내";
            
            popup.dummyContext = SBDataSheet.Instance.EventLocale[9995] != null
                ? SBDataSheet.Instance.EventLocale[9995].KoKR
                : "이벤트 기간이 종료되었습니다.";
        }
        else
        {
            popup.dummyHeaderText = SBDataSheet.Instance.EventLocale[9993] != null
                ? SBDataSheet.Instance.EventLocale[9993].EnUS
                : "안내";
            
            popup.dummyContext = SBDataSheet.Instance.EventLocale[9995] != null
                ? SBDataSheet.Instance.EventLocale[9995].EnUS
                : "Event period has ended.";
        }
        popup.isCloseBtnNeed = true;
        
        Popup.Load("System/ConfirmPopup", popup, (pop, result) =>
        {
            base.OnTriggerX();
        });
    }

    //보상 획득시 다른 슬롯 모두 잠금
    private void LockAllItemViews()
    {
        for (int i = 0; i < this.gridItems.Length; i++)
        {
            AttendanceItem attendanceItem = this.gridItems[i];
            attendanceItem.canOpen = false;
        }
    }

    private void UpdateSeasonRemainingTime(TimeSpan time)
    {
        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
        string playerLocale = playerStorage.Locale;
        
        if (time.Days >= 1)
        {
            int targetLocaleCode = 301;
            string localeStr = string.Empty;
            if (playerLocale == "ko")
            {
                localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].KoKR;
            }
            else
            {
                localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].EnUS;
            }
            
            localeStr = localeStr.Replace("{0}", time.Days.ToString());
            localeStr = localeStr.Replace("{1}", time.Hours.ToString());
            this.remainingTime.text = localeStr;
        }
        else if (time.Hours >= 1)
        {
            int targetLocaleCode = 302;
            string localeStr = string.Empty;
            if (playerLocale == "ko")
            {
                localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].KoKR;
            }
            else
            {
                localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].EnUS;
            }
            
            localeStr = localeStr.Replace("{0}", time.Hours.ToString());
            localeStr = localeStr.Replace("{1}", time.Minutes.ToString());
            this.remainingTime.text = localeStr;
        }
        else
        {
            if (time.Minutes >= 1)
            {
                int targetLocaleCode = 304;
                string localeStr = string.Empty;
                if (playerLocale == "ko")
                {
                    localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].KoKR;
                }
                else
                {
                    localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].EnUS;
                }
                
                localeStr = localeStr.Replace("{0}", time.Minutes.ToString());
                localeStr = localeStr.Replace("{1}", time.Seconds.ToString());
                this.remainingTime.text = localeStr;
            }
            else
            {
                int targetLocaleCode = 305;
                string localeStr = string.Empty;
                if (playerLocale == "ko")
                {
                    localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].KoKR;
                }
                else
                {
                    localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].EnUS;
                }
                
                localeStr = localeStr.Replace("{0}", time.Seconds.ToString());
                this.remainingTime.text = localeStr;
            }
        }
    }

    private void SetLocales()
    {
        Params pB = this.paramBuffer as Params;
        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
        
        if (pB.notReceiveRewardCommentLocale.HasValue)
        {
            if (playerStorage.Locale == "ko")
            {
                if (this.todayReceivedReward == 0) this.headerText.text = SBDataSheet.Instance.EventLocale[pB.notReceiveRewardCommentLocale.Value].KoKR;
                else
                {
                    if (pB.alreadyReceiveRewardCommentLocale.HasValue)
                    {
                        this.headerText.text = SBDataSheet.Instance
                            .EventLocale[pB.alreadyReceiveRewardCommentLocale.Value].KoKR;
                    }
                    else
                    {
                        this.headerText.text = "금일 보상을 이미 획득하였습니다.";
                    }
                }
            }
            else if (playerStorage.Locale == "en")
            {
                if(this.todayReceivedReward == 0) this.headerText.text = SBDataSheet.Instance.EventLocale[pB.notReceiveRewardCommentLocale.Value].EnUS;
                else
                {
                    if (pB.alreadyReceiveRewardCommentLocale.HasValue)
                    {
                        this.headerText.text = SBDataSheet.Instance
                            .EventLocale[pB.alreadyReceiveRewardCommentLocale.Value].EnUS;
                    }
                    else
                    {
                        this.headerText.text = "금일 보상을 이미 획득하였습니다.";
                    }
                }
            }
            else if(playerStorage.Locale=="jp")
            {
                if(this.todayReceivedReward == 0)
                    this.headerText.text = SBDataSheet.Instance.EventLocale[pB.notReceiveRewardCommentLocale.Value].JaJP;
                else
                {
                    if (pB.alreadyReceiveRewardCommentLocale.HasValue)
                    {
                        this.headerText.text = SBDataSheet.Instance
                            .EventLocale[pB.alreadyReceiveRewardCommentLocale.Value].JaJP;
                    }
                    else
                    {
                        this.headerText.text = "금일 보상을 이미 획득하였습니다.";
                    }
                }
            }
            else
            {
                if (this.todayReceivedReward == 0)
                    this.headerText.text = SBDataSheet.Instance.EventLocale[pB.notReceiveRewardCommentLocale.Value].ThTH;
                else
                {
                    if (pB.alreadyReceiveRewardCommentLocale.HasValue)
                    {
                        this.headerText.text = SBDataSheet.Instance
                            .EventLocale[pB.alreadyReceiveRewardCommentLocale.Value].ThTH;
                    }
                    else
                    {
                        this.headerText.text = "금일 보상을 이미 획득하였습니다.";
                    }
                }
            }
        }
        
        if (pB.bottomHeaderLocale.HasValue)
        {
            if (playerStorage.Locale == "ko")
            {
                this.bottomHeaderText.text = SBDataSheet.Instance.EventLocale[pB.bottomHeaderLocale.Value].KoKR;
            }
            else if (playerStorage.Locale == "en")
            {
                this.bottomHeaderText.text = SBDataSheet.Instance.EventLocale[pB.bottomHeaderLocale.Value].EnUS;
            }
            else if(playerStorage.Locale=="jp")
            {
                this.bottomHeaderText.text = SBDataSheet.Instance.EventLocale[pB.bottomHeaderLocale.Value].JaJP;
            }
            else
            {
                this.bottomHeaderText.text = SBDataSheet.Instance.EventLocale[pB.bottomHeaderLocale.Value].ThTH;
            }
        }

        if (pB.bottomContextLocale.HasValue)
        {
            this.bottomContext.text = this.GetSystemLocale(pB.bottomContextLocale.Value);
        }
        
        EventLocale countLocale = SBDataSheet.Instance.EventLocale[9999];
        if (countLocale != null)
        {
            if (playerStorage.Locale == "ko")
            {
                this.totalRewardReceivedCount.text = pB.dailyDto.count + countLocale.KoKR;
            }
            else if(playerStorage.Locale == "en")
            {
                this.totalRewardReceivedCount.text = pB.dailyDto.count + countLocale.EnUS;
            }
            else if(playerStorage.Locale == "jp")
            {
                this.totalRewardReceivedCount.text = pB.dailyDto.count + countLocale.JaJP;
            }
            else
            {
                this.totalRewardReceivedCount.text = pB.dailyDto.count + countLocale.ThTH;
            }
        }
        else
            this.totalRewardReceivedCount.text = pB.dailyDto.count.ToString();
    }

    private void InitBackground(string localeAddress)
    {
        string filePath = AssetPathController.PATH_FOLDER_ASSETS + localeAddress;
        if (!File.Exists(filePath)) return;

        Texture2D texture = new Texture2D(1, 1);
        texture.LoadImage(File.ReadAllBytes(filePath));
        this.background.sprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), 100.0f);
        this.background.SetNativeSize();
    }

    private void InitAccumulateRewardImage(List<DailyCumReward> cumRewards, string playerLocale)
    {
        //순서대로 들어온다는 전제로..
        for (int i = 0; i < cumRewards.Count; i++)
        {
            DailyCumReward dailyReward = cumRewards[i];

            AccumulateRewardItemView.Params rewardItemViewParams = new AccumulateRewardItemView.Params();

            int rewardBoxResCode = dailyReward.RewardBoxRes;
            int rewardBoxOpenResCode = dailyReward.RewardBoxOpenRes;
            
            string rewardBoxPath = String.Empty;
            string rewardBoxOpenPath = String.Empty;
            
            if (playerLocale == "ko")
            {
                rewardBoxPath = AssetPathController.PATH_FOLDER_ASSETS + 
                                SBDataSheet.Instance.EventResource[rewardBoxResCode].KoKRAddress;
                rewardBoxOpenPath = AssetPathController.PATH_FOLDER_ASSETS + 
                                    SBDataSheet.Instance.EventResource[rewardBoxOpenResCode].KoKRAddress;
            }
            else if (playerLocale == "en")
            {
                rewardBoxPath = AssetPathController.PATH_FOLDER_ASSETS + 
                                SBDataSheet.Instance.EventResource[rewardBoxResCode].EnUSAddress;
                rewardBoxOpenPath = AssetPathController.PATH_FOLDER_ASSETS + 
                                    SBDataSheet.Instance.EventResource[rewardBoxOpenResCode].EnUSAddress;
            }
            else
            {
                rewardBoxPath = AssetPathController.PATH_FOLDER_ASSETS + 
                                SBDataSheet.Instance.EventResource[rewardBoxResCode].KoKRAddress;
                rewardBoxOpenPath = AssetPathController.PATH_FOLDER_ASSETS + 
                                    SBDataSheet.Instance.EventResource[rewardBoxOpenResCode].KoKRAddress;
            }

            if (File.Exists(rewardBoxPath))
            {
                var tex = new Texture2D(1, 1);
                tex.LoadImage(File.ReadAllBytes(rewardBoxPath));

                rewardItemViewParams.defaultImageTexture = tex;
            }

            if (File.Exists(rewardBoxOpenPath))
            {
                var tex = new Texture2D(1, 1);
                tex.LoadImage(File.ReadAllBytes(rewardBoxOpenPath));

                rewardItemViewParams.openImageTexture = tex;
            }
            rewardItemViewParams.number = dailyReward.DailyCumCount;
            rewardItemViewParams.playerLocale = playerLocale;
            rewardItemViewParams.isReceived = this.dailyCumRewards[i].DailyCumCount <= this.dailyDto.count;
            rewardItemViewParams.dailyCumReward = dailyReward;
            rewardItemViewParams.descriptionModal = this.descriptionModal;
            
            this.accumulateRewardItemViews[i].Refresh(rewardItemViewParams);
        }
    }

    private Dictionary<int, int> _weightDict = null;
    private void SetWeightDict(List<DailyReward> list)
    {
        _weightDict = new Dictionary<int, int>();

        foreach (DailyReward dailyReward in list)
        {
            _weightDict.Add(dailyReward.Code, dailyReward.Probability);
        }
    }

    private bool isRewardTask = false;
    /// <summary>
    /// 보상 요청을 위한 클릭
    /// </summary>
    public void OnClickReward(AttendanceItem attendanceItem)
    {
        //이미 다른 보상을 요청중이면 차단
        if(this.isRewardTask) return;
        if(!attendanceItem.CanClick()) return;
        
        this.isRewardTask = true;

        this.LockAllItemViews();
        
        attendanceItem.OnClick(this, (slotIndex, acquiredItemDto) =>
        {
            EventStorage eventStorage = GameStorage.Instance.GetStorage<EventStorage>();
            var eventData = eventStorage.GetDailyEventData(this.eventCode);
            eventData.dailyDto.todayReceivedPos = slotIndex;

            var targetReward = this.dailyRewards.Find(x => 
                (x.Bundle == eventData.dailyBonusInfo.DailyReward) && 
                (x.Item == acquiredItemDto.acquiredItems[0].code) && 
                ((x.Value == acquiredItemDto.acquiredItems[0].count)));
            
            if (targetReward != null)
            {
                this.todayReceivedReward = eventData.dailyDto.todayReceivedReward = targetReward.Code;
            }

            if (acquiredItemDto != null)
            {
                //Deep Copy to copiedAcquiredItemDto
                VariationItemDto[] copiedAcquiredItems = acquiredItemDto.acquiredItems != null ? 
                    new VariationItemDto[acquiredItemDto.acquiredItems.Length] : new VariationItemDto[0];
                VariationItemDto[] copiedNotReceivedItems = acquiredItemDto.notReceivedItems != null ?
                    new VariationItemDto[acquiredItemDto.notReceivedItems.Length] : new VariationItemDto[0];
                
                for (int i = 0; i < copiedAcquiredItems.Length; i++)
                {
                    copiedAcquiredItems[i] = acquiredItemDto.acquiredItems[i];
                }
                for (int i = 0; i < copiedNotReceivedItems.Length; i++)
                {
                    copiedNotReceivedItems[i] = acquiredItemDto.notReceivedItems[i];
                }
                
                AcquiredItemDto copiedAcquiredItemDto = new AcquiredItemDto(
                    copiedAcquiredItems, 
                    copiedNotReceivedItems
                );

                if (acquiredItemDto.cards == null)
                {
                    VariationItemDto[] tmp1 = new VariationItemDto[]{ acquiredItemDto.acquiredItems[0] };
                    copiedAcquiredItemDto.acquiredItems = tmp1;
                }
                //end Deep Copy

                this.rewardPopupDimmedObj.SetActive(true);
                
                //1단계...보상 획득 팝업 처리
                ViewController.OpenRewardPopup(copiedAcquiredItemDto, () =>
                {
                    Int32[] tmp;
                    if (eventData.dailyDto.receivedCumRewards.Length == 0) { tmp = new Int32[0]; }
                    else { tmp = eventData.dailyDto.receivedCumRewards; }
                    var tmp2 = new Int32[tmp.Length + 1];
                    for (int i = 0; i < tmp.Length; i++) { tmp2[i] = tmp[i]; }
                    tmp2[tmp.Length] = copiedAcquiredItemDto.acquiredItems[0].code;

                    eventData.dailyDto.receivedCumRewards = tmp2;
                    eventData.dailyDto.count += 1;
            
                    //도달된 누적보상이 있다면, 해당 누적보상 index값을 찾는다.
                    if (this.dailyCumRewards != null)
                    {
                        bool isCloseBlack = true;
                        for (int i = 0; i < this.dailyCumRewards.Count; i++)
                        {
                            DailyCumReward dailyCumReward = this.dailyCumRewards[i];
                            bool isBoxItem = SBDataSheet.Instance.ItemProduction[dailyCumReward.Item].ItemType == 3;

                            //박스 보상인 경우
                            if (isBoxItem)
                            {
                                var boxItems = SBDataSheet.Instance.ItemProduction[dailyCumReward.Item].GetBoxBundle();
                                
                                //누적보상에 도달한 경우
                                if (dailyCumReward.DailyCumCount == eventData.dailyDto.count)
                                {
                                    VariationItemDto[] acquiredItems = new VariationItemDto[boxItems.Count];
                                    for (int j = 0; j < boxItems.Count; j++)
                                    {
                                        acquiredItems[j] = new VariationItemDto(boxItems[j].ItemProduction, boxItems[j].ItemQuantity);
                                    }
                                   
                                    VariationItemDto[] notReceivedItems = new VariationItemDto[0];

                                    AcquiredItemDto itemDto = new AcquiredItemDto(acquiredItems, notReceivedItems);
                                    
                                    //2단계...누적포상 팝업
                                    var i1 = i;
                                    
                                    var itemProduction = dailyCumReward.GetItemProductionByItem();
                                    bool isCardItem = (ItemDataItemType)itemProduction.ItemType == ItemDataItemType.Card;
                                    if (isCardItem)
                                    {
                                        copiedAcquiredItemDto.cards = acquiredItemDto.cards;
                                        itemDto.cards = copiedAcquiredItemDto.cards;
                                    }

                                    isCloseBlack = false;
                                    ViewController.OpenRewardPopup(itemDto, () =>
                                    {
                                        this.accumulateRewardItemViews[i1].SetStateOpen();
                                        this.rewardPopupDimmedObj.SetActive(false);
                                        
                                        ItemStorage itemStorage = GameStorage.Instance.GetStorage<ItemStorage>();
                                        itemStorage.GetReward(acquiredItemDto);
                                    });
                                }
                            }
                            //박스 보상이 아닌 경우
                            else
                            {
                                if (dailyCumReward.DailyCumCount == eventData.dailyDto.count)
                                {
                                    VariationItemDto[] acquiredItems = new VariationItemDto[1];
                                    acquiredItems[0] = new VariationItemDto(
                                        this.dailyCumRewards[i].Item, 
                                        this.dailyCumRewards[i].Value
                                    );
                                    VariationItemDto[] notReceivedItems = new VariationItemDto[0];
                                    AcquiredItemDto itemDto = new AcquiredItemDto(acquiredItems, notReceivedItems);
                                
                                    //2단계...누적포상 팝업
                                    var i1 = i;
                                    
                                    var itemProduction = dailyCumReward.GetItemProductionByItem();
                                    bool isCardItem = (ItemDataItemType)itemProduction.ItemType == ItemDataItemType.Card;
                                    if (isCardItem)
                                    {
                                        copiedAcquiredItemDto.cards = acquiredItemDto.cards;
                                        itemDto.cards = copiedAcquiredItemDto.cards;
                                    }

                                    isCloseBlack = false;
                                    ViewController.OpenRewardPopup(itemDto, () =>
                                    {
                                        this.accumulateRewardItemViews[i1].SetStateOpen();
                                        this.rewardPopupDimmedObj.SetActive(false);
                                        
                                        ItemStorage itemStorage = GameStorage.Instance.GetStorage<ItemStorage>();
                                        itemStorage.GetReward(acquiredItemDto);
                                    });
                                }
                            }
                        }

                        if(isCloseBlack)
                        {
                            this.rewardPopupDimmedObj.SetActive(false);

                            ItemStorage itemStorage = GameStorage.Instance.GetStorage<ItemStorage>();
                            itemStorage.GetReward(acquiredItemDto);
                        }
                    }
                    else
                    {
                        this.rewardPopupDimmedObj.SetActive(false);
                        
                        ItemStorage itemStorage = GameStorage.Instance.GetStorage<ItemStorage>();
                        itemStorage.GetReward(acquiredItemDto);
                    }
                    
                    this.SetLocales();
                
                    this.isRewardTask = false;
                });
            }
        });
    }

    //보상 수령 요청 (보상 코드, 선택 포지션)
    public void RequestReward(int slotIndex, AttendanceItem attendanceItem, Action<ResponseDto<AcquiredItemDto>> cb)
    {
        UInt16 requestCount = 0;
        //code값은 DailyInfo의 Code값을 전달한다!
        DailyReceiveDto receiveDto = new DailyReceiveDto(this.eventCode, slotIndex);

        RequestDto<DailyReceiveDto> requestDto = new RequestDto<DailyReceiveDto>(
            requestCount,
            receiveDto,
            Guid.NewGuid().ToString(),
            SBTime.Instance.ISOServerTime
        );
        
        BroadcastTunnel<string, int>.Notify("com.snowballs.SWHJ.OffNewMark", this.lobbyIconCode);
        
        GameDaily.Receive(requestDto, (responseDto) =>
        {
            if (responseDto.code == (int)ResponseCode.OK)
            {
                HistoryDto dto = new HistoryDto("DailyBonusInfo", this.eventCode);
                RequestDto<HistoryDto> historyRequestDto = new RequestDto<HistoryDto>(0, dto,
                    Guid.NewGuid().ToString(), SBTime.Instance.ISOServerTime);

                var networkManager = GameScene.Instance.NetworkManager;
                networkManager.Ack(historyRequestDto, (ackResponse) =>
                {
                    cb?.Invoke(responseDto);
                });

                GameStorage.DailyDtoUpdatedTime = SBTime.Instance.ServerTime;
            }
            else
            {
                SBDebug.Log("Reward Receive Request Error In AttendanceCheckPopup");
                cb?.Invoke(responseDto);
            }
        });
    }

    public void OpenDummyAccumReceivedPopup()
    {
        VariationItemDto[] acquiredItems = new[] { new VariationItemDto(1, 100), new VariationItemDto(4, 100) };
        VariationItemDto[] notReceivedItems = null;
        AcquiredItemDto dummyRewards = new AcquiredItemDto(acquiredItems, notReceivedItems);

        string dummyHeaderText = String.Empty;
        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
        if (playerStorage.Locale == "ko")
        {
            dummyHeaderText = SBDataSheet.Instance.EventLocale[9998].KoKR;
        }
        else
        {
            dummyHeaderText = SBDataSheet.Instance.EventLocale[9998].EnUS;
        }
        
        ViewController.OpenRewardPopup(dummyHeaderText, dummyRewards, () => { });
    }
    
    /// <summary>
    /// 누적 보상 획득 연출
    /// </summary>
    /// <param name="cb">완료 콜백</param>
    private void OpenAccumReceivedPopup(Action cb)
    {
        DailyCumReward dailyCumReward = null;
        
        Params pB = this.paramBuffer as Params;
        var accumRewards = pB.dailyCumRewardData;
        
        for (int i=0; i<accumRewards.Count; i++)
        {
            if (this.totalAccumulateCount >= accumRewards[i].DailyCumCount)
                dailyCumReward = accumRewards[i];
        }

        if (dailyCumReward == null)
        {
            //누적 보상 받을게 없는 경우 콜백만 호출하고 종료한다.
            cb?.Invoke();
            return;
        }

        VariationItemDto[] acquiredItems = new[]
        {
            new VariationItemDto(dailyCumReward.Item, dailyCumReward.Value)
        };
        VariationItemDto[] notReceivedItems = null;
        AcquiredItemDto rewards = new AcquiredItemDto(acquiredItems, notReceivedItems);
        
        string headerText = String.Empty;
        
        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
        if (playerStorage.Locale == "ko")
        {
            headerText = SBDataSheet.Instance.EventLocale[9998].KoKR;
        }
        else
        {
            headerText = SBDataSheet.Instance.EventLocale[9998].EnUS;
        }
        
        ViewController.OpenRewardPopup(headerText, rewards, () =>
        {
            cb?.Invoke();
        });
    }

    public void BlockCloseButton()
    {
        this.closeBtn.enabled = false;
    }

    public void UnBlockCloseButton()
    {
        this.closeBtn.enabled = true;
    }
    
    private void SetState(STATE newState)
    {
        if (this.CurrentState == newState)
        {
            Debug.Log("Same State!!");
            return;
        }
        
        switch (newState)
        {
            case STATE.LoadingData:
                LoadingIndicator.Show();
                this.Init();
                break;
            case STATE.LoadingDataDone:
                LoadingIndicator.Hide();
                break;
        }
        this._currentState = newState;
    }
    
    public enum STATE
    {
        None = 0,
        LoadingData = 1,
        LoadingDataDone = 2,
        Done = 5
    }
}
