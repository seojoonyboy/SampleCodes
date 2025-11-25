using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Artistar.Rhythm.Controller;
using Snowballs.Client.Model;
using Snowballs.Client.View;
using Snowballs.Client.Scene;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Newtonsoft.Json;

using Snowballs.Client.Foundation.Tutorial;
using Snowballs.Client.Etc.Tutorial;
using Snowballs.Client.Ext.Event;
using Snowballs.Sheets;
using Snowballs.Sheets.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Snowballs.Client.Controller.Tutorial
{
    public class TutorialManager : MonoBehaviour
{
    [SerializeField] private VideoPlayer tutorialVideoPlayer;
    [SerializeField] private RawImage targetVideoTexture;
    [SerializeField] private GameObject fullSizeTutorial;

    [SerializeField] private GameObject skipButtonObj;
    [SerializeField] private TextMeshProUGUI skipButtonText;
    [SerializeField] private TextMeshProUGUI newTutorialSkipButtonText;
    [SerializeField] private GameObject toNextTextObj;
    [SerializeField] private TextMeshProUGUI toNextText;
    
    private bool isOpened = false;
    private Coroutine prepareVideoCoroutine = null;

    private TutorialRecord _tutorialRecord;
    private NewTutorialRecord _newTutorialRecord;
    
    [Header("Tutorial")]
    [SerializeField] private GameObject maskCanvas;                     //SWIK : 튜토리얼용 마스크 캔버스
    [SerializeField] private GameObject textUICanvas;                   //SWIK : 튜토리얼용 텍스트 캔버스
    [SerializeField] private TextMeshProUGUI textUICanvasText;          //SWIK : 튜토리얼용 텍스트 컴포넌트
    [SerializeField] private RectTransform textUIRect;                  //SWIK : 튜토리얼용 텍스트 박스 Rect
    [SerializeField] private GameObject softMask;                       //SWIK : 튜토리얼용 SoftMask [Punch용 이미지]
    [SerializeField] private GameObject dimmedObj;                      //SWIK : 튜토리얼용 Dimmed GameObject [화면 Dimmed용]
    [SerializeField] private GameObject ingameDimmedObj;                //SWIK : 튜토리얼 인게임 전용 Dimmed GameObject
    [SerializeField] private RectTransform ingameDimmedRect;            //SWIK : 튜토리얼 인게임 전용 Dimmed Recttransform
    [SerializeField] private GameObject portraitImage;                  //SWIK : 튜토리얼용 텍스트의 초상화 이미지
    [SerializeField] private RectTransform softMaskRect;                //SWIK : 튜토리얼용 SoftMask Rect [Punch용 이미지]
    [SerializeField] private Camera gameSceneCam;                       //SWIK : GameScene 카메라
    [SerializeField] private Sprite defaultMaskImage;                   //SWIK : 튜토리얼용 기본 SoftMask 이미지
    
    [SerializeField] private RectTransform gameSceneRootCanvasRect;
    [SerializeField] private CanvasScaler gameSceneRootCanvasScaler;
    [SerializeField] private GameSceneResolutionController gameSceneResolutionController;
    private float prevGameSceneRootCanvasScaler;
    
    private List<Dictionary<string, object>> tutorialCommonDict;        //SWIK
    private List<Dictionary<string, object>> tutorialTypeDict;          //SWIK
    private List<Dictionary<string, object>> tutorialLocaleDict;        //SWIK

    [SerializeField] private RawImage selectedPortrait;
    
    public void Open(Action cb)
    {
        this.isOpened = true;
        
        bool res = this.LoadTutorialRecordFile();
        if (!res)
        {
            cb?.Invoke();
            return;
        }

        res = this.LoadNewTutorialRecordFile();
        if (!res)
        {
            cb?.Invoke();
            return;
        }

        this.UpdatePortraitTexture();

        this.newTutorialSkipButtonText.text = LocaleController.GetSystemLocale(40077);
        this.toNextText.text = LocaleController.GetSystemLocale(40112);
        
        this.ingameDimmedRect.sizeDelta = new Vector2(
            this.gameSceneRootCanvasRect.rect.width + 1000, 
            this.gameSceneRootCanvasRect.rect.height + 1000
        );
        
        BroadcastTunnel<string, int>.Add("com.snowballs.SWAT.ProfileChanged", this.OnProfileChanged);
        
        cb?.Invoke();
    }

    private void OnDisable()
    {
        BroadcastTunnel<string, int>.Remove("com.snowballs.SWAT.ProfileChanged", this.OnProfileChanged);
    }

    public void UpdatePortraitTexture()
    {
        var selectedTutorialVoiceCode = GameStorage.SelectedTutorialVoiceResourceCode;
        
        if (GameStorage.SelectedTutorialVoiceResourceCode != -1)
        {
            if(!GameStorage.TutorialProfileStorage.IsTargetCodeExist(selectedTutorialVoiceCode))
            {
                selectedTutorialVoiceCode = GameStorage.TutorialProfileStorage.DisplayList.FirstOrDefault().Value.itemResourceCode;
                GameStorage.SelectedTutorialVoiceResourceCode = selectedTutorialVoiceCode;
            }

            var downloadPath = GameStorage.TutorialProfileStorage
                .GetTutorialDowloadPath(selectedTutorialVoiceCode);
            this.selectedPortrait.SetTexture(downloadPath);
        }
    }

    private CancellationTokenSource _tutorialCancellationToken;
    private CancellationTokenSource cancelTextAnimToken;

    public void OnClickSkipButton()
    {
        CommonProcessController.PlayEffectSound("Lobby", 1);
        
        isSkipButtonClicked = true;
        this.CloseTutorial();
    }

    //현재 튜토리얼 진행중인 상태인가?
    private bool isPlayingTutorial = false;

    private TestUIController _testUIController;
    private Artistar.Rhythm.Controller.StageController _stageController;
    
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

    private bool _isRhythmIngame = false;
    
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

    private async UniTask WaitSeconds(TutorialCommon commonData)
    {
        if(!this.maskCanvas.activeSelf) this.maskCanvas.SetActive(true);
        
        this.HighlightTarget(commonData);
        
        string tutorialText = LocaleController.GetTutorialTextLocale(commonData.TutorialText);
        
        this.SetTextBoxRect(commonData);
        
        int portraitXPos = commonData.PortraitXPos;
        int portraitYPos = commonData.PortraitYPos;
        this.SetPortraitPosition(portraitXPos, portraitYPos);

        int textBoxYPos = commonData.TextYPos;

        this.canTextTyping = true;
        
        this.cancelTextAnimToken = new CancellationTokenSource();
        
        this.TextTyping(tutorialText, commonData.LocaleTime, cancelTextAnimToken).Forget();
        
        this.cancelTextAnimToken.Token.ThrowIfCancellationRequested();
        this._tutorialCancellationToken.Token.ThrowIfCancellationRequested();

        await UniTask.Delay(TimeSpan.FromSeconds(commonData.RhythmIngameTime));
        
        this.cancelTextAnimToken.Cancel();
        this.textUICanvasText.text = String.Empty;
    }

    public void OnClickCanvas()
    {
        isScreenClicked = true;
    }

    private bool isScreenClicked = false;
    private bool isSkipButtonClicked = false;
    
    private async UniTask WaitClick(TutorialCommon commonData)
    {
        this.isScreenClicked = false;
        this.isSkipButtonClicked = false;
        
        if(!this.maskCanvas.activeSelf) this.maskCanvas.SetActive(true);
        
        SBDebug.Log("HighlightTarget Code : " + commonData.TargetObject);
        this.HighlightTarget(commonData);
        
        string tutorialText = LocaleController.GetTutorialTextLocale(commonData.TutorialText);

        this.SetTextBoxRect(commonData);
        
        int portraitXPos = commonData.PortraitXPos;
        int portraitYPos = commonData.PortraitYPos;
        this.SetPortraitPosition(portraitXPos, portraitYPos);

        int textBoxYPos = commonData.TextYPos;

        this.canTextTyping = true;
        
        this.cancelTextAnimToken = new CancellationTokenSource();
        this.TextTyping(tutorialText, commonData.LocaleTime, cancelTextAnimToken).Forget();

        if (commonData.TutoVoiceActive)
        {
            CommonProcessController.PlayTutorialVoiceSound(commonData.TutoVoiceResource);
        }
        
        this.skipButtonObj.SetActive(commonData.TutorialSkip);
        
        SBDebug.Log("SJW TutorialManager CommonData : " + commonData.Code);
        
        this.cancelTextAnimToken.Token.ThrowIfCancellationRequested();
        this._tutorialCancellationToken.Token.ThrowIfCancellationRequested();
        
        await UniTask.WaitUntil(() => isScreenClicked || isSkipButtonClicked);

        if (isSkipButtonClicked) { return; }

        //텍스트 타이핑이 끝나지 않은 상태인 경우 타이핑 효과는 강제 종료하고, 클릭을 대기한다.
        if (this.IsTypingText)
        {
            this.cancelTextAnimToken.Cancel();
            this.IsTypingText = false;
            this.textUICanvasText.text = String.Empty;

            await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
            this.SetContextImmediately(tutorialText);
            this.toNextTextObj.SetActive(true);
            
            SBDebug.Log("화면 터치 1차 발생 !");
            
            //한번 더 화면을 터치하면 다음 단계로 넘어간다
            
            this.isScreenClicked = false;
            await UniTask.WaitUntil(() => 
                isScreenClicked, 
                PlayerLoopTiming.Update,
                _tutorialCancellationToken.Token
            );
        }
        
        this.cancelTextAnimToken.Cancel();
        this.textUICanvas.gameObject.SetActive(false);
        this.textUICanvasText.text = String.Empty;
        SBDebug.Log("화면 터치 2차 발생 !");
    }

    private void HighlightTarget(TutorialCommon commonRowData)
    {
        var tutorialTypeRaw = SBDataSheet.Instance.TutorialType[commonRowData.TutorialType];
        
        if(tutorialTypeRaw == null) return;

        string targetSceneName = tutorialTypeRaw.ContentLink;
        if (IsGameSceneTarget(commonRowData.TargetObject)) { targetSceneName = "Game"; }

        int tutorialTargetGameObjectCode = commonRowData.TargetObject;
        
        TutorialContentLink tutorialContentLink = this.GetContentLinkComponent(targetSceneName);
        if(tutorialContentLink == null) return;
        
        GameObject targetGameObject = tutorialContentLink.GetTargetGameObject(tutorialTargetGameObjectCode);
        
        if(targetGameObject != null) this.SetMaskTarget(targetGameObject, tutorialContentLink.mainCam);
    }

    private int[] ingameTargetList = new int[]
    {
        1003,   //로비(퍼즐,리듬) 이동 버튼
        1005,   //골드 재화
        1006,   //다이아 재화
        1007,   //매거진 버튼
        1008,   //내카드 버튼
        1009,   //카드뽑기 버튼
        1010,   //상점 버튼
        22002,  //하트 재화 영역
        23702,   //리듬 티켓 재화 영역
        23605    //시즌 정보 팝업 내 버프 카드 영역
    };
    
    private bool IsGameSceneTarget(int targetCode)
    {
        var targetInArr = ingameTargetList.Where(x => x == targetCode).ToArray();
        return targetInArr.Length > 0;
    }

    private void SetPortraitPosition(int xPos, int yPos)
    {
        RectTransform portraitRect = this.portraitImage.GetComponent<RectTransform>();
        //좌측
        if (xPos == 0)
        {
            portraitRect.pivot = new Vector2(0.0f, 0.5f);
            //좌측 상단
            if (yPos == 1)
            {
                portraitRect.anchorMin = new Vector2(0.0f, 1.0f);
                portraitRect.anchorMax = new Vector2(0.0f, 1.0f);
                
                portraitRect.anchoredPosition = new Vector3(-8.0f, -75.0f);
            }
            //좌측 하단
            else
            {
                portraitRect.anchorMin = new Vector2(0.0f, 0.0f);
                portraitRect.anchorMax = new Vector2(0.0f, 0.0f);

                portraitRect.anchoredPosition = new Vector3(-8.0f, 100.0f);
            }
        }
        //우측
        else
        {
            portraitRect.pivot = new Vector2(1.0f, 0.5f);
            //우측 상단
            if (yPos == 1)
            {
                portraitRect.anchorMin = new Vector2(1.0f, 1.0f);
                portraitRect.anchorMax = new Vector2(1.0f, 1.0f);
                
                portraitRect.anchoredPosition = new Vector3(-256.0f, 125.0f);
            }
            //우측 상단
            else
            {
                portraitRect.anchorMin = new Vector2(1.0f, 1.0f);
                portraitRect.anchorMax = new Vector2(1.0f, 1.0f);

                portraitRect.anchoredPosition = new Vector3(0.0f, 100.0f);
            }
        }
    }

    private void SetTextBoxRect(TutorialCommon tutorialCommon)
    {
        //yPos 0 : 하단, 1 : 중간, 2 : 상단
        bool isPortrait = Screen.width < Screen.height;

        float imageWidth = 1066f;
        float imageHeight = 633f;
        
        //세로 모드인 경우
        if (isPortrait)
        {
            this.textUIRect.sizeDelta = new Vector2(1066f, 633f);
            
            if (tutorialCommon.TextYPos == 0)
            {
                this.textUIRect.anchorMin = new Vector2(0.5f, 0.0f);
                this.textUIRect.anchorMax = new Vector2(0.5f, 0.0f);
                this.textUIRect.anchoredPosition = new Vector2(0, 540f);
            }
            else if (tutorialCommon.TextYPos == 1)
            {
                this.textUIRect.anchorMin = new Vector2(0.5f, 0.5f);
                this.textUIRect.anchorMax = new Vector2(0.5f, 0.5f);
                this.textUIRect.anchoredPosition = new Vector2(0, 0f);
            }
            else
            {
                this.textUIRect.anchorMin = new Vector2(0.5f, 1.0f);
                this.textUIRect.anchorMax = new Vector2(0.5f, 1.0f);
                this.textUIRect.anchoredPosition = new Vector2(0, -440f);
            }
            
            this.textUICanvasText.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -208f);
            this.textUICanvasText.fontSizeMax = 55f;
        }
        
        //가로 모드인 경우 
        else
        {
            //가로, 세로 해상도 영역 내에서 비율을 유지하면서 적당한 크기까지 늘려준다.
            this.textUIRect.sizeDelta = new Vector2(1920f, imageHeight * 1920f / imageWidth);
            
            if (tutorialCommon.TextYPos == 0)
            {
                this.textUIRect.anchorMin = new Vector2(0.5f, 0.0f);
                this.textUIRect.anchorMax = new Vector2(0.5f, 0.0f);
                this.textUIRect.anchoredPosition = new Vector2(0, 780f);
            }
            else if (tutorialCommon.TextYPos == 1)
            {
                this.textUIRect.anchorMin = new Vector2(0.5f, 0.5f);
                this.textUIRect.anchorMax = new Vector2(0.5f, 0.5f);
                this.textUIRect.anchoredPosition = new Vector2(0, 0f);
            }
            else
            {
                this.textUIRect.anchorMin = new Vector2(0.5f, 1.0f);
                this.textUIRect.anchorMax = new Vector2(0.5f, 1.0f);
                this.textUIRect.anchoredPosition = new Vector2(0, -780f);
            }
            
            this.textUICanvasText.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -400f);
            this.textUICanvasText.fontSizeMax = 85f;
        }
    }

    private bool isTypingText = false;
    public bool IsTypingText
    {
        get
        {
            return this.isTypingText;
        }
        set
        {
            this.isTypingText = value;
        }
    }

    //글자수가 일정 수 이하이면 LocaleTime에 구애받지 않게 하기 위한 기준 값
    private int textTypeSpeedLimitCount = 10;
    private bool canTextTyping = true;
    
    private async UniTask TextTyping(string context, float totalSec, CancellationTokenSource token)
    {
        if(string.IsNullOrEmpty(context)) return;
        
        CommonProcessController.PlayEffectSound("Common", 5);
        
        this.textUICanvas.gameObject.SetActive(true);
        this.IsTypingText = true;

        int textTotalCount = context.Trim().Length;
        float intervalSec = totalSec * 1000 / textTotalCount;
        if (textTotalCount <= textTypeSpeedLimitCount)
        {
            intervalSec = 1;
        }
        
        string subText = string.Empty;
        for (int i = 0; i <= textTotalCount; i++)
        {
            if(!canTextTyping) return;
            
            token.Token.ThrowIfCancellationRequested();
            
            await UniTask.Delay(TimeSpan.FromMilliseconds(intervalSec));
            
            if(!canTextTyping) return;
            
            subText += context.Substring(0, i);
            this.textUICanvasText.text = subText;

            subText = string.Empty;
        }
        this.IsTypingText = false;
        
        if(!_isRhythmIngame) this.toNextTextObj.SetActive(true);
    }

    private void CloseTutorial()
    {
        this.skipButtonObj.SetActive(false);
        this.maskCanvas.SetActive(false);
        this.textUICanvas.gameObject.SetActive(false);
        
        GameScene.Instance.UnLockBackButton();
        this.isPlayingTutorial = false;
        
        this.textUICanvasText.text = String.Empty;
        
        if(this.cancelTextAnimToken != null) this.cancelTextAnimToken.Cancel();
        if(this._tutorialCancellationToken != null) this._tutorialCancellationToken.Cancel();

        if(this._isRhythmIngame) this.gameSceneRootCanvasScaler.matchWidthOrHeight = this.prevGameSceneRootCanvasScaler;
    }

    private void SetContextImmediately(string context)
    {
        this.canTextTyping = false;
        this.textUICanvasText.text = context;
    }

    public TutorialContentLink GetContentLinkComponent(string sceneName)
    {
        switch (sceneName)
        {
            case "Lobby":
                return GameObject.Find(TutorialPath.LOBBY_VIEW_CANVAS).GetComponent<TutorialContentLink>();
            
            case "RhythmGamePlayPopup":
            case "Game":
                return GameObject.Find(TutorialPath.GAME_SCENE_CANVAS).GetComponent<TutorialContentLink>();
            
            case "PuzzleGameLobby":
            case "ScoreMode":   
                return GameObject.Find(TutorialPath.PUZZLE_LOBBY_VIEW_CANVAS).GetComponent<TutorialContentLink>();
            
            case "Magazine":
            case "MagazineDes":
                return GameObject.Find(TutorialPath.ARTBOOK_VIEW_CANVAS).GetComponent<TutorialContentLink>();
            
            case "MyCard":
            case "MyCardDes":
            case "CardSetting":
            case "CardDeco":
                return GameObject.Find(TutorialPath.MYCARD_VIEW_CANVAS).GetComponent<TutorialContentLink>();
            
            case "RhythmGame":
                return GameObject.Find(TutorialPath.RHYTHMLOBBY_VIEW_CANVAS).GetComponent<TutorialContentLink>();
        }

        return null;
    }
    
    private List<TutorialCommon> GetTutorialCommonDict(int tutorialType)
    {
        var targetTutorialGroup = SBDataSheet.Instance.TutorialCommon.Values
            .Where(x => x.TutorialType == tutorialType)
            .OrderBy(x => x.Code)
            .ToList();
        return targetTutorialGroup;
    }

    private Dictionary<string, object> GetTargetTutorialType(string code)
    {
        foreach (Dictionary<string, object> row in this.tutorialTypeDict)
        {
            if (row["Code"].ToString() == code)
            {
                return row;
            }
        }
        return null;
    }

    /// <summary>
    /// targetObj 크기에 맞추어 Rect Size를 재조정 해준다. [highlight 영역 지정]
    /// </summary>
    /// <param name="targetObj"></param>
    private void SetMaskTarget(GameObject targetObj, Camera targetCam)
    {
        RectTransform targetObjRect = targetObj.GetComponent<RectTransform>();
        this.softMaskRect.anchorMin = targetObjRect.anchorMin;
        this.softMaskRect.anchorMax = targetObjRect.anchorMax;
        this.softMaskRect.pivot = targetObjRect.pivot;
        this.softMaskRect.sizeDelta = targetObjRect.rect.size;

        if (targetObj.GetComponent<Image>() != null)
        {
            this.softMaskRect.GetComponent<Image>().sprite = targetObj.GetComponent<Image>().sprite;
        }
        else if (targetObj.GetComponent<RawImage>() != null)
        {
            var texture = targetObj.GetComponent<RawImage>().texture as Texture2D;
            if (texture != null)
                this.softMaskRect.GetComponent<Image>().sprite = Sprite.Create(texture,
                    new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100.0f);
        }
        else
        {
            this.softMaskRect.GetComponent<Image>().sprite = this.defaultMaskImage;
        }

        Vector3 imageWorldPosition = targetObj.transform.position;
        Vector3 screenPosition = RectTransformUtility.WorldToScreenPoint(targetCam, imageWorldPosition);
        
        Vector3 targetPos = Vector3.zero;
        RectTransformUtility.ScreenPointToWorldPointInRectangle(this.softMaskRect, screenPosition, gameSceneCam, out targetPos);
        this.softMaskRect.transform.position = targetPos;
        
        Debug.Log("Image World Position: " + imageWorldPosition);
        Debug.Log("Image Screen Position: " + screenPosition);
    }

    private void TestCode()
    {
        this._tutorialRecord.recordDict[50] = false;
    }

    private IEnumerator PrepareVideo(Action cb)
    {
        this.tutorialVideoPlayer.Prepare();
        
        while (!this.tutorialVideoPlayer.isPrepared)
        {
            yield return new WaitForSeconds(0.5f);
        }

        yield return null;
        cb?.Invoke();
    }

    //튜토리얼 유저 기록 파일 Load
    private bool LoadTutorialRecordFile()
    {
        //파일이 없는 상태인 경우 파일을 새로 만든다.
        try
        {
            string tutorialFilePath = AssetPathController.PATH_TUTORIAL_RECORD_FILE_PATH.ToString();

            string jsonStr = String.Empty;
            if (File.Exists(tutorialFilePath))
            {
                //파일은 있지만 읽지 못한 경우
                jsonStr = File.ReadAllText(tutorialFilePath);
                if (string.IsNullOrEmpty(jsonStr)) jsonStr = this.WriteTutorialRecordFile();
            }
            //파일이 존재하지 않는 경우
            else jsonStr = this.WriteTutorialRecordFile();
            
            this._tutorialRecord = JsonConvert.DeserializeObject<TutorialRecord>(jsonStr);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Tutorial] LoadTutorialRecordFile error!!!");
            Debug.LogWarning(ex.Message);
            return false;
        }
    }

    private bool LoadNewTutorialRecordFile()
    {
        //파일이 없는 상태인 경우 파일을 새로 만든다.
        try
        {
            string tutorialFilePath = AssetPathController.PATH_NEW_TUTORIAL_RECORD_FILE_PATH.ToString();

            string jsonStr = String.Empty;
            if (File.Exists(tutorialFilePath))
            {
                //파일은 있지만 읽지 못한 경우
                jsonStr = File.ReadAllText(tutorialFilePath);
                if (string.IsNullOrEmpty(jsonStr)) jsonStr = this.WriteNewTutorialRecord();
            }
            //파일이 존재하지 않는 경우
            else jsonStr = this.WriteNewTutorialRecord();
            
            this._newTutorialRecord = JsonConvert.DeserializeObject<NewTutorialRecord>(jsonStr);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Tutorial] LoadNewTutorialRecordFile error!!!");
            Debug.LogWarning(ex.Message);
            return false;
        }
    }
    
    public string WriteNewTutorialRecord()
    {
        try
        {
            if (this._newTutorialRecord == null) { this._newTutorialRecord = new NewTutorialRecord(); }
        
            string json = JsonConvert.SerializeObject(this._newTutorialRecord);
        
            string filePath = AssetPathController.PATH_NEW_TUTORIAL_RECORD_FILE_PATH.ToString();
            File.WriteAllText(filePath, json);
            
            return File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Tutorial] WriteNewTutorialRecordFile error!!!");
            Debug.LogWarning(ex.Message);
            return null;
        }
    }

    //튜토리얼 파일 Write
    private string WriteTutorialRecordFile()
    {
        try
        {
            if(!this.isOpened) return String.Empty;
            
            if (this._tutorialRecord == null) this._tutorialRecord = new TutorialRecord();

            string json = JsonConvert.SerializeObject(this._tutorialRecord);
            
            string filePath = AssetPathController.PATH_TUTORIAL_RECORD_FILE_PATH.ToString();
            Debug.Log("write tutorial File : " + filePath);
            File.WriteAllText(filePath, json);
            
            return File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Tutorial] WriteTutorialRecordFile error!!!");
            Debug.LogWarning(ex.Message);
            return null;
        }
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
    
    public bool IsAlreadyWatchedTutorial(int code)
    {
        if (!this.isOpened) return true;

        try
        {
            if (this._tutorialRecord == null) this._tutorialRecord = new TutorialRecord();

            var recordDict = this._tutorialRecord.recordDict;
            if (recordDict.ContainsKey(code))
            {
                return recordDict[code];
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Tutorial] IsAlreadyWatchedTutorial error!!!");
            Debug.LogWarning(ex.Message);

            return true;
        }
    }

    //해당 스테이지의 튜토리얼이 존재하는가?
    public bool IsIngameTutorialExist(int targetLevel)
    {
        if (!this.isOpened) return false;
        if (this._tutorialRecord == null) return false;

        return SBDataSheet.Instance.StageInfo[targetLevel].TutorialBoolean;
    }

    public int GetIngameTutorialCode(int targetLevel)
    {
        if (!this.isOpened) return -1;
        if (this._tutorialRecord == null) return -1;

        return SBDataSheet.Instance.StageInfo[targetLevel].TutorialPopupRes;
    }

    private Action ingameVideoFinished;
    public void OpenIngameTutorialVideo(int stageLevel, int code, Action onFinished)
    {
        this.ingameVideoFinished = onFinished;
        if (!this.isOpened)
        {
            this.ingameVideoFinished?.Invoke();
            return;
        }

        var targetStageInfo = SBDataSheet.Instance.StageInfo[stageLevel];
        IngameTutorialPopup.Params popupParam = new IngameTutorialPopup.Params();
        popupParam.titleLocaleCode = targetStageInfo.TutorialPopupTitleLocale;
        popupParam.contextLocaleCode = targetStageInfo.TutorialPopupDesLocale;

        if (SBDataSheet.Instance.SystemLocale[15510] != null)
        {
            popupParam.confirmButtonLocaleCode = SBDataSheet.Instance.SystemLocale[15510].Code;    
        }
        popupParam.videoLocaleCode = targetStageInfo.TutorialPopupRes;
        popupParam.playerLocale = GameStorage.Instance.GetStorage<PlayerStorage>().Locale;
        popupParam.onVideoFinished += () => { };
        
        Popup.Load("Tutorial/IngameTutorialPopup", popupParam, (popup, result) =>
        {
            GameScene.Instance.sceneCam.enabled = false;
            onFinished?.Invoke();
        });

        this._tutorialRecord.recordDict[code] = true;
        this.WriteTutorialRecordFile();
    }
    
    public void ClearOutRenderTexture(RenderTexture renderTexture)
    {
        if(!this.isOpened) return;
        
        RenderTexture rt = RenderTexture.active;
        RenderTexture.active = renderTexture;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = rt;
    }

    
    private Action onVideoFinished;
    //로비용 튜토리얼 영상 재생
    public void OpenFullScreenTutorialVideo(int code, Action onFinished)
    {
        //사용하지 않음
        onFinished?.Invoke();
        return;
        
        if(!this.isOpened) return;
        
        this.ClearOutRenderTexture(this.tutorialVideoPlayer.targetTexture);
        
        if(!this.isOpened) return;
        fullSizeTutorial.SetActive(true);

        if(!GameStorage.Instance.IsOpened) return;
        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();

        string videoResAddress = string.Empty;
        if (playerStorage.Locale == "ko")
        {
            videoResAddress = SBDataSheet.Instance.TutorialResource[code].KoKRAddress;
            this.skipButtonText.text = SBDataSheet.Instance.SystemLocale[103].KoKR;
        }
        else
        {
            videoResAddress = SBDataSheet.Instance.TutorialResource[code].EnUSAddress;
            this.skipButtonText.text = SBDataSheet.Instance.SystemLocale[103].EnUS;
        }
        
        string videoFilePath = AssetPathController.PATH_FOLDER_ASSETS + videoResAddress;
#if (UNITY_ANDROID || UNITY_IOS) //&& !UNITY_EDITOR
        //videoFilePath = string.Format("file://{0}", videoFilePath);
#endif
        Debug.Log("Video Path ko : " + videoFilePath);
        
        this.tutorialVideoPlayer.url = videoFilePath;
        this.onVideoFinished = onFinished;
        this.tutorialVideoPlayer.loopPointReached += OnVideoFinished;
        this.tutorialVideoPlayer.Play();

        GameScene.Instance.AllStopCourutine();
        GameScene.Instance.SetBGMVolume(0.1f);

        //TutorialRecord 파일 갱신
        this._tutorialRecord.recordDict[code] = true;
        this.WriteTutorialRecordFile();
    }

    private void OnVideoFinished(VideoPlayer source)
    {
        GameScene.Instance.SetBGMVolume(0.4f);
        this.onVideoFinished?.Invoke();
        this.fullSizeTutorial.SetActive(false);
    }

    public void OnSkipButton()
    {
        if(!this.isOpened) return;

        GameScene.Instance.SetBGMVolume(0.4f);
        this.tutorialVideoPlayer.Stop();
        this.tutorialVideoPlayer.frame = 0;
        this.tutorialVideoPlayer.loopPointReached -= OnVideoFinished;
        this.fullSizeTutorial.SetActive(false);
        this.onVideoFinished?.Invoke();
    }

    private void OnDestroy()
    {
        CoroutineTaskManager.RemoveTask(this.prepareVideoCoroutine);
        this.onVideoFinished = null;
    }
    
    private void OnProfileChanged(int profileCode)
    {
        var artistCode = SBDataSheet.Instance.ProfileSon[profileCode].ArtistCode;
        GameStorage.SelectedTutorialVoiceCode = artistCode;

        int resourceCode = -1;
        switch (artistCode)
        {
            case 1:
                resourceCode = 2001000;
                break;
            case 2:
                resourceCode = 2002000;
                break;
            case 3:
                resourceCode = 2003000;
                break;
            case 4:
                resourceCode = 2004000;
                break;
            
            case 5:
                resourceCode = 2005000;
                break;
            
            case 6:
                resourceCode = 2006000;
                break;
        }

        if (resourceCode != -1)
        {
            GameStorage.SelectedTutorialVoiceResourceCode = resourceCode;
        }
        
        this.UpdatePortraitTexture();
    }
}

    [System.Serializable]
    public class TutorialRecord
    {
        //code값, 시청여부
        public Dictionary<int, bool> recordDict;

        public TutorialRecord()
        {
            if(SBDataSheet.Instance == null) return;
            this.recordDict = new Dictionary<int, bool>();

            var ingameTutorials = SBDataSheet.Instance.StageInfo.Values.ToList()
                .FindAll(x => x.TutorialBoolean == true);
            
            foreach (StageInfo stageInfo in ingameTutorials)
            {
                this.recordDict.Add(stageInfo.TutorialPopupRes, false);
            }
        }
    }

    /// <summary>
    /// 신규 튜토리얼 레코드 관리 [추후 통합 예정]
    /// </summary>
    public class NewTutorialRecord
    {
        public Dictionary<int, bool> recordDict;

        public NewTutorialRecord()
        {
            if(SBDataSheet.Instance == null) return;
            this.recordDict = new Dictionary<int, bool>();
            
            
        }
    }
}
