using System.Collections;

using UnityEngine;
using UnityEngine.UI;

using com.dalcomsoft.project.client.model.type;
using com.dalcomsoft.project.app.control.contents;
using com.dalcomsoft.project.app.control;

using Ext.Async;
using Ext.String;

namespace com.dalcomsoft.project.app.view.sub
{
    public class ARMiniGameView : MonoBehaviour
    {
        [Header("SubUICanvas")]
        [SerializeField]
        private Canvas infoUICanvas;

        [SerializeField]
        private Canvas miniGameUICanvas;

        [SerializeField]
        private Canvas resultUICanvas;

        [Header("MiniGame Sub Prefab")]
        [SerializeField]
        private GameObject infoObj;     //안내 문구 프리팹

        [SerializeField]
        private GameObject miniGameObj; //미니게임 프리팹

        [SerializeField]
        private GameObject resultObj;   //결과화면 프리팹

        [Header("Info Sub UI")]
        [SerializeField]
        private Text infoHeaderText;

        [SerializeField]
        private Text infoContext;

        [SerializeField]
        private Text infoBtnText;

        [SerializeField]
        private GameObject miniGameStartBtnObj;

        public GameObject MiniGameStartBtnObj { get { return miniGameStartBtnObj; } }

        public Text InfoHeaderText { get { return this.infoHeaderText; } }
        public Text InfoContext { get { return this.infoContext; } }
        public Text InfoBtnText { get { return this.infoBtnText; } }

        [Header("MiniGame Sub UI")]
        [SerializeField]
        private Text miniGameContext;

        [SerializeField]
        private Text scoreText;

        public Text ScoreText { get { return this.scoreText; } }

        [SerializeField]
        private Text remainTimeText;            //남은 시간

        public Text RemainTimeText { get { return this.remainTimeText; } }

        [SerializeField]
        public Text remainQuizHeaderText;   //남은 문제 수(헤더)
        public Text RemainQuizHeaderText { get { return this.remainQuizHeaderText; } }

        [SerializeField]
        private Text remainQuizValText;     //남은 문제 수(값)
        public Text RemainQuizText { get { return this.remainQuizValText; } }

        [SerializeField]
        public GameObject trueBtnObj, falseBtnObj, retryBtnObj;

        [SerializeField]
        public ARMiniGameButtonView trueBtnView, falseBtnView;

        [SerializeField]
        private Image timerGage;

        [Header("Animations")]
        [SerializeField]
        public Animation scoreTextAnim;            //미니게임 스코어 획득 Text Anim
        [SerializeField]
        public Animation infoPopupAppearAnim;      //미니게임 안내 문구 등장 Anim
        [SerializeField]
        public Animation quizContextAnim;          //미니게임 내 퀴즈 등장 Anim
        [SerializeField]
        public Animation timerAnim;                //미니게임 타이머 Anim
        [SerializeField]
        public Animation resultAnim;
        [SerializeField]
        public Animation resultHighScoreAnim;

        public bool IsScoreTextAnimCompleted { get { return !this.scoreTextAnim.isPlaying; } }
        public bool IsInfoPopupAppearAnimCompleted { get { return !this.infoPopupAppearAnim.isPlaying; } }
        public bool IsQuizContextAnimCompleted { get { return !this.quizContextAnim.isPlaying; } }
        public bool IsTimerAnimCompleted { get { return !this.timerAnim.isPlaying; } }
        public bool IsResultHighScoreAnimCompleted { get { return !this.resultHighScoreAnim.isPlaying; } }

        [Header("Result Sub UI")]
        [SerializeField]
        private Text resultPopupHeader;
        [SerializeField]
        private Text resultTotalScoreHeader;
        [SerializeField]
        private Text resultTotalScoreVal;
        [SerializeField]
        private Text resultCorrectNumHeader;
        [SerializeField]
        private Text resultCorrectNumVal;
        [SerializeField]
        private Text resultIncorrectNumHeader;
        [SerializeField]
        private Text resultIncorrectNumVal;
        [SerializeField]
        private Text resultRetryBtnText;
        [SerializeField]
        private GameObject resultGlowEffectObj;

        public Text ResultTotalScoreVal { get { return this.resultTotalScoreVal; } }


#if ENABLE_AR_CONTENT
        private float gageWarningTime;
        private ARMiniGameButtonView clickedButtonView;

