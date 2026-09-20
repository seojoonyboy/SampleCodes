using Snowballs.Client.Model;
using Snowballs.Client.View;
using Snowballs.Client.Scene;
using Snowballs.Network;
using Snowballs.Network.Dto;
using Snowballs.Sheets;
using Snowballs.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;

public class PurchaseController : IStoreListener
{
    private static PurchaseController instance;
    private static PurchaseController Instance
    {
        get
        {
            if (instance == null)
            {
                //게임 인스턴스가 없다면 하나 생성해서 넣어준다.
                instance = new PurchaseController();
            }
            return instance;
        }
    }

    public PurchaseController()
    {

    }

    private static Action<bool> initializeCallback;

    public static bool IsPurchaseStart { private set; get; }

    public static bool IsPendingProduct { get { return PurchaseController.Instance.PendingProduct != null;  } }

    IStoreController StoreController; // The Unity Purchasing system.

    public class PendingProductInfo
    {
        public string Sku;
        public string TransactionId;
        public string Receipt;

        public PendingProductInfo (string sku, string transactionId, string receipt)
        {
            this.Sku = sku;
            this.TransactionId = transactionId;
            this.Receipt = receipt;
        }
    }

    public PendingProductInfo PendingProduct;


        public class ProductInfo
    {

        public string Sku;                          // sku 값. data.Sku = (스토어 상품 ID)
        public ProductType Type;                // 상품타입. data.Type = Consumable

        public string CurrencyString;          // data.CurrencyString = ₩1,100
        public string LocalizedAmount;       // data.LocalizedAmount = 1,100
        public string Symbol;                     // data.Symbol = ₩
        public double Amount;                   // data.Amount = 1100
        public double LocalPrice;                // data.LocalPrice = 1.99
        public string ISOCurrencyCode;      // data.ISOCurrencyCode = KRW

        public ProductInfo(string sku, ProductType type, double localPrice)
        {
            this.Sku = sku;
            this.Type = type;
            this.LocalPrice = localPrice;
        }
    }

    public Dictionary <string, ProductInfo> ProductMap;



    // 재시작하거나 할때 호출해줘야함. 변수 초기화.
    public static void Close()
    {
        PurchaseController.IsPurchaseStart = false;
        PurchaseController.Instance.PendingProduct = null;
        PurchaseController.Instance.ProductMap = null;
    }

    public static void AddProduct(string sku, ProductType type, double localPrice)
    {
        if(PurchaseController.Instance.ProductMap == null)
        {
            PurchaseController.Instance.ProductMap = new Dictionary<string, ProductInfo>();
        }

        if (!PurchaseController.Instance.ProductMap.ContainsKey(sku))
        {
            PurchaseController.Instance.ProductMap.Add(sku, new ProductInfo(sku, type, localPrice));
        }
    }

    public static ProductInfo GetProductInfo(string sku)
    {
        if (PurchaseController.Instance.ProductMap != null && PurchaseController.Instance.ProductMap.ContainsKey(sku))
        {
            return PurchaseController.Instance.ProductMap[sku];
        }
        else
        {
            return null;
        }
    }

    public static void InitializePurchasing(Action<bool> callback)
    {
        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

        var enumerator = PurchaseController.Instance.ProductMap.GetEnumerator();
        while (enumerator.MoveNext())
        {
            try
            {
                var data = enumerator.Current.Value;
                builder.AddProduct(data.Sku, data.Type);
            }
            catch (Exception e)
            {
                Debug.LogWarning(e);
            }
        }

        PurchaseController.initializeCallback = callback;

        UnityPurchasing.Initialize(PurchaseController.Instance, builder);
    }



    public class PurchaseResult
    {
   //     public ResponseDto<int> Result;
        public int buyNo;
        public int dataCode;
        public ResponseCode ResponseCode;
        public ResponseCode AckResponseCode;
        public AcquiredItemDto AcquiredDto;
        public string TransactionId;
        public string SkuDetail;
        public string PurchaseData;
        public string DataSignature;
        public bool isSkipErrorPopup;

