using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PassDescriptionModal : MonoBehaviour
{
    [SerializeField] private GameObject rawPrefab;
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private Transform itemParent;
    [SerializeField] private RectTransform innerBgRect;
    
    private Action onClosed;
    public class Params
    {
        public List<Item> rewardItems;
        public Action onClosed;
    }

    public class Item
    {
        public Texture2D iconTexture;
        public int amount;

        public Item(Texture2D iconTexture, int amount)
        {
            this.iconTexture = iconTexture;
            this.amount = amount;
        }
    }

    public void OnOpen(Params p)
    {
        this.onClosed = p.onClosed;

        int slotNumPerRaw = 4;
        int totalRawNum = (int)Math.Ceiling((float)p.rewardItems.Count / 4);

        this.innerBgRect.gameObject.SetActive(true);
        this.innerBgRect.sizeDelta = new Vector2(1000, 200 * totalRawNum + 140f);
        
        int itemIndex = 0;
        for (int i = 1; i <= totalRawNum; i++)
        {
            GameObject rawObj = Instantiate(this.rawPrefab, this.itemParent);

            for (int j = 0; j < slotNumPerRaw; j++)
            {
                Transform slotTf = rawObj.transform.GetChild(j);
                if (itemIndex < p.rewardItems.Count)
                {
                    TextMeshProUGUI numberText = slotTf.GetChild(0).GetComponent<TextMeshProUGUI>();
                    numberText.text = "x" + p.rewardItems[itemIndex].amount.ToString();
                
                    var image = slotTf.GetComponent<Image>();
                    image.sprite = Sprite.Create(
                        p.rewardItems[itemIndex].iconTexture, 
                        new Rect(0.0f, 0.0f, p.rewardItems[itemIndex].iconTexture.width, p.rewardItems[itemIndex].iconTexture.height), 
                        new Vector2(0.5f, 0.5f), 
                        100f
                    );
                }
                //빈 슬롯
                else
                {
                    slotTf.gameObject.SetActive(false);
                }
                itemIndex++;
            }
        }
        gameObject.SetActive(true);
    }

    public void OnClose()
    {
        foreach (Transform child in this.itemParent)
        {
            Destroy(child.gameObject);
        }
        this.onClosed?.Invoke();
        this.gameObject.SetActive(false);
    }
}
