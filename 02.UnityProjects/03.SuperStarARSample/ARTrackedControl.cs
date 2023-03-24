using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;

using com.dalcomsoft.project.app.view.sub;
using com.dalcomsoft.project.client.command;
using com.dalcomsoft.project.client.model.type;

using Ext.Async;
using Ext.Unity3D.Cdn;

namespace com.dalcomsoft.project.app.control.contents
{
    public class ARTrackedControl : MonoBehaviour
    {
        [SerializeField]
        private AR3DVideoView videoView;    //동영상 재생 관련 프리팹

        [SerializeField]
        private ARMiniGameView miniGameView; //미니게임 관련 프리팹

        Coroutine rewardReceiveCoroutine = null;

#if ENABLE_AR_CONTENT
        private bool isPlay;
        public bool IsPlay
        {
            get
            {
                return this.isPlay;
            }
            set
            {
                this.isPlay = value;
            }
        }

        private bool isMiniGameEndReceived = false;

        private MiniGameBoard miniGameBoard;
        private ARControl.Data selectedARData;
        private Parm prevParm;
        public void Init(Parm p)
        {
            Ext.DebugX.Log("ARTrackedControl Init...");
            this.isMiniGameEndReceived = false;
            this.selectedARData = p.arData;

            this.miniGameView.gameObject.SetActive(p.contentType == ContentType.MINI_GAME);
            this.videoView.gameObject.SetActive(p.contentType == ContentType.VIDEO);

            if (p.contentType == ContentType.MINI_GAME)
            {
                Ext.DebugX.Log("ARTrackedControl Init ContentType.MINI_GAME...");

                Button trueBtn = miniGameView.trueBtnObj.GetComponent<Button>();
                trueBtn.onClick.RemoveAllListeners();
                trueBtn.onClick.AddListener(() => { this.OnMiniGameBtnClicked(true); });

                Button falseBtn = miniGameView.falseBtnObj.GetComponent<Button>();
                falseBtn.onClick.RemoveAllListeners();
                falseBtn.onClick.AddListener(() => { this.OnMiniGameBtnClicked(false); });

                Button retryBtn = miniGameView.retryBtnObj.GetComponent<Button>();
                retryBtn.onClick.RemoveAllListeners();
                retryBtn.onClick.AddListener(() => { this.OnRetryBtnClicked(); });

                Button startMiniGameBtn = miniGameView.MiniGameStartBtnObj.GetComponent<Button>();
                startMiniGameBtn.onClick.RemoveAllListeners();
                startMiniGameBtn.onClick.AddListener(() => { this.OnStartMiniGameBtnClicked(); });

                miniGameView.Init(p.camera);
                miniGameView.infoPopupAppearAnim.Play("ar_popup_open");

                if (this.miniGameBoard != null)
                    this.miniGameBoard.Reset();

                MiniGameBoard.Parm miniGameParm = new MiniGameBoard.Parm();
                miniGameParm.arQuizDatas = p.arQuizDatas;
                miniGameParm.arMiniGameView = this.miniGameView;
                miniGameParm.callback = this.OnFinishedMiniGame;
                if (p.arData.TimeLimit.HasValue)
                {
                    miniGameParm.beginRemainTime = p.arData.TimeLimit.Value;
                }
                else miniGameParm.beginRemainTime = 0;

                //test code.
                //miniGameParm.beginRemainTime = 10;
                //end test code.

                miniGameBoard = new MiniGameBoard(miniGameParm);

                ARQuizControl.Data selectedQuizData = miniGameBoard.PickQuizData();
                if (selectedQuizData != null)
                {
                    miniGameView.Refresh(selectedQuizData, this.miniGameBoard);
                }
                else
                {
                    Ext.DebugX.Log("Pick Quiz Data Failed!");
                }

                LocalePopupControl.Data localePopupData = LocalePopupControl.Find(PopupCodes.AR_MINIGAME_INFO_POPUP);
                this.miniGameView.InfoHeaderText.text = localePopupData.Title;
                this.miniGameView.InfoContext.text = localePopupData.GetMessage(0);
                this.miniGameView.InfoBtnText.text = localePopupData.GetButton(0);

                this.PlayBGM();
            }
            else if (p.contentType == ContentType.VIDEO)
            {
                Ext.DebugX.Log("ARTrackedControl Init ContentType.VIDEO...");

                if (!this.videoView.IsPlay)
                {
                    this.videoView.Play(p.arData.ARVideoPath.FilePath(), this.OnVideFinished);
                }
            }

            this.prevParm = p;
            this.IsPlay = true;
        }

