using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using com.snowballs.SWHJ.client.model;
using com.snowballs.SWHJ.client.view;
using com.snowballs.SWHJ.type;
using Snowballs.Network.API;
using Snowballs.Network.Dto;
using Snowballs.Sheets;
using Snowballs.Sheets.Data;
using Snowballs.Util;

using EnhancedUI.EnhancedScroller;
using UnityEditor;
using com.snowballs.SWHJ.presenter;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Tls.Crypto;

public class PassItemView : EnhancedScrollerCellView
{
    [SerializeField] private Image normalIcon, premiumIcon;

    [SerializeField] private TextMeshProUGUI normalAmountText, premiumAmountText;
    [SerializeField] private TextMeshProUGUI levelText;

    [SerializeField] private GameObject bgOn;
    [SerializeField] private GameObject bgOff;
    [SerializeField] private Image bgOnImage;
    [SerializeField] private Image bgOffImage;
    [SerializeField] private Image infiniteGoldAreaImage;

    [SerializeField] private GameObject premiumUnlockStateObj, premiumLockStateObj;

    [SerializeField] private GameObject premiumReceivedStateObj, premiumUnreceivedStateObj;
    [SerializeField] private GameObject normalReceivedStateObj, normalUnreceivedStateObj;
    [SerializeField] private GameObject normalLockStateObj;

    [SerializeField] private Button normalReceiveButton, premiumReceiveButton,normalRequestButton,premiumRequestButton;
    [SerializeField] private TextMeshProUGUI normalReceiveButtonText, premiumReceiveButtonText;

    [SerializeField] private Image gauge;
    [SerializeField] private Image infiniteRewardIcon;

    [SerializeField] private GameObject rewardArea, infiniteGoldArea;
    [SerializeField] private Slider goldGauge;
    [SerializeField] private GameObject goldReceiveButtonObj;
    [SerializeField] private GameObject goldLockObj;
    [SerializeField] private TextMeshProUGUI goldDescriptionText;
    [SerializeField] private TextMeshProUGUI gaugeLabel;
    [SerializeField] private TextMeshProUGUI goldReceiveButtonText;

    [SerializeField] private Material grayScale;

    private STATE normalRewardState;        //현재 일반 보상 버튼 상태
    private STATE premiumRewardState;       //프리미엄 보상 버튼 상태

    private PassDto passDto;
    private PassInfo passInfo;
    private PassRewardDto passRewardDto;
    private PassReward passReward;

    private PassPopup passPopup;

    private int itemIndex;
    private int passCode;

    private int maxGoldValue;

    private Texture2D goldInfiniteTexture;
    private Action onClickGoldReceive;
    public class Params
    {
        public string focusedTextureFilePath;
        public string goldAreaTextureFilePath;

        public int passCode;

        public PassPopup passPopup;
        public int itemIndex;               //현재 Item의 Index 값

        public PassDto passDto;
        public PassInfo passInfo;
        public int maxGoldValue;

        public PassRewardDto passRewardDto;
        public PassReward passReward;

        public int currentPassLevel;

        public Action onClickGoldReceive;

        public int lastIndex;
    }

    private bool isGoldTextureInit = false;

