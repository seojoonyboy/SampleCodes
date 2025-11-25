using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using com.snowballs.SWHJ.client.model;
using com.snowballs.SWHJ.client.view;
using com.snowballs.SWHJ.Foundation;
using com.snowballs.SWHJ.presenter;
using EnhancedUI.EnhancedScroller;
using Snowballs.Network.API;
using Snowballs.Network.Dto;
using Snowballs.Sheets;
using Snowballs.Sheets.Data;
using Snowballs.Util;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static com.snowballs.SWHJ.client.model.EventStorage;

public class PassPopup : Popup, IEnhancedScrollerDelegate
{
    private List<PassItemView.Params> _data;
    public float cellViewSize;
    
    [SerializeField] private EnhancedScrollerCellView cellViewPrefab;
    [SerializeField] private EnhancedScroller scroller;

    [SerializeField] private TextMeshProUGUI eventTermHeaderText, eventTermValueText;     //이벤트 진행 기간
    [SerializeField] private TextMeshProUGUI remainTimeValueText;    //남은 시간

    [SerializeField] public Slider topGauge;
    [SerializeField] public TextMeshProUGUI topGaugeValueText;
    [SerializeField] private TextMeshProUGUI topGaugeLevelValueText;
    [SerializeField] private TextMeshProUGUI topGaugeInfiniteValueText;
    
    //이미지 관련
    [SerializeField] private Image topBg;                   //최상단 헤더 이미지
    [SerializeField] private Image innerBg;
    [SerializeField] private Image innerHeaderBg1;          //게이지 있는 영역 헤더 이미지
    [SerializeField] private Image innerHeaderBg2;          //무료 & 프리미엄 헤더 이미지
    [SerializeField] private Image topGaugeHeaderIcon;
    [SerializeField] private Image topGaugeInfiniteRewardIcon;

    [SerializeField] private Button premiumAlreadyActivatedButton;
    [SerializeField] private Button waitPurchaseButton;
    [SerializeField] private Button receiveAllButton;
    
    [SerializeField] private TextMeshProUGUI premiumAlreadyActivatedButtonText;
    [SerializeField] private TextMeshProUGUI waitPurchaseButtonText;
    [SerializeField] private TextMeshProUGUI receiveAllButtonText;
    
    [SerializeField] private PassDescriptionModal descriptionModal;
    
    [SerializeField] private GameObject helpPopupDimmedObj;
    public GameObject HelpPopupDimmed => this.helpPopupDimmedObj;
    
    [SerializeField] private GameObject infoBtnObj;

    public Texture2D goldInfiniteTexture;
    public Texture2D infiniteRewardIconTexture;
    
    [SerializeField] private Material greyScale;
    
    public new class Params : Popup.Params
    {
        public int passCode;
        public PassDto passDto;
        public PassInfo passInfo;

        public GameObject passIconObj;
        
        public List<PassRewardDto> passReward;
    }
    
    public Func<DateTime> timeFunc;
    public DateTime endTime;

    private Int32? toastMsg_clickCannotReceiveLocale;                   //패스_레벨 미달성 보상 터치
    private Int32? toastMsg_clickPremiumLocaleBeforeUnlock;             //패스_프리미엄 미구매 보상 터치
    private Int32? toastMsg_receiveComplete;                            //패스_획득 완료 보상 터치
    private Int32? toastMsg_goldBonusBeforeComplete;                    //패스_골드 보너스 MAX 전 보상 터치
    
    private Int32? buyCompleteButtonLocale;                             //활성화 완료
    
    private string playerLocale = String.Empty;

    public GameObject passIconObj;
    public GameObject levelObj;
    public override void OnOpen()
    {
        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
        this.playerLocale = playerStorage.Locale;
        
        this.toastMsg_clickCannotReceiveLocale = 21;
        this.toastMsg_clickPremiumLocaleBeforeUnlock = 22;
        this.toastMsg_receiveComplete = 23;
        this.toastMsg_goldBonusBeforeComplete = 24;

        this.buyCompleteButtonLocale = 81;
        
        this.HideUI();
        
        base.OnOpen();
        
        Params pB = this.paramBuffer as Params;
        this.timeFunc = () => { return SBTime.Instance.ServerTime; };
        this.endTime = pB.passInfo.DT_EndAt;
        this.passIconObj = pB.passIconObj;
        
        this.InitTextures(pB.passInfo, pB.passDto);
        this.InitEventTime(pB.passInfo);
        
        this.LoadData(pB);
        
        this.InitGauge(pB.passDto, pB.passInfo);
        this.TogglePremiumButton(pB.passDto);

        this.topGaugeInfiniteValueText.text = "x" + pB.passInfo.GoldBonusRewardValue;
        
        //무제한 보상쪽 텍스쳐 로딩...
        {
            string infiniteGoldPath = this.GetInfiniteGoldAreaTextureFilePath(pB.passInfo);
            WWWFile.DownloadPath downloadPath =
                new WWWFile.DownloadPath(WWWFile.TYPE.Bytes, String.Empty, infiniteGoldPath);
            
            TextureController.GetTexture(downloadPath, false, (isSuccess, texture) =>
            {
                this.goldInfiniteTexture = texture;
            });
        }

        this.showUITask = CoroutineTaskManager.AddTask(ShowUI(0.5f));
    }

