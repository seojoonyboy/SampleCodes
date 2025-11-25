using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using com.snowballs.SWHJ.client.model;
using com.snowballs.SWHJ.Ext.Event;
using com.snowballs.SWHJ.presenter;
using com.snowballs.SWHJ.type;
using Snowballs.Sheets;
using Snowballs.Sheets.Data;
using Snowballs.Util;
using TMPro;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif
using UnityEngine.UI;

/// <summary>
/// 하트 남은 시간은 시간을 분으로 환산하여 표시
/// 무제한 하트 발동중인 경우 하트 영역에 무제한 표시 + 무제한 하트의 남은 시간을 텍스트에 표시
/// 무제한 하트가 발동중이 아니고, 남은 하트 수가 5개 이하일 때 충전까지 남은 시간을 텍스트에 표시
/// 남은 하트가 99개 이상일 때는 99+ 로 표시
/// 무제한 하트 발동중인 경우 추가로 무제한 하트를 얻는 경우 남은 시간을 늘려준다. (새로 발동되는 양만큼..)
/// (optional) 하트가 MAX 또는 무제한 하트 발동중인 경우 + 아이콘 제거
/// </summary>
public class HeartAutoCharger : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI heartAmountText;
    [SerializeField] private TextMeshProUGUI heartTimerText;

    [SerializeField] private Image heartIcon;
    [SerializeField] private Sprite unlimitedHeartImage;
    [SerializeField] private Sprite normalHeartImage;

    private int chargeTime;             //하트 충전 걸리는 시간
    private int chargeAmount;           //하트 한번에 몇개씩 충전할지
    private int chargeStandard;         //하트 충전 시작 기준

    private bool isOpened = false;

    public void OnOpen()
    {
        this.isOpened = true;
        BroadcastTunnel<string, int>.Add("com.snowballs.SWHJ.AddActiveItem", this.OnConsumeItemEvent);
        BroadcastTunnel<string, int>.Add("com.snowballs.SWHJ.AddUsableItem", this.OnConsumeItemEvent);
    }

    private void OnConsumeItemEvent(int paymentItem)
    {
        if(!this.isOpened) return;

        Debug.Log("Heart 관련 ConsumeItem Event 감지됨");
        try
        {
            this.CheckCondition();
        }
        catch (Exception) { }
    }

    private DateTime? focusOutTime;                                 //백그라운드로 빠져나갈 당시 DateTime
    private DateTime? focusOutChargeRefTime;                        //백그라운드로 빠져나갈 당시 서버 충전 시작 시간
    private DateTime? focusOutNextHeartChargeTime;                  //백그라운드로 빠져나갈 당시 하트 1개 충전예정 시간
    private TimeSpan? beforeFocusOutRemainUnlimitedHeartTimeSpan;
    private bool hasFocus = true;

    public void SetHeartPushAfterQuit(Action cb)
    {
        if (GameStorage.IsPushNotification)
        {
            PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();

            if (playerStorage.Heart < this.chargeStandard)
            {
                //충전을 하기 시작한 서버 시간
                DateTime dtoChargeRefTime = SBTime.Instance
                    .dtoTimeToServerTime(playerStorage.HeartDto.chargeReferenceTime);
                DateTime now = SBTime.Instance.ServerTime;

                //충전 완료예정 시간
                DateTime nextChargeFinishedDateTime = dtoChargeRefTime.AddSeconds(this.chargeTime);
                SBDebug.Log(string.Format("<color=green>SJW #01 focusOutNextHeartChargeTime : {0}</color>", this.focusOutNextHeartChargeTime));

                //하트 MAX 충전까지 남은 시간
                int restNumberToCharge = (int)(this.chargeStandard - playerStorage.Heart);
                var restDateTimeToFullCharge = nextChargeFinishedDateTime
                    .AddSeconds((restNumberToCharge - 1) * this.chargeTime);
                var timeSpanRestTimeToFullCharge = restDateTimeToFullCharge - now;

                if (timeSpanRestTimeToFullCharge.TotalSeconds < 60) return;

                // this.ScheduleHeartFullNotification(
                //     (int)timeSpanRestTimeToFullCharge.TotalSeconds,
                //     "하트 충전 알림",
                //     "하트가 모두 충전되었습니다."
                // );
                //
                // SBDebug.Log(string.Format("<color=green>OnApplicationQuit {0}초 후 로컬 푸시 예정...</color>", (int)timeSpanRestTimeToFullCharge.TotalSeconds));
            }

            cb?.Invoke();
        }
    }

    public void CancelAllNotifications()
    {
#if UNITY_ANDROID
        AndroidNotificationCenter.CancelAllNotifications();
#endif
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if(hasFocus) OnFocus();
        else OnFocusOut();

        this.hasFocus = hasFocus;

        void OnFocus()
        {
            this.CancelAllNotifications();

            //재시작 기준 시작 [Config 값. Default 값은 1200초 -> 20분]
            int restartConfigValue = 1200;

            //test code
            // restartConfigValue = 120;
            //end test code

            int configCode = 13;
            if (SBDataSheet.Instance.GameConfig[configCode] != null)
                restartConfigValue = SBDataSheet.Instance.GameConfig[configCode].integerValue;

            var now = SBTime.Instance.ServerTime;
            if (this.focusOutTime.HasValue)
            {
                TimeSpan returnTimeSpan = now - this.focusOutTime.Value;
                if (returnTimeSpan.TotalMinutes * 60 >= restartConfigValue)
                {
                    GameScene.Instance.OnRestart();
                    return;
                }

                if (this.focusOutNextHeartChargeTime.HasValue && this.focusOutChargeRefTime.HasValue)
                {
                    SBDebug.Log(string.Format("<color=yellow> {0}초 후에 복귀함 </color>", returnTimeSpan.TotalMinutes * 60));

                    PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
                    var unlimitedHeartItems = playerStorage.ActiveItems.ToList()
                        .FindAll(x => (x.code == 1) && (SBTime.Instance.dtoTimeToServerTime(x.endedAt) >= SBTime.Instance.ServerTime));
                    bool isUnlimitedItemExist = unlimitedHeartItems.Count > 0;
                    if (isUnlimitedItemExist)
                    {
                        SBDebug.Log("<color=green>SJW #02 무제한 하트 갱신 </color>");
                    }
                    else
                    {
                         //이미 하트가 MAX인 경우 발동하지 않는다.
                        if (playerStorage.Heart >= this.chargeStandard)
                        {
                            SBDebug.Log("<color=green>SJW #02 하트 가득참 </color>");
                            // Reset();
                            return;
                        }

                        //다음 충전 예정시간 이후로 돌아온 경우
                        if (this.focusOutNextHeartChargeTime.Value < now)
                        {
                            SBDebug.Log(string.Format("<color=green>SJW #03 일반 하트 충전시간 뒤에 돌아옴 </color>"));

                            TimeSpan timeSpanBetweenNowAndChargeRefTime =
                                now.Subtract(this.focusOutNextHeartChargeTime.Value);

                            int res1 = (int)((float)timeSpanBetweenNowAndChargeRefTime.TotalMinutes * 60 /
                                             this.chargeTime) + 1;
                            int wantChargeAmount = res1 * this.chargeAmount;

                            DateTime nextChargeRefTime = this.focusOutChargeRefTime.Value
                                .AddSeconds(this.chargeTime * res1);
                            string strNextChargeRefTime =
                                SBTime.Instance.localTimeToUTC(nextChargeRefTime).ToString("o");
                            SBDebug.Log(string.Format("<color=green>SJW #03 strNextChargeRefTime 값 : {0}</color>",
                                strNextChargeRefTime));

                            playerStorage.Heart += wantChargeAmount;
                            if (playerStorage.Heart > this.chargeStandard) playerStorage.Heart = this.chargeStandard;
                            // playerStorage.HeartDto.chargeReferenceTime = strNextChargeRefTime;
                        }
                        else
                        {
                            SBDebug.Log(string.Format("<color=green>SJW #03 일반 하트 충전시간 이전에 돌아옴 </color>"));
                        }
                    }
                }
            }

            Reset();
            this.CheckCondition();
        }

        void OnFocusOut()
        {
            this.focusOutTime = SBTime.Instance.ServerTime;

            PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
            var unlimitedHeartItems = playerStorage.ActiveItems.ToList()
                .FindAll(x => (x.code == 1) && (SBTime.Instance.dtoTimeToServerTime(x.endedAt) >= SBTime.Instance.ServerTime));

            bool isUnlimitedItemExist = unlimitedHeartItems.Count > 0;
            if (isUnlimitedItemExist)
            {
                //무제한 하트가 종료되는 시간
                var endAt =  SBTime.Instance
                    .dtoTimeToServerTime(unlimitedHeartItems[0].endedAt);
                DateTime now = SBTime.Instance.ServerTime;

                this.beforeFocusOutRemainUnlimitedHeartTimeSpan = endAt.Subtract(now);

                SBDebug.Log(string.Format("<color=green>SJW #01 Focus Out 시간 : {0}</color>", now));
                SBDebug.Log(string.Format("<color=green>SJW #01 무제한 하트 종료까지 남은 시간 {0}</color>", this.beforeFocusOutRemainUnlimitedHeartTimeSpan));
            }
            else
            {
                //1. 일반 하트가 한개 충전되려면 남은 시간을 계산
                //이미 하트가 MAX인 경우 발동하지 않는다.
                if (playerStorage.Heart >= this.chargeStandard)
                {
                    SBDebug.Log("<color=green>SJW #02 하트 가득참 </color>");
                    // Reset();
                    return;
                }

                //충전을 하기 시작한 서버 시간
                DateTime dtoChargeRefTime = SBTime.Instance
                    .dtoTimeToServerTime(playerStorage.HeartDto.chargeReferenceTime);
                DateTime now = SBTime.Instance.ServerTime;

                this.focusOutChargeRefTime = dtoChargeRefTime;

                //충전 완료예정 시간
                this.focusOutNextHeartChargeTime = AddInterval(this.focusOutChargeRefTime.Value);
                SBDebug.Log(string.Format("<color=green>SJW #01 focusOutNextHeartChargeTime : {0}</color>", this.focusOutNextHeartChargeTime));

                if (GameStorage.IsPushNotification && this.IsLocalPushAvailable())
                {
                    //하트 MAX 충전까지 남은 시간
                    int restNumberToCharge = (int)(this.chargeStandard - playerStorage.Heart);
                    var restDateTimeToFullCharge = this.focusOutNextHeartChargeTime.Value
                        .AddSeconds((restNumberToCharge - 1) * this.chargeTime);
                    var timeSpanRestTimeToFullCharge = restDateTimeToFullCharge - now;
                    // this.ScheduleHeartFullNotification(
                    //     (int)timeSpanRestTimeToFullCharge.TotalSeconds,
                    //     "하트 충전 알림",
                    //     "하트가 모두 충전되었습니다."
                    // );
                    //
                    // SBDebug.Log(string.Format("<color=green>SJW on focus out {0}초 후 로컬 푸시 예정...</color>", timeSpanRestTimeToFullCharge.TotalSeconds));
                }
            }

            DateTime AddInterval(DateTime serverBeginTime)
            {
                var endTime = serverBeginTime.AddSeconds(this.chargeTime);
                while (endTime < SBTime.Instance.ServerTime)
                {
                    SBDebug.Log("AddInterval");
                    endTime = endTime.AddSeconds(this.chargeTime);
                }

                SBDebug.Log(endTime.ToString("o"));
                return endTime;
            }
        }

        void Reset()
        {
            this.focusOutTime = null;
            this.focusOutChargeRefTime = null;
            this.focusOutNextHeartChargeTime = null;
            this.beforeFocusOutRemainUnlimitedHeartTimeSpan = null;
        }
    }

    public void CheckCondition()
    {
        StopAllCoroutines();

        if(SceneController.currentSceneSceneId < SceneController.Scene.Lobby) return;

        if(!this.isOpened) return;
        if(this.gameObject == null || !this.gameObject.activeInHierarchy) return;

        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();

        Debug.Log("<color=orange>HeartAutoCharger CheckCondition</color>");
        int heartItemProductionCode = (int)PaymentItem.Heart;
        int timeChargeInfoCode = SBDataSheet.Instance.UsableItemInfo[heartItemProductionCode].TimeChargeInfo;
        this.chargeTime = SBDataSheet.Instance.TimeChargeItemInfo[timeChargeInfoCode].ChargeTime;
        this.chargeAmount = SBDataSheet.Instance.TimeChargeItemInfo[timeChargeInfoCode].ChargeValue;
        this.chargeStandard = ((GameStorage.PlayerStorage.IsSubscription) ? SBDataSheet.Instance.SubscribeBuff[1].GetBuffByBuff().Value + SBDataSheet.Instance.TimeChargeItemInfo[timeChargeInfoCode].ChargeMaxValue : SBDataSheet.Instance.TimeChargeItemInfo[timeChargeInfoCode].ChargeMaxValue);

        if (playerStorage.ActiveItems == null) return;
        var unlimitedHeartItems = playerStorage.ActiveItems.ToList().FindAll(x => (x.code == 1) && (SBTime.Instance.dtoTimeToServerTime(x.endedAt) >= SBTime.Instance.ServerTime));
        //무제한 하트가 존재하는 경우
        if (unlimitedHeartItems.Count > 0)
        {
            DateTime endTimeDT = SBTime.Instance.dtoTimeToServerTime(unlimitedHeartItems[0].endedAt);
            if(endTimeDT <= SBTime.Instance.UTCServerTime){ return; }
            Debug.Log("<color=yellow>HEART 무제한 하트 감지되었고, endedAt : " + endTimeDT + "</color>");

            this.BeginUnlimitedHeartRemainTimer(endTimeDT);
        }
        //무제한 하트가 존재하지 않음
        else
        {
            this.heartIcon.sprite = this.normalHeartImage;

            if (playerStorage.Heart < this.chargeStandard)
            {
                //충전을 하기 시작한 서버 시간
                var dtoChargeRefTime = SBTime.Instance
                    .dtoTimeToServerTime(playerStorage.HeartDto.chargeReferenceTime);

                this.BeginHeartAutoChargeTimer(dtoChargeRefTime);
            }
            else
            {
                this.StopTimer();
                if (playerStorage.Heart >= this.chargeStandard)
                {
                    this.heartTimerText.text = "MAX";
                }

                if (playerStorage.Heart > 99)
                {
                    this.heartAmountText.text = "99+";
                }
                else
                {
                    this.heartAmountText.text = playerStorage.Heart.ToString();
                }
            }
        }
    }

    Coroutine timerCoroutine1 = null;
    Coroutine timerCoroutine2 = null;
    //타이머 정지
    public void StopTimer()
    {
        if(this == null) return;

        if (timerCoroutine1 != null) StopCoroutine(timerCoroutine1);
        if (timerCoroutine2 != null) StopCoroutine(timerCoroutine2);
    }

    private bool IsLocalPushAvailable()
    {
        try
        {
            int pushOnOffConfigCode = 12;
            return SBDataSheet.Instance.GameConfig[pushOnOffConfigCode].booleanValue;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    //하트 자동충전에 대한 타이머 (endTime값은 asset 정보로 전달)
    public void BeginHeartAutoChargeTimer(DateTime serverBeginTime)
    {
        //이미 동작중인 경우 기존꺼를 멈추고 다시 시작한다.
        this.StopTimer();

        CancellableSignal signal = new CancellableSignal(() => { return this == null; });
        if (!gameObject.activeInHierarchy) return;


        this.timerCoroutine1 = StartCoroutine(HeartAutoChargeRemainingTimeTask(signal, serverBeginTime));

        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();

        if (playerStorage.Heart > 99)
        {
            this.heartAmountText.text = "99+";
            this.heartTimerText.text = "MAX";
        }
        else
        {
            this.heartAmountText.text = playerStorage.Heart.ToString();
        }

        this.heartIcon.sprite = this.normalHeartImage;
        Debug.Log("<color=yellow>HEART 자동충전 동작시킵니다.</color>");
    }

    //하트 무제한 남은시간에 대한 타이머 (endTimer값은 PlayerDto 값 전달)
    public void BeginUnlimitedHeartRemainTimer(DateTime endTime)
    {
        //이미 동작중인 경우 기존꺼를 멈추고 다시 시작한다.
        this.StopTimer();

        CancellableSignal signal = new CancellableSignal(() => { return this == null; });
        if (!gameObject.activeInHierarchy) return;

        this.timerCoroutine2 = StartCoroutine(UnlimitedHeartRemainingTimeTask(signal, endTime));

        this.heartAmountText.text = String.Empty;
        this.heartIcon.sprite = this.unlimitedHeartImage;
        Debug.Log("<color=yellow>HEART 무제한 타이머 동작시킵니다.</color>");
    }

    //하트 자동충전에 대한 코루틴
    IEnumerator HeartAutoChargeRemainingTimeTask(CancellableSignal signal, DateTime serverBeginTime)
    {
        yield return null;

        DateTime endTime = AddInterval(serverBeginTime);

        var wfef = new WaitForEndOfFrame();

        DateTime now = serverBeginTime;
        TimeSpan remainingTime;
        do
        {
            if (!gameObject.activeInHierarchy) { yield break; }

            now = SBTime.Instance.ServerTime;

            if(!this.hasFocus) yield break;

            if (now <= endTime)
            {
                remainingTime = endTime - now;
                this.UpdateRemainingTimeText(remainingTime);
            }
            else
            {
                Debug.Log("HeartAutoChargeRemainingTimeTask 타임아웃!");
                PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
                playerStorage.Heart += this.chargeAmount;

                Debug.Log("AddSec : " + this.chargeTime);
                DateTime nextDateTime = SBTime.Instance.UTCServerTime;

                SBDebug.Log(string.Format("playerStorage.HeartDto.chargeReferenceTime : {0}", nextDateTime));
                SBDebug.Log(string.Format("playerStorage.HeartDto.nextDateTime.ToString('o') : {0}", nextDateTime.ToString("o")));

                // playerStorage.HeartDto.chargeReferenceTime = nextDateTime.ToString("o");
                this.heartAmountText.text = playerStorage.Heart.ToString();

                this.CheckCondition();
            }

            yield return wfef;
            if (CancellableSignal.IsCanceled(signal)) { yield break; }
        }
        while (now <= endTime);

        DateTime AddInterval(DateTime serverBeginTime)
        {
            var endTime = serverBeginTime.AddSeconds(this.chargeTime);
            while (endTime < SBTime.Instance.ServerTime)
            {
                SBDebug.Log("AddInterval");
                endTime = endTime.AddSeconds(this.chargeTime);
            }

            SBDebug.Log(endTime.ToString("o"));
            return endTime;
        }
    }

    /// <summary>
    /// 남은 시간
    /// </summary>
    /// <param name="signal"></param>
    /// <param name="endTime"></param>
    /// <returns></returns>
    IEnumerator UnlimitedHeartRemainingTimeTask(CancellableSignal signal, DateTime endTime)
    {
        var wfef = new WaitForEndOfFrame();

        DateTime now;
        TimeSpan remainingTime;
        do
        {
            if(!gameObject.activeSelf) { yield break; }
            now = SBTime.Instance.ServerTime;

            // if(!this.hasFocus) yield break;

            if (now <= endTime)
            {
                remainingTime = endTime - now;
                this.UpdateRemainingTimeText(remainingTime);
            }
            else
            {
                this.StopTimer();
                this.CheckCondition();
            }

            yield return wfef;
            if (CancellableSignal.IsCanceled(signal)) { yield break; }
        }
        while (now <= endTime);
    }

    private void UpdateRemainingTimeText(TimeSpan remainTime)
    {
        string output = string.Format("{0:D2}:{1:D2}", (int)remainTime.TotalMinutes, remainTime.Seconds);
        this.heartTimerText.text = output;
    }

    private void OnDestroy()
    {
        if(this.timerCoroutine1 != null) StopCoroutine(this.timerCoroutine1);
        if(this.timerCoroutine2 != null) StopCoroutine(this.timerCoroutine2);

        BroadcastTunnel<string, int>.Remove("com.snowballs.SWHJ.AddActiveItem", this.OnConsumeItemEvent);
        BroadcastTunnel<string, int>.Remove("com.snowballs.SWHJ.AddUsableItem", this.OnConsumeItemEvent);
    }

    private const string channel_id = "heartChange";
    private int channel_index = -1;
    private void ScheduleHeartFullNotification(int maxAfterSeconds, string localeHeaderText, string localeContext)
    {
#if UNITY_ANDROID
        SBDebug.Log("ScheduleHeartFullNotification");

        var channel = new AndroidNotificationChannel()
        {
            Id = channel_id,
            Name = "Default Channel",
            Importance = Importance.Default,
            Description = "Generic notifications"
        };

        AndroidNotificationCenter.RegisterNotificationChannel(channel);

        var notification = new AndroidNotification(
            localeHeaderText,
            localeContext,
            DateTime.Now.AddSeconds(maxAfterSeconds)
        );

        this.channel_index = AndroidNotificationCenter.SendNotification(
            notification,
            channel_id
        );
#endif
    }
}