        private void OnStartMiniGameBtnClicked()
        {
            this.miniGameView.StartMiniGame();
            this.miniGameBoard.StartTimer();
        }

        public void Stop(ARControl.Data arData)
        {
            if (selectedARData != null)
            {
                if (arData.Code == this.selectedARData.Code)
                {
                    //미니게임
                    if (arData.ContentType == 1)
                    {
                        this.miniGameView.DeactiveGamePanels();
                    }
                    //비디오
                    else if (arData.ContentType == 0)
                    {
                        this.videoView.Pause();
                    }
                }
            }

            this.StopPlayBGM();
            this.IsPlay = false;
        }

        private void OnVideFinished(AR3DVideoView videoView)
        {
            Ext.DebugX.Log("OnVideFinished");

            this.IsRewardReceived((isReceived) =>
            {
                if (!isReceived)
                {
                    this.rewardReceiveCoroutine = CoroutineTaskManager.AddTask(this.RequestReward());
                }
                else
                {
                    this.IsPlay = false;
                }
            });
        }

        private void OnFinishedMiniGame()
        {
            if (this.isMiniGameEndReceived) return;

            this.isMiniGameEndReceived = true;

            this.miniGameBoard.updateTimer = false;

            Ext.DebugX.Log("OnFinishedMiniGame");

            if (this.selectedARData == null) return;

            //결과화면 팝업
            Ext.DebugX.Log("OnFinishedMiniGame ARVideo not exist");
            this.StopPlayBGM();

            //미니게임을 클리어 하였고
            //결과화면을 잠깐 보여주고
            //특별 영상이 존재하면 영상을 재생시킨다.
            //보상 수령이 존재하는 경우 보상 수령 이후 영상 재생
            int rewardCutlineScore = this.prevParm.arData.RewardScore.HasValue ? this.prevParm.arData.RewardScore.Value : 0;

            bool isResultSuccesss = this.miniGameBoard.score >= rewardCutlineScore;
            this.miniGameView.EndMiniGame(this.miniGameBoard, isResultSuccesss);
            if (isResultSuccesss)
            {
                this.miniGameView.ResultHighScoreAnim();
                this.miniGameView.PlayResultSuccessSound();
                this.IsRewardReceived((isReceived) =>
                {
                    if (!isReceived)
                    {
                        //3.0초 뒤에 보상획득 연출
                        this.prevParm.arSceneCloseBtn.SetActive(false);
                        this.rewardReceiveCoroutine = CoroutineTaskManager.AddTask(this.RequestReward());
                    }
                    else
                    {
                        Ext.DebugX.Log("Already Received Reward!");
                        this.PlayMiniGameVideo();

                        this.isMiniGameEndReceived = false;
                    }
                });
            }
            else
            {
                this.miniGameView.PlayResultFailedSound();
            }
        }

        public void PlayBGM()
        {
            if (!BgmPlayer.IsPlaying(AssetControl.SOUND_AR_MINIGAME_BGM))
                BgmPlayer.PlaySingle(AssetControl.SOUND_AR_MINIGAME_BGM);
        }

        public void StopPlayBGM()
        {
            if (BgmPlayer.IsPlaying(AssetControl.SOUND_AR_MINIGAME_BGM))
                BgmPlayer.Stop(AssetControl.SOUND_AR_MINIGAME_BGM);
        }

        private void PlayMiniGameVideo()
        {
            try
            {
                bool isARVideoExist = this.selectedARData.ARVideoPath != null;
                if (isARVideoExist)
                {
                    //전체화면 동영상 재생
                    //재생 끝나면 로비로...
                    this.prevParm.fullScreenVideoView.transform.parent.gameObject.SetActive(true);
                    this.prevParm.fullScreenVideoView.gameObject.SetActive(true);
                    this.prevParm.fullScreenVideoView.Play(this.selectedARData.ARVideoPath.FilePath(), (videoView) =>
                    {
                        SceneControl.OpenScene(SceneControl.SCENE_TYPE.Lobby);
                    });

                    this.prevParm.arSceneCloseBtn.SetActive(true);
                }
            }
            catch (System.Exception ex) { }
        }

