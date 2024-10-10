Unity 기반 전략 카드 게임
==========================
> 달콤소프트 신규 플랫폼 게임 클라이언트 개발   
> 개발 기간 : 2021.03 ~ 2022.05 [약 1년]   
> 출시 여부 : 프로토타입 제작   

개발 환경
==========================
엔진 : Unity 2020.3.25f1      
플랫폼 : Nintendo Switch  
버전 관리 : Git, Github      
이슈 관리 : Github   


프로젝트 소개
==========================
*슈퍼스타 핑크퐁 특징*   
달콤소프트의 주력인 슈퍼스타 리듬게임을 닌텐도 스위치에서 즐길 수 있게 개발하는 것이 목표.   
핑크퐁 IP를 활용하여 제작      
기존 슈퍼스타 시리즈에는 없었던 협동 모드를 추가 구현   
> 두 플레이어가 각각 Nintendo Switch JoyCon을 이용하여 한 화면을 공유하여 플레이하는 방식

프로젝트 관리
===========================
Github으로 관리하였으며, develop/feature/release 브랜치로 나누어 관리.    
이슈는 Github Issue Tracker를 활용하여 관리.   

***
User의 현재 JoyCon 결합상태를 확인하여 분리가 필요한 경우 분리를 유도하는 코드   
![NativePopup](https://github.com/user-attachments/assets/ed81169a-ee7e-4d9b-bfb3-02083af0a76d)   

<pre>
  <code>
    private void OnSelect()
    {
        //인풋 모드 선택완료
        if(MenuIndex == 0)
        {
#if UNITY_EDITOR
            SoundFx.Play(AssetControl.SOUND_DECIDE);
            RemoveControllerListeners();
            SwitchConfig.KeyInput = SelectIndex;

            Menu1.SetActive(false);
            Menu2.SetActive(true);

            SelectIndex = 0;
            MenuIndex++;
            StartCoroutine(WaitEvent());

#elif !UNITY_EDITOR && UNITY_SWITCH
            //Motion을 선택했는데 NPadStyle이 Handheld인 경우
            //Joycon 분리 유도
            var nPadStyle = NintendoPlayModeControl.Instance.CheckCurrentNPadStyle();
            if((nPadStyle == nn.hid.NpadStyle.Handheld || nPadStyle == nn.hid.NpadStyle.None) && SelectIndex == 1)
            {
                AlertPopup.Params p = new AlertPopup.Params();

                p.activeTime = 2.0f;
                p.rewardType = AlertPopup.RewardType.RhythmPoint;
                p.context = "Joycon을 분리해 주세요!";

                ViewControl.OpenPopup("System/AlertPopup", p, (pop, btn) => { });
            }
            else
            {
                SoundFx.Play(AssetControl.SOUND_DECIDE);
                RemoveControllerListeners();
                SwitchConfig.KeyInput = SelectIndex;

                Menu1.SetActive(false);
                Menu2.SetActive(true);

                SelectIndex = 0;
                MenuIndex++;
                StartCoroutine(WaitEvent());
            }
#endif
        }

        //싱글 멀티 모드 선택완료
        else if(MenuIndex == 1)
        {
            SoundFx.Play(AssetControl.SOUND_DECIDE);

#if UNITY_EDITOR
            SwitchConfig.MULTI_MODE = SelectIndex == 0 ? false : true;
            TriggerOnOk();
#elif !UNITY_EDITOR && UNITY_SWITCH
            var nPadStyle = NintendoPlayModeControl.Instance.CheckCurrentNPadStyle();
            if((nPadStyle == nn.hid.NpadStyle.Handheld || nPadStyle == nn.hid.NpadStyle.None) && SelectIndex == 1)
            {
                AlertPopup.Params p = new AlertPopup.Params();

                p.activeTime = 2.0f;
                p.rewardType = AlertPopup.RewardType.RhythmPoint;
                p.context = "Joycon을 분리해 주세요!";

                ViewControl.OpenPopup("System/AlertPopup", p, (pop, btn) => { });
            }
            else{
                SwitchConfig.MULTI_MODE = SelectIndex == 0 ? false : true;
                TriggerOnOk();
            }
#endif
        }
    }
  </code>
</pre>

<pre>
  <code>
    public NpadStyle CheckCurrentNPadStyle()
    {
        NpadId npadId = NpadId.Handheld;
        NpadStyle npadStyle = NpadStyle.None;

        npadStyle = Npad.GetStyleSet(npadId);
        if (npadStyle != NpadStyle.Handheld)
        {
            npadId = NpadId.No1;
            npadStyle = Npad.GetStyleSet(npadId);
        }

        if(prevPadStyle != npadStyle)
        {
            this.padStyleChangedCallback?.Invoke(npadStyle);
        }

        this.prevPadStyle = npadStyle;
        //Debug.Log(string.Format("CheckCurrentNPadStyle : {0}", npadStyle));
        return npadStyle;
    }
  </code>
</pre>

***
JoyCon을 흔들어[자이로 센서 이용] 스티커를 긁어 서서히 나타나는 효과를 구현한 코드   

![Shake](https://github.com/user-attachments/assets/1c686145-b60f-40b2-ac66-c0cd2a151eb8)

<pre>
  <code>
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
  </code>
</pre>
