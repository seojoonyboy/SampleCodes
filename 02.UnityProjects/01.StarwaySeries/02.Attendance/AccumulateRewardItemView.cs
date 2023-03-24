using System;
using System.Collections;
using System.Collections.Generic;
using com.snowballs.SWHJ.client.model;
using Snowballs.Sheets;
using Snowballs.Sheets.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AccumulateRewardItemView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI numberText;
    
    [SerializeField] private Image openImage;
    [SerializeField] private Image defaultImage;

    [SerializeField] private GameObject openObj;
    [SerializeField] private GameObject defaultObj;

    private DailyCumReward dailyCumReward;
    private AttendanceRewardDescriptionModal descriptionModal;
    
    private bool isReceived = false;

    int count = 0;

    public class Params
    {
        public int number;
        public bool isReceived;

        public string playerLocale;
        
        public Texture2D openImageTexture;
        public Texture2D defaultImageTexture;

        public AttendanceRewardDescriptionModal descriptionModal;
        public DailyCumReward dailyCumReward;
    }

    public void Refresh(Params p)
    {
        Texture2D openImageTexture = p.openImageTexture;
        Texture2D defaultImageTexture = p.defaultImageTexture;
        
        this.openImage.sprite = Sprite.Create(
            openImageTexture, 
            new Rect(0.0f, 0.0f, openImageTexture.width, openImageTexture.height),
            new Vector2(0.5f, 0.5f), 100.0f
        );
        this.defaultImage.sprite = Sprite.Create(
            defaultImageTexture, 
            new Rect(0.0f, 0.0f, defaultImageTexture.width, defaultImageTexture.height),
            new Vector2(0.5f, 0.5f), 100.0f
        );
        
        if (p.isReceived)
        {
            this.SetStateOpen();
        }
        else
        {
            this.SetStateClose();
            
            if(string.IsNullOrEmpty(p.playerLocale))
                this.numberText.text = p.number.ToString();
            else
            {
                Int32 eventLocaleCode = 9999;
                if (p.playerLocale == "ko")
                {
                    this.numberText.text = p.number + SBDataSheet.Instance.EventLocale[eventLocaleCode].KoKR;
                }
                else if (p.playerLocale == "en")
                {
                    this.numberText.text = p.number + SBDataSheet.Instance.EventLocale[eventLocaleCode].EnUS;
                }
                else if (p.playerLocale == "jp")
                {
                    this.numberText.text = p.number + SBDataSheet.Instance.EventLocale[eventLocaleCode].JaJP;
                }
                else
                {
                    this.numberText.text = p.number + SBDataSheet.Instance.EventLocale[eventLocaleCode].ThTH;
                }
            }
        }

        this.dailyCumReward = p.dailyCumReward;
        this.descriptionModal = p.descriptionModal;
    }

    public void SetStateOpen()
    {
        this.isReceived = true;
        
        this.defaultObj.SetActive(false);
        this.openObj.SetActive(true);
    }

    public void SetStateClose()
    {
        this.isReceived = false;
        
        this.defaultObj.SetActive(true);
        this.openObj.SetActive(false);
    }

    private Coroutine downloadCoroutine;
    public void OnClick()
    {
        if(Input.touchCount > 1) return;

        if(Input.GetMouseButtonUp(0))
        {
            count++;
            if(count > 1)
            {
                count = 0;
                return;
            }
        }
        
        if(this.isReceived) return;
        if(this.descriptionModal.gameObject.activeSelf) return;
        
        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
        
        AttendanceRewardDescriptionModal.Params modalParam = new AttendanceRewardDescriptionModal.Params();
        modalParam.rewardItems = new List<AttendanceRewardDescriptionModal.Item>();

        this.downloadCoroutine = CoroutineTaskManager.AddTask(_DownloadTextures(() =>
        {
            this.descriptionModal.OnOpen(modalParam);
        }));

        IEnumerator _DownloadTextures(Action cb)
        {
            bool isBoxItem = SBDataSheet.Instance.ItemProduction[this.dailyCumReward.Item].ItemType == 3;

            if (!isBoxItem)
            {
                bool isFinished = false;
                
                int itemResourceCode = SBDataSheet.Instance.ItemProduction[this.dailyCumReward.Item].IconImage;
                
                string iconImageResAddress = String.Empty;
                if (playerStorage.Locale == "ko")
                {
                    iconImageResAddress = SBDataSheet.Instance.ItemResource[itemResourceCode].KoKRAddress;
                }
                else
                {
                    iconImageResAddress = SBDataSheet.Instance.ItemResource[itemResourceCode].EnUSAddress;
                }

                string iconFilePath = AssetPathController.PATH_FOLDER_ASSETS + iconImageResAddress;
                WWWFile.DownloadPath downloadPath = new WWWFile.DownloadPath(
                    WWWFile.TYPE.Bytes, 
                    String.Empty, 
                    iconFilePath
                );
        
                TextureController.GetTexture(downloadPath, false, (isSuccess, texture) =>
                {
                    modalParam.rewardItems.Add(
                        new AttendanceRewardDescriptionModal.Item(
                            texture, 
                            this.dailyCumReward.Value)
                    );

                    isFinished = true;
                });
                
                yield return new WaitUntil(() => isFinished);
            }
            else
            {
                var boxItems = SBDataSheet.Instance.ItemProduction[this.dailyCumReward.Item].GetBoxBundle();
                
                for (int i = 0; i < boxItems.Count; i++)
                {
                    bool isFinished = false;
                    
                    int itemProductionCode = boxItems[i].ItemProduction;

                    string iconImageResAddress = String.Empty;
                    iconImageResAddress =LocaleController.GetItemAddress(itemProductionCode);


                    string iconFilePath = AssetPathController.PATH_FOLDER_ASSETS + iconImageResAddress;
                    WWWFile.DownloadPath downloadPath = new WWWFile.DownloadPath(
                        WWWFile.TYPE.Bytes, 
                        String.Empty, 
                        iconFilePath
                    );

                    TextureController.GetTexture(downloadPath, false, (isSuccess, texture) =>
                    {
                        modalParam.rewardItems.Add(
                            new AttendanceRewardDescriptionModal.Item(
                                texture,
                                boxItems[i].ItemQuantity)
                        );

                        isFinished = true;
                    });
                    
                    yield return new WaitUntil(() => isFinished);
                }
            }
            
            cb?.Invoke();
        }
    }

    private void OnDestroy()
    {
        if(this.downloadCoroutine != null) 
            CoroutineTaskManager.RemoveTask(downloadCoroutine);
    }
}
