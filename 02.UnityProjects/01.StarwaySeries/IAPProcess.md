1단계 : Unity Purchasing Library를 활용하여 Apple, Google Store 구매 처리 진행   
<pre>
	<code>
	    PurchaseController.BuyProduct(sku, (sku, purchaseData, appAccountToken, cb) =>
	</code>
</pre>
<pre>
	<code>
	    public static void BuyProduct(string sku, OnRequestGameCallback reqeust, OnRequestAckCallback ackRequest, OnPurchaseResultCallback callback)
	    {
		// 상점에서 구매해서 TRUE로 변경해준다.
		PurchaseController.IsPurchaseStart = true;
	
		PurchaseController.Instance.onRequestGameCallback = reqeust;
		PurchaseController.Instance.onRequestAckCallback = ackRequest;
		PurchaseController.Instance.onPurchaseResultCallback = callback;
	
		PurchaseController.Instance.appAccountToken = string.Empty;
	
	#if UNITY_IOS
		PurchaseController.Instance.appAccountToken = Guid.NewGuid().ToString();
		PurchaseController.SetApplicationUsername(PurchaseController.Instance.appAccountToken);
	
		PlayerPrefs.SetString(sku, PurchaseController.Instance.appAccountToken);
	#endif
	
		PurchaseController.Instance.StoreController.InitiatePurchase(sku);
	    }
	</code>
</pre>

2단계 : 구매 결과 정보를 자사 Server에 전달한다. [CommonProcessController.BuyFromRecommend]   
<pre>
	<code>
	    PurchaseController.BuyProduct(sku, (sku, purchaseData, appAccountToken, cb) =>
	    {
		// 구글, 애플 결제 완료 후 우리 게임서버로 전달.
		CommonProcessController.BuyFromRecommend(data, 0, sku, purchaseData, appAccountToken, cb);
	    }, (buyNo, dataCode, cb) =>
	</code>
</pre>

3단계 : 구매 History가 클라이언트와 Server가 현재 일치하는지 확인한다.
<pre>
	<code>
	    CommonProcessController.Ack(CommonProcessController.AckType.Recommend, buyNo, dataCode, cb);
	</code>
</pre>

4단계 : 구매 History까지 일치하면 최종적으로 Client의 구매에 따른 재화나 보상 처리를 이펙트를 보여주고 갱신처리 한다.
<pre>
	<code>
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
	</code>
</pre>
