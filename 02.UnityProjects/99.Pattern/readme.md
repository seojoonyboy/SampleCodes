개방 폐쇄원칙(Open-Closed Pricipal)에 입각한 팝업창 설계
================

STARWAY는 확인/취소, 재화 부족, 보상, 구매 등 실제로 50종이 넘는 팝업 프리팹을 운영한다. 새 팝업이
추가될 때마다 팝업을 여닫는 로더 코드나 다른 팝업의 구현까지 함께 손대야 한다면 개방-폐쇄 원칙을
지킬 수 없다. 그래서 팝업의 "생성·등록·공통 동작"은 `Popup` 한 클래스에 고정해두고(폐쇄), 새 팝업은
`Popup`을 상속받는 새 클래스와 프리팹 하나만 추가하면 기존 코드를 전혀 건드리지 않고 붙는(확장에
열려있는) 구조로 설계했다.

모든 팝업은 Popup 클래스를 상속받아 설계   
![Popup](https://github.com/user-attachments/assets/0b8bb013-63eb-402c-ae1d-b25aa1d25509)   

내부 함수는 Virtual 함수로 작성하여, 공통 기본 동작을 기술   
```csharp
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
```

새 팝업을 만들 때 실제로 손대는 곳은 `Popup.Load`를 호출하는 쪽과 새 팝업 클래스뿐이다. 로더 자체는
아래처럼 프리팹 이름 문자열 하나로 어떤 팝업이든 동일한 절차(로드 → 인스턴스화 → PopupRoot 등록 →
Open/OnOpen 호출)로 띄운다 — 팝업 종류가 50개든 100개든 이 함수는 수정되지 않는다.

```csharp
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
```

> `popupName` 문자열로 `Resources.Load<Popup>(basePath + popupName)`을 호출하는 부분이 개방-폐쇄
> 원칙의 실질적인 근거다. 새 팝업은 `Prefabs/Popup/` 아래에 프리팹을 하나 추가하고 `Popup`을 상속한
> 클래스를 하나 작성하면 끝이다. `Popup.Load`, `PopupRoot`, 그 밖의 기존 팝업 코드는 새 팝업이
> 추가되어도 재컴파일 대상이 아니다 — 확장은 새 파일을 더하는 것으로 끝나고, 기존 코드는 닫혀 있다.

MVC 패턴   
================

STARWAY MVC Pattern 설계 예시   
> 기본 설계는 MVC를 바탕으로 설계했습니다.

<img src="https://github.com/user-attachments/assets/255f3be5-f8b6-405c-a34b-f63b463cd431" width = "70%" height = "70%">

> 구매 팝업에 대한 예시 이미지   
<img src = "https://github.com/user-attachments/assets/bbc8e947-7144-4fe5-a167-10a7a29308df" width = "30%" height = "30%">

<br/><br/>
구매 버튼 클릭시 OnClickToBuyPremiumPass 함수 호출   
> 패스 구매 팝업(PassBuyPopup)을 띄웁니다.   
> PassBuyPopup.Params를 통해 패스 구매 팝업에 필요한 IAP 상품의 SKU 값과 기타 필요한 정보를 함께 전달합니다.   

```csharp
    public void OnClickToBuyPremiumPass()
    {
        var item = GameStorage.StoreStorage.RecommendStoreDisplayList.Find(x => x.GoodsType == 2);
        if(item == null) return;
        
        PassBuyPopup.Params passBuyPopup = new PassBuyPopup.Params();
        passBuyPopup.Code = 76;
        
        passBuyPopup.isCloseBtnNeed = true;
        
        int passCode = GameStorage.PlayerStorage.PlayerSubDto.pass.code;
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
        passBuyPopup.price = "₩ " + item.StoreData.InAppPriceKo.ConvertCommaString();
#else
        passBuyPopup.price = (PurchaseController.GetProductInfo(item.Sku) != null) ? PurchaseController.GetProductInfo(sku).CurrencyString : "₩ " + item.StoreData.InAppPriceKo.ConvertCommaString();
#endif
        passBuyPopup.isBuyAvaliable = (GameStorage.PlayerStorage.GetRecommendStoreCounting(item.Code).total == 0);
        passBuyPopup.alreadyActiveText = LocaleController.GetSystemLocale(81);
        passBuyPopup.infomationText = LocaleController.GetSystemLocale(205);
        
        Popup.Load("PassBuyPopup", passBuyPopup, (pop, result) =>
        {
            this.ReOpenPopup();
        });
        
        //test code
        // this.ReOpenPopup();
        //end test code
    }
```

> `PassBuyPopup.Params`에 담기는 값들을 보면 View(팝업)가 자기 힘으로 알아낼 수 없는 정보 — 상품
> 데이터(`item`), 서버 시간을 얻는 함수(`timeFunc`), 가격 문자열, 이미 활성화되어 있는지 여부 —
> 를 호출부(Controller 역할)가 전부 계산해서 넘겨준다는 것을 알 수 있다. 팝업 자신은 "이 값들을
> 어떻게 화면에 그릴지"만 책임지고, "이 값이 무엇인지"는 몰라도 된다.

패스 구매 팝업에서 구매하기 버튼 클릭시   
> OnClickBuy 함수가 호출됩니다.   
> View에서 구매 처리를 Controller에게 전달합니다. PurchaseController.BuyProduct   
> View -> Controller   

> 구매 처리의 안정성을 보장하기 위해 구매 결과 Response를 Server에 재확인 합니다. CommonProcessController.Ack   
> 구매 처리가 Ack 확인 이후까지 정상적인 경우 (result.ResponseCode == ResponseCode.OK) Controller에서 View에게 Callback을 전달합니다.   
> 최종적으로 플레이어 패스 정보에 대한 갱신을 합니다.   
> View -> Model   

```csharp
    public void OnClickBuy()
    {
        Params param = (Params)this.paramBuffer;

        // 패스는 현금만 존재!! 테스트하기 위해. 삭제되어야 할 코드.///////////////
        // param.OnClickBuy(this);
        //return;
#if UNITY_EDITOR
        if (param.data.InAppBool)
        {
            Debug.LogWarning("UNITY_EDITOR 에서는 인앱상품을 지원하지 않습니다.");
            return;
        }
#endif
        ////////////////////////////////////////////////////////////////////////////

        //   var data = SBDataSheet.Instance.RecommendStore[param.recommendStoreCode];

        string sku = param.data.Sku;
/*#if UNITY_ANDROID
        sku = data.GoogleSku;
#elif UNITY_IOS
        sku = data.AppleSku;
#endif*/


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
                // 구글, 애플 결제 완료 후 우리 게임서버로 전달.
                CommonProcessController.BuyFromRecommend(param.data, 0, sku, purchaseData, appAccountToken, cb);
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

                            param.isBuyAvaliable = false;
                            this.price.text = (param.isBuyAvaliable) ? param.price : param.alreadyActiveText;
                            this.buyButton.interactable = param.isBuyAvaliable;

                            this.result.isOnOk = true;
                            this.Close();
                        },false,LocaleController.GetSystemLocale(614));

                        //PlayerStorage 갱신 [프리미엄 구독 상태로 변경]
                        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
                        playerStorage.PlayerSubDto.pass.isPremium = true;

                        //EventStorage 갱신 [프리미엄 구독 상태로 변경]
                        EventStorage eventStorage = GameStorage.Instance.GetStorage<EventStorage>();
                        var targetEventData = eventStorage.GetPassEventData(playerStorage.PlayerSubDto.pass.code);
                        targetEventData.passDto.isPremium = true;
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
```

> `result.ResponseCode`(스토어 결제 성공 여부)와 `result.AckResponseCode`(서버 재확인 성공 여부)를
> 분리해서 검사하는 것이 View -> Model 갱신의 전제 조건이다. 두 값이 모두 `OK`일 때만
> `PlayerStorage`/`EventStorage`의 패스 프리미엄 상태(`isPremium`)를 갱신한다 — 구매 자체는
> 성공했더라도 서버 재확인 전에는 로컬 모델을 앞서서 바꾸지 않는다.

설계 포인트
------------
> 이 문서의 두 코드 블록은 서로 다른 층위의 개방-폐쇄 원칙을 보여준다. 첫 번째(`Popup`/`Popup.Load`)는
> "팝업을 어떻게 만들고 띄우는가"라는 인프라 층위의 확장성이고, 두 번째(`PassBuyPopup`의 MVC 흐름)는
> "하나의 팝업 안에서 View·Controller·Model이 어떻게 역할을 나누는가"라는 개별 기능 층위의 확장성이다.
> 새 구매 상품을 추가할 때 View(`PassBuyPopup`)나 Model(`PlayerStorage`) 구조를 바꿀 필요 없이
> `OnClickToBuyPremiumPass` 같은 호출부에서 `Params` 값만 다르게 채우면 되는 것도 같은 원리의 연장이다.

관련 코드: [PopupUIPattern.md](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/PopupUIPattern.md) · [IAPProcess.md](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/IAPProcess.md) · [01.Pass](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/01.Pass)

