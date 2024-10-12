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
