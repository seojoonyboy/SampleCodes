개방 폐쇄원칙(Open-Closed Pricipal)에 입각한 팝업창 설계
================

모든 팝업은 Popup 클래스를 상속받아 설계   
![Popup](https://github.com/user-attachments/assets/0b8bb013-63eb-402c-ae1d-b25aa1d25509)   

내부 함수는 Virtual 함수로 작성하여, 공통 기본 동작을 기술   
<pre>
  <code>
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
  </code>
</pre>

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

<pre>
  <code>
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
  </code>
</pre>

패스 구매 팝업에서 구매하기 버튼 클릭시   
> OnClickBuy 함수가 호출됩니다.   
> View에서 구매 처리를 Controller에게 전달합니다. PurchaseController.BuyProduct   
> View -> Controller   

> 구매 처리의 안정성을 보장하기 위해 구매 결과 Response를 Server에 재확인 합니다. CommonProcessController.Ack   
> 구매 처리가 Ack 확인 이후까지 정상적인 경우 (result.ResponseCode == ResponseCode.OK) Controller에서 View에게 Callback을 전달합니다.   
> 최종적으로 플레이어 패스 정보에 대한 갱신을 합니다.   
> View -> Model   

<pre>
  <code>
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
  </code>
</pre>