    public void SetData(Params data)
    {
        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();

        LocaleParam localeParam = new LocaleParam();
        localeParam.receiveButtonLocale = 10004;
        localeParam.playerLocale = playerStorage.Locale;
        this.SetLocales(localeParam);

        RectTransform rect = GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(Screen.width, 300);

        this.passDto = data.passDto;
        this.passInfo = data.passInfo;
        this.passRewardDto = data.passRewardDto;
        this.passReward = data.passReward;

        this.levelText.text = data.itemIndex.ToString();
        this.itemIndex = data.itemIndex;
        this.passPopup = data.passPopup;
        this.passCode = data.passCode;
        this.maxGoldValue = data.maxGoldValue;

        //마지막 아이템 [골드 보상 영역]
        if (data.itemIndex > data.lastIndex)
        {
            this.infiniteGoldArea.gameObject.SetActive(true);
            this.rewardArea.gameObject.SetActive(false);
            

            if (!isGoldTextureInit)
            {
                Image image = this.infiniteGoldAreaImage.GetComponent<Image>();
                image.sprite = Sprite.Create(
                    this.passPopup.goldInfiniteTexture,
                    new Rect(0.0f, 0.0f, this.passPopup.goldInfiniteTexture.width, this.passPopup.goldInfiniteTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f
                );

                image.SetNativeSize();

                this.infiniteRewardIcon.sprite = Sprite.Create(
                    this.passPopup.infiniteRewardIconTexture,
                    new Rect(0.0f, 0.0f, this.passPopup.infiniteRewardIconTexture.width, this.passPopup.infiniteRewardIconTexture.height),
                    new Vector2(0.5f,0.5f)
                );

                isGoldTextureInit = true;
            }

            this.onClickGoldReceive = data.onClickGoldReceive;
            this.InitBonusGold(data.passDto, data.maxGoldValue, data.passInfo);
        }
        else
        {
            this.infiniteGoldArea.gameObject.SetActive(false);
            this.rewardArea.gameObject.SetActive(true);

            //focus 되었을 때 배경 텍스쳐 세팅
            {
                WWWFile.DownloadPath downloadPath =
                    new WWWFile.DownloadPath(WWWFile.TYPE.Bytes, String.Empty, data.focusedTextureFilePath);

                TextureController.GetTexture(downloadPath, false, (isSuccess, texture) =>
                {
                    this.bgOn.GetComponent<Image>().sprite = Sprite.Create(
                        texture,
                        new Rect(0.0f, 0.0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f
                    );

                    RectTransform bgOnRect = this.bgOn.GetComponent<RectTransform>();
                    bgOnRect.sizeDelta = new Vector2(texture.width, bgOnRect.sizeDelta.y);
                });
            }

            //노말 보상 아이콘 세팅
            {
                int normalRewardItemCode = this.passReward.NormalReward;
                int normalRewardIconImageCode = SBDataSheet.Instance.ItemProduction[normalRewardItemCode].IconImage;
                string normalRewardIconAddress = String.Empty;
                if (playerStorage.Locale == "ko")
                {
                    normalRewardIconAddress = SBDataSheet.Instance.ItemResource[normalRewardIconImageCode].KoKRAddress;
                }
                else
                {
                    normalRewardIconAddress = SBDataSheet.Instance.ItemResource[normalRewardIconImageCode].EnUSAddress;
                }

                string normalIconFilePath = AssetPathController.PATH_FOLDER_ASSETS + normalRewardIconAddress;


                WWWFile.DownloadPath downloadPath =
                    new WWWFile.DownloadPath(WWWFile.TYPE.Bytes, String.Empty, normalIconFilePath);
                TextureController.GetTexture(downloadPath, false, (isSuccess, texture) =>
                {
                    this.normalIcon.sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f
                    );
                });

                this.normalAmountText.text = "x" + this.passReward.NormalRewardValue.ToString();
            }

            //프리미엄 보상 아이콘 세팅
            {
                int premiumRewardItemCode = this.passReward.PremiumReward;
                int premiumRewardIconImageCode = SBDataSheet.Instance.ItemProduction[premiumRewardItemCode].IconImage;
                string premiumRewardIconAddress = String.Empty;
                if (playerStorage.Locale == "ko")
                {
                    premiumRewardIconAddress = SBDataSheet.Instance.ItemResource[premiumRewardIconImageCode].KoKRAddress;
                }
                else
                {
                    premiumRewardIconAddress = SBDataSheet.Instance.ItemResource[premiumRewardIconImageCode].EnUSAddress;
                }

                string premiumRewardIconFilePath = AssetPathController.PATH_FOLDER_ASSETS + premiumRewardIconAddress;

                WWWFile.DownloadPath downloadPath =
                    new WWWFile.DownloadPath(WWWFile.TYPE.Bytes, String.Empty, premiumRewardIconFilePath);
                TextureController.GetTexture(downloadPath, false, (isSuccess, texture) =>
                {
                    this.premiumIcon.sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f
                    );
                });

                this.premiumAmountText.text = "x" + this.passReward.PremiumRewardValue.ToString();
            }

            if (data.itemIndex == 0)
            {
                this.ToggleMiddleGauge(true);
            }
            else
            {
                this.ToggleMiddleGauge(data.itemIndex <= data.currentPassLevel);
            }

            //프리미엄 보상 Lock/Unlock 유무
            bool isPremium = playerStorage.PlayerDto.pass.isPremium;
            Debug.Log("SJW PlayerDto IsPremium : " + isPremium);
            //test code
            // isPremium = true;
            //end test code

            //프리미엄 구매 여부에 따라 Lock/Unlock 처리
            this.premiumLockStateObj.SetActive(!isPremium);
            this.premiumUnlockStateObj.SetActive(isPremium);

            //다음 목표 UI 포커싱
            if (data.currentPassLevel == 0)
            {
                if(data.itemIndex == 1) this.bgOn.SetActive(true);
                else this.bgOn.SetActive(false);
            }
            else
            {
                this.bgOn.SetActive(data.itemIndex == data.currentPassLevel + 1);
            }

            var isRewardInfoExist = this.passDto.rewards.ToList()
                .Find(x => x.code == this.passReward.Code);
            
            SBDebug.Log(string.Format(
                "item index : {0} isRewardInfoExist : {1}", 
                this.itemIndex, 
                isRewardInfoExist != null
                )
            );
            
            //보상을 받지 않은 상태
            if (isRewardInfoExist == null)
            {
                //수령 가능 레벨 도달한 경우
                if (data.itemIndex <= data.currentPassLevel)
                {
                    this.SetNormalRewardsState(STATE.UnReceived);

                    //프리미엄 구독중인 경우
                    if (isPremium)
                    {
                        this.SetPremiumRewardsState(STATE.UnReceived);
                    }
                    //프리미엄 구동중이 아닌경우
                    else
                    {
                        if (SBDataSheet.Instance.ItemProduction[this.passReward.PremiumReward].ItemType == 3)
                        {
                            this.premiumReceiveButton.onClick.RemoveAllListeners();
                            this.premiumReceiveButton.onClick.AddListener(
                                () => this.OnClickToDescriptionModal(false)
                            );
                        }
                        else
                        {
                            this.premiumReceiveButton.onClick.RemoveAllListeners();
                            this.premiumReceiveButton.onClick.AddListener(() =>
                            {
                                this.passPopup.ToastMessage(22);
                            });
                        }
                    }
                }
                //수령 불가능한 레벨
                else
                {
                    this.SetNormalRewardsState(STATE.None);

                    //프리미엄 구독중인 경우
                    if (isPremium)
                    {
                        this.SetPremiumRewardsState(STATE.None);
                    }
                    //프리미엄 구독중이 아닌경우
                    else
                    {
                        this.premiumReceiveButton.onClick.RemoveAllListeners();
                        this.premiumReceiveButton.onClick.AddListener(
                            () => this.OnClickToDescriptionModal(true)
                        );
                    }
                }
            }
            //보상 무언가 받음
            else
            {
                //노말 받은 경우
                if (this.passRewardDto.normal)
                {
                    this.SetNormalRewardsState(STATE.Received);
                }
                else
                {
                    this.SetNormalRewardsState(STATE.UnReceived);
                }
                
                //받은 상태인 경우
                if (this.passRewardDto.premium)
                {
                    this.SetPremiumRewardsState(STATE.Received);
                }
                else
                {
                    this.SetPremiumRewardsState(STATE.UnReceived);
                }
                
                //프리미엄 구매 여부에 따라 Lock/Unlock 처리
                this.premiumLockStateObj.SetActive(!isPremium);
                this.premiumUnlockStateObj.SetActive(isPremium);

                if (!isPremium)
                {
                    this.SetPremiumRewardsState(STATE.None);
                }
            }
        }
    }