        public PurchaseResult(string transactionId, string skuDetail, string purchaseData, string dataSignature)
        {
            this.TransactionId = transactionId;
            this.SkuDetail = skuDetail;
            this.PurchaseData = purchaseData;
            this.DataSignature = dataSignature;
        }
    }

    public PurchaseResult purchaseResult;

    public delegate void OnPurchaseResultCallback(PurchaseResult result);

    public OnPurchaseResultCallback onPurchaseResultCallback;

    public OnPurchaseResultCallback onPurchaseAckResultCallback;

    public delegate void OnRequestGameCallback(string sku, string purchaseData, string appAccountToken, OnResponseFromGameServerCallback responseCallback);

    public OnRequestGameCallback onRequestGameCallback;


    public delegate void OnRequestAckCallback(int buyNo, int dataCode, OnAckResponseFromGameServerCallback responseCallback);
    public OnRequestAckCallback onRequestAckCallback;

    public delegate void OnResponseFromGameServerCallback(ResponseDto<AcquiredItemDto> responseCode, int buyNo, int dataCode);


    public delegate void OnAckResponseFromGameServerCallback(ResponseDto<String> ackResponseCode);


    string appAccountToken; 

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

    Action<bool> RestoreCallback;

    public static void Restore(Action<bool> callback)
    {
        Debug.Log("Restore");
        PurchaseController.Instance.RestoreCallback = callback;
        PurchaseController.Instance.appleExtensions.RestoreTransactions(PurchaseController.Instance.OnRestore);
    }

    void OnRestore(bool success)
    {
        var restoreMessage = "";
        if (success)
        {
            // This does not mean anything was restored,
            // merely that the restoration process succeeded.
            restoreMessage = "Restore Successful";
        }
        else
        {
            // Restoration failed.
            restoreMessage = "Restore Failed";
        }

        RestoreCallback(success);

        Debug.Log(restoreMessage);
    }


    // 초기화 성공해서 해당 정보 데이터 셋팅.
    IAppleExtensions appleExtensions;
    public static void SetApplicationUsername(string guid)
    {
        PurchaseController.Instance.appleExtensions.SetApplicationUsername(guid);
        Debug.Log("SetApplicationUsername Complete.");
    }
    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        Debug.Log("In-App Purchasing successfully initialized");

#if  UNITY_IOS
        appleExtensions = extensions.GetExtension<IAppleExtensions>();
        appleExtensions.RegisterPurchaseDeferredListener(PurchaseController.Instance.OnDeferred);
#endif

        StoreController = controller;

        foreach (Product item in controller.products.all)
        {
            if (item.availableToPurchase)
            {
                if(PurchaseController.Instance.ProductMap.ContainsKey(item.definition.id))
                {
                    var data = PurchaseController.Instance.ProductMap[item.definition.id];

                    data.CurrencyString = item.metadata.localizedPriceString;
                    data.Symbol = ToCurrenySymbol(item.metadata.localizedPriceString);
                    data.LocalizedAmount = item.metadata.localizedPriceString.Replace(data.Symbol, string.Empty);
                    data.Amount = (double)item.metadata.localizedPrice;
                    data.ISOCurrencyCode = item.metadata.isoCurrencyCode;

                    Debug.Log("data.Sku = " + data.Sku);
                    Debug.Log("data.Type = " + data.Type.ToString());
                    Debug.Log("data.CurrencyString = " + data.CurrencyString);
                    Debug.Log("data.LocalizedAmount = " + data.LocalizedAmount);
                    Debug.Log("data.Symbol = " + data.Symbol);
                    Debug.Log("data.Amount = " + data.Amount);
                    Debug.Log("data.LocalPrice = " + data.LocalPrice);
                    Debug.Log("data.ISOCurrencyCode = " + data.ISOCurrencyCode);
                }
            }
        }