        private void IsRewardReceived(System.Action<bool> callback)
        {
            if (this.prevParm == null)
            {
                callback(true);
                return;
            }

            Account.getUserARContentsStatus((handle, response) =>
            {
                if (handle.isSucceed)
                {
                    //이미 보상 받음
                    if (response.status == 1)
                    {
                        callback(true);
                        return;
                    }
                    else
                    {
                        callback(false);
                        return;
                    }
                }
                else
                {
                    callback(false);
                    return;
                }
            }, this.prevParm.code);
        }

        IEnumerator RequestReward()
        {
            if (this.prevParm == null) yield break;

            yield return new WaitForSeconds(3.0f);

            Ext.DebugX.Log("ARTrackedControl RequestReward : " + this.prevParm.code);

            this.prevParm.arSceneCloseBtn.SetActive(false);
            this.prevParm.camera.enabled = false;

            Account.getARContentsReward((handle, rewardList) =>
            {
                if (handle.isSucceed)
                {
                    com.dalcomsoft.project.app.scene.ARScene.Instance.StopARForcely();

                    Ext.DebugX.Log("ARTrackedControl OpenRewardPopup");
                    ARControl.OpenRewardPopup(rewardList, (use, itemList) =>
                    {
                        //미니게임인 경우에만 전체영상 재생 후 로비화면으로 자동 이동
                        if (this.prevParm.contentType == ContentType.MINI_GAME)
                        {
                            this.Reset();
                            this.PlayMiniGameVideo();
                        }
                        else if (this.prevParm.contentType == ContentType.VIDEO)
                        {
                            com.dalcomsoft.project.app.scene.ARScene.Instance.ProcceedARForcely();

                            this.prevParm.camera.enabled = true;
                            this.prevParm.arSceneCloseBtn.SetActive(true);

                            this.IsPlay = false;
                        }
                    });
                }
                else
                {
                    Ext.DebugX.Log("ARTrackedControl getARContentsReward Failed");
                }
            }, this.prevParm.code);
        }

        public delegate void OnFinishedScoreAnim();


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