    private Coroutine showUITask = null;
    private void HideUI()
    {
        LoadingIndicator.Show();
        RectTransform rect = GetComponent<RectTransform>();
        rect.localPosition = new Vector3(100000, 0, 0);
    }

    IEnumerator ShowUI(float waitTime)
    {
        yield return new WaitForSeconds(0.5f);
        this.scroller.Delegate = this;
        
        yield return new WaitForSeconds(waitTime);
        
        RectTransform rect = GetComponent<RectTransform>();
        rect.localPosition = new Vector3(0, 0, 0);

        var pB = this.paramBuffer as Params;

        int scrollIndex = pB.passDto.level > 0 ? pB.passDto.level - 1 : 0;
        this.scroller.JumpToDataIndex(scrollIndex);
        // this.scroller.JumpToDataIndex(31);
        LoadingIndicator.Hide();
    }

    private Coroutine resizeScrollRect = null;
    
    private void ResizeScrollRect(Action cb)
    {
        resizeScrollRect = CoroutineTaskManager.AddTask(Task(
            () => cb?.Invoke()
            )
        );
        
        IEnumerator Task(Action cb)
        {
            yield return new WaitForEndOfFrame();
            
            var scrollerRect = this.scroller.GetComponent<RectTransform>();
            float innerBgHeight = Screen.height < 1920 ? 1920 : PopupRoot.Instance.GetHeight();
            this.innerBg.GetComponent<RectTransform>().sizeDelta = new Vector2(1661, innerBgHeight);
            float topBgHeight = this.topBg.GetComponent<RectTransform>().sizeDelta.y;
            float height = innerBgHeight - (topBgHeight + 146.0f + 143.0f);     //배너 영역 + 상단 UI 1 + 상단 UI 2
            if (Screen.width < 1080)
            {
                scrollerRect.sizeDelta = new Vector2(1080f, height);
            }
            else
            {
                scrollerRect.sizeDelta = new Vector2(Screen.width, height);
            }
            this.closeBtn.gameObject.SetActive(true);
            cb?.Invoke();
        }
    }
    
    private void LoadData(Params p)
    {
        _data = new List<PassItemView.Params>();
        
        //전체 보상 리스트
        var rewards = p.passInfo.GetLevReward();
        for (int i = 0; i < rewards.Count; i++)
        {
            PassItemView.Params item = new PassItemView.Params();
            item.itemIndex = i;
            item.focusedTextureFilePath = this.GetItemFocusedFilePath(p.passInfo);
            item.maxGoldValue = p.passInfo.GoldBonusRewardMax;

            item.passDto = p.passDto;
            item.passInfo = p.passInfo;

            var targetRewardInfo = rewards.Find(x => x.PassLevel == item.itemIndex);
            item.passReward = targetRewardInfo;
            item.passPopup = this;
            item.passCode = p.passCode;
            
            item.lastIndex = rewards.Count - 1;
            item.onClickGoldReceive = this.OnClickGoldReceiveButton;
            item.currentPassLevel = p.passDto.level;

            item.passRewardDto = item.passDto.rewards
                .ToList()
                .Find(x => x.code == item.passReward.Code);

            var verticalLayoutGroup = this.scroller.Container.GetComponent<VerticalLayoutGroup>();
            if (verticalLayoutGroup != null)
            {
                verticalLayoutGroup.padding.bottom = 200;
            }
            
            _data.Add(item);
        }
        
        //골드 획득 영역
        PassItemView.Params lastItem = new PassItemView.Params();
        lastItem.passPopup = this;
        lastItem.passDto = p.passDto;
        lastItem.passInfo = p.passInfo;
        lastItem.passCode = p.passCode;
        lastItem.itemIndex = rewards.Count;
        lastItem.lastIndex = rewards.Count - 1;
        lastItem.maxGoldValue = p.passInfo.GoldBonusRewardMax;
        _data.Add(lastItem);
    }

    private void OnClickGoldReceiveButton()
    {
        //scroll 갱신
        
    }

    private void InitEventTime(PassInfo passInfo)
    {
        //이벤트 기간 표시
        DateTime startAt = passInfo.DT_StartAt;
        DateTime endAt = passInfo.DT_EndAt;

        // StringBuilder sb = new StringBuilder();
        // sb
        //     .Append(startAt.ToString("yyyy.MM.dd"))
        //     .Append(" ~ ")
        //     .Append(endAt.ToString("yyyy.MM.dd"));
        //
        // this.eventTermValueText.text = sb.ToString();

        var signal = new CancellableSignal(() =>
        {
            return this == null;
        });

        TimerParam timerParam = new TimerParam();
        timerParam.endTime = this.endTime;
        
        //test code
        //timerParam.endTime = DateTime.Now;
        //this.endTime = timerParam.endTime;
        //end test code

        timerParam.timeFunc = this.timeFunc;
        CoroutineTaskManager.AddTask(this.RemainTimeTask(signal, timerParam));
    }

