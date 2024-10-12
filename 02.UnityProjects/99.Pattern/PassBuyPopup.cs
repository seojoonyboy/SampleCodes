using Snowballs.Client.Model;
using Snowballs.Client.View;
using Snowballs.Network.Dto;
using Snowballs.Sheets;
using System;
using System.Collections;
using Snowballs.Client.Scene;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PassBuyPopup : Popup
{
    #region Info
    [Space(10)]
    [Header("팝업 설명")]
    [Space(10)]
    [InfoBox("패스 구매하기 버튼을 눌렀을때 나타납니다.")]
    [HorizontalLine(color: EColor.Red)]
    [Space(20)]
    #endregion
    
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

     //   public Action<PassBuyPopup> OnClickBuy;

        public bool isBuyAvaliable;

        public string price;
        public string alreadyActiveText;

        public string infomationText;
        //     public int recommendStoreCode;
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

}
