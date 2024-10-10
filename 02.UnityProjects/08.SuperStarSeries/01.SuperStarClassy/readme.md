Unity 기반 AR 컨텐츠 개발
==========================
> 달콤소프트 게임 신규 컨텐츠 개발   
> 개발 기간 : 2021.11 ~ 2022.02 [약 4개월]   
> 출시 여부 : 출시

개발 환경
==========================
엔진 : Unity 2020.3.25f1      
플랫폼 : Android, iOS   
버전 관리 : SVN   
이슈 관리 : JIRA   


프로젝트 소개
==========================
*슈퍼스타 클라씨 특징*   
클라씨 멤버의 실물 포토카드를 인식하면, 관련 미니게임을 즐길 수 있음.   
일정 시간동안 최대한 많은 O,X 문제를 푸는 것이 목표   
기존 슈퍼스타의 설계 패턴(MVP에 가까움)에 맞게 작성  

***
ARMiniGameView.cs 코드 일부   
AR 미니게임에 대한 View 영역   

초기화 부분   

<pre>
  <code>
    public void Init(Camera camera)
    {
        //infoUI 초기화
        Ext.DebugX.Log("ARMiniGameView Init");

        this.trueBtnObj.GetComponent<Button>().interactable = true;
        this.falseBtnObj.GetComponent<Button>().interactable = true;

        this.infoUICanvas.worldCamera = camera;
        this.miniGameUICanvas.worldCamera = camera;
        this.resultUICanvas.worldCamera = camera;

        this.ChangeScoreText();

        this.ActiveGamePanel(infoObj);

        this.isARSceneExited = false;

        SoundFx.Play(AssetControl.SOUND_AR_MINIGAME_GAME_START, 0.7f);

        this.isTimeLimitSoundPlayed = false;
    }
  </code>
</pre>

ARTrackedControl.cs 코드 일부
AR 미니게임에 대한 Control 영역   
사용자 입력 [O, X 버튼 클릭] 에 대한 처리를 Model의 Data를 이용하여 한다.   
Queue를 활용하여 퀴즈를 진행한다.   

<pre>
  <code>
    public void OnMiniGameBtnClicked(bool isTrueBtnClicked)
    {
        //1. 정답 유무 판단
        //2. 정답/오답 애니메이션 재생 + 점수 증가 애니메이션 + 버튼 비활성화
        //3. 2번의 애니메이션이 모두 종료될때까지 대기
        //4. 다음 퀴즈 추출 및 출력 애니메이션
        //5. 4번 종료되면 버튼 활성화

        bool rightAnswer = System.Convert.ToBoolean(miniGameBoard.CurrentQuizData.Answer);
        bool isCorrect = rightAnswer == isTrueBtnClicked;
        int increaseScorePerCorrect = this.prevParm.arData.AnswerScore.HasValue ? this.prevParm.arData.AnswerScore.Value : 100;

        ARMiniGameView.EffectParm p = new ARMiniGameView.EffectParm();
        p.isTrueBtn = isTrueBtnClicked;
        p.isCorrect = isCorrect;
        p.proceedTime = 1.0f;
        p.changeScoreAmount = increaseScorePerCorrect;
        p.miniGameBoard = this.miniGameBoard;

        this.miniGameView.OnMiniGameBtnClicked(p, () =>
        {
            this.RefreshQuiz();
        });

        if (isCorrect)
        {
            this.miniGameBoard.AddScore(increaseScorePerCorrect);
            this.miniGameBoard.IncreaseCorrectNum();

            Ext.DebugX.Log("Correct Answer!!");
        }
        else
        {
            Ext.DebugX.Log("OnMiniGameBtnClicked score :" + this.miniGameBoard.score);

            float reduceTime = this.prevParm.arData.WronAnswerTime.HasValue ? this.prevParm.arData.WronAnswerTime.Value : 5.0f;
            miniGameBoard.ReduceRemainTime(reduceTime);
            miniGameBoard.IncreaseIncorrectNum();

            Ext.DebugX.Log("Not Correct Answer!!");
        }
    }

    private void RefreshQuiz()
    {
        ARQuizControl.Data selectedQuizData = this.miniGameBoard.PickQuizData();
        if (selectedQuizData != null)
        {
            miniGameView.Refresh(selectedQuizData, this.miniGameBoard);
            miniGameView.quizContextAnim.Play("ar_context");

            SoundFx.Play(AssetControl.SOUND_AR_MINIGAME_QUIZ_APPEAR, 0.7f);
        }
        else
        {
            this.OnFinishedMiniGame();
            Ext.DebugX.Log("No More Quiz!");
        }
    }

    public ARQuizControl.Data PickQuizData()
    {
        if (this.gameQuiz.Count == 0) return null;

        this.currentQuizIndex++;
        Ext.DebugX.Log(
            string.Format("PickQuizData and {0} quiz left", gameQuiz.Count - 1)
        );
        var res = this.gameQuiz.Dequeue();
        this.CurrentQuizData = res;

        return res;
    }
  </code>