    public void ToastMessage(Int32 code)
    {
        string message = GetLocaleText(code);
        Toast.Active(message, 0.8f);

        string GetLocaleText(Int32? localeCode)
        {
            if(!localeCode.HasValue) return String.Empty;

            PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
            
            if (playerStorage.Locale == "ko") return SBDataSheet.Instance.SystemLocale[localeCode.Value].KoKR;
            return SBDataSheet.Instance.SystemLocale[localeCode.Value].EnUS;
        }
    }

    private string GetItemFocusedFilePath(PassInfo passInfo)
    {
        int eventResourceCode = passInfo.Middle3Res;
        string address = string.Empty;

        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
        if (playerStorage.Locale == "ko")
        {
            address = SBDataSheet.Instance.EventResource[eventResourceCode].KoKRAddress;
        }
        else
        {
            address = SBDataSheet.Instance.EventResource[eventResourceCode].EnUSAddress;
        }

        return AssetPathController.PATH_FOLDER_ASSETS + address;
    }

    private string GetInfiniteGoldAreaTextureFilePath(PassInfo passInfo)
    {
        int eventResourceCode = passInfo.GoldBonusRes;
        string address = string.Empty;

        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
        if (playerStorage.Locale == "ko")
        {
            address = SBDataSheet.Instance.EventResource[eventResourceCode].KoKRAddress;
        }
        else
        {
            address = SBDataSheet.Instance.EventResource[eventResourceCode].EnUSAddress;
        }

        return AssetPathController.PATH_FOLDER_ASSETS + address;
    }

    private void InitGauge(PassDto passDto, PassInfo passInfo)
    {
        int totalExp = passInfo.ExpPerLev;
        int currentExp = passDto.exp;

        float ratio = (float)currentExp / totalExp;
        this.topGauge.value = ratio;

        StringBuilder sb = new StringBuilder();
        sb
            .Append(currentExp)
            .Append("/")
            .Append(totalExp);

        this.topGaugeValueText.text = sb.ToString();

        if (passDto.level == 0) this.topGaugeLevelValueText.text = "1";
        else this.topGaugeLevelValueText.text = (passDto.level + 1).ToString();
    }

    private void TogglePremiumButton(PassDto passDto)
    {
        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
        bool isPremium = playerStorage.PlayerDto.pass.isPremium;    //프리미엄 구독중인가?
        
        //이벤트 기간이 끝난 경우
        DateTime now = this.timeFunc();
        if (now > this.endTime)
        {
            this.premiumAlreadyActivatedButton.gameObject.SetActive(isPremium);
            this.waitPurchaseButton.gameObject.SetActive(false);
            // this.receiveAllButton.gameObject.SetActive(true);
        }
        else
        {
            //이벤트 기간인 경우
            if (isPremium)
            {
                this.premiumAlreadyActivatedButton.gameObject.SetActive(true);
                this.waitPurchaseButton.gameObject.SetActive(false);
            }
            else
            {
                this.premiumAlreadyActivatedButton.gameObject.SetActive(false);
                this.waitPurchaseButton.gameObject.SetActive(true);
            }
        }

        if (playerStorage.Locale == "ko")
        {
            if (this.buyCompleteButtonLocale.HasValue)
            {
                this.premiumAlreadyActivatedButtonText.text =
                    SBDataSheet.Instance.SystemLocale[this.buyCompleteButtonLocale.Value].KoKR;

                this.receiveAllButtonText.text = SBDataSheet.Instance.SystemLocale[83].KoKR;
            }
            else this.premiumAlreadyActivatedButtonText.text = "활성화 완료";
            
            this.waitPurchaseButtonText.text = SBDataSheet.Instance.SystemLocale[202].KoKR;
        }
        else
        {
            if (this.buyCompleteButtonLocale.HasValue)
            {
                this.premiumAlreadyActivatedButtonText.text =
                    SBDataSheet.Instance.SystemLocale[this.buyCompleteButtonLocale.Value].EnUS;
                
                this.receiveAllButtonText.text = SBDataSheet.Instance.SystemLocale[83].EnUS;
            }
            else this.waitPurchaseButtonText.text = "활성화";
            
            this.waitPurchaseButtonText.text = SBDataSheet.Instance.SystemLocale[202].EnUS;
        }

        // if (passDto.canReceiveBonus)
        // {
        //     this.receiveAllButton.interactable = true;
        //     this.receiveAllButton.image.material = null;
        // }
        // else
        // {
        //     this.receiveAllButton.interactable = false;
        //     this.receiveAllButton.image.material = this.greyScale;
        // }
    }