        private bool textIncreaseAnimFinished = true;

        private Coroutine effectCoroutine = null;
        private Coroutine textIncreaseCoroutine = null;
        private Coroutine timeLimitSoundCoroutine = null;

        private bool isARSceneExited = false;

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

        public void SetGageWarningTime(float warningTime)
        {
            this.gageWarningTime = warningTime;
        }

        public void Refresh(ARQuizControl.Data data, ARTrackedControl.MiniGameBoard miniGameBoard)
        {
            Ext.DebugX.Log("<color=yellow>Refresh : " + miniGameBoard.score + "</color>");
            Ext.DebugX.Log("<color=yellow>Refresh CurrentQuizIndex : " + miniGameBoard.CurrentQuizIndex + "</color>");
            Ext.DebugX.Log("<color=yellow>Refresh TotalQuizCount : " + miniGameBoard.TotalQuizCount + "</color>");

            this.ChangeScoreText(miniGameBoard.score);
            this.ChangeRemainQuestionNumberText(
                miniGameBoard.CurrentQuizIndex,
                miniGameBoard.TotalQuizCount
            );

            this.miniGameContext.text = data.Context.ToString();
        }

        private void ChangeScoreText(int score = 0)
        {
            this.scoreText.text = score.ToCommaSeparatedStringI();
        }

        private void ChangeRemainQuestionNumberText(int currentQuizIndex, int totalQuizNum)
        {
            string remainQuizLocaleHeader = LocaleControl.GetString(LocaleCodes.AR_MINIGAME_REMAIN_QUIZ_NUM_HEADER);
            this.remainQuizHeaderText.text = remainQuizLocaleHeader;

            string remainQuizLocaleVal = LocaleControl.GetString(LocaleCodes.AR_MINIGAME_REMAIN_QUIZ_NUM_VALUE);
            Ext.DebugX.Log("remainQuizLocaleVal : " + remainQuizLocaleVal);

            remainQuizLocaleVal = remainQuizLocaleVal
                .Replace("{0}", currentQuizIndex.ToString())
                .Replace("{1}", totalQuizNum.ToString());

            this.remainQuizValText.text = remainQuizLocaleVal;
        }

        private bool isTimeLimitSoundPlayed = false;
        public void UpdateRemainTimeText(float remainTime, float fillAmount)
        {
            string remainTimeLocaleValue = LocaleControl.GetString(LocaleCodes.AR_MINIGAME_REMAIN_TIME_VALUE);

            System.TimeSpan dateTime = System.TimeSpan.FromSeconds(remainTime);
            string timeSpanText = string.Format(
                "{0:D2} : {1:D2}", dateTime.Minutes, dateTime.Seconds
            );

            this.RemainTimeText.text = remainTimeLocaleValue.Replace("{0}", timeSpanText);

            if (remainTime <= this.gageWarningTime)
            {
                if (!this.isTimeLimitSoundPlayed)
                {
                    SoundFx.Play(AssetControl.SOUND_AR_MINIGAME_TIME_LIMIT, 0.7f);
                    this.isTimeLimitSoundPlayed = true;

                    this.timeLimitSoundCoroutine = CoroutineTaskManager.AddTask(this.TimeLimitSound(this.gageWarningTime));
                }

                if (remainTime > 0)
                {
                    if (IsTimerAnimCompleted)
                        this.timerAnim.Play("ar_timer");
                }
                else
                {
                    if (!IsTimerAnimCompleted)
                        this.timerAnim.Stop();
                }
            }
            this.timerGage.fillAmount = fillAmount;
            //Ext.DebugX.Log("UpdateRemainTimeText : " + remainTime);
        }

        private IEnumerator TimeLimitSound(float gageWarningTime = 10)
        {
            float index = gageWarningTime;
            while (index > 0)
            {
                yield return new WaitForSeconds(1.0f);
                SoundFx.Play(AssetControl.SOUND_AR_MINIGAME_TIME_LIMIT, 0.7f);

                index--;
            }
        }

        private void ActiveGamePanel(GameObject obj)
        {
            this.infoObj.SetActive(infoObj == obj);
            this.miniGameObj.SetActive(miniGameObj == obj);
            this.resultObj.SetActive(resultObj == obj);
        }