    private void Reset(bool isPremium)
    {
        if (isPremium)
        {
            this.premiumReceivedStateObj.SetActive(false);
            this.premiumUnreceivedStateObj.SetActive(false);
        }
        else
        {
            this.normalReceivedStateObj.SetActive(false);
            this.normalUnreceivedStateObj.SetActive(false);
        }
    }

    private void ToggleMiddleGauge(bool isOn)
    {
        if (isOn)
        {
            this.gauge.GetComponent<Image>().fillAmount = 1;
        }
        else
        {
            this.gauge.GetComponent<Image>().fillAmount = 0;
        }
    }

    private void SetPremiumRewardsState(STATE state)
    {
        this.Reset(true);
        this.premiumReceiveButton.onClick.RemoveAllListeners();
        switch (state)
        {
            case STATE.None:
                if (SBDataSheet.Instance.ItemProduction[this.passReward.PremiumReward]
                    .GetBoxBundle() == null)
                {
                    this.premiumLockStateObj.SetActive(true);
                    this.premiumReceiveButton.onClick
                        .AddListener(() => this.passPopup.ToastMessage(21));
                }
                else
                {
                    this.premiumReceiveButton.onClick
                        .AddListener(() => this.OnClickToDescriptionModal(true));
                    this.premiumLockStateObj.SetActive(true);
                }
                break;
            case STATE.Received:
                this.premiumLockStateObj.SetActive(false);
                this.premiumReceiveButton.onClick
                    .AddListener(() => this.passPopup.ToastMessage(23));
                this.premiumReceivedStateObj.SetActive(true);
                break;
            case STATE.UnReceived:
                if (SBDataSheet.Instance.ItemProduction[this.passReward.PremiumReward]
                    .GetBoxBundle() != null)
                {
                    this.premiumReceiveButton.onClick
                    .AddListener(() => this.OnClickToDescriptionModal(true));
                }
                break;

        }

        if (state.Equals(STATE.UnReceived))
        { 
            this.premiumLockStateObj.SetActive(false);
            this.premiumRequestButton.onClick
                .AddListener(() => this.OnClickRequestRewardButton(true));
            this.premiumUnreceivedStateObj.SetActive(true);
        }
    }

