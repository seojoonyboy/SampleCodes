![image](https://github.com/user-attachments/assets/53ac0c47-3160-4cee-9d56-e2352e3aa898)

인앱 구매는 "스토어 결제"와 "게임 서버의 보상 지급"이 서로 다른 두 시스템에서 일어난다. 결제는
완료됐는데 통신 장애로 서버가 이를 못 받거나, 서버는 기록했는데 클라이언트가 응답을 못 받아 재화를
못 받는 등, 두 시스템의 상태가 어긋날 여지가 구조적으로 존재한다. STARWAY의 인앱 구매 프로토콜은
TCP의 3-way handshake에서 착안해 **①사전 확인 → ②스토어 결제 → ③결제 결과를 서버에 통보 → ④서버
Ack로 재확인** 이후에야 최종적으로 보상을 지급하도록 되어 있다 — 어느 한쪽만의 판단으로 구매를
완료 처리하지 않는다.

0단계 : 구매 전 서버에 상품 구매 가능 여부를 먼저 확인한다 [`NetworkManager.InAppRecommendCheck`]
```csharp
public void InAppRecommendCheck(RequestDto<StoreBuyDto> requestDto, Action<ResponseDto<CodeDto>> callback)
{
    GameStore.InAppRecommendCheck(requestDto, (response) =>
    {
        callback(response);
    });
}
```
```csharp
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
            // ... 중략: 확인 성공 시 실제 스토어 결제(1단계)로 진행 ...
```

1단계 : Unity Purchasing Library를 활용하여 Apple, Google Store 구매 처리 진행 [`PassBuyPopup.OnClickBuy`]
```csharp
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
```

2단계 : 구매 결과 정보를 자사 Server에 전달한다 [`CommonProcessController.BuyFromRecommend`]
```csharp
            PurchaseController.BuyProduct(sku, (sku, purchaseData, appAccountToken, cb) =>
            {
                // 구글, 애플 결제 완료 후 우리 게임서버로 전달.
                CommonProcessController.BuyFromRecommend(param.data, 0, sku, purchaseData, appAccountToken, cb);
            }, (buyNo, dataCode, cb) =>
            {
                // Ack 처리.
                CommonProcessController.Ack(CommonProcessController.AckType.Recommend, buyNo, dataCode, cb);
            }, (result) =>
```

3단계 : 구매 History가 클라이언트와 Server가 현재 일치하는지 서버에 재확인한다 [`NetworkManager.Ack`]
> `Ack`는 단순히 "성공/실패"만 돌려주지 않는다. 응답이 `OK`면 `requestDto.data.resource` 값(구매
> 종류)에 따라 `GameScene.unreceivedStageReward`, `unreceivedPassReward`, `unreceivedDailyBonusReward`
> 같은 "아직 수령하지 못한 보상" 목록에서 해당 항목을 제거한다. 즉 클라이언트는 구매/보상 요청을
> 보낸 시점에 이 목록에 항목을 먼저 넣어두고, 서버가 최종 Ack로 확인해줘야만 그 항목을 지운다 —
> 중간에 앱이 꺼지거나 네트워크가 끊겨도 "아직 못 받은 보상" 상태가 로컬에 남아있게 되는 구조다.
```csharp
public void Ack(RequestDto<HistoryDto> requestDto, Action<ResponseDto<String>> callback)
{
    GameHistory.Ack(requestDto, (response) =>
    {
        if (response != null && (ResponseCode)response.code == ResponseCode.OK)
        {
            switch (requestDto.data.resource)
            {
                case "StageRewardInfo":
                    if (GameScene.unreceivedStageReward != null)
                    {
                        var targetItem = GameScene.unreceivedStageReward.Find(x => x.value == requestDto.data.no);
                        if (targetItem != null)
                            GameScene.unreceivedStageReward.Remove(targetItem);
                    }
                    break;

                // ... 중략: DailyBonusInfo / PassInfo / Advertisement / CardGachaSon 등
                //          동일한 형태의 case가 리소스 종류별로 반복 ...

                case "PopupStore":
                    break;
            }
        }

        callback(response);
    });
}
```

4단계 : 구매 History까지 서버와 일치하면(Ack 성공) 그제서야 클라이언트의 재화/보상 반영과 이펙트를
보여주고 화면을 갱신한다 [`PassBuyPopup.BuyPassProduct` 콜백]
```csharp
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
```

> `result.ResponseCode`(스토어 결제 결과 전달)와 `result.AckResponseCode`(서버 재확인 결과)가 분리된
> 두 값으로 내려온다는 점이 이 흐름의 핵심이다. 구매 자체는 성공했더라도 `AckResponseCode`가 `OK`가
> 아니면 보상 지급 UI(`ViewController.OpenRewardPopup`)와 `param.isBuyAvaliable = false` 갱신이
> 실행되지 않는다 — 클라이언트가 "결제 성공"과 "보상 지급 확정"을 같은 신호로 취급하지 않는다.

설계 포인트
------------
> 이 4단계 구조에서 가장 중요한 지점은 3단계 `Ack`다. 구매 확정을 "스토어 결제 성공" 한 번의 신호로
> 끝내지 않고, 서버가 자신의 구매 이력과 클라이언트가 보고한 이력이 서로 일치하는지 재확인한 뒤에야
> `unreceivedXXXReward` 목록에서 항목을 지우고 클라이언트도 보상 UI를 띄운다. TCP의 3-way handshake와
> 같은 아이디어로, 한쪽의 판단만으로 거래를 종료하지 않고 양쪽 상태가 맞아떨어지는 것을 확인한 뒤에야
> 완료 처리하는 구조다.   
> 이 Ack 확인 단계를 구매 프로토콜에 넣은 이후, 결제는 됐는데 보상을 못 받았다거나 반대로 서버와
> 클라이언트의 구매 상태가 어긋나는 유형의 라이브 이슈로 인한 CS 문의가 주 1~3건 수준에서 사실상
> 0건으로 줄었다 — 결제 신뢰성 문제로 인한 운영 부담이 눈에 띄게 줄어든 부분이다.

관련 코드: [PopupUIPattern.md](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/PopupUIPattern.md) · [99.Pattern](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/99.Pattern) · [05.Network](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/05.Network)
