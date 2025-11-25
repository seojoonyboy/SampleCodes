using System;
using System.Collections;
using com.snowballs.SWHJ.client.model;
using Snowballs.Network.API;
using Snowballs.Network.Dto;
using Snowballs.Sheets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AttendanceItem : MonoBehaviour
{
    [SerializeField] private GameObject openEffect;
    [SerializeField] private Image mainImage;
    [SerializeField] private Image rewardIcon;
    
    [SerializeField] private TextMeshProUGUI numberText;
    [SerializeField] private Button btn;
    
    private Action<int> onClick = null;
    
    private Texture2D frontCardImageTexture;
    private Texture2D backCardImageTexture;

    public int itemIndex = 0;

    private bool isOpened = false;
    public bool canOpen = true;
    
    public void Init(Param p)
    {
        this.itemIndex = p.itemIndex;
        
        this.isOpened = p.isOpened;
        
        this.frontCardImageTexture = p.frontCardImageTexture;
        this.backCardImageTexture = p.backCardImageTexture;

        if (this.isOpened)
        {
            mainImage.sprite = Sprite.Create(
                this.frontCardImageTexture, 
                new Rect(0.0f, 0.0f, this.frontCardImageTexture.width, this.frontCardImageTexture.height), 
                new Vector2(0.5f, 0.5f), 100f
            );

            if (p.todayReceivedReward.HasValue)
            {
                this.UpdateRewardedIcon(p.todayReceivedReward.Value);
            }
        }
        else
        {
            mainImage.sprite = Sprite.Create(
                this.backCardImageTexture, 
                new Rect(0.0f, 0.0f, this.backCardImageTexture.width, this.backCardImageTexture.height), 
                new Vector2(0.5f, 0.5f), 100f
            );
        }
    }

    public void UpdateRewardedIconWithAcquiredItems(AcquiredItemDto acquiredItemDto)
    {
        //Note. 여러개인 가능성은 없다고 가정..
        var acquiredItem = acquiredItemDto.acquiredItems[0];
        
        int itemCode = acquiredItem.code;
        int amount = (int)acquiredItem.count;
        
        this.rewardIcon.gameObject.SetActive(true);
        this.numberText.text = "x" + amount;
        
        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
        string iconAddress = String.Empty;
        if (playerStorage.Locale == "ko")
        {
            iconAddress = SBDataSheet.Instance.ItemResource[acquiredItem.code].KoKRAddress;
        }
        else
        {
            iconAddress = SBDataSheet.Instance.ItemResource[acquiredItem.code].EnUSAddress;
        }
        
        string iconPath = AssetPathController.PATH_FOLDER_ASSETS + iconAddress;

        WWWFile.DownloadPath downloadPath =
            new WWWFile.DownloadPath(WWWFile.TYPE.Bytes, String.Empty, iconPath);

        TextureController.GetTexture(downloadPath, false, (isSuccess, rewardIconTexture) =>
        {
            this.rewardIcon.sprite = Sprite.Create(
                rewardIconTexture, 
                new Rect(0.0f, 0.0f, rewardIconTexture.width, rewardIconTexture.height), 
                new Vector2(0.5f, 0.5f), 
                100f
            );
        });
    }

    public void UpdateRewardedIcon(int dailyRewardCode)
    {
        int itemCode = SBDataSheet.Instance.DailyReward[dailyRewardCode].Item;
        int amount = SBDataSheet.Instance.DailyReward[dailyRewardCode].Value;
        
        this.rewardIcon.gameObject.SetActive(true);
        this.numberText.text = "x" + amount;
        
        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
        string iconAddress = String.Empty;
        if (playerStorage.Locale == "ko")
        {
            iconAddress = SBDataSheet.Instance.ItemResource[itemCode].KoKRAddress;
        }
        else
        {
            iconAddress = SBDataSheet.Instance.ItemResource[itemCode].EnUSAddress;
        }

        string iconPath = AssetPathController.PATH_FOLDER_ASSETS + iconAddress;

        WWWFile.DownloadPath downloadPath =
            new WWWFile.DownloadPath(WWWFile.TYPE.Bytes, String.Empty, iconPath);

        TextureController.GetTexture(downloadPath, false, (isSuccess, rewardIconTexture) =>
        {
            this.rewardIcon.sprite = Sprite.Create(
                rewardIconTexture, 
                new Rect(0.0f, 0.0f, rewardIconTexture.width, rewardIconTexture.height), 
                new Vector2(0.5f, 0.5f), 
                100f
            );
        });
    }

    public class Param
    {
        public int itemIndex;
        public int? todayReceivedReward;
        
        public bool isOpened = false;
        
        public Texture2D frontCardImageTexture;
        public Texture2D backCardImageTexture;
    }

    public bool CanClick()
    {
        if (this.isOpened) return false;
        if (!this.canOpen) return false;
        
        return true;
    }

    public void OnClick(AttendanceCheckPopup popup, Action<int, AcquiredItemDto> cb)
    {
        this.isOpened = true;
        this.canOpen = false;
        
        popup.BlockCloseButton();
        
        CoroutineTaskManager.AddTask(Tasks(cb));

        IEnumerator Tasks(Action<int, AcquiredItemDto> cb)
        {
            bool isEffectFinished = true;
            bool protocolFinished = false;

            AcquiredItemDto acquiredItemDto = null;
            popup.RequestReward(this.itemIndex, this, responseDto =>
            {
                protocolFinished = true;
                acquiredItemDto = responseDto.data;
                this.UpdateRewardedIconWithAcquiredItems(responseDto.data);
            });
            
            this.openEffect.SetActive(true);
            
            mainImage.sprite = Sprite.Create(
                this.frontCardImageTexture, 
                new Rect(0.0f, 0.0f, this.frontCardImageTexture.width, this.frontCardImageTexture.height), 
                new Vector2(0.5f, 0.5f), 100f
            );
            
            yield return new WaitForSeconds(1.5f);
            yield return new WaitUntil(() => protocolFinished && isEffectFinished);
            
            popup.UnBlockCloseButton();
            cb?.Invoke(this.itemIndex, acquiredItemDto);
        }
    }
}