    private void SetNormalRewardsState(STATE state)
    {
        // if (this.passRewardDto != null)
        // {
        //     SBDebug.Log(string.Format("this.passRewardDto.code : {0}",this.passRewardDto.code));
        //     SBDebug.Log(string.Format("this.passRewardDto.normal : {0}",this.passRewardDto.normal));
        //     SBDebug.Log(string.Format("this.passRewardDto.premium : {0}",this.passRewardDto.premium));
        // }

        this.Reset(false);
        this.normalReceiveButton.onClick.RemoveAllListeners();
        switch (state)
        {
            case STATE.None:
                if (SBDataSheet.Instance.ItemProduction[this.passReward.NormalReward]
                    .GetBoxBundle() == null)
                {
                    this.normalLockStateObj.SetActive(true);
                    this.normalReceiveButton.onClick
                    .AddListener(() => this.passPopup.ToastMessage(21));
                }
                else
                {
                    this.normalReceiveButton.onClick
                    .AddListener(() => this.OnClickToDescriptionModal(false));
                    this.normalLockStateObj.SetActive(true);
                }
                break;
            case STATE.Received:
                this.normalReceivedStateObj.SetActive(true);
                this.normalReceiveButton.onClick
                    .AddListener(() => this.passPopup.ToastMessage(23));
                this.normalLockStateObj.SetActive(false);
                break;
            case STATE.UnReceived:
                if (SBDataSheet.Instance.ItemProduction[this.passReward.PremiumReward]
                    .GetBoxBundle() != null)
                {
                    this.normalReceiveButton.onClick
                    .AddListener(() => this.OnClickToDescriptionModal(false));
                }
                break;
                ////받을 수는 있지만 아직 받지 않은 상태
                //case STATE.UnReceived:
                //    this.normalUnreceivedStateObj.SetActive(true);
                //    this.normalReceiveButton.onClick
                //        .AddListener(() => this.OnClickRequestRewardButton(false));
                //    this.normalLockStateObj.SetActive(false);
                //    break;
        }

        if(state.Equals(STATE.UnReceived))
        {
            this.normalUnreceivedStateObj.SetActive(true);
            this.normalRequestButton.onClick
                .AddListener(() => this.OnClickRequestRewardButton(false));
            this.normalLockStateObj.SetActive(false);
        }
    }

