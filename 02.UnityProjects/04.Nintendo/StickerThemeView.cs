using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using com.dalcomsoft.project.app.control.contents;
using com.dalcomsoft.project.app.control;
using Coffee.UIExtensions;
using com.dalcomsoft.project.app.view.scene;
using com.dalcomsoft.project.client.model.asset;

public class StickerThemeView : MonoBehaviour
{
    private StickerBookView mainView;

    //public Text titleText;
    public GameObject HiddenObj;
    public bool canEquip = false;
    public bool isAllSticker = false;
    public int equipCount = 0;
    public short characterCode;
    public GameObject ShakeUI;

    private Material stickerM;
    private StickerThemeControl.Data data;

    public void Init(StickerBookView mainView, StickerThemeControl.Data data)
    {
        this.mainView = mainView;
        this.data = data;

        foreach(var stickerCode in data.StickersList)
        {
            var targetSticker = InventoryControl.GetSticker(stickerCode);
            foreach(Transform hiddenObj in HiddenObj.transform)
            {
                if(hiddenObj.name == stickerCode.ToString())
                {
                    //보유하고 있고, 이미 장착한 경우
                    if(targetSticker != null && targetSticker.isEquip)
                    {
                        hiddenObj.gameObject.SetActive(false);
                    }
                    //보유하고 있고, 아직 장착하지 않은 경우
                    else if(targetSticker != null && !targetSticker.isEquip)
                    {
                        hiddenObj.gameObject.SetActive(true);
                    }
                }
            }
        }
    }

    public List<short> GetStickerCodes()
    {
        if(data != null) return data.StickersList;
        return null;
    }

    public int GetThemeCode()
    {
        return data.Code;
    }

    public short GetCharCode()
    {
        return characterCode;
    }

    private void OnEnable()
    {
        if(LocaleControl.IsOpened)
            mainView.title.text = LocaleControl.GetString(StickerThemeControl.Map[Int16.Parse(transform.name)].LocaleName);
    }

    public void InitTitle(int _themeCode, Material m)
    {

        for(int i = 0; i <HiddenObj.transform.childCount; i++)
        {
            HiddenObj.transform.GetChild(i).Find("outline").GetComponent<Image>().material = m;
        }
    }

    public void WaitShake(short code, OnCompleteShaking callback)
    {
        Debug.LogFormat("code : {0}", code);

        StartCoroutine(
            ShakeToFinalEquip(GetHiddenObj(code), callback)
        );
    }

    public GameObject GetHiddenObj(short code)
    {
        return HiddenObj.transform.Find(code.ToString()).gameObject;
    }

    public delegate void OnCompleteShaking();
    IEnumerator ShakeToFinalEquip(GameObject obj, OnCompleteShaking callback)
    {
        obj.transform.Find("SpeechBubble").gameObject.SetActive(true);

        UIDissolve dissolve = obj.GetComponent<UIDissolve>();
        while(dissolve.location <1)
        {
#if UNITY_EDITOR
            if(Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.KeypadEnter))
            {
                // SoundFx.StopAll();
                Debug.LogFormat("dissolve.location : {0}", dissolve.location);
                SoundFx.Play(AssetControl.SOUND_SLIDEGROUPLIST);
                dissolve.location += Time.deltaTime;
                yield return new WaitForSeconds(0.1f);
            }
#else
            if(HidSixAxisSensor.inst.ControllerA_OneFrame_Angle >10)
            {   
               // SoundFx.StopAll();
                SoundFx.Play(AssetControl.SOUND_SLIDEGROUPLIST);
                dissolve.location += Time.deltaTime*3;
                             yield return new WaitForSeconds(0.1f);
                  
            }
            else if(HidSixAxisSensor.inst.ControllerB_OneFrame_Angle > 10)
            { 
               // SoundFx.StopAll();
                SoundFx.Play(AssetControl.SOUND_SLIDEGROUPLIST);
                dissolve.location += Time.deltaTime*3;
                             yield return new WaitForSeconds(0.1f);
                
            }
#endif
            yield return null;
        }
        SoundFx.Play(AssetControl.SOUND_BOUNS);
        yield return new WaitForSeconds(0.2f);
        obj.transform.Find("SpeechBubble").gameObject.SetActive(false);
        canEquip = false;

        mainView.stickerBookCursor.CursorImage.SetActive(true);
        mainView.isSelected = false;

        callback();
    }
}