    private bool isAlreadyClicked = false;
    /// <summary>
    /// 이벤트 종료시 모두 받기 버튼 클릭을 통한 보상 요청
    /// </summary>
    public void RequestAllRewards()
    {
        if(isAlreadyClicked) return;
        isAlreadyClicked = true;
        
        Params pB = paramBuffer as Params;
        PassReceiveDto passReceiveDto = new PassReceiveDto(pB.passCode, true);
        RequestDto<PassReceiveDto> requestDto =
            new RequestDto<PassReceiveDto>(
                0, 
                passReceiveDto, 
                Guid.NewGuid().ToString(), 
                SBTime.Instance.ISOServerTime
            );
        
        GamePass.Receive(requestDto, responseDto =>
        {
            if (responseDto.code == (int)ResponseCode.OK)
            {
                //GameHistory Ack 체크
                int passAckCode = pB.passCode;        //해당 Pass의 시트코드
                HistoryDto dto = new HistoryDto("PassInfo", passAckCode);
                RequestDto<HistoryDto> historyReqDto = new RequestDto<HistoryDto>(0, dto, Guid.NewGuid().ToString(), SBTime.Instance.ISOServerTime);
                var networkManager = GameScene.Instance.NetworkManager;
                networkManager.Ack(historyReqDto, ackResponse =>
                {
                    if (ackResponse != null && (ResponseCode)ackResponse.code == ResponseCode.OK)
                    {
                        SBDebug.Log("<color=green>수령 Ack 완료</color>");
                    }
                    else
                    {
                        SBDebug.Log("<color=red>수령 Ack 실패</color>");
                    }
                    
                    isAlreadyClicked = false;
                });
                
                this.ToastMessage(82);
                
                EventStorage eventStorage = GameStorage.Instance.GetStorage<EventStorage>();
                var targetEventData = eventStorage.GetPassEventData(pB.passCode);
                if (targetEventData != null)
                {
                    targetEventData.UpdateAllReceived();
                    targetEventData.passDto.canReceiveBonus = false;
                }

                ItemStorage itemStorage = GameStorage.Instance.GetStorage<ItemStorage>();
                itemStorage.GetReward(responseDto.data);
                
                pB.passDto.canReceiveBonus = false;
                // this.receiveAllButton.interactable = false;
                // this.receiveAllButton.image.material = this.greyScale;
                
                this.RefreshForce(pB);
            }
            else
            {
                SBDebug.Log("<color=red>수령 요청 실패</color>");
                isAlreadyClicked = false;
            }
        });
    }
    