    private bool isAlreadyClicked = false;
    public void OnClickRequestRewardButton(bool isPremium)
    {
        if(isAlreadyClicked) return;
        isAlreadyClicked = true;
        
        if (isPremium)
        {
            if (this.premiumLockStateObj.activeSelf)
            {
                this.passPopup.ToastMessage(22);
                SBDebug.Log("Premium 이 lock 상태입니다!");
                return;
            }
        }

        //요청 완료시 toast message를 띄운다
        //Received 상태로 변경한다.
        PassReceiveDto passReceiveDto = new PassReceiveDto(this.passCode, false);
        passReceiveDto.rewardCode = this.passReward.Code;
        passReceiveDto.isPremium = isPremium;

        RequestDto<PassReceiveDto> requestDto =
            new RequestDto<PassReceiveDto>(
                0,
                passReceiveDto,
                Guid.NewGuid().ToString(),
                SBTime.Instance.ServerTime.ToString()
            );

        GamePass.Receive(requestDto, (responseDto) =>
        {
            if (responseDto.code == (int)ResponseCode.OK)
            {
                //GameHistory Ack 체크
                int passAckCode = this.passCode;        //해당 Pass의 시트코드
                HistoryDto dto = new HistoryDto("PassInfo", passAckCode);
                RequestDto<HistoryDto> historyReqDto = new RequestDto<HistoryDto>(0, dto, Guid.NewGuid().ToString(), SBTime.Instance.ISOServerTime);
                var networkManager = GameScene.Instance.NetworkManager;
                networkManager.Ack(historyReqDto, ackResponse =>
                {
                    GameStorage.PlayerStorage.PlayerDto.pass.canReceiveBonus = false;
                    if (ackResponse != null && (ResponseCode)ackResponse.code == ResponseCode.OK)
                    {
                        SBDebug.Log("<color=green>수령 Ack 완료</color>");
                    }
                    else
                    {
                        SBDebug.Log("<color=red>수령 Ack 실패</color>");
                    }
                });

                this.passPopup.ToastMessage(82);
                this.passPopup.RefreshForce(this.passCode,  this.itemIndex, this.passReward.Code, responseDto.data, isPremium);

                //[단일 보상]프로필, 카드, 매거진
                {
                    if (responseDto.data.acquiredItems != null && responseDto.data.acquiredItems.Length > 0)
                    {
                        int itemCode = responseDto.data.acquiredItems[0].code;
                        if ((ItemDataItemType)SBDataSheet.Instance.ItemProduction[itemCode].ItemType ==
                            ItemDataItemType.Card)
                        {
                            this.passPopup.HelpPopupDimmed.SetActive(true);
                            ViewController.OpenRewardPopup(responseDto.data, () =>
                            {
                                this.passPopup.HelpPopupDimmed.SetActive(false);
                            });
                        }
                        else if ((ItemDataItemType)SBDataSheet.Instance.ItemProduction[itemCode].ItemType ==
                                 ItemDataItemType.Profile)
                        {
                            this.passPopup.HelpPopupDimmed.SetActive(true);
                            ViewController.OpenRewardPopup(responseDto.data, () => {
                                this.passPopup.HelpPopupDimmed.SetActive(false);
                            });
                        }
                        else if ((ItemDataItemType)SBDataSheet.Instance.ItemProduction[itemCode].ItemType ==
                                 ItemDataItemType.SpecialMagazinePicture)
                        {
                            this.passPopup.HelpPopupDimmed.SetActive(true);
                            ViewController.OpenRewardPopup(responseDto.data, () =>
                            {
                                this.passPopup.HelpPopupDimmed.SetActive(false);
                            });
                        }

                        ItemStorage itemStorage = GameStorage.Instance.GetStorage<ItemStorage>();
                        itemStorage.GetReward(responseDto.data);
                    }
                }
                //[박스 보상]
                {
                    bool isBoxItem = responseDto.data.acquiredItems != null && responseDto.data.acquiredItems.ToList().Exists(x => x.parent > 0);
                    if (isBoxItem)
                    {
                        this.passPopup.HelpPopupDimmed.SetActive(true);
                        ViewController.OpenRewardPopup(responseDto.data, () => {
                            this.passPopup.HelpPopupDimmed.SetActive(false);
                        });
                    }
                }
            }
            else
            {
                SBDebug.Log("<color=red>수령 요청 실패</color>");
            }

            isAlreadyClicked = false;
        });
    }
    