</pre>

Model의 정보를 가져와 View에서 필요한 정보를 Dictionary에 담는다.
<pre>
  <code>
    public static void Open()
    {
        Control.Map = new Dictionary<short, Data>(ARQuizDataStorage.Count);

        var node = ARQuizDataStorage.First;
        while (node != null)
        {
            var pair = node.Value;
            node = node.Next;
            Data data = new Data(pair.Value);

            Control.Map.Add(pair.Key, data);
        }
    }
  </code>
</pre>

ARTrackedImageInfoRuntimeControl.cs 코드 일부   
AR에 대한 초기화 부분   
ARFoundation을 이용하여 Android, iOS의 각각 Native Library 에 접근한다.  

<pre>
  <code>
    private void SettingAR()
    {
        Ext.DebugX.Log("ARScene SettingAR...");
        ARTrackedImageInfoRuntimeControl.Open();

        ARView arView = this.sceneView;
        UnityEngine.XR.ARFoundation.ARTrackedImageManager trackedImageManager = arView.arTrackedImageManager;

        trackedImageManager.enabled = false;
        arView.arContentsControl.enabled = false;

        ARTrackedImageInfoRuntimeControl.ARImageInit(trackedImageManager, (isSucess, lib) =>
        {
            if (!isSucess)
            {
#if UNITY_EDITOR
                Ext.DebugX.Log("Unity Editor detected.");
                this.sceneView.dummyObj.SetActive(true);
                ARTrackedControl trackedView = this.sceneView
                    .dummyObj
                    .transform.GetChild(0)
                    .GetComponent<ARTrackedControl>();

                ARTrackedControl.Parm p = new ARTrackedControl.Parm();

                p.camera = this.sceneView.arCamera;
                short dummyCode = 0;
                var arData = control.contents.ARControl.GetARData(dummyCode);
                p.arData = arData;
                p.contentType = ARTrackedControl.ContentType.VIDEO;
                p.arQuizDatas = control.contents.ARQuizControl.GetARQuizData(100);
                p.code = arData.Code;
                p.fullScreenVideoView = this.sceneView.videoView;
                p.arSceneCloseBtn = this.sceneView.closeBtn;

                trackedView.Init(p);
#else
                Ext.DebugX.Log("ARImageInit Failed! maybe unsupported device");
                this.sceneView.OnUnsupportedEvent();
#endif

            }
            //Image Setting Success
            else
            {
                trackedImageManager.referenceLibrary = lib;
                trackedImageManager.requestedMaxNumberOfMovingImages = 1;
                trackedImageManager.trackedImagePrefab = arView.trackedImagePrefab;

                trackedImageManager.enabled = true;
                arView.arContentsControl.enabled = true;

                Ext.DebugX.Log("ARAscene ARTrackedImageInfoRuntimeControl.ARImageInit callback success");
                this.SetStateDone();
            }
        });
    }

    public static void ARImageInit(ARTrackedImageManager trackedImageManager, InitResult callback)
    {
        Ext.DebugX.Log("ARTrackedImageInfoRuntimeControl ARImageInit...");
        if (!Control.IsOpened) return;

        Control.callback = callback;
        Control.trackedImageManager = trackedImageManager;

        CoroutineTaskManager.AddTask(Control.ARImageInitialize());
    }

    private static IEnumerator ARImageInitialize()
    {
        Ext.DebugX.Log(
            string.Format("ARSession Init State : {0}", ARSession.state.ToString())
        );

#if UNITY_EDITOR
        Control.callback?.Invoke(false, null);
        yield break;
#endif

        if ((ARSession.state == ARSessionState.None) ||
            (ARSession.state == ARSessionState.CheckingAvailability))
        {
            yield return ARSession.CheckAvailability();
        }
        else if (ARSession.state == ARSessionState.Unsupported)
        {
            Control.callback?.Invoke(false, null);
            Control.callback = null;
        }
        else if (ARSession.state == ARSessionState.SessionInitializing)
        {
            MutableRuntimeReferenceImageLibrary runtimeLib = Control
                .trackedImageManager
                .CreateRuntimeLibrary() as MutableRuntimeReferenceImageLibrary;

            CoroutineTaskManager.AddTask(
                Control.AddImages(runtimeLib)
            );
        }
    }
  </code>
</pre>