    private void InitTextures(PassInfo passInfo, PassDto passDto)
    {
        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
        
        //패스 상단 무제한 보상 이미지
        {
            if (passDto.level >= passInfo.MaxLev)
            {
                this.topGaugeInfiniteRewardIcon.gameObject.SetActive(true);
            }
            
            int itemCode = passInfo.GoldBonusReward;
            int itemImageCode = SBDataSheet.Instance.ItemProduction[itemCode].IconImage;
            string itemImageAddress = String.Empty;
            if (playerStorage.Locale == "ko")
            {
                itemImageAddress = SBDataSheet.Instance.ItemResource[itemImageCode].KoKRAddress;
            }
            else
            {
                itemImageAddress = SBDataSheet.Instance.ItemResource[itemImageCode].EnUSAddress;
            }

            string itemImageFilePath = AssetPathController.PATH_FOLDER_ASSETS + itemImageAddress;

            WWWFile.DownloadPath downloadPath =
                new WWWFile.DownloadPath(WWWFile.TYPE.Bytes, String.Empty, itemImageFilePath);
            
            TextureController.GetTexture(downloadPath, false, (isSuccess, texture) =>
            {
                if (isSuccess)
                {
                    this.infiniteRewardIconTexture = texture;
                    
                    this.topGaugeInfiniteRewardIcon.sprite = Sprite.Create(
                        texture, 
                        new Rect(0, 0, texture.width, texture.height), 
                        new Vector2(0.5f, 0.5f)
                    );
                }
            });
        }
        
        //패스 아이콘 이미지
        {
            int iconResCode = passInfo.IconRes;
            string iconResAddress = string.Empty;
            if (playerStorage.Locale == "ko")
                iconResAddress = SBDataSheet.Instance.EventResource[iconResCode].KoKRAddress;
            else
                iconResAddress = SBDataSheet.Instance.EventResource[iconResCode].EnUSAddress;

            string iconFilePath = AssetPathController.PATH_FOLDER_ASSETS + iconResAddress;

            WWWFile.DownloadPath downloadPath = new WWWFile.DownloadPath(
                WWWFile.TYPE.Bytes, 
                String.Empty, 
                iconFilePath
            );
        
            TextureController.GetTexture(downloadPath, false, (isSuccess, texture) =>
            {
                this.topGaugeHeaderIcon.sprite = Sprite.Create(
                    texture, 
                    new Rect(0, 0, texture.width, texture.height), 
                    new Vector2(0.5f, 0.5f), 100.0f
                );
            });
        }

        //배너 이미지
        {
            int bannerImageCode = passInfo.TopRes;
            string bannerImageAddress = String.Empty;
            if (playerStorage.Locale == "ko")
            {
                bannerImageAddress = SBDataSheet.Instance.EventResource[bannerImageCode].KoKRAddress;
            }
            else
            {
                bannerImageAddress = SBDataSheet.Instance.EventResource[bannerImageCode].EnUSAddress;
            }

            string bannerImageFilePath = AssetPathController.PATH_FOLDER_ASSETS + bannerImageAddress;
        
            WWWFile.DownloadPath bannerDownloadPath = new WWWFile.DownloadPath(
                WWWFile.TYPE.Bytes, 
                String.Empty, 
                bannerImageFilePath
            );
        
            TextureController.GetTexture(bannerDownloadPath, false, (isSuccess, texture) =>
            {
                this.topBg.sprite = Sprite.Create(
                    texture, 
                    new Rect(0, 0, texture.width, texture.height), 
                    new Vector2(0.5f, 0.5f), 100.0f
                );
                
                this.topBg.SetNativeSize();
            });
        }

        //패스 보상 영역 이미지
        {
            int middle1Res = passInfo.Middle1Res;
            string middle1ResAddress = String.Empty;
            
            if (playerStorage.Locale == "ko")
            {
                middle1ResAddress = SBDataSheet.Instance.EventResource[middle1Res].KoKRAddress;
            }
            else
            {
                middle1ResAddress = SBDataSheet.Instance.EventResource[middle1Res].EnUSAddress;
            }

            string middle1ResFilePath = AssetPathController.PATH_FOLDER_ASSETS + middle1ResAddress;
            
            WWWFile.DownloadPath middle1ResDownloadPath = new WWWFile.DownloadPath(
                WWWFile.TYPE.Bytes, 
                String.Empty, 
                middle1ResFilePath
            );
            
            TextureController.GetTexture(middle1ResDownloadPath, false, (isSuccess, texture) =>
            {
                this.innerHeaderBg1.sprite = Sprite.Create(
                    texture, 
                    new Rect(0, 0, texture.width, texture.height), 
                    new Vector2(0.5f, 0.5f), 100.0f
                );
                
                this.innerHeaderBg1.SetNativeSize();
            });
        }

        {
            int middle2Res = passInfo.Middle2Res;
            string middle2ResAddress = String.Empty;
            
            if (playerStorage.Locale == "ko")
            {
                middle2ResAddress = SBDataSheet.Instance.EventResource[middle2Res].KoKRAddress;
            }
            else
            {
                middle2ResAddress = SBDataSheet.Instance.EventResource[middle2Res].EnUSAddress;
            }

            string middle2ResFilePath = AssetPathController.PATH_FOLDER_ASSETS + middle2ResAddress;
            
            WWWFile.DownloadPath middle2ResDownloadPath = new WWWFile.DownloadPath(
                WWWFile.TYPE.Bytes, 
                String.Empty, 
                middle2ResFilePath
            );
            
            TextureController.GetTexture(middle2ResDownloadPath, false, (isSuccess, texture) =>
            {
                this.innerHeaderBg2.sprite = Sprite.Create(
                    texture, 
                    new Rect(0, 0, texture.width, texture.height), 
                    new Vector2(0.5f, 0.5f), 100.0f
                );
                
                this.innerHeaderBg2.SetNativeSize();
            });
        }

        //스크롤 영역 배경
        {
            int middle4Res = passInfo.Middle4Res;
            string middle4ResAddress = String.Empty;
            
            if (playerStorage.Locale == "ko")
            {
                middle4ResAddress = SBDataSheet.Instance.EventResource[middle4Res].KoKRAddress;
            }
            else
            {
                middle4ResAddress = SBDataSheet.Instance.EventResource[middle4Res].EnUSAddress;
            }

            string middle4ResFilePath = AssetPathController.PATH_FOLDER_ASSETS + middle4ResAddress;
            
            WWWFile.DownloadPath middle4ResDownloadPath = new WWWFile.DownloadPath(
                WWWFile.TYPE.Bytes, 
                String.Empty, 
                middle4ResFilePath
            );

            TextureController.GetTexture(middle4ResDownloadPath, false, (isSuccess, texture) =>
            {
                this.innerBg.sprite = Sprite.Create(
                    texture, 
                    new Rect(0, 0, texture.width, texture.height), 
                    new Vector2(0.5f, 0.5f), 100.0f
                );

                RectTransform innerBgRect = this.innerBg.GetComponent<RectTransform>();
                
                if (Screen.height < 1920)
                {
                    innerBgRect.sizeDelta = new Vector2(texture.width, 1920);
                }
                else
                {
                    innerBgRect.sizeDelta = new Vector2(texture.width, Screen.height);
                }
                this.innerBg.type = Image.Type.Tiled;
                
                this.ResizeScrollRect(() => { });
                //this.innerBg.SetNativeSize();
            });
        }
    }

    public int GetNumberOfCells(EnhancedScroller scroller)
    {
        return this._data.Count;
    }

    public float GetCellViewSize(EnhancedScroller scroller, int dataIndex)
    {
        return this.cellViewSize;
    }