        public void DeactiveGamePanels()
        {
            this.infoObj.SetActive(false);
            this.resultObj.SetActive(false);
            this.miniGameObj.SetActive(false);
        }

        public void StartMiniGame()
        {
            Ext.DebugX.Log("ARMiniGameView StartMiniGame");
            this.ActiveGamePanel(this.miniGameObj);

            SoundFx.Play(AssetControl.SOUND_AR_MINIGAME_QUIZ_BTN, 0.7f);
        }

        public void EndMiniGame(ARTrackedControl.MiniGameBoard miniGameBoard, bool isSuccess)
        {
            this.ActiveGamePanel(this.resultObj);

            this.resultGlowEffectObj.SetActive(isSuccess);

            this.timerAnim
                .transform
                .Find("Bar")
                .GetComponent<Image>().color = new Color32(0, 214, 210, 255);
            this.timerAnim.Stop();

            if (isSuccess)
                this.resultAnim.Play("ar_popup_open");

            this.resultPopupHeader.text = LocaleControl.GetString(LocaleCodes.AR_RESULT_POP_TITLE);

            this.resultTotalScoreHeader.text = LocaleControl.GetString(LocaleCodes.AR_RESULT_POP_SCORE_TITLE);
            this.ResultTotalScoreVal.text = miniGameBoard.score.ToCommaSeparatedStringI();

            this.resultCorrectNumHeader.text = LocaleControl.GetString(LocaleCodes.AR_RESULT_POP_CORRECT_NUM);
            this.resultCorrectNumVal.text = miniGameBoard.correct.ToString();

            this.resultIncorrectNumHeader.text = LocaleControl.GetString(LocaleCodes.AR_RESULT_POP_INCORRECT_NUM);
            this.resultIncorrectNumVal.text = miniGameBoard.inCorrect.ToString();

            this.resultRetryBtnText.text = LocaleControl.GetString(LocaleCodes.AR_RESULT_POP_RETRY_BTN);

            this.ResultScoreTextIncreaseAnim(miniGameBoard);

            SoundFx.Play(AssetControl.SOUND_AR_MINIGAME_RESULT_POPUP_APPEAR, 0.7f);
        }

        public void OnMiniGameBtnClicked(EffectParm p, System.Action callback)
        {
            this.trueBtnObj.GetComponent<Button>().interactable = false;
            this.falseBtnObj.GetComponent<Button>().interactable = false;

            this.clickedButtonView = p.isTrueBtn ? this.trueBtnView : this.falseBtnView;

            if (this.effectCoroutine != null)
                CoroutineTaskManager.RemoveTask(this.effectCoroutine);

            this.effectCoroutine = CoroutineTaskManager.AddTask(
                this.MiniGameBtnClickedEffect(p, callback)
            );
        }

        private IEnumerator MiniGameBtnClickedEffect(EffectParm p, System.Action callback)
        {
            this.ActiveBtnEffect(p.isCorrect);

            if (p.isCorrect)
            {
                this.AddScoreAnim(p.isCorrect, p.changeScoreAmount);
                this.ScoreTextIncreaseAnim(p.miniGameBoard, p.changeScoreAmount);

                SoundFx.Play(AssetControl.SOUND_AR_MINIGAME_QUIZ_CORRECT, 0.7f);

                yield return new WaitUntil(() => this.IsScoreTextAnimCompleted);
            }
            else
            {
                this.textIncreaseAnimFinished = true;

                SoundFx.Play(AssetControl.SOUND_AR_MINIGAME_QUIZ_INCORRECT, 0.7f);
            }

            yield return new WaitUntil(() => this.clickedButtonView.IsAnimFinished);
            yield return new WaitUntil(() => this.textIncreaseAnimFinished);

            this.trueBtnObj.GetComponent<Button>().interactable = true;
            this.falseBtnObj.GetComponent<Button>().interactable = true;

            this.scoreTextAnim.gameObject.SetActive(false);

            callback();
        }

