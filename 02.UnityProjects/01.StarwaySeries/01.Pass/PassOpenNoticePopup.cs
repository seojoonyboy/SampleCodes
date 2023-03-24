using System;
using System.Collections;
using System.Collections.Generic;
using com.snowballs.SWHJ.client.model;
using Snowballs.Sheets;
using Snowballs.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PassOpenNoticePopup : Popup
{
    [SerializeField] private Toggle today;
    
    [SerializeField] private RawImage mainImage;
    [SerializeField] private TextMeshProUGUI confirmButtonText;
    [SerializeField] private TextMeshProUGUI remainTimeText;

    public Func<DateTime> timeFunc = () =>
    {
        return SBTime.Instance.ServerTime;
    };

    private string playerLocale;
    public new class Params : Popup.Params
    {
        public Int32? toggleContextLocaleCode;
        public Texture2D mainTexture;
        
        public DateTime endTime;
    }
    
    public override void OnOpen()
    {
        base.OnOpen();

        this.playerLocale = GameStorage.Instance.GetStorage<PlayerStorage>().Locale;
        
        Params pB = this.paramBuffer as Params;
        mainImage.texture = pB.mainTexture;
        
        this.SetLocales(pB.toggleContextLocaleCode);
        
        var signal = new CancellableSignal(() =>
        {
            return this == null;
        });

        CoroutineTaskManager.AddTask(this.RemainingTimeTask(signal, pB.endTime));
    }

    private void SetLocales(Int32? toggleContextLocaleCode)
    {
        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();

        Int32 confirmButtonLocaleCode = 3;
        if (toggleContextLocaleCode.HasValue)
        {
            if (playerStorage.Locale == "ko")
            {
                this.confirmButtonText.text = SBDataSheet.Instance
                    .EventLocale[confirmButtonLocaleCode]
                    .KoKR;
            }
            else
            {
                this.confirmButtonText.text = SBDataSheet.Instance
                    .EventLocale[confirmButtonLocaleCode]
                    .EnUS;
            }
        }
    }
    
    IEnumerator RemainingTimeTask(CancellableSignal signal, DateTime endTime)
    {
        var wfef = new WaitForEndOfFrame();

        DateTime now;
        TimeSpan remainingTime;
        do
        {
            now = this.timeFunc();

            if (now <= endTime)
            {
                remainingTime = endTime - now;
                this.UpdateSeasonRemainingTime(remainingTime);
            }
            else
            {
                this.SetEndTime();
            }
            yield return wfef;
            if (CancellableSignal.IsCanceled(signal)) { yield break; }
        }
        while (now <= endTime);

        // this.SetEndTime();
    }

    private void SetEndTime()
    {
        base.OnTriggerX();
    }
    
    private void UpdateSeasonRemainingTime(TimeSpan time)
    {
        if (time.Days >= 1)
        {
            int targetLocaleCode = 301;
            string localeStr = string.Empty;
            if (playerLocale == "ko")
            {
                localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].KoKR;
            }
            else
            {
                localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].EnUS;
            }
            
            localeStr = localeStr.Replace("{0}", time.Days.ToString());
            localeStr = localeStr.Replace("{1}", time.Hours.ToString());
            this.remainTimeText.text = localeStr;
        }
        else if (time.Hours >= 1)
        {
            int targetLocaleCode = 302;
            string localeStr = string.Empty;
            if (playerLocale == "ko")
            {
                localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].KoKR;
            }
            else
            {
                localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].EnUS;
            }
            
            localeStr = localeStr.Replace("{0}", time.Hours.ToString());
            localeStr = localeStr.Replace("{1}", time.Minutes.ToString());
            this.remainTimeText.text = localeStr;
        }
        else
        {
            if (time.Minutes >= 1)
            {
                int targetLocaleCode = 304;
                string localeStr = string.Empty;
                if (playerLocale == "ko")
                {
                    localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].KoKR;
                }
                else
                {
                    localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].EnUS;
                }
                
                localeStr = localeStr.Replace("{0}", time.Minutes.ToString());
                localeStr = localeStr.Replace("{1}", time.Seconds.ToString());
                this.remainTimeText.text = localeStr;
            }
            else
            {
                int targetLocaleCode = 305;
                string localeStr = string.Empty;
                if (playerLocale == "ko")
                {
                    localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].KoKR;
                }
                else
                {
                    localeStr = SBDataSheet.Instance.SystemLocale[targetLocaleCode].EnUS;
                }
                
                localeStr = localeStr.Replace("{0}", time.Seconds.ToString());
                this.remainTimeText.text = localeStr;
            }
        }
    }

    public void OnCloseBtn()
    {
        result.args = false;
        base.OnTriggerX();
    }

    public void OnUrlMove()
    {
        result.args = true;
        base.OnTriggerOk();
    }
}