    public EnhancedScrollerCellView GetCellView(EnhancedScroller scroller, int dataIndex, int cellIndex)
    {
        PassItemView cellView = scroller.GetCellView(cellViewPrefab) as PassItemView;
        
        cellView.name = "Cell " + dataIndex.ToString();
        cellView.SetData(_data[dataIndex]);
        
        return cellView;
    }

    public class TimerParam
    {
        public Func<DateTime> timeFunc;
        public DateTime endTime;
    }

    IEnumerator RemainTimeTask(CancellableSignal signal, TimerParam param)
    {
        var wfef = new WaitForEndOfFrame();

        DateTime now;
        TimeSpan remainingTime;
        do
        {
            now = param.timeFunc();

            if (now <= param.endTime)
            {
                remainingTime = param.endTime - now;
                this.UpdateSeasonRemainingTime(remainingTime);
            }
            else
            {
                //프리미엄 버튼을 모두 받기 버튼으로 변경
                this.OnEndTime();
            }
            yield return wfef;
            if (CancellableSignal.IsCanceled(signal)) { yield break; }
        }
        while (now <= param.endTime);
    }
    
    void UpdateSeasonRemainingTime(TimeSpan time)
    {
        if (time.Days >= 1)
        {
            int targetLocaleCode = 301;
            string localeStr = string.Empty;
            if (this.playerLocale == "ko")
            {
                localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].KoKR;
            }
            else
            {
                localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].EnUS;
            }
            
