![image](https://github.com/user-attachments/assets/adae5ed1-726d-42c9-badd-ba9d8ad4aee9)

튜토리얼은 신규 아티스트/시즌이 추가될 때마다 스텝이 늘어나고, 밸런스 조정 때문에 순서가 자주 바뀐다.
매번 코드를 고쳐야 한다면 기획자가 직접 튜토리얼 흐름을 조정할 수 없기 때문에, 튜토리얼 한 스텝을
데이터 시트의 한 행(`TutorialCommon`)으로 정의하고, 실행 엔진(`TutorialManager`)은 `UniTask` +
LINQ로 그 행들을 순서대로 소비하기만 하는 구조로 분리했다. 스텝을 추가/재배치하는 작업은 코드 수정이
아니라 시트 데이터 편집으로 끝난다.

*튜토리얼 테이블에 대한 Class*
> 한 행이 튜토리얼 한 스텝에 대응한다. `TargetObject`(하이라이트할 UI), `TutorialText`(대사),
> `NextTutorial`(다음 스텝), `TutorialSkip`(스킵 가능 여부), `LimitTime`/`RhythmIngameTime`(대기 시간)
> 처럼 연출에 필요한 값이 모두 컬럼으로 노출되어 있다. 각 `GetXXXBy...` 메서드는 이 행이 들고 있는
> 코드값을 다른 데이터 시트(`TutorialType`, `TutorialLocale`, `ArtistVoice`, `ItemBox`, `Target` 등)의
> 실제 레코드로 해석해주는 관계형 조회 역할을 한다 — 즉 튜토리얼 한 스텝의 정의가 여러 시트에 걸친
> 외래키 참조로 구성되어 있다.

```csharp
using System;
using System.Collections.Generic;
using Snowballs.Sheets;
using Snowballs.Util;

namespace Snowballs.Sheets.Data
{
	[Serializable]
	public class TutorialCommon
	{
		public Int32 Code;
		public Int32 TutorialType;
		public Int32 TutorialText;
		public Boolean TutoVoiceActive;
		public Int32 TutoVoiceResource;
		public Boolean TutorialSkip;
		public Int32 NextTutorial;
		public Boolean TutorialReward;
		public Int32 TutorialRewardBundleCode;
		public Int32 TutorialPopupResource;
		public Int32 TextYPos;
		public Int32 TargetObject;
		public Int32 PortraitXPos;
		public Int32 PortraitYPos;
		public Int32 TUTOPopup;
		public Boolean TUTOType;
		public Int32 LimitTime;
		public Int32 LocaleTime;
		public Int32 RhythmIngameTime;

		public TutorialType GetTutorialTypeByTutorialType()
		{
			if (TutorialType == default) return null;
			return SBDataSheet.Instance.TutorialType[TutorialType];
		}
		public TutorialLocale GetTutorialLocaleByTutorialText()
		{
			if (TutorialText == default) return null;
			return SBDataSheet.Instance.TutorialLocale[TutorialText];
		}
		public List<ArtistVoice> GetTutoVoiceResource()
		{
			if (TutoVoiceResource == default) return null;
			return SBDataSheet.Instance.GetArtistVoiceListByBundle(TutoVoiceResource);
		}
		public TutorialCommon GetTutorialCommonByNextTutorial()
		{
			if (NextTutorial == default) return null;
			return SBDataSheet.Instance.TutorialCommon[NextTutorial];
		}
		public List<ItemBox> GetTutorialRewardBundleCode()
		{
			if (TutorialRewardBundleCode == default) return null;
			return SBDataSheet.Instance.GetItemBoxListByBundle(TutorialRewardBundleCode);
		}
		public TutorialResource GetTutorialResourceByTutorialPopupResource()
		{
			if (TutorialPopupResource == default) return null;
			return SBDataSheet.Instance.TutorialResource[TutorialPopupResource];
		}
		public Target GetTargetByTargetObject()
		{
			if (TargetObject == default) return null;
			return SBDataSheet.Instance.Target[TargetObject];
		}
	}
}
```

*튜토리얼이 실제로 구동되는 과정*

*해당하는 튜토리얼 정보를 Linq를 통해 찾는다.*
> `TutorialType` 컬럼 값으로 시트 전체를 필터링하고 `Code` 순으로 정렬해, 하나의 튜토리얼 시퀀스를
> 구성하는 스텝 목록을 뽑아낸다. 시트에 새 행을 추가하고 `TutorialType`/`Code`만 맞춰 넣으면 이 질의가
> 자동으로 그 행을 시퀀스에 포함시킨다 — 시퀀스 자체가 코드에 나열되어 있지 않다.

```csharp
    private List<TutorialCommon> GetTutorialCommonDict(int tutorialType)
    {
        var targetTutorialGroup = SBDataSheet.Instance.TutorialCommon.Values
            .Where(x => x.TutorialType == tutorialType)
            .OrderBy(x => x.Code)
            .ToList();
        return targetTutorialGroup;
    }
```

*최초 1회만 재생하는 일반 튜토리얼 진입점*
> `PlayTutorial`은 `IsAlreadyWatchedNewTutorial`로 이미 시청한 튜토리얼인지부터 확인한다. 시청
> 여부는 `tutorialType`을 키로 하는 딕셔너리에 로컬 JSON 파일로 저장되어, 앱을 재설치하지 않는 한
> 같은 튜토리얼이 두 번 나오지 않는다.

```csharp
    public void PlayTutorial(int tutorialType, Action onFinished = null, Action onStarted = null)
    {
#if DISABLE_TUTORIAL
        return;
#endif
        if(this.isPlayingTutorial) return;
        
        if(IsAlreadyWatchedNewTutorial(tutorialType)) return;
        SBDebug.Log(tutorialType + "번 튜토리얼 재생 요청");
        
        this.newTutorialSkipButtonText.text = LocaleController.GetSystemLocale(40077);
        this.toNextText.text = LocaleController.GetSystemLocale(40112);
        
        onStarted?.Invoke();
        
        _tutorialCancellationToken = new CancellationTokenSource();
        
        this.skipButtonObj.SetActive(false);
        var tutorialSet = this.GetTutorialCommonDict(tutorialType);
        this.Run(tutorialType, tutorialSet, false, onFinished).Forget();
        
        if (_newTutorialRecord.recordDict.ContainsKey(tutorialType))
        {
            _newTutorialRecord.recordDict[tutorialType] = true;
        }
        else
        {
            _newTutorialRecord.recordDict.Add(tutorialType, true);
        }
        this.WriteNewTutorialRecord();
    }

    public bool IsAlreadyWatchedNewTutorial(int code)
    {
        if (!this.isOpened) return true;
        
        try
        {
            if (this._newTutorialRecord == null) this._newTutorialRecord = new NewTutorialRecord();

            var recordDict = this._newTutorialRecord.recordDict;
            if (recordDict.ContainsKey(code))
            {
                return recordDict[code];
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Tutorial] IsAlreadyWatchedNewTutorial error!!!");
            Debug.LogWarning(ex.Message);

            return true;
        }
    }
```

> `IsAlreadyWatchedNewTutorial`의 예외 처리 방향이 흥미롭다 — 기록 파일을 읽다가 예외가 나면
> `false`(안 봤음)가 아니라 `true`(이미 봤음)를 반환한다. 저장 파일이 손상되거나 파싱에 실패하는
> 최악의 경우, 유저에게 튜토리얼을 다시 강제로 띄우기보다는 "이미 본 것으로 치고 넘어가는" 쪽을
> 선택한 방어적 기본값이다.

*리듬 게임 인게임용 튜토리얼 진입점*
> 스코어 모드/리듬 게임처럼 실제 플레이 도중에 끼어드는 튜토리얼은 시청 기록 체크 없이 매번 별도
> 진입점(`PlayRhythmIngameTutorial`)으로 들어간다. 두 진입점 모두 최종적으로는 같은 `Run()`으로
> 합류하고, `isRhythmIngame` 플래그 하나로 이후 분기를 결정한다.

```csharp
     public void PlayRhythmIngameTutorial(
        int tutorialType, 
        TestUIController controller, 
        Artistar.Rhythm.Controller.StageController stageController)
    {
#if DISABLE_TUTORIAL
        return;
#endif
        this._testUIController = controller;
        this._stageController = stageController;
        
        if(this.isPlayingTutorial) return;
        
        _tutorialCancellationToken = new CancellationTokenSource();
        
        this.skipButtonObj.SetActive(false);
        var tutorialSet = this.GetTutorialCommonDict(tutorialType);

        this.Run(tutorialType, tutorialSet, true).Forget();
    }
```

*스텝을 순서대로 소비하는 메인 루프*
> `Run`은 `TutorialCommon` 리스트를 `foreach`로 순회하며 한 스텝씩 처리하는, 사실상 큐를 순차
> 소비하는 async 루프다. 팝업이 떠 있으면 튜토리얼을 미루고(`UniTask.WaitUntil`), 일반 UI 튜토리얼은
> `WaitClick`(화면 터치 대기)으로, 리듬 인게임 튜토리얼은 `WaitSeconds`(정해진 시간만큼 대기)로 각
> 스텝의 "다음으로 넘어가는 조건"을 분기한다. `_tutorialCancellationToken`을 통한 취소는 코루틴의
> `yield break` 대신 `OperationCanceledException`을 던지고 받는 `try/catch`로 처리되어 있다 —
> UniTask 기반 코드에서 자연스러운 취소 관용구다.

```csharp
    private async UniTask Run(int tutorialType, List<TutorialCommon> set, bool isRhythmIngame, Action onFinished = null)
    {
        this._isRhythmIngame = isRhythmIngame;
        this.isPlayingTutorial = true;

        int[] popupWaitExceptTutorialTypes = new int[] { 13, 14 };
        
        if (!popupWaitExceptTutorialTypes.Contains(tutorialType) && PopupRoot.Instance.IsPopupExist())
        {
            SBDebug.Log("팝업이 존재하여 튜터리얼 대기");
            if (!isRhythmIngame)
            {
                await UniTask.WaitUntil(() => PopupRoot.Instance.IsPopupExist() == false);
            }
        }

        if (this._isRhythmIngame)
        {
            this.prevGameSceneRootCanvasScaler = this.gameSceneRootCanvasScaler.matchWidthOrHeight;
            this.gameSceneRootCanvasScaler.matchWidthOrHeight = 1.0f;
        }
     
        GameScene.Instance.LockBackButton();
        
        foreach (TutorialCommon row in set)
        {
            try
            {
                this.toNextTextObj.SetActive(false);
                
                if (!isRhythmIngame)
                {
                    this.portraitImage.SetActive(true);
                    this.ingameDimmedObj.SetActive(false);
                    this.softMask.SetActive(true);
                    
                    SBDebug.Log("Tutorial TargetObject : " + row.TargetObject);
                    
                    await WaitClick(row);
                }
                //리듬 인게임에 대한 처리
                else
                {
                    this.portraitImage.SetActive(false);
                    if (row.TargetObject != -1)
                    {
                        //튜토리얼 등장까지 대기
                        this.gameSceneCam.enabled = false;
                        await UniTask.WaitUntil(() => this._stageController.newTick >= row.TargetObject);
                        //CoroutineTaskManager.AddTask(this._stageController.MusicFadeOut());
                        this.gameSceneCam.enabled = true;
                    }
                    
                    this.ingameDimmedObj.SetActive(true);
                    this.softMask.SetActive(false);
                    this.skipButtonObj.SetActive(false);
                    
                    //텍스트 유지 시간동안 대기
                    await WaitSeconds(row);
                    //CoroutineTaskManager.AddTask(this._stageController.MusicFadeIn());
                }
            }
            catch (OperationCanceledException e)
            {
                SBDebug.Log("튜토리얼 취소됨");
                this.CloseTutorial();
                
                //if(_isRhythmIngame) CoroutineTaskManager.AddTask(this._stageController.MusicFadeIn());
                onFinished?.Invoke();
                return;
            }
        }

        if (isRhythmIngame)
        {
            this.gameSceneCam.enabled = false;
        }
        
        onFinished?.Invoke();
        GameScene.Instance.UnLockBackButton();
        this.CloseTutorial();
    }
```

설계 포인트
------------
> 튜토리얼 스텝의 "정의"(대사, 대상 오브젝트, 대기 시간, 다음 스텝)는 전부 `TutorialCommon` 시트에,
> "진행 방식"(터치를 기다릴지 시간을 기다릴지, 팝업이 떠 있으면 미룰지)은 `TutorialManager`의 `Run`
> 하나에 모여 있다. 이 분리 덕분에 기획자는 시트에 행을 추가/재배열하는 것만으로 새 튜토리얼 시퀀스를
> 만들 수 있고, 엔지니어는 `Run`의 분기(`isRhythmIngame`)만 관리하면 된다 — 실행 순서를 배열이나
> switch-case로 하드코딩하지 않고 `Where + OrderBy` LINQ 질의 결과에 그대로 위임한 것이 핵심이다.   
> 진입점이 `PlayTutorial`(시청 기록 체크 있음) / `PlayRhythmIngameTutorial`(체크 없음, 인게임 전용)로
> 나뉘어 있지만, 두 경로 모두 결국 같은 `Run()` 코루틴/async 루프로 합류하는 것도 같은 원칙의 연장이다
> — 진입 조건이 다르더라도 "스텝을 순서대로 소비한다"는 실행 로직 자체는 하나로 유지된다.

관련 코드: [99.Pattern](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/99.Pattern) · [09.Tutorial](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/09.Tutorial) · [PopupUIPattern.md](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/PopupUIPattern.md)