        /// <summary>
        /// 상단에 +N 애니메이션
        /// </summary>
        /// <param name="isCorrect"></param>
        /// <param name="increaseAmount"></param>
        private void AddScoreAnim(bool isCorrect, int increaseAmount)
        {
            this.scoreTextAnim.gameObject.SetActive(true);
            Text scoreText = this.scoreTextAnim.GetComponent<Text>();
            if (isCorrect)
            {
                scoreText.text = string.Format("+{0}", increaseAmount);
                this.scoreTextAnim.Play("ar_score_correct");
            }
            else
            {
                scoreText.text = string.Empty;
            }
        }

        /// <summary>
        /// 미니게임 도중 전체 점수 증가 애니메이션
        /// </summary>
        private void ScoreTextIncreaseAnim(ARTrackedControl.MiniGameBoard miniGameBoard, int amount)
        {
            this.textIncreaseAnimFinished = false;

            int startAt = miniGameBoard.score;
            int endAt = miniGameBoard.score + amount;
            float animTime = 1.0f;

            if (this.textIncreaseCoroutine != null)
                CoroutineTaskManager.RemoveTask(this.textIncreaseCoroutine);

            CancellableSignal signal = new CancellableSignal(() => this.isARSceneExited);

            this.textIncreaseCoroutine = CoroutineTaskManager.AddTask(
                this.TextIncreaseAnim(startAt, endAt, animTime, this.ScoreText, signal)
            );
        }

        private void ResultScoreTextIncreaseAnim(ARTrackedControl.MiniGameBoard miniGameBoard)
        {
            if (this.textIncreaseCoroutine != null)
                CoroutineTaskManager.RemoveTask(this.textIncreaseCoroutine);

            CancellableSignal signal = new CancellableSignal(() => this.isARSceneExited);
            this.textIncreaseCoroutine = CoroutineTaskManager.AddTask(
                this.TextIncreaseAnim(0, miniGameBoard.score, 1.0f, this.ResultTotalScoreVal, signal)
            );
        }

        IEnumerator TextIncreaseAnim(float startAt, float endAt, float animTime, Text targetText, CancellableSignal signal)
        {
            if (animTime <= 0) yield break;
            if (startAt > endAt) yield break;

            targetText.text = startAt.ToCommaSeparatedStringF();

            float deltaVal = endAt - startAt;
            float currentTime = 0;
            while (currentTime < animTime)
            {
                if (CancellableSignal.IsCanceled(signal))
                    yield break;

                currentTime += Time.deltaTime;
                double val = (double)startAt + (double)((deltaVal * currentTime) / animTime);
                double roundVal = System.Math.Round(val);
                targetText.text = System.Convert
                    .ToSingle(roundVal)
                    .ToCommaSeparatedStringF();

                yield return new WaitForEndOfFrame();
            }

            targetText.text = endAt.ToCommaSeparatedStringF();
            this.textIncreaseAnimFinished = true;
        }

        private void ActiveBtnEffect(bool isCorrect)
        {
            this.clickedButtonView.Effect(isCorrect, 0.8f);
        }

        public void ResultHighScoreAnim()
        {
            if (this.IsResultHighScoreAnimCompleted)
                this.resultHighScoreAnim.Play("ar_popup_open");
        }

        public void PlayResultSuccessSound()
        {
            SoundFx.Play(AssetControl.SOUND_AR_MINIGAME_RESULT_SUCCESS, 0.7f);
        }

        public void PlayResultFailedSound()
        {
            SoundFx.Play(AssetControl.SOUND_AR_MINIGAME_RESULT_FAILED, 0.7f);
        }

        public class EffectParm
        {
            public bool isTrueBtn;                      //클릭한 버튼 종류
            public bool isCorrect;                      //정답유무
            public float proceedTime;                   //애니메이션 진행 시간
            public int changeScoreAmount = 100;         //점수 변화량

            public ARTrackedControl.MiniGameBoard miniGameBoard;
        }

        private void OnDisable()
        {
            this.isARSceneExited = true;

            if (this.textIncreaseCoroutine != null)
                CoroutineTaskManager.RemoveTask(this.textIncreaseCoroutine);

            if (this.effectCoroutine != null)
                CoroutineTaskManager.RemoveTask(this.effectCoroutine);

            if (this.timeLimitSoundCoroutine != null)
                CoroutineTaskManager.RemoveTask(this.timeLimitSoundCoroutine);


        }
#endif  // ENABLE_AR_CONTENT
    }
}