    //보상을 받을 수 없는 상태인 경우, 보상 내용 보기 모달을 띄운다.
    public void OnClickToDescriptionModal(bool isPremium)
    {
        SBDebug.Log("OnClickToDescriptionModal " + isPremium);

        if (isPremium)
        {
            if (SBDataSheet.Instance.ItemProduction[this.passReward.PremiumReward].ItemType != 3)
            {
                PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
                bool isPremiumUser = playerStorage.PlayerDto.pass.isPremium;
                if(!isPremiumUser) this.passPopup.ToastMessage(22);     //프리미엄 구독상태가 아닌 경우
                else this.passPopup.ToastMessage(21);                   //프리미엄 구독상태인 경우

                // passPopup.OnOpenRewardDescriptionModal(
                //     this.passReward.PremiumReward,
                //     this.passReward.PremiumRewardValue
                // );
            }
            else
            {
                var targetItem = SBDataSheet.Instance.ItemProduction.Values.ToList().Find(x => x.Code == this.passReward.PremiumReward);
                if (targetItem != null)
                {
                    var boxBundle = SBDataSheet.Instance.ItemProduction[this.passReward.PremiumReward]
                    .GetBoxBundle();
                    if (boxBundle != null)
                    {
                        passPopup.OnOpenRewardDescriptionModal(boxBundle);
                    }
                    else
                    {
                        passPopup.OnOpenRewardDescriptionModal(
                            this.passReward.PremiumReward,
                            this.passReward.PremiumRewardValue
                        );
                    }
                }
            }
        }
        else
        {
            if (SBDataSheet.Instance.ItemProduction[this.passReward.NormalReward].ItemType != 3)
            {
                this.passPopup.ToastMessage(21);
                // passPopup.OnOpenRewardDescriptionModal(
                //     this.passReward.NormalReward,
                //     this.passReward.NormalRewardValue
                // );
            }
            //박스 아이콘을 클릭한 경우 (5000 코드) 박스의 Bundle 아이템들을 보여줘야 한다.
            else
            {
                var targetItem = SBDataSheet.Instance.ItemProduction.Values.ToList().Find(x => x.Code == this.passReward.NormalReward);

                if (targetItem != null)
                {
                    var boxBundle = SBDataSheet.Instance.ItemProduction[this.passReward.NormalReward]
                        .GetBoxBundle();
                    if (boxBundle != null)
                    {
                        passPopup.OnOpenRewardDescriptionModal(boxBundle);
                    }
                    else
                    {
                        passPopup.OnOpenRewardDescriptionModal(
                            this.passReward.NormalReward,
                            this.passReward.NormalRewardValue
                        );
                    }
                }
            }
        }
    }

