![image](https://github.com/user-attachments/assets/9addb6f2-4a92-4d04-94f8-09459f75dcf6)

*최상단 View 설계[Popup.cs]*
> 1. 확인/취소/닫기 버튼에 대한 기본 기능을 Virtual 함수 형태로 구현하여, 하위 클래스에서 세부 구현을 하여 사용하도록 함   
> 2. Controller에게 팝업에서의 사용자 이벤트를 전달하기 위한 Delegate를 지정 [OnResultCallback, OnLoadedCallback]   
> 3. View에 필요한 Model 정보를 Params 인스턴스 형태로 활성화 하면서 Controller가 전달하게 된다.   

*실제 구현된 팝업 종류 일부*   
![image](https://github.com/user-attachments/assets/b076b255-f612-4526-8139-108b4e42111f)

*최상단 Popup 클래스*
<pre>
  <code>
using System;
using System.Collections.Generic;
using Snowballs.Client.View;
using DG.Tweening;
using Snowballs.Client.View;
using Snowballs.Sheets;
using Snowballs.Sheets.Data;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Video;

public class Popup : View
{

    #region const var
    private const string basePath = "Prefabs/Popup/";
    #endregion

    public delegate void OnResultCallback(Popup popup, Result result);
    public OnResultCallback ResultCallback { protected set; get; }

    public delegate void OnLoadedCallback(Popup thiz);
    public OnLoadedCallback LoadedCallback { protected set; get; }

    #region protected var
    protected Result result;
    protected Params paramBuffer = null;
    public ManageParams manageBuffer;

    protected PopupInfo popupInfo;
    protected List<PopupDes> popupDescList;
    protected Dictionary<Int32, SystemLocale> systemLocDict;
    protected SystemLocale headerLocale;

    //상단 재화 정보 세팅 (어떻게 보여줄 것인가)
    protected GoodsView.ViewMode viewMode = GoodsView.ViewMode.None;

    [SerializeField] protected Button confirmBtn;
    [SerializeField] protected Button cancelBtn;
    [SerializeField] protected Button closeBtn;

    [SerializeField] protected GameObject contextObj;
    [SerializeField] protected GameObject sideContextObj;

    [SerializeField] protected TextMeshProUGUI headerText;
    [SerializeField] protected TextMeshProUGUI context;
    [SerializeField] protected TextMeshProUGUI sideContext;
    [SerializeField] protected TextMeshProUGUI yesBtnText, noBtnText;
    #endregion

    public GoodsView.ViewMode GetViewMode()
    {
        return this.viewMode;
    }

    protected virtual void Awake()
    {
        this.result = new Result();
        this.manageBuffer = new ManageParams();
    }

    #region Inner Classs

    public class Params
    {
        public Int32 Code;  //팝업 코드
        public string[] headerArgs;                // Header 문자열에 {0}.. 내용이 있을경우 채워줘야할 문자를 정의.

        public Int32? contextLocaleCode;        // Context 코드. (팝업 내용)
        public string[] contextArgs;                // Context 문자열에 {0}.. 내용이 있을경우 채워줘야할 문자를 정의.

        public Int32? sideContextLocaleCode; // SideContext 코드. (팝업 하위 내용)
        public string[] sideContextArgs;          // sideContext 문자열에 {0}.. 내용이 있을경우 채워줘야할 문자를 정의.

        public Int32? yesBtnLocaleCode;        //  Yes 버튼 텍스트 코드.
        public Int32? noBtnLocaleCode;         //  No 버튼 텍스트 코드.

        public bool isCloseBtnNeed;              //  X 버튼.

        public string dummyHeaderText = String.Empty;       //상단 제목 더미
        public string dummyContext = String.Empty;            //메인 설명 더미
        public string dummySideContext = String.Empty;      //하단에 쪼그만 설명 더미
        public string dummyYesBtnContext = String.Empty;      //하단에 쪼그만 설명 더미
        public string dummyNoBtnContext = String.Empty;      //하단에 쪼그만 설명 더미

        public bool isLockBackButton = false;

        public BackButtonType backButtonType = BackButtonType.Cancel;
    }

    public enum BackButtonType
    {
        Cancel,
        Ok,
    }

    public class Result
    {
        public bool isOnOk;
        public bool isOnX;
        public bool needSound = true;

        public object args;

        public virtual void Clear()
        {
            this.isOnOk = false;
            this.isOnX = false;
        }
    }
    #endregion
    public class ManageParams
    {
        public GameObject esc;
        public GameObject equip;
        public GameObject equipLock;

        public bool isLocked;
        public bool isEquipped;
    }

    /// <summary>
    /// Entry Point. 팝업을 생성한다.
    /// </summary>
    /// <param name="popupName">팝업 프리팹 이름</param>
    /// <param name="parms">팝업 관련 파라미터</param>
    /// <param name="callback">팝업 완료 콜백</param>
    /// <returns></returns>
    public static Popup Load(string popupName, Params parms, OnResultCallback callback = null, OnLoadedCallback loadedCallback = null, bool isGoodsViewHide = true)
    {
        if (PopupRoot.Instance == null)
        {
            return null;
        }

        string path = basePath + popupName;
        Popup prefab = Resources.Load<Popup>(path);
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = (GameObject)GameObject.Instantiate(prefab.gameObject, PopupRoot.Instance.popupInstantiateRoot, true);
        RectTransform rect = instance.GetComponent<RectTransform>();

        instance.transform.localPosition = Vector3.zero;

        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(0, 0);
        rect.offsetMax = new Vector2(0, 0);
 

        //var doTweenAnim = instance.GetComponent<DOTweenAnimation>();
        var doTweenAnim = instance.GetComponent<SequenceTest>();
        if (doTweenAnim != null)        
        {
            //instance.transform.localScale = Vector3.zero;
            //doTweenAnim.DOPlay();
            doTweenAnim.Show();            
            //doTweenAnim.DOLocalMoveY();                             
            //doTweenAnim.DOLocalMoveX();
        }
        else instance.transform.localScale = Vector3.one;
        
        if (null == instance)
        {
            UnityEngine.Object.Destroy(prefab);
            return null;
        }

        Popup popup = instance.GetComponent<Popup>();
        if (popup == null)
        {
            GameObject.Destroy(instance);
            UnityEngine.Object.Destroy(prefab);
        }

        PopupRoot.Instance.cam.enabled = true;

        PopupRoot.Instance.AddPopup(popup);
        popup.Open(parms, callback, loadedCallback);
        popup.OnOpen();
        PopupRoot.Instance.RefreshGoodsView();
        
        return popup;
    }

    private void Open(Params parm, OnResultCallback resultCallback, OnLoadedCallback loadedCallback)
    {
        this.gameObject.layer = 7;  //GameSceneLayer
        
        this.paramBuffer = parm;

        if ((SBDataSheet.Instance != null) && (SBDataSheet.Instance.PopupInfo != null))
        {
            this.popupInfo = SBDataSheet.Instance
                .PopupInfo[this.paramBuffer.Code];
            this.popupDescList = popupInfo?.GetPopUpLocale();
            this.headerLocale = popupInfo?.GetSystemLocaleByTitleLocale();
            this.systemLocDict = new Dictionary<int, SystemLocale>();
            if (this.popupDescList != null)
            {
                foreach (PopupDes popupDes in this.popupDescList)
                {
                    SystemLocale systemLocale = popupDes.GetSystemLocaleByDesLocale();
                    this.systemLocDict.Add(systemLocale.Code, systemLocale);
                }
            }
        }

        this.ResultCallback = resultCallback;
        this.LoadedCallback = loadedCallback;
    }

    public virtual void OnOpen()
    {
        GoodsView.Instance.MailBox.SetActive(false);
        
        var param = this.paramBuffer;


        string localeHeaderText = this.GetLocaleHeaderText();

        if (this.headerText)
        {
            this.headerText.text = !string.IsNullOrEmpty(localeHeaderText)
                ? (param.headerArgs != null && param.headerArgs.Length > 0) ? string.Format(localeHeaderText, param.headerArgs) : localeHeaderText
                : param.dummyHeaderText;
        }

        if (this.context)
        {
            this.context.text = param.contextLocaleCode.HasValue
                    ? (param.contextArgs != null && param.contextArgs.Length > 0) ? string.Format(this.GetSystemLocale(param.contextLocaleCode.Value), param.contextArgs) : this.GetSystemLocale(param.contextLocaleCode.Value)
                    : param.dummyContext;
        }


        if (!string.IsNullOrEmpty(param.dummyYesBtnContext))
        {
            if (this.yesBtnText)
                this.yesBtnText.text = param.dummyYesBtnContext;
        }

        if (param.yesBtnLocaleCode.HasValue)
        {
            if (this.yesBtnText)
            {
                this.yesBtnText.text = this.GetSystemLocale(param.yesBtnLocaleCode.Value);

                if (String.IsNullOrEmpty(this.yesBtnText.text))
                    this.yesBtnText.text = LocaleController.GetSystemLocale(param.yesBtnLocaleCode.Value);
            }
        }


        if (!string.IsNullOrEmpty(param.dummyNoBtnContext))
        {
            if (this.noBtnText)
                this.noBtnText.text = param.dummyNoBtnContext;
        }
        if (param.noBtnLocaleCode.HasValue)
        {
            if (this.noBtnText)
            {
                this.noBtnText.text = this.GetSystemLocale(param.noBtnLocaleCode.Value);

                if (String.IsNullOrEmpty(this.noBtnText.text))
                    this.noBtnText.text = LocaleController.GetSystemLocale(param.noBtnLocaleCode.Value);
            }
        }


        if (param.sideContextLocaleCode.HasValue)
        {
            if(this.sideContextObj)
                this.sideContextObj.SetActive(true);

            if (this.sideContext)
            {
                this.sideContext.text = (param.sideContextArgs != null && param.sideContextArgs.Length > 0) ? string.Format(this.GetSystemLocale(param.sideContextLocaleCode.Value), param.sideContextArgs) : this.GetSystemLocale(param.sideContextLocaleCode.Value);

                if (String.IsNullOrEmpty(this.sideContext.text))
                    this.sideContext.text = LocaleController.GetSystemLocale(param.sideContextLocaleCode.Value);
            }

        }
        else
        {
            if (!string.IsNullOrEmpty(param.dummySideContext))
            {
                if (this.sideContextObj)
                    this.sideContextObj.SetActive(true);

                if (this.sideContext)
                    this.sideContext.text = param.dummySideContext;
            }
            else
            {
                if (this.sideContextObj)
                    this.sideContextObj.SetActive(false);
            }
        }

        if (this.closeBtn)
            this.closeBtn.gameObject.SetActive(param.isCloseBtnNeed);
    }

    #region Inspector connect functions
    //(확인)팝업 닫기
    //Inspector 상으로 버튼 연결용 함수
    public virtual void OnTriggerOk()
    {
        this.result.isOnOk = true;
        this.result.isOnX = false;

        if(this.IsSoundExistPopup())
            CommonProcessController.PlayEffectSound("Common", 1);

        this.Close();
    }

    //(취소)팝업 닫기
    //Inspector 상으로 버튼 연결용 함수
    public virtual void OnTriggerX()
    {
        this.result.isOnOk = false;
        this.result.isOnX = true;

        if(this.IsSoundExistPopup())
            CommonProcessController.PlayEffectSound("Common", 1);
        
        this.Close();
    }

    public virtual void OnBack()
    {
        if (this.paramBuffer.isLockBackButton)
        {
            return;
        }

        if (this.paramBuffer.backButtonType == BackButtonType.Cancel)
        {
            OnTriggerX();
        }
        else
        {
            OnTriggerOk();
        }
    }

    #endregion

    public virtual void Close()
    {
        this.ResultCallback?.Invoke(this, this.result);

        foreach (var tweenAnimation in GetComponents<DOTweenAnimation>())
        {
            tweenAnimation.DOKill();
        }

        this.LoadedCallback = null;
        
        if(this.IsSoundExistPopup())
            CommonProcessController.PlayButtonSound();

        PopupRoot.Instance.RemovePopup(this);
        if (!PopupRoot.Instance.IsPopupExist())
        {
            PopupRoot.Instance.enabled = false;
        }
        
    }

    private bool IsSoundExistPopup()
    {
        if (this.GetType() == typeof(BeforeStartPopup))
        {
            return this.result.needSound;
        }
        else if (this.GetType() == typeof(ResultFailPopup))
        {
            return this.result.needSound;
        }
        return this.GetType() != typeof(IngameDesPopup);
    }

    public void ForcedClose()
    {
        this.LoadedCallback = null;
        this.Close();
    }


    public void NoneResultClose()
    {
        this.ResultCallback = null;
        this.Close();
    }


    protected bool IsLocaleOpened()
    {
        if (this.popupInfo == null) return false;
        if (this.popupDescList == null) return false;
        if (this.systemLocDict == null) return false;

        return true;
    }

    protected string GetSystemLocale(Int32 key)
    {
        if (!this.IsLocaleOpened()) return null;

        if (this.systemLocDict.ContainsKey(key))
            return LocaleController.GetSystemLocale(this.systemLocDict[key]);
        return String.Empty;
    }

    protected string GetLocaleHeaderText()
    {
        if (this.headerLocale == null) return String.Empty;
        return LocaleController.GetSystemLocale(this.headerLocale);
    }

    public void ClearResult()
    {
        this.result.Clear();
    }

    private void Start()
    {
        OnLoadedCallback loadedCallback = this.LoadedCallback;
        this.LoadedCallback = null;

        if (null != loadedCallback)
            loadedCallback(this);
    }

    public void Show()
    {
        Vector3 pos = this.transform.localPosition;
        if (pos.y >= 1000000)
        {
            pos.y -= 1100000;
            this.transform.localPosition = pos;
        }

        ClearResult();
    }

    public void Hide()
    {
        Vector3 pos = this.transform.localPosition;
        if (pos.y < 1000000)
        {
            pos.y += 1100000;
            this.transform.localPosition = pos;
        }
    }
}
  </code>
</pre>

*실제 구현한 팝업 예시 [하트 구매 팝업]*
<pre>
  <code>
    using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;

using Snowballs.Client.Model;
using Snowballs.Client.Scene;
using Snowballs.Sheets;
using Snowballs.Sheets.Data;
using Snowballs.Util;

using TMPro;
using Snowballs.Client.Type;
using Snowballs.Client.View;
using Snowballs.Client.Ext.Event;
using NaughtyAttributes;
using Snowballs.Network.Dto;

public class BuyHeartPopup : Popup
{
    #region Info
    [Space(10)]
    [Header("팝업 설명")]
    [Space(10)]
    [InfoBox("일반모드 시작 버튼을 눌렀는데 하트가 없는 경우 \n게임 실패하고 다시 시작할때 하트가 없는 경우 \n하트가 없을때 상단 위에 하트 버튼을 눌렀을때 나타납니다.")]
    [HorizontalLine(color: EColor.Red)]
    [Header("특이 사항")]
    [Space(10)]
    [InfoBox("하트 이미지는 에셋테이블에서 가져옵니다.\n" + "이 안에 Package와 프리팹으로 된 Package는 일부가 달라서 이 안에 있는 Package를 수정하시는게 좋습니다. ",EInfoBoxType.Warning)]
    [HorizontalLine(color: EColor.Blue)]
    [Space(20)]
    #endregion
    
    [SerializeField] private RawImage heart;
    [SerializeField] private TMP_Text heartCount;
    [SerializeField] private RawImage buttonResourceIconImage;
    
    [SerializeField] private TMP_Text buyButtonText;
    [SerializeField] private TMP_Text price;

    [SerializeField] private PackageItemView packageItem;

    [SerializeField] private TMP_Text revokeText;

    public new class Params : Popup.Params
    {
        public StagePurchaseInfo data;

        public StoreStorage.RecommendStoreData  recommendStoreData;
        public PackageItemView.Params packageParam;

        public WWWFile.DownloadPath heartPath;
        public WWWFile.DownloadPath buttonIconPath;
        
        public string heartCount;
        public string buyButtonText;
        public string price;

        public string revokeText;

        public bool needMoveToShopScene;        //재화 부족시 상점으로 이동 여부
    }
    
    public override void OnOpen()
    {
        base.OnOpen();

        Params param = (Params)this.paramBuffer;

        this.heart.SetTexture(param.heartPath);
        this.buttonResourceIconImage.SetTexture(param.buttonIconPath);
        
        this.heartCount.text = param.heartCount;
        this.buyButtonText.text = param.buyButtonText;
        this.price.text = param.price;

        this.revokeText.text = param.revokeText;

        if (param.recommendStoreData != null)
        {
            this.packageItem.gameObject.SetActive(true);
            this.packageItem.Refresh(param.packageParam);
        }
        else
        {
            this.packageItem.gameObject.SetActive(false);
        }
    }


    public void OnClickBuyHeart()
    {
        SBDebug.Log("OnClickBuyHeart!");

        Params param = (Params)this.paramBuffer;

        var data = param.data;

        var currencyItem = (PaymentItem)data.PriceItem;

        // 재화 부족.
        if (GameStorage.PlayerStorage.GetCurrencyItemValue(currencyItem) < data.PriceValue)
        {
            int popupCode = 0;
            switch (currencyItem)
            {
                case PaymentItem.Gold:
                    popupCode = 40;
                    break;
                case PaymentItem.FreeDiamond:
                    popupCode = 101;
                    break;
                case PaymentItem.Mileage:
                    popupCode = 42;
                    break;
            }
            this.Hide();

            if (param.needMoveToShopScene)
            {
                this.OnTriggerX();
                LoadingIndicator.Show();
                SceneController.OpenScene(SceneController.Scene.Shop, () =>
                {
                    LoadingIndicator.Hide();
                });
            }
            else
            {
                //재화 부족 팝업 띄우기
                ViewController.OpenNotEnoughPopup(popupCode, RecommendInPopupType.BuyHeart, (isOnOk) =>
                {
                    if (isOnOk)
                    {
                        if (currencyItem == PaymentItem.FreeDiamond)
                        {
                            ViewController.OpenBuyDiamondPopup((_isOnOk) =>
                            {
                                //다이아몬드 구매팝업 -> 완료
                                if (_isOnOk)
                                {
                                    //재화 부족 팝업 닫기
                                    this.OnTriggerX();
                                }
                                //다이아몬드 구매팝업 -> 닫기
                                else
                                {
                                    this.Show();
                                }
                            });
                        }
                        else if(currencyItem == PaymentItem.Gold)
                        {
                            ViewController.OpenBuyGoldPopup((_isOnOk) =>
                            {
                                //골드 구매팝업 -> 완료
                                if (_isOnOk)
                                {
                                    //재화 부족 팝업 닫기
                                    this.OnTriggerX();
                                }
                                //골드 구매팝업 -> 닫기
                                else
                                {
                                    this.Show();
                                }
                            });
                        }
                    }
                    else
                    {
                        this.Show();
                    }
                });
            }
            return;
        }

        LoadingIndicator.Show();

        CommonProcessController.BuyChance(data, currencyItem, (response, buyNo, dataCode) =>
        {
            // BuyByMileage 성공.
            if (response != null && (ResponseCode)response.code == ResponseCode.OK)
            {
                CommonProcessController.Ack(CommonProcessController.AckType.StagePurchase, buyNo, dataCode, (ackResponse) =>
                {
                    // Ack 성공.
                    if (ackResponse != null && (ResponseCode)ackResponse.code == ResponseCode.OK)
                    {
                        LoadingIndicator.Hide();

                        this.Hide();
                        ViewController.OpenRewardPopup(response.data, () =>
                        {
                            this.Close();
                            // 얻은 상품을 셋팅한다.
                            GameStorage.ItemStorage.GetReward(response.data);

                            // 하트 갱신을 위한 이벤트 발생
                            BroadcastTunnel<string, int>
                                .Notify("com.snowballs.SWHJ.AddActiveItem", (int)PaymentItem.Heart);
                        },false,LocaleController.GetSystemLocale(614));
                    }
                    // Ack 실패.
                    else
                    {
                        LoadingIndicator.Hide();            
                    }
                });
            }
            //  BuyByMileage 실패.
            else
            {
                LoadingIndicator.Hide(); 
            }
        });
    }


    public void OnClickBuyRecommend()
    {
        SBDebug.Log("OnClickBuyHeart!");

        Params param = (Params)this.paramBuffer;

        var data = param.recommendStoreData;

        var currencyItem = (PaymentItem)data.GoodsPrice;

        // 재화 부족.
        if (GameStorage.PlayerStorage.GetCurrencyItemValue(currencyItem) < data.GoodsPrice)
        {
            int popupCode = 0;
            switch (currencyItem)
            {
                case PaymentItem.Gold:
                    popupCode = 40;
                    break;
                case PaymentItem.FreeDiamond:
                    popupCode = 41;
                    break;
                case PaymentItem.Mileage:
                    popupCode = 42;
                    break;
            }

            this.Hide();
            ViewController.OpenNotEnoughPopup(popupCode, (isOk) =>
            {
                if (isOk)
                {
                    this.Show();
                }
            });
            return;
        }

#if UNITY_EDITOR
        if (data.InAppBool)
        {
            Debug.LogWarning("UNITY_EDITOR 에서는 인앱상품을 지원하지 않습니다.");
            return;
        }
#endif

        LoadingIndicator.Show();

        // 인앱상품일때.
        if (data.InAppBool)
        {
            string sku = data.Sku;

            // BuyFromRecommend와 같은 requestDto 를 세팅
            var (requestDto,_) = CommonProcessController.GetRecommendRequestDto(data, 0, sku, string.Empty, string.Empty);
                    
            var networkManager = GameScene.Instance.NetworkManager;

            networkManager.InAppRecommendCheck(requestDto, (response) =>
            {
                if (response == null || (ResponseCode)response.code != ResponseCode.OK) {
                    LoadingIndicator.Hide();
                    return;
                }
                
                PurchaseController.BuyProduct(sku, (sku, purchaseData, appAccountToken, cb) =>
                {
                    // 구글, 애플 결제 완료 후 우리 게임서버로 전달.
                    CommonProcessController.BuyFromRecommend(data, 0, sku, purchaseData, appAccountToken, cb);
                }, (buyNo, dataCode, cb) =>
                {
                    // Ack 처리.
                    CommonProcessController.Ack(CommonProcessController.AckType.Recommend, buyNo, dataCode, cb);
                }, (result) =>
                {
                    // BuyDiamond, Ack 결과에 따라 처리.
                    if (result.ResponseCode == ResponseCode.OK)
                    {
                        if (result.AckResponseCode == ResponseCode.OK)
                        {
                            LoadingIndicator.Hide();

                            this.Hide();
                            ViewController.OpenRewardPopup(result.AcquiredDto, () =>
                            {
                                GameStorage.ItemStorage.GetReward(result.AcquiredDto);

                                this.packageItem.gameObject.SetActive(false);

                                this.Show();
                            },false,LocaleController.GetSystemLocale(614));
                        }
                        else
                        {
                            LoadingIndicator.Hide();
                            // Ack Fail.
                        }
                    }
                    else
                    {
                        LoadingIndicator.Hide();
                        // BuyDiamond Fail.
                    }
                });
            });
        }
        // 일반 상품일때.
        else
        {
            CommonProcessController.BuyFromRecommend(data, currencyItem, string.Empty, string.Empty, string.Empty, (response, buyNo, dataCode) =>
            {
                // BuyFromRecommend 성공.
                if (response != null && (ResponseCode)response.code == ResponseCode.OK)
                {
                    CommonProcessController.Ack(CommonProcessController.AckType.Recommend, buyNo, dataCode, (ackResponse) =>
                    {
                        // Ack 성공.
                        if (ackResponse != null && (ResponseCode)ackResponse.code == ResponseCode.OK)
                        {
                            LoadingIndicator.Hide();

                            this.Hide();
                            ViewController.OpenRewardPopup(response.data, () =>
                            {
                                // 얻은 상품을 셋팅한다.
                                GameStorage.ItemStorage.GetReward(response.data);
                                this.packageItem.gameObject.SetActive(false);
                                this.Show();
                            },false,LocaleController.GetSystemLocale(614));
                        }
                        // Ack 실패.
                        else
                        {
                            LoadingIndicator.Hide();
                        }
                    });
                }
                //  BuyFromRecommend 실패.
                else
                {
                    LoadingIndicator.Hide();
                }
            });
        }
    }
}
  </code>
</pre>