            localeStr = localeStr.Replace("{0}", time.Days.ToString());
            localeStr = localeStr.Replace("{1}", time.Hours.ToString());
            this.remainTimeValueText.text = localeStr;
        }
        else if (time.Hours >= 1)
        {
            int targetLocaleCode = 302;
            string localeStr = string.Empty;
            if (this.playerLocale == "ko")
            {
                localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].KoKR;
            }
            else
            {
                localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].EnUS;
            }
            
            localeStr = localeStr.Replace("{0}", time.Hours.ToString());
            localeStr = localeStr.Replace("{1}", time.Minutes.ToString());
            this.remainTimeValueText.text = localeStr;
        }
        else
        {
            if (time.Minutes >= 1)
            {
                int targetLocaleCode = 304;
                string localeStr = string.Empty;
                if (this.playerLocale == "ko")
                {
                    localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].KoKR;
                }
                else
                {
                    localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].EnUS;
                }
                
                localeStr = localeStr.Replace("{0}", time.Minutes.ToString());
                localeStr = localeStr.Replace("{1}", time.Seconds.ToString());
                this.remainTimeValueText.text = localeStr;
            }
            else
            {
                int targetLocaleCode = 305;
                string localeStr = string.Empty;
                if (this.playerLocale == "ko")
                {
                    localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].KoKR;
                }
                else
                {
                    localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].EnUS;
                }
                
                localeStr = localeStr.Replace("{0}", time.Seconds.ToString());
                this.remainTimeValueText.text = localeStr;
            }
        }
    }

    private bool isTimeEndTaskCalled = false;
    private void OnEndTime()
    {
        if(isTimeEndTaskCalled) return;
        
        this.isTimeEndTaskCalled = true;
        Params pB = paramBuffer as Params;
        
        //Storage 단에서 Data를 갱신한다. (EventStorage)
        EventStorage eventStorage = GameStorage.Instance.GetStorage<EventStorage>();
        var targetEventData = eventStorage.GetPassEventData(pB.passCode);
        if(targetEventData == null) return;

        this.remainTimeValueText.text = LocaleController.GetSystemLocale(314);

        //1. 이벤트가 종료되고, 프리미엄 구독자이면서, 동시에 이전에 보상을 받지 않은 경우라면 골드 보너스 수령 가능 상태로 세팅
        //   -> 팝업에 계속 잔류하는 경우임 [이때는 Gold Gauge Bar 가 Max 도달을 하지 않아도 되는 경우임
        if ((pB.passDto.isPremium) && (targetEventData.passDto.bonus!=0) && !targetEventData.passDto.isBonusReceived)
        {
            targetEventData.passDto.canReceiveBonus = true;
            pB.passDto.canReceiveBonus = true;
        }
        
        //2. 보상 수령, 잠금 등 상태는 그대로 둔다.
        pB.passDto.rewards = targetEventData.passDto.rewards;          
        this.paramBuffer = pB;
        
        //3. 모두 받기 버튼 활성화 [+프리미엄 패스 유무 버튼 비활성화]
        this.premiumAlreadyActivatedButton.gameObject.SetActive(false);
        this.waitPurchaseButton.gameObject.SetActive(false);
        // this.receiveAllButton.gameObject.SetActive(true);
        
        int receivableCount = eventStorage.GetPassUnReceivedRewardCount(pB.passCode);
        LobbySubMenuButtonView buttonView = this.passIconObj.GetComponent<LobbySubMenuButtonView>();
        if(receivableCount == 0) buttonView.DeActiveNewMark();
        else buttonView.ActiveNewMark(receivableCount);

        this.LoadData(pB);
        this.scroller.ReloadData();
    }

    public void OpenInfoPopup()
    {
        this.helpPopupDimmedObj.SetActive(true);
        
        HelpPopup.Params param = new HelpPopup.Params();
        param.Code = 93;
        
        var data = SBDataSheet.Instance.PopupInfo[param.Code];
        param.isCloseBtnNeed = data.CloseBtnBool;
        param.helpPath = TextureController.GetTexturePath(data.GetSystemResourceByDesImage());
        
        Popup.Load("HelpPopup", param, (popup, result) =>
        {
            if (result.isOnOk) { }
            
            this.helpPopupDimmedObj.SetActive(false);
        });
    }

    public void OnOpenRewardDescriptionModal(List<ItemBox> itemBoxes)
    {
        CoroutineTaskManager.AddTask(_Open(itemBoxes));

        IEnumerator _Open(List<ItemBox> _itemBoxes)
        {
            PassDescriptionModal.Params modalParam = new PassDescriptionModal.Params();
            modalParam.rewardItems = new List<PassDescriptionModal.Item>();
        
            PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
            
            foreach (ItemBox itemBox in _itemBoxes)
            {
                bool isFinished = false;
                
                int iconImageCode = SBDataSheet.Instance.ItemProduction[itemBox.ItemProduction].IconImage;
                string iconImageResAddress = String.Empty;
                if (playerStorage.Locale == "ko")
                {
                    iconImageResAddress = SBDataSheet.Instance.ItemResource[iconImageCode].KoKRAddress;
                }
                else
                {
                    iconImageResAddress = SBDataSheet.Instance.ItemResource[iconImageCode].EnUSAddress;
                }

                string iconFilePath = AssetPathController.PATH_FOLDER_ASSETS + iconImageResAddress;
                WWWFile.DownloadPath downloadPath = new WWWFile.DownloadPath(
                    WWWFile.TYPE.Bytes, 
                    String.Empty, 
                    iconFilePath
                );
        
                TextureController.GetTexture(downloadPath, false, (isSuccess, texture) =>
                {
                    modalParam.rewardItems.Add(
                        new PassDescriptionModal.Item(texture, itemBox.ItemQuantity)
                    );

                    isFinished = true;
                });

                yield return new WaitUntil(() => isFinished);
            }
        
            this.descriptionModal.OnOpen(modalParam);
        }
    }

    public void OnOpenRewardDescriptionModal(int itemCode, int amount)
    {
        CoroutineTaskManager.AddTask(_Open(itemCode, amount));
        
        IEnumerator _Open(int itemCode, int amount)
        {
            PassDescriptionModal.Params modalParam = new PassDescriptionModal.Params();
            modalParam.rewardItems = new List<PassDescriptionModal.Item>();
        
            PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
        
            int iconImageCode = SBDataSheet.Instance.ItemProduction[itemCode].IconImage;
            string iconImageResAddress = String.Empty;
            if (playerStorage.Locale == "ko")
            {
                iconImageResAddress = SBDataSheet.Instance.ItemResource[iconImageCode].KoKRAddress;
            }
            else
            {
                iconImageResAddress = SBDataSheet.Instance.ItemResource[iconImageCode].EnUSAddress;
            }

            string iconFilePath = AssetPathController.PATH_FOLDER_ASSETS + iconImageResAddress;
            WWWFile.DownloadPath downloadPath = new WWWFile.DownloadPath(
                WWWFile.TYPE.Bytes, 
                String.Empty, 
                iconFilePath
            );

            bool isFinished = false;
        
            TextureController.GetTexture(downloadPath, false, (isSuccess, texture) =>
            {
                modalParam.rewardItems.Add(
                    new PassDescriptionModal.Item(texture, amount)
                );
            
                isFinished = true;
            });

            yield return new WaitUntil(() => isFinished);
            this.descriptionModal.OnOpen(modalParam);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Y))
        {
            this.TestBuyPremiumInEditor();
        }
    }

    public void TestBuyPremiumInEditor()
    {
        //PlayerStorage 갱신 [프리미엄 구독 상태로 변경]
        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
        playerStorage.PlayerDto.pass.isPremium = true;
                
        //EventStorage 갱신 [프리미엄 구독 상태로 변경]
        EventStorage eventStorage = GameStorage.Instance.GetStorage<EventStorage>();
        var targetEventData = eventStorage.GetPassEventData(playerStorage.PlayerDto.pass.code);
        targetEventData.passDto.isPremium = true;

        //팝업 ReOpen
        this.ReOpenPopup();
    }

    public void OnClickToBuyPremiumPass()
    {
        var item = GameStorage.StoreStorage.RecommendStoreDisplayList.Find(x => x.GoodsType == 2);
        // var item = GameStorage.StoreStorage.RecommendStoreDisplayList[2];
        if(item == null) return;
        
        PassBuyPopup.Params passBuyPopup = new PassBuyPopup.Params();
        passBuyPopup.Code = 76;
        
        passBuyPopup.isCloseBtnNeed = true;
        
        int passCode = GameStorage.PlayerStorage.PlayerDto.pass.code;
        passBuyPopup.imagePath = TextureController.GetPassMainTexturePathByPassCode(passCode);

        passBuyPopup.timeFunc = () =>
        {
            return SBTime.Instance.ServerTime;
        };
        
        passBuyPopup.data = item;

        passBuyPopup.endTime = item.SellEndAt;
        
        string sku = string.Empty;

        sku = item.Sku;

#if UNITY_EDITOR
        passBuyPopup.price = "￦ " + item.StoreData.InAppPriceKo.ConvertCommaString();
#else
        passBuyPopup.price = (PurchaseController.GetProductInfo(item.Sku) != null) ? PurchaseController.GetProductInfo(sku).CurrencyString : "￦ " + item.StoreData.InAppPriceKo.ConvertCommaString();
#endif
        passBuyPopup.isBuyAvaliable = (GameStorage.PlayerStorage.GetRecommendStoreCounting(item.Code).total == 0);
        passBuyPopup.alreadyActiveText = LocaleController.GetSystemLocale(81);
        passBuyPopup.infomationText = LocaleController.GetSystemLocale(205);// "[청약철회 규정] 구매일로부터 7일이 지났거나 사용 시 청약철회가 불가능합니다. 개봉 / 확률형 아이템은 개봉 즉시 사용한 것으로 간주합니다. 미성년자가 법정 대리인의 동의 없이 결제한 경우, 약관에 의거해 결제를 취소할 수 있습니다. 패키지에 포함된 무제한 아이템은 구매 즉시 사용이 시작됩니다. 판매 가격은 부가세 10 % 가 포함된 가격입니다.";
        
        Popup.Load("PassBuyPopup", passBuyPopup, (pop, result) =>
        {
            this.ReOpenPopup();
        });
        
        //test code
        // this.ReOpenPopup();
        //end test code
    }

    public void ReOpenPopup()
    {
        this.result = new Result();
        this.result.args = true;
        base.OnTriggerX();
    }

    /// <summary>
    /// Storage단에서 보상 획득 이후 갱신
    /// </summary>
    public void RefreshForce(int passCode, int itemIndex, int passRewardCode, AcquiredItemDto acquiredItemDto, bool isPremium)
    {
        SBDebug.Log(string.Format("passcode : {0}, passRewardCode : {1}", passCode, passRewardCode));
        
        //Storage 단에서 Data를 갱신한다. (EventStorage)
        EventStorage eventStorage = GameStorage.Instance.GetStorage<EventStorage>();
        var targetEventData = eventStorage.GetPassEventData(passCode);
        
        //EventStorage 갱신
        if(targetEventData == null) return;
        targetEventData.UpdateReceiveInfo(passRewardCode, isPremium);

        var pB = this.paramBuffer as Params;
        if (pB != null)
        {
            pB.passDto.rewards = targetEventData.passDto.rewards;
            this.paramBuffer = pB;

            int receivableCount = eventStorage.GetPassUnReceivedRewardCount(passCode);
            LobbySubMenuButtonView buttonView = this.passIconObj.GetComponent<LobbySubMenuButtonView>();
            if(receivableCount == 0) buttonView.DeActiveNewMark();
            else buttonView.ActiveNewMark(receivableCount);
            
            this.LoadData(pB);
        }
        
        this.scroller.ReloadData();
        this.scroller.JumpToDataIndex(itemIndex);
    }

    public void RefreshForce(Params pB)
    {
        //Storage 단에서 Data를 갱신한다. (EventStorage)
        EventStorage eventStorage = GameStorage.Instance.GetStorage<EventStorage>();
        var targetEventData = eventStorage.GetPassEventData(pB.passCode);
        
        //EventStorage 갱신
        if(targetEventData == null) return;

        foreach (PassReward passReward in pB.passInfo.GetLevReward())
        {
            //Note. 모두 받기 프로토콜 수행시 서버쪽에서는 1레벨 보상까지 받아짐
            if (pB.passDto.level >= passReward.PassLevel - 1)
            {
                targetEventData.UpdateReceiveInfo(passReward.Code, false);
                if(pB.passDto.isPremium) targetEventData.UpdateReceiveInfo(passReward.Code, true);
            }
        }
        
        pB.passDto.rewards = targetEventData.passDto.rewards;
        this.paramBuffer = pB;
        
        int receivableCount = eventStorage.GetPassUnReceivedRewardCount(pB.passCode);
        LobbySubMenuButtonView buttonView = this.passIconObj.GetComponent<LobbySubMenuButtonView>();
        if(receivableCount == 0) buttonView.DeActiveNewMark();
        else buttonView.ActiveNewMark(receivableCount);

        this.TogglePremiumButton(pB.passDto);
        
        this.LoadData(pB);
        this.scroller.ReloadData();
    }

    private void OnDestroy()
    {
        if(this.showUITask != null) 
            CoroutineTaskManager.RemoveTask(this.showUITask);
    }
}
