![image](https://github.com/user-attachments/assets/9addb6f2-4a92-4d04-94f8-09459f75dcf6)

STARWAY에는 확인/취소 팝업, 재화 부족 팝업, 보상 팝업, 구매 팝업 등 실제로 50종이 넘는 팝업 프리팹이
존재한다. 각 팝업마다 열고 닫는 애니메이션, 로케일 텍스트 세팅, 버튼 사운드, 뒤로가기 처리를 매번
새로 구현하면 팝업이 늘어날수록 유지보수 비용이 그대로 곱해진다. 이 문제를 상위 `Popup` 클래스 하나가
공통 동작을 책임지고, 개별 팝업은 필요한 부분만 오버라이드하는 구조로 풀고 있다.

*최상단 View 설계[Popup.cs]*
> 1. 확인/취소/닫기 버튼에 대한 기본 기능을 Virtual 함수 형태로 구현하여, 하위 클래스에서 세부 구현을 하여 사용하도록 함   
> 2. Controller에게 팝업에서의 사용자 이벤트를 전달하기 위한 Delegate를 지정 [OnResultCallback, OnLoadedCallback]   
> 3. View에 필요한 Model 정보를 Params 인스턴스 형태로 활성화 하면서 Controller가 전달하게 된다.   
> 4. 팝업 생성은 `Popup.Load(...)` 라는 단일 정적 함수로 통일해, 프리팹 로드 → 인스턴스화 → Open → OnOpen 호출까지의 절차를 모든 팝업이 동일하게 거치도록 함

*실제 구현된 팝업 종류 일부*   
![image](https://github.com/user-attachments/assets/b076b255-f612-4526-8139-108b4e42111f)

*최상단 Popup 클래스*
```csharp
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
```

*코드 구조 훑어보기*
> `Popup` 안의 세 이너 클래스(`Params`, `Result`, `ManageParams`)는 각각 역할이 분명히 나뉜다.
> `Params`는 팝업을 열 때 컨트롤러가 채워서 넘기는 "입력값"(헤더/본문 로케일 코드와 `{0}` 치환 인자,
> 버튼 텍스트, 뒤로가기 동작 방식)이고, `Result`는 팝업이 닫힐 때 되돌려주는 "출력값"(확인/닫기 여부,
> 사운드 필요 여부, 임의 데이터를 담는 `object args`)이다. `ManageParams`는 장비창 등 잠금/장착 아이콘을
> 다루는 팝업 전용 필드 묶음으로, 모든 팝업이 쓰지는 않지만 공통 베이스에 미리 자리를 만들어 둔 것이다.
> 이 세 클래스만으로 "팝업에 무엇을 넣어줄지"와 "팝업에서 무엇을 돌려받을지"를 팝업 종류와 무관하게
> 동일한 형태로 주고받을 수 있다.   
> `OnOpen`은 로케일 코드가 있으면 데이터 시트 텍스트를, 없으면 `dummyHeaderText`류의 더미 문자열을
> 쓰는 이중 경로를 헤더/본문/버튼/서브텍스트마다 반복한다. 기획 데이터가 아직 없는 상태에서도 더미
> 문자열로 팝업을 먼저 붙여볼 수 있게 해주는 부분이다. `IsSoundExistPopup()`은 `GetType() ==
> typeof(...)`로 특정 팝업 타입만 예외 처리하는 방식인데, 팝업 개수가 늘어날수록 이 타입 나열 방식은
> 새 예외가 생길 때마다 `Popup` 베이스 코드 자체를 건드려야 한다는 한계도 함께 보여준다 — 공통 베이스
> 클래스 패턴이 모든 예외 케이스를 깨끗하게 흡수하지는 못한다는 실제 사례다.

설계 포인트
------------
> `Popup.Load(...)` 하나가 프리팹 로드부터 `PopupRoot`에 등록, `Open`(파라미터/콜백 바인딩), `OnOpen`
> (화면 세팅) 호출까지를 담당하는 **팩토리 역할**을 하기 때문에, 개별 팝업 스크립트는 `OnOpen`을
> 오버라이드해서 자신에게 필요한 필드만 채우면 된다. `confirmBtn`/`cancelBtn`/`closeBtn` 클릭은 모두
> `OnTriggerOk`/`OnTriggerX`처럼 이미 사운드 재생과 `Close()` 호출까지 끝낸 Virtual 함수로 연결되어
> 있어, 새 팝업을 만들 때 사운드나 닫기 처리를 매번 새로 구현할 필요가 없다.   
> 팝업이 사용자의 선택을 컨트롤러에게 돌려주는 방법도 강하게 결합된 참조가 아니라 `OnResultCallback`
> 델리게이트다. `Popup.Load` 호출부는 팝업 인스턴스를 몰라도 되고, 팝업이 닫히면서 `Close()` 안에서
> `this.ResultCallback?.Invoke(this, this.result)`를 호출해 결과를 전달한다 — 호출자와 팝업 구현이
> 서로의 구체 타입을 몰라도 되는 결과-콜백 패턴이다. `Show()`/`Hide()`가 `Destroy` 대신 화면 밖으로
> 좌표를 이동시키는 방식으로 구현된 것도 눈에 띈다 — 재화 부족 팝업처럼 "다른 팝업을 띄웠다가 다시
> 돌아오는" 흐름에서 인스턴스를 유지한 채 화면에서만 감췄다 복원할 수 있다.

*실제 구현한 팝업 예시 [패스 구매 팝업]*
> `PassBuyPopup`은 `Popup`을 상속만 받고, `Params`도 `Popup.Params`를 상속해 패스 구매에 필요한 필드
> (`data`, `imagePath`, `timeFunc`, `endTime`, `price` 등)만 덧붙인다. `OnOpen`은 `base.OnOpen()`을
> 가장 먼저 호출해 상위 클래스가 처리하는 공통 헤더/버튼 세팅을 그대로 받은 뒤, 패스 구매 팝업만의
> 이미지/가격/남은 시간 표시를 이어서 채운다 — Template Method 패턴에서 하위 클래스가 상위 동작을
> 대체가 아니라 확장하는 전형적인 형태다.

```csharp
public class PassBuyPopup : Popup
{
    [SerializeField] private RawImage image;
    [SerializeField] private TMP_Text remainingTime;
    [SerializeField] private TMP_Text price;
    [SerializeField] private TMP_Text infomation;
    [SerializeField] private Button buyButton;

    public new class Params : Popup.Params
    {
        public StoreStorage.RecommendStoreData data;

        public WWWFile.DownloadPath imagePath;

        public Func<DateTime> timeFunc;
        public DateTime endTime;

        public bool isBuyAvaliable;

        public string price;
        public string alreadyActiveText;

        public string infomationText;
    }

    public override void OnOpen()
    {
        base.OnOpen();

        Params param = (Params)this.paramBuffer;

        this.image.SetTexture(param.imagePath);

        this.price.text = (param.isBuyAvaliable) ? param.price : param.alreadyActiveText;

        this.infomation.text = param.infomationText;

        this.buyButton.interactable = param.isBuyAvaliable;

        var now = param.timeFunc();
        var remainingTime = param.endTime - now;

        if (remainingTime.TotalDays >= 1)
        {
            this.remainingTime.text = string.Format(CommonProcessController.GetRemainingString(CommonProcessController.TimeStringType.DayHour), remainingTime.Days, remainingTime.Hours);
        }
        else
        {
            var signal = new CancellableSignal(() =>
            {
                return this == null;
            });

            CoroutineTaskManager.AddTask(this.RemainingTimeTask(signal, param));
        }

        int passCode = GameStorage.PlayerStorage.PlayerSubDto.pass.code;
        int nameLocaleCode = SBDataSheet.Instance.PassInfo[passCode].NameLocale;
        this.headerText.text = LocaleController.GetEventLocale(nameLocaleCode);
    }


    IEnumerator RemainingTimeTask(CancellableSignal signal, Params param)
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
                //this.SetEndTime();
            }
            yield return wfef;
            if (CancellableSignal.IsCanceled(signal)) { yield break; }
        }
        while (now <= param.endTime);

        // this.SetEndTime();
    }

    void UpdateSeasonRemainingTime(TimeSpan time)
    {
        if (time.Hours >= 1)
        {
            this.remainingTime.text = string.Format(CommonProcessController.GetRemainingString(CommonProcessController.TimeStringType.HourMinute), time.Hours, time.Minutes);
        }
        else
        {
            if (time.Minutes >= 1)
            {
                this.remainingTime.text = string.Format(CommonProcessController.GetRemainingString(CommonProcessController.TimeStringType.MinuteSecond), time.Minutes, time.Seconds);
            }
            else
            {
                this.remainingTime.text = string.Format(CommonProcessController.GetRemainingString(CommonProcessController.TimeStringType.Second), time.Seconds);
            }
        }
    }


    public void OnClickBuy()
    {
        Params param = (Params)this.paramBuffer;

#if UNITY_EDITOR
        if (param.data.InAppBool)
        {
            Debug.LogWarning("UNITY_EDITOR 에서는 인앱상품을 지원하지 않습니다.");
            return;
        }
#endif
        string sku = param.data.Sku;

        this.BuyPassProduct(sku);
    }


    public void BuyPassProduct(string sku)
    {
        Params param = (Params)this.paramBuffer;

        LoadingIndicator.Show();

        var (requestDto,_) = CommonProcessController.GetRecommendRequestDto(param.data, 0, sku, string.Empty, string.Empty);

        var networkManager = GameScene.Instance.NetworkManager;

        networkManager.InAppRecommendCheck(requestDto, (response) =>
        {
            if (response == null || (ResponseCode)response.code != ResponseCode.OK) {
                LoadingIndicator.Hide();
                return;
            }
            
            PurchaseController.BuyProduct(sku, (sku, purchaseData, appAccountToken, cb) =>
            {
                CommonProcessController.BuyFromRecommend(param.data, 0, sku, purchaseData, appAccountToken, cb);
            }, (buyNo, dataCode, cb) =>
            {
                CommonProcessController.Ack(CommonProcessController.AckType.Recommend, buyNo, dataCode, cb);
            }, (result) =>
            {
                if (result.ResponseCode == ResponseCode.OK)
                {
                    if (result.AckResponseCode == ResponseCode.OK)
                    {
                        LoadingIndicator.Hide();

                        this.Hide();
                        ViewController.OpenRewardPopup(result.AcquiredDto, () =>
                        {
                            GameStorage.ItemStorage.GetReward(result.AcquiredDto);

                            param.isBuyAvaliable = false;
                            this.price.text = (param.isBuyAvaliable) ? param.price : param.alreadyActiveText;
                            this.buyButton.interactable = param.isBuyAvaliable;

                            this.result.isOnOk = true;
                            this.Close();
                        },false,LocaleController.GetSystemLocale(614));
                    }
                    else
                    {
                        LoadingIndicator.Hide();
                    }
                }
                else
                {
                    LoadingIndicator.Hide();
                }
            });
        });
    }
}
```

> `param.timeFunc()`로 시간을 직접 주입받는 것도 주목할 부분이다. 서버 시간(`SBTime.Instance.ServerTime`)에
> 대한 의존을 팝업 내부에 하드코딩하지 않고 `Params`를 통해 함수로 전달받기 때문에, 남은 시간이 하루
> 이상이면 정적으로 한 번만 표시하고 그 미만이면 `CoroutineTaskManager`에 등록한 `RemainingTimeTask`
> 코루틴으로 매 프레임 갱신한다 — 표시 갱신 빈도를 남은 시간 크기에 따라 다르게 가져가는 최적화다.
> `RemainingTimeTask`는 팝업이 파괴된 뒤에도 코루틴이 남아있지 않도록 `CancellableSignal`로 취소
> 신호를 받는데, 그 판단 기준이 `() => this == null`이다 — 팝업 오브젝트 자체가 파괴되면 신호가 취소된
> 것으로 간주해 루프를 빠져나간다.   
> `OnClickBuy`/`BuyPassProduct`가 구매 완료 후 하는 일도 `Popup` 베이스 클래스가 만들어 둔 결과-콜백
> 패턴을 그대로 따른다 — `this.result.isOnOk = true`로 결과 객체를 채운 뒤 `this.Close()`를 호출하면,
> 베이스 클래스의 `Close()`가 `ResultCallback`을 통해 이 결과를 호출자(`PassPopup`)에게 돌려준다.
> `PassBuyPopup` 자신은 자신을 연 쪽이 누구인지 몰라도 되고, 그저 "팝업이 어떻게 끝났는지"만 `Result`에
> 담아 넘기면 된다. 구매 결과를 서버에 재확인하는 `Ack` 절차 자체의 신뢰성 설계는 서버-클라이언트 구매
> 검증을 다루는 문서에서 별도로 설명한다.

관련 코드: [99.Pattern](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/99.Pattern) · [IAPProcess.md](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/IAPProcess.md) · [01.Pass](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/01.Pass)