        if (PurchaseController.initializeCallback != null)
        {
            PurchaseController.initializeCallback(true);
            PurchaseController.initializeCallback = null;
        }
    }
    public void OnDeferred(UnityEngine.Purchasing.Product item)
    {
        
    }



    public string ToCurrenySymbol(string str)
    {
        string sPattern = @"[\d-,.]";
        str = System.Text.RegularExpressions.Regex.Replace(str, sPattern, string.Empty);
        return str;
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        SBDebug.Log($"In-App Purchasing initialize failed: {error}");
        if(PurchaseController.initializeCallback != null)
        {
            PurchaseController.initializeCallback(false);
            PurchaseController.initializeCallback = null;
        }
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        SBDebug.Log($"In-App Purchasing initialize failed case 02 : {error}");
        if(PurchaseController.initializeCallback != null)
        {
            PurchaseController.initializeCallback(false);
            PurchaseController.initializeCallback = null;
        }
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        var product = args.purchasedProduct;

        Debug.Log($"ProcessPurchase - Product: {product.definition.id}");

        string sku = args.purchasedProduct.definition.id;
        string transactionId = args.purchasedProduct.transactionID;
        string receipt = args.purchasedProduct.receipt;

        Debug.Log("sku = " + sku);
        Debug.Log("transactionId = " + transactionId);
        Debug.Log("receipte = " + receipt); // (테스트 결제 로그 예시 값은 공개용으로 제거)

        // 소비형이면 기존 프로세스
        if (args.purchasedProduct.definition.type == ProductType.Consumable)
        {
            if (PurchaseController.IsPurchaseStart)
            {
                CoroutineTaskManager.AddTask(PurchaseController.Instance.ProcessPurchaseComplete(sku, transactionId, receipt));
            }
            else
            {
                this.PendingProduct = new PendingProductInfo(sku, transactionId, receipt);
            }

            return PurchaseProcessingResult.Pending;
        }
        // 구독이면 
        else
        {
            // 플레이어가 구독중이면.
            if(GameStorage.PlayerStorage.IsSubscription)
            {
                return PurchaseProcessingResult.Complete;
            }
            // 구독중이 아니면 잘못 되었으니 서버 처리 요청.
            else
            {
                if (PurchaseController.IsPurchaseStart)
                {
                    CoroutineTaskManager.AddTask(PurchaseController.Instance.ProcessPurchaseComplete(sku, transactionId, receipt));
                }
                else
                {
                    this.PendingProduct = new PendingProductInfo(sku, transactionId, receipt);
                }

                return PurchaseProcessingResult.Pending;
            }
        }
    }

    // 로비에서 호출하기로하자. 펜딩된 상품있으면...
    public static void PurchasingFinishTransaction(OnRequestGameCallback reqeust, OnRequestAckCallback ackRequest, OnPurchaseResultCallback callback)
    {
        PurchaseController.Instance.onRequestGameCallback = reqeust;
        PurchaseController.Instance.onRequestAckCallback = ackRequest;
        PurchaseController.Instance.onPurchaseResultCallback = callback;

        var data = PurchaseController.Instance.PendingProduct;


        PurchaseController.Instance.appAccountToken = PlayerPrefs.GetString(data.Sku);

        CoroutineTaskManager.AddTask(PurchaseController.Instance.ProcessPurchaseComplete(data.Sku, data.TransactionId, data.Receipt));
    }


    private IEnumerator ProcessPurchaseComplete(string sku, string transactionID, string receipt)
    {
        string purchaseData = string.Empty;
        string signature = string.Empty;
        var wrapper = (Dictionary<string, object>)UnityEngine.Purchasing.MiniJson.JsonDecode(receipt);
        if (null != wrapper)
        {           
            purchaseData = (string)wrapper["Payload"];

            if (Application.platform == RuntimePlatform.Android)
            {
                var details = (Dictionary<string, object>)UnityEngine.Purchasing.MiniJson.JsonDecode(purchaseData);
                purchaseData = (string)details["json"];
                signature = (string)details["signature"];
            }
        }
        else
        {
            Debug.LogErrorFormat("ProcessPurchaseComplete. but json decode error!! receipt:{0}", receipt);
        }

        string transactionId = transactionID;
        string skuDetail = string.Empty;

        if (Application.platform == RuntimePlatform.Android)
        {
            skuDetail = StoreController.products.WithID(sku).metadata.GetGoogleProductMetadata().originalJson;
        }
 
        string dataSignature = signature;   // iOS는 빈값.

        Debug.Log("skuDetail = " + skuDetail); // (테스트 결제 로그 예시 값은 공개용으로 제거)

        Debug.Log("purchaseData = " + purchaseData); // (테스트 결제 로그 예시 값은 공개용으로 제거)

        Debug.Log("transactionId = " + transactionId); // (테스트 결제 로그 예시 값은 공개용으로 제거)

        Debug.Log("signature = " + signature); // (테스트 결제 로그 예시 값은 공개용으로 제거)


        purchaseResult = new PurchaseResult(transactionId, skuDetail, purchaseData, dataSignature);
                                                                                                   
        // 서버 처리.
        yield return ProcessToServer(sku, purchaseData, null);

        if(purchaseResult.ResponseCode == ResponseCode.OK)
        {
            StoreController.ConfirmPendingPurchase(StoreController.products.WithID(sku));

            if (PlayerPrefs.HasKey(sku))
                PlayerPrefs.DeleteKey(sku);

            if (!IsPurchaseStart)
            {
                PendingProduct = null;
            }

            yield return ProcessToServerAck(purchaseResult.buyNo, purchaseResult.dataCode);
        }
        /* 다른 방법으로 추후 수정예정.
        // 해당 에러코드고, 3번연속 펜딩에 실패..
        else if((purchaseResult.ResponseCode == ResponseCode.GameNotSellNow ||
                  purchaseResult.ResponseCode == ResponseCode.GameTryOverDailyBuy ||
                  purchaseResult.ResponseCode == ResponseCode.GameTryOverTotalBuy ||
                  purchaseResult.ResponseCode == ResponseCode.GameAlreadyPremiumPass) &&
                  this.IsConfirmPendingContinueFailed(sku))
        {
            StoreController.ConfirmPendingPurchase(StoreController.products.WithID(sku));

            if (PlayerPrefs.HasKey(sku))
                PlayerPrefs.DeleteKey(sku);

            this.DeleteConfirmPendingPlayerPrefs(sku);

            if (!IsPurchaseStart)
            {
                PendingProduct = null;
            }

            purchaseResult.isSkipErrorPopup = true;
        }*/

        onPurchaseResultCallback(purchaseResult);
    }

    /* 다른 방법으로 추후 수정예정.
    private bool IsConfirmPendingContinueFailed(string sku)
    {
        string key = string.Format("Pending_{0}", sku);
        if(PlayerPrefs.HasKey(key) && PlayerPrefs.GetInt(key) >= 2)
        {
            return true;
        }

        if(PlayerPrefs.HasKey(key))
        {
            int value = PlayerPrefs.GetInt(key);
            PlayerPrefs.SetInt(key, value + 1);
        }
        else
        {
            PlayerPrefs.SetInt(key, 1);
        }

        return false;
    }

    private void DeleteConfirmPendingPlayerPrefs(string sku)
    {
        string key = string.Format("Pending_{0}", sku);

        if (PlayerPrefs.HasKey(key))
        {
            PlayerPrefs.DeleteKey(key);
        }
    }*/

    /*private void ConfirmPendingPurchase()
    {
        StoreController.ConfirmPendingPurchase(StoreController.products.WithID(sku));

        if (!IsPurchaseStart)
        {
            PendingProduct = null;
        }
    }*/


    // 실제에서 사용하지 않은 함수임.
    /*public static void ForcedTransaction(string sku)
    {
        PurchaseController.Instance.StoreController.ConfirmPendingPurchase(PurchaseController.Instance.StoreController.products.WithID(sku));
    }*/

    private IEnumerator ProcessToServer(string sku, string purchaseData, object extraArgs)
    {
        bool isWait = true;

        Debug.Log("~~appAccountToken = " + appAccountToken);

        PurchaseController.Instance.onRequestGameCallback(sku, purchaseData, appAccountToken, (result, buyNo, dataCode) =>
        {
            isWait = false;
            purchaseResult.buyNo = buyNo;
            purchaseResult.dataCode = dataCode;
            purchaseResult.ResponseCode = (ResponseCode)result.code;
            purchaseResult.AcquiredDto = result.data;
        });

        while (isWait)
            yield return null;
    }


    private IEnumerator ProcessToServerAck(int buyNo, int dataCode)
    {
        bool isWait = true;

        PurchaseController.Instance.onRequestAckCallback(buyNo, dataCode, (result) =>
        {
            isWait = false;
            purchaseResult.AckResponseCode = (ResponseCode)result.code;
        });

        while (isWait)
            yield return null;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        LoadingIndicator.Hide();
        SBDebug.Log($"Purchase failed - Product: '{product.definition.id}', PurchaseFailureReason: {failureReason}");
    }


    public static bool IsSubscribedTo(string sku)
    {
#if UNITY_EDITOR
        return false;
#endif

        var Product = PurchaseController.Instance.StoreController.products.WithID(sku);

        return PurchaseController.Instance.IsSubscribedTo(Product);
    }


    // 구독중인지 확인.
    bool IsSubscribedTo(Product subscription)
    {
        // If the product doesn't have a receipt, then it wasn't purchased and the user is therefore not subscribed.
        if (subscription.receipt == null)
        {
            return false;
        }

        //The intro_json parameter is optional and is only used for the App Store to get introductory information.
        var subscriptionManager = new SubscriptionManager(subscription, null);

        // The SubscriptionInfo contains all of the information about the subscription.
        // Find out more: https://docs.unity3d.com/Packages/com.unity.purchasing@3.1/manual/UnityIAPSubscriptionProducts.html
        var info = subscriptionManager.getSubscriptionInfo();

        return info.isSubscribed() == Result.True;
    }




    // 첫 구매 날짜 구하기.
    public static DateTime GetPurchaseDate(string sku)
    {
        var Product = PurchaseController.Instance.StoreController.products.WithID(sku);
        return PurchaseController.Instance.GetPurchaseDate(Product);

    }
    /// <summary>
    /// 제품의 구매 날짜를 반환합니다.
    /// Apple의 경우 구매 날짜는 구독을 구매하거나 갱신한 날짜입니다.Google의 경우 구매 날짜는 구독을 처음 구매한 날짜입니다.
    /// </summary>
    /// <param name="subscription"></param>
    /// <returns></returns>
    DateTime GetPurchaseDate(Product subscription)
    {
        if (subscription.receipt != null)
        {
            var subscriptionManager = new SubscriptionManager(subscription, null);
            var info = subscriptionManager.getSubscriptionInfo();

            return info.getPurchaseDate();
        }

        return DateTime.MinValue;
    }


    public static DateTime GetExpireDate(string sku)
    {
        var Product = PurchaseController.Instance.StoreController.products.WithID(sku);
        return PurchaseController.Instance.GetExpireDate(Product);
    }

    /// <summary>
    /// 제품의 다음 자동 갱신 또는 만료 날짜를 반환합니다(취소된 자동 갱신 구독의 경우).
    /// 애플리케이션이 Android 인앱 결제 API 버전 6 이상을 지원하지 않는 경우 Google Play 스토어의 제품은 TimeSpan.MaxValue를 반환합니다.
    /// </summary>
    /// <param name="subscription"></param>
    /// <returns></returns>
    DateTime GetExpireDate(Product subscription)
    {
        if (subscription.receipt != null)
        {
            var subscriptionManager = new SubscriptionManager(subscription, null);
            var info = subscriptionManager.getSubscriptionInfo();

            return info.getExpireDate();
        }

        return DateTime.MinValue;
    }




    /// <summary>
    /// 이 제품이 만료되었는지 여부를 나타내는 결과 열거형을 반환합니다.
    /// * Apple Store의 재생 불가 제품은 Result.Unsupported값을 반환합니다.
    /// * Apple 스토어의 자동 갱신 제품 및 Google Play 스토어의 구독 제품은 Result.True또는 Result.False값을 반환합니다.
    /// </summary>
    /// <param name="subscription"></param>
    /// <returns></returns>
    bool IsExpired(Product subscription)
    {
        if (subscription.receipt != null)
        {
            bool isExpired = false;

            var subscriptionManager = new SubscriptionManager(subscription, null);
            var info = subscriptionManager.getSubscriptionInfo();

            switch(info.isExpired())
            {
                case Result.False:
                    isExpired = false;
                    break;

                default:
                    isExpired = true;
                    break;
            }

            return isExpired;
        }

        return true;
    }


}