        public void OnRetryBtnClicked()
        {
            this.Init(this.prevParm);

            SoundFx.Play(AssetControl.SOUND_AR_MINIGAME_QUIZ_BTN, 0.7f);
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

        public class Parm
        {
            public int code;

            public ARControl.Data arData;
            public List<ARQuizControl.Data> arQuizDatas;
            public Camera camera;
            public AR2DVideoView fullScreenVideoView;
            public GameObject arSceneCloseBtn;

            public ContentType contentType;
        }

        public enum ContentType
        {
            VIDEO,
            MINI_GAME
        }

        public class MiniGameBoard
        {
            public int score;       //점수
            public int correct;     //정답 수
            public int inCorrect;   //오답 수

            public int totalQuizCount = 0;
            public int TotalQuizCount
            {
                get
                {
                    return this.totalQuizCount;
                }
            }

            public int currentQuizIndex = 0;
            public int CurrentQuizIndex
            {
                get
                {
                    return this.currentQuizIndex;
                }
            }

            Queue<ARQuizControl.Data> gameQuiz;

            private ARQuizControl.Data currentQuizData;
            public ARQuizControl.Data CurrentQuizData
            {
                get
                {
                    return this.currentQuizData;
                }
                set
                {
                    this.currentQuizData = value;
                }
            }

            private float remainTime;
            public float RemainTime
            {
                get
                {
                    return this.remainTime;
                }
                set
                {
                    this.remainTime = value;
                    if (this.remainTime < 0)
                    {
                        this.RemainTime = 0;
                        this.Stop(true);
                    }
                }
            }

            public bool updateTimer = false;
            private Coroutine remainTimeUpdateSecond = null;
            private Coroutine remainTimeGageUpdate = null;
            private ARMiniGameView miniGameView;

            private float startTime = 0.0f;

            public MiniGameBoard(Parm p)
            {
                this.score = 0;
                this.correct = 0;
                this.inCorrect = 0;

                this.RemainTime = p.beginRemainTime;

                //test code
                //this.RemainTime = 1000;
                //end test code

                this.startTime = p.beginRemainTime;

                this.miniGameView = p.arMiniGameView;
                this.callback = p.callback;


                this.updateTimer = true;

                this.miniGameView.SetGageWarningTime(this.startTime / 10.0f);
                this.gameQuiz = new Queue<ARQuizControl.Data>();

                //값 복사
                //그대로 p.arQuiDatas를 전달하는 경우 참조됨
                //값을 복사한 뒤에 그 값들로 Queue를 구성
                List<ARQuizControl.Data> quizDatas = p.arQuizDatas.ToList();
                this.totalQuizCount = quizDatas.Count;
                this.currentQuizIndex = 0;

                RandomlyAddToQueue(quizDatas);
            }

            public void StartTimer()
            {
                var remainTimeSignal = new CancellableSignal(() =>
                {
                    return this.updateTimer == false;
                });
                this.remainTimeUpdateSecond = CoroutineTaskManager.AddTask(this.RemainTimeUpdateSecond(remainTimeSignal));
            }

            IEnumerator RemainTimeUpdateSecond(CancellableSignal signal)
            {
                yield return new WaitForEndOfFrame();

                while (true)
                {
                    if (this.updateTimer)
                    {
                        this.RemainTime -= Time.deltaTime;

                        float fillAmountGage = 0;
                        if (this.startTime >= 0)
                        {
                            if (this.remainTime > 0)
                            {
                                fillAmountGage = this.remainTime / this.startTime;
                            }
                            else
                            {
                                fillAmountGage = 0.0f;
                            }
                        }
                        else
                        {
                            fillAmountGage = 1.0f;
                        }

                        this.miniGameView.UpdateRemainTimeText(this.RemainTime, fillAmountGage);
                    }

                    if (CancellableSignal.IsCanceled(signal))
                        yield break;

                    yield return new WaitForEndOfFrame();
                }
            }

            private void RandomlyAddToQueue(List<ARQuizControl.Data> list)
            {
                if (list == null || list.Count == 0)
                {
                    Ext.DebugX.Log("QuizData is empty...!");
                    return;
                }

                short range = (short)UnityEngine.Random.Range(0, list.Count);
                ARQuizControl.Data selectedQuizData = list[range];
                this.gameQuiz.Enqueue(selectedQuizData);
                list.Remove(selectedQuizData);

                if (list != null && list.Count > 0) RandomlyAddToQueue(list);
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

            public void AddScore(int amount)
            {
                this.score += amount;
                if (this.score < 0) this.score = 0;
            }

            public void IncreaseCorrectNum()
            {
                this.correct++;
            }

            public void IncreaseIncorrectNum()
            {
                this.inCorrect++;
            }

            public void ReduceRemainTime(float amount)
            {
                this.RemainTime -= amount;
            }

            public void Stop(bool callbackNeed = true)
            {
                if (callbackNeed)
                    this.callback?.Invoke();

                this.callback = null;
                this.Reset();
            }

            public void Reset()
            {
                if (this.remainTimeUpdateSecond != null)
                    CoroutineTaskManager.RemoveTask(this.remainTimeUpdateSecond);

                if (this.remainTimeGageUpdate != null)
                    CoroutineTaskManager.RemoveTask(this.remainTimeGageUpdate);

                this.updateTimer = false;
            }

            public delegate void OnFinishedCallback();
            private OnFinishedCallback callback;
            public class Parm
            {
                public List<ARQuizControl.Data> arQuizDatas;
                public ARMiniGameView arMiniGameView;
                public float beginRemainTime;

                public OnFinishedCallback callback;
            }
        }

        private void OnDisable()
        {
            this.Reset();
        }

        private void Reset()
        {
            if (this.miniGameBoard != null)
                this.miniGameBoard.Stop(false);

            Button trueBtn = miniGameView.trueBtnObj.GetComponent<Button>();
            if (trueBtn != null) trueBtn.onClick.RemoveAllListeners();

            Button falseBtn = miniGameView.falseBtnObj.GetComponent<Button>();
            if (falseBtn != null) falseBtn.onClick.RemoveAllListeners();

            if (this.rewardReceiveCoroutine != null)
                CoroutineTaskManager.RemoveTask(this.rewardReceiveCoroutine);

            this.IsPlay = false;
        }
#endif  // ENABLE_AR_CONTENT
    }
}
