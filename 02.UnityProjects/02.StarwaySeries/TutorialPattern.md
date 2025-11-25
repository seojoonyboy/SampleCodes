![image](https://github.com/user-attachments/assets/adae5ed1-726d-42c9-badd-ba9d8ad4aee9)

*튜토리얼 테이블에 대한 Class*
<pre>
  <code>
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
  </code>
</pre>

*튜토리얼이 실제로 구동되는 과정*

*해당하는 튜토리얼 정보를 Linq를 통해 찾는다.*
<pre>
  <code>
    private List<TutorialCommon> GetTutorialCommonDict(int tutorialType)
    {
        var targetTutorialGroup = SBDataSheet.Instance.TutorialCommon.Values
            .Where(x => x.TutorialType == tutorialType)
            .OrderBy(x => x.Code)
            .ToList();
        return targetTutorialGroup;
    }
  </code>
</pre>

<pre>
  <code>
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
  </code>
</pre>

<pre>
  <code>
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
  </code>
</pre>