    public void InitBonusGold(PassDto passDto, int maxGoldValue, PassInfo passInfo)
    {
        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
        bool isPremium = playerStorage.PlayerDto.pass.isPremium;
        // this.passPopup.levelObj.gameObject.SetActive(false);

        //프리미엄 미구독인 경우 Lock 처리
        if (!isPremium)
        {
            this.goldLockObj.SetActive(true);
            this.goldReceiveButtonObj.SetActive(true);
            this.goldReceiveButtonObj.GetComponent<Image>().material = this.grayScale;
            this.goldReceiveButtonObj.GetComponent<Button>().interactable = false;
        }
        //프리미엄 구독중인 경우
        else
        {
            this.goldLockObj.SetActive(false);
            this.goldReceiveButtonObj.SetActive(true);

            //케이스 1. 인게임에서만 passDto가 변동된다. [PassPopup.cs 850 Line 주석 참고]
            if (passDto.canReceiveBonus && (passDto.bonus > 0))
            {
                this.goldReceiveButtonObj.GetComponent<Image>().material = null;
                this.goldReceiveButtonObj.GetComponent<Button>().interactable = true;
            }
            else
            {
                this.goldReceiveButtonObj.GetComponent<Image>().material = this.grayScale;
                this.goldReceiveButtonObj.GetComponent<Button>().interactable = false;
            }
        }

        this.goldGauge.maxValue = maxGoldValue;
        
        //골드 보너스까지 모두 달성한 경우 상단 게이지 MAX로 표가
        int midRes = passDto.bonus;
        if (midRes >= maxGoldValue)
        {
            var localeCode = 4;
            this.passPopup.topGaugeValueText.text = LocaleController.GetEventLocale(localeCode);
            
            this.passPopup.topGauge.value = 1;

            if (passDto.isPremium && this.passDto.canReceiveBonus)
            {
                this.goldReceiveButtonObj.GetComponent<Image>().material = null;
                this.goldReceiveButtonObj.GetComponent<Button>().interactable = true;
            }
        }
        this.goldGauge.value = midRes;
        this.gaugeLabel.text = midRes + " / " + this.goldGauge.maxValue;

        var goldDescriptLocale = 2;
        this.goldDescriptionText.text = LocaleController.GetEventLocale(goldDescriptLocale);
    }

    public void OnClickGoldReceiveButton()
    {
        EventStorage eventStorage = GameStorage.Instance.GetStorage<EventStorage>();
        var passEventData = eventStorage.GetPassEventData(this.passCode);

        CodeDto codeDto = new CodeDto(this.passCode);
        RequestDto<CodeDto> requestDto = new RequestDto<CodeDto>(
            0,
            codeDto,
            Guid.NewGuid().ToString(),
            SBTime.Instance.ISOServerTime
        );

        GamePass.Bonus(requestDto, responseDto =>
        {
            if (responseDto.code == (int)ResponseCode.OK)
            {
                HistoryDto dto = new HistoryDto("PassInfo", passCode);
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
                
                    ViewController.OpenRewardPopup(responseDto.data, () =>
                    {
                        this.onClickGoldReceive?.Invoke();

                        //TODO 골드 게이지 갱신
                        // passEventData.passDto.bonus = 0;
                        passEventData.passDto.canReceiveBonus = false;
                        passEventData.passDto.isBonusReceived = true;
                        this.InitBonusGold(this.passDto, this.maxGoldValue, this.passInfo);
                    });
                    
                    ItemStorage itemStorage = GameStorage.Instance.GetStorage<ItemStorage>();
                    itemStorage.GetReward(responseDto.data);
                });
            }
            
            isAlreadyClicked = false;
        });
    }

    public class LocaleParam
    {
        public string playerLocale;
        public Int32? receiveButtonLocale;
    }

    private void SetLocales(LocaleParam p)
    {
        if (p.receiveButtonLocale.HasValue)
        {
            if (p.playerLocale == "ko")
            {
                string localeText = SBDataSheet.Instance.SystemLocale[p.receiveButtonLocale.Value].KoKR;
                this.normalReceiveButtonText.text = localeText;
                this.premiumReceiveButtonText.text = localeText;
                this.goldReceiveButtonText.text = localeText;
            }
            else
            {
                string localeText = SBDataSheet.Instance.SystemLocale[p.receiveButtonLocale.Value].EnUS;
                this.normalReceiveButtonText.text = localeText;
                this.premiumReceiveButtonText.text = localeText;
                this.goldReceiveButtonText.text = localeText;
            }
        }
    }

    public enum STATE
    {
        None,
        Received,
        UnReceived
    }
}
