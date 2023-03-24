using System;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine;
using BestHTTP.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocketFormat;
using IngameEditor;
using TMPro;

public delegate void DequeueCallback();

/// 서버로부터 데이터를 받아올 때 reflection으로 string을 함수로 바로 발동하게 하는 부분
public partial class BattleConnector : MonoBehaviour {
    public GameState gameState;
    private string raceName;
    public Queue<ReceiveFormat> queue = new Queue<ReceiveFormat>();
    public ShieldStack shieldStack = new ShieldStack();

    private Type thisType;
    public ResultFormat result = null;
    public bool isOpponentPlayerDisconnected = false;
    public ReceiveFormat gameResult = null;

    string matchKey = string.Empty;
    private int? serverNum;
    
    public static bool canPlaySound = true;
    protected bool dequeueing = false;
    public DequeueCallback callback;
    private GameObject reconnectModal;
    public bool ExecuteMessage = true;      //연결이 끊어진 이후에 다시 받는 메시지인지

    protected bool isDisconnected = false;
    protected bool isReceivingResendMessage = false;
    public bool isForcedReconnectedFromMainScene = false;
    
    public delegate void DequeueAfterAction();
    private DateTime prevTime = default;
    private void ReceiveStart(WebSocket webSocket, string message) {
        Debug.Log(message);
        JObject jMessage = JObject.Parse(message);
        this.webSocket.OnMessage -= ReceiveStart;
        if(jMessage.Property("connected") == null) return;
        SocketConnected();
    }
    private void OnApplicationPause(bool pauseStatus) {
        if(pauseStatus) prevTime = DateTime.Now;
    }

    private void OnApplicationFocus(bool focus) {
        if(!focus) prevTime = DateTime.Now;
    }

    public UnityAction OnBattleFinished;
    private void ReceiveMessage(WebSocket webSocket, string message) {
        try {
            ReceiveFormat result = dataModules.JsonReader.Read<ReceiveFormat>(message);
            Debug.Log("<color=green>소켓으로 받은 메시지!</color> : " + message);
            if (result.method == "begin_end_game") {
                gameResult = result;
                battleGameFinish = true;
                
                OnBattleFinished?.Invoke();
            }
            if (result.method == "current_state") {
                StartCoroutine(RecoverGameEnv(message));
            }
            
            if (isDisconnected && !string.IsNullOrEmpty(result.method)) HandleDisconnected(result);
            else queue.Enqueue(result);
        }
        catch(Exception e) {
            Debug.Log("소켓! : " + message);
            Debug.Log(e);
        }
    }

    /// <summary>
    /// 메인화면에서 재접속한 경우 핸드 및 유닛 복원
    /// </summary>
    /// <returns></returns>
    IEnumerator RecoverGameEnv(string message) {
        ReceiveFormat __result = dataModules.JsonReader.Read<ReceiveFormat>(message);
        GameState prevState = dataModules.JsonReader.Read<GameState>(__result.args.ToString());
        yield return new WaitUntil(() => PlayMangement.instance != null && PlayMangement.instance.player != null);
        
        PlayMangement playMangement = PlayMangement.instance;
        playMangement.player.Init();
        gameState = prevState;
        
        playMangement.player.playerUI.transform.parent.Find("FirstDrawWindow").gameObject.SetActive(false);
        yield return RecoverHands(prevState);
        // yield return RecoverUnits(prevState);
        
        playMangement.SyncPlayerHp();
        playMangement.enemyPlayer.UpdateCardCount();
        
        ReConnectReady();
    }

    IEnumerator RecoverHands(GameState gameState) {
        var player = PlayMangement.instance.player;
        bool isPlayerHuman = player.isHuman;
        bool isDrawFinished = false;
        Card[] handCards = isPlayerHuman
            ? gameState.players.human.deck.handCards
            : gameState.players.orc.deck.handCards;

        yield return PlayMangement.instance.player.cdpm.AddMultipleCard(handCards);
    }

    IEnumerator RecoverUnits(GameState gameState) {
        PlayMangement.instance.RefreshUnit();
        yield return new WaitForEndOfFrame();
    }
    
    Queue<ReceiveFormat> tmpQueue;
    private void HandleDisconnected(ReceiveFormat result) {
        if (result.method == "resend_begin") {
            tmpQueue = new Queue<ReceiveFormat>();
            isReceivingResendMessage = true;
            dequeueing = true;
        }

        if (result.method == "resend_end") {
            isReceivingResendMessage = false;
            if (queue != null && queue.Count > 0) {
                var lastQueue = queue.Last();
                if (lastQueue != null) gameState = lastQueue.gameState;
            }
            
            isDisconnected = false;

            //TODO : 최적화 필요함...
            ReceiveFormat resend_end = result;
            var exclusiveQueue = tmpQueue.Where(x => x.method != "resend_begin" && x.method != "resend_end");
            queue.Clear();
            queue.Enqueue(resend_end);
            foreach (var data in exclusiveQueue) {
                queue.Enqueue(data);
            }
            dequeueing = false;
            
            if (reconnectModal != null) Destroy(reconnectModal);
            isOpponentPlayerDisconnected = false;
            isDisconnected = false;
        }
        else {
            if(!isReceivingResendMessage) ExecuteSocketMessage(result);
            else {
                tmpQueue.Enqueue(result);
            }
        }
    }

    private async void DelayDequeueSocket(DequeueCallback callback, float time = 0f, DequeueAfterAction action = null) {        
        await Task.Delay(TimeSpan.FromSeconds(time));
        action?.Invoke();
        callback();
    }

    class RecieveFormatComparer : IEqualityComparer<ReceiveFormat> {
        public bool Equals(ReceiveFormat x, ReceiveFormat y) {
            return x.id == y.id && x.method == y.method;
        }
        public int GetHashCode(ReceiveFormat receiveFormat) {
            int id = receiveFormat.id.HasValue ? receiveFormat.id.Value : -1;
            int name = receiveFormat.method.GetHashCode();
            return id ^ name;
        }
    }

    private void showMessage(ReceiveFormat result) {
        JObject json = null;
        if(result.gameState != null) {
            json = JObject.Parse(JsonConvert.SerializeObject(result.gameState.map));
            json["lines"].Parent.Remove();
        }
        Logger.Log(string.Format("<color=blue>Deaueue 되어 실행되는 메소드</color> : {0}, args : {1}, map : {2}", result.method, result.args, 
        result.gameState != null ? JsonConvert.SerializeObject(json, Formatting.Indented)  : null));
    }
    #if UNITY_EDITOR
    private void FixedUpdate() {
        if(Input.GetKeyDown(KeyCode.D)) webSocket.Close(500, "shutdown");
    }
    #endif

    private void Update() {
        //Test code
        if (Input.GetKeyDown(KeyCode.D)) {
            webSocket.Close();
            Logger.LogWarning("강제로 소켓 연결을 끊습니다.");
        }

        if (ExecuteMessage == true) {
            DequeueSocket();
        }
    }

    private void Start() {
        callback = () => dequeueing = false;
    }

    public void ForceDequeing(bool isBlock) {
        if (isBlock) { dequeueing = true; }
        else dequeueing = false;
    }

    private int? lastQueueId = 0;
    private void DequeueSocket() {
        if(dequeueing || queue.Count == 0) return;
        dequeueing = true;
        Debug.Log(queue.Peek().method);
        ReceiveFormat result = queue.Dequeue();
        
        if(result.id != null) {
            if(lastQueueId.Value > result.id.Value) {
                dequeueing = false;
                return;
            }
            lastQueueId = result.id;    //모든 메시지가 ID를 갖고 있지는 않음
        }
        if(result.gameState != null) gameState = result.gameState;
        if(result.error != null) {
            Logger.LogError("WebSocket play wrong Error : " + result.error);
            dequeueing = false;
        }

        ExecuteSocketMessage(result);
        CheckSendMessage();
    }

    private void ExecuteSocketMessage(ReceiveFormat result) {
        if(result.method == null) {dequeueing = false; return;}
        MethodInfo theMethod = thisType.GetMethod(result.method);
        if(theMethod == null) { Debug.LogError(result.method + "에 대한 함수가 없습니다!"); dequeueing = false; return;}
        object[] args = new object[]{result.args, result.id, callback};
        showMessage(result);
        try {
            theMethod.Invoke(this, args);
        }
        catch(Exception e) {
            Debug.LogError("Message Method : " + result.method + "Error : " + e);
            callback();
        }
    }
    
    public void ClearForResult() {
        queue.Clear();
        queue.Enqueue(gameResult);
        DequeueSocket();
    }

    public void FreePassSocket(string untilMessage, DequeueCallback callback = null) {
        ReceiveFormat result;
        ExecuteMessage = false;
        do {
            if(queue.Count != 0)
                result = queue.Dequeue();
            else { 
                Debug.Log("queue is Empty!");
                break;
            }
        } while(result.method.CompareTo(untilMessage)!=0);
        dequeueing = false;
        ExecuteMessage = true;
    }

    AccountManager.LeagueInfo orcLeagueInfo, humanLeagueInfo;
    public void begin_ready(object args, int? id, DequeueCallback callback) {
        StartCoroutine(__begin_ready(callback, args));
    }

    IEnumerator __begin_ready(DequeueCallback callback, object args) {
        string battleType = PlayerPrefs.GetString("SelectedBattleType");
        if (battleType == "league" || battleType == "leagueTest") {
            JObject json = (JObject)args;
            orcLeagueInfo = dataModules.JsonReader.Read<AccountManager.LeagueInfo>(json["orc"].ToString());
            humanLeagueInfo = dataModules.JsonReader.Read<AccountManager.LeagueInfo>(json["human"].ToString());

            CustomVibrate.Vibrate(1000);
        }

        string findMessage = AccountManager.Instance.GetComponent<Fbl_Translator>().GetLocalizedText("MainUI", "ui_page_league_foundopponent");

        message.text = findMessage;
        textBlur.SetActive(true);
        FindObjectOfType<BattleConnectSceneAnimController>().PlayStartBattleAnim();

        StopCoroutine(timeCheck);
        if (canPlaySound) {
            SoundManager.Instance.PlayIngameSfx(IngameSfxSound.GAMEMATCH);
        }
        
        SetUserInfoText();
        SetSaveGameId();
        
        callback();
        yield return 0;
    }

    public void SetSaveGameId() {
        string gameId = gameState.gameId;
        string camp = PlayerPrefs.GetString("SelectedRace").ToLower();
        string battleType = PlayerPrefs.GetString("SelectedBattleType");
        NetworkManager.ReconnectData data = new NetworkManager.ReconnectData(gameId, camp, battleType);
        PlayerPrefs.SetString("ReconnectData", JsonConvert.SerializeObject(data));
    }

    /// <summary>
    /// 매칭 화면에서 유저 정보 표기
    /// </summary>
    private void SetUserInfoText() {
        string race = PlayerPrefs.GetString("SelectedRace").ToLower();
        string mode = PlayerPrefs.GetString("SelectedBattleType");

        var orcPlayerData = gameState.players.orc;
        var orcUserData = orcPlayerData.user;
        var humanPlayerData = gameState.players.human;
        var humanUserData = humanPlayerData.user;

        string orcPlayerNickName = orcUserData.nickName;
        if (string.IsNullOrEmpty(orcPlayerNickName)) orcPlayerNickName = "Bot";

        string orcHeroName = orcPlayerData.hero.name;
        string humanPlayerNickName = humanUserData.nickName;
        if (string.IsNullOrEmpty(humanPlayerNickName)) humanPlayerNickName = "Bot";
        string humanHeroName = humanPlayerData.hero.name;

        //Logger.Log(orcPlayerNickName);
        //Logger.Log(humanPlayerNickName);

        TextMeshProUGUI enemyNickNameTxt = machine.transform.Find("EnemyName/PlayerName").GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI enemyHeroNameTxt = machine.transform.Find("EnemyHero/HeroName").GetComponent<TextMeshProUGUI>();

        TextMeshProUGUI playerNickNameTxt = machine.transform.Find("PlayerName/PlayerName").GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI playerHeroNameTxt = machine.transform.Find("PlayerHero/HeroName").GetComponent<TextMeshProUGUI>();

        //Logger.Log(race);
        Transform enemyName = machine.transform.Find("EnemyName");
        Transform playerName = machine.transform.Find("PlayerName");

        Transform playerHero = machine.transform.Find("PlayerHero");
        Transform enemyHero = machine.transform.Find("EnemyHero");

        var PlayerTierParent = playerHero.Find("Tier");
        var EnemyTierParent = enemyHero.Find("Tier");
        int humanTier = gameState.players.human.hero.tier;
        int orcTier = gameState.players.orc.hero.tier;
        

        if (race == "human") {
            playerHeroNameTxt.text = "<color=#BED6FF>" + humanHeroName + "</color>";
            playerNickNameTxt.text = humanPlayerNickName;
            
            enemyHeroNameTxt.text = (mode == "story") ? AccountManager.Instance.resource.ScenarioUnitResource[PlayMangement.chapterData.enemyHeroId].name : orcHeroName;
            enemyNickNameTxt.text = (mode == "story") ? AccountManager.Instance.resource.ScenarioUnitResource[PlayMangement.chapterData.enemyHeroId].name : orcPlayerNickName;

            for (int i = 0; i < humanTier; i++) {
                PlayerTierParent.GetChild(i).Find("Active").gameObject.SetActive(true);
                PlayerTierParent.GetChild(i).Find("Deactive").gameObject.SetActive(false);
            }
            for (int i = 0; i < orcTier; i++) {
                EnemyTierParent.GetChild(i).Find("Active").gameObject.SetActive(true);
                EnemyTierParent.GetChild(i).Find("Deactive").gameObject.SetActive(false);
            }
            if (mode == "story")
                machine.transform.Find("EnemyCharacter/EnemyKracus").gameObject.GetComponent<Image>().sprite = AccountManager.Instance.resource.heroPortraite[PlayMangement.chapterData.enemyHeroId];
            else
                machine.transform.Find("EnemyCharacter/EnemyKracus").gameObject.GetComponent<Image>().sprite = AccountManager.Instance.resource.heroPortraite[gameState.players.orc.hero.id];
        }
        else if (race == "orc") {
            playerHeroNameTxt.text = "<color=#FFCACA>" + orcHeroName + "</color>";
            playerNickNameTxt.text = orcPlayerNickName;
            
            enemyHeroNameTxt.text = (mode == "story") ? AccountManager.Instance.resource.ScenarioUnitResource[PlayMangement.chapterData.enemyHeroId].name : humanHeroName;
            enemyNickNameTxt.text = (mode == "story") ? AccountManager.Instance.resource.ScenarioUnitResource[PlayMangement.chapterData.enemyHeroId].name : humanPlayerNickName;
            
            
            for (int i = 0; i < orcTier; i++) {
                PlayerTierParent.GetChild(i).Find("Active").gameObject.SetActive(true);
                PlayerTierParent.GetChild(i).Find("Deactive").gameObject.SetActive(false);
            }
            for (int i = 0; i < humanTier; i++) {
                EnemyTierParent.GetChild(i).Find("Active").gameObject.SetActive(true);
                EnemyTierParent.GetChild(i).Find("Deactive").gameObject.SetActive(false);
            }

            if (mode == "story") {
                machine.transform.Find("EnemyCharacter/EnemyZerod").gameObject.GetComponent<Image>().sprite = AccountManager.Instance.resource.heroPortraite[PlayMangement.chapterData.enemyHeroId];
            }
            else
                machine.transform.Find("EnemyCharacter/EnemyZerod").gameObject.GetComponent<Image>().sprite = AccountManager.Instance.resource.heroPortraite[gameState.players.human.hero.id];

        }
        timer.text = null;
        returnButton.onClick.RemoveListener(BattleCancel);
        returnButton.gameObject.SetActive(false);

        if (mode == "league" || mode == "leagueTest") {
            if(race == "human") {
                //ai는 rankdetail 정보가 없음
                //임시로 나와 동일한 rank로 표기
                if (orcLeagueInfo.rankDetail == null) {
                    orcLeagueInfo.rankDetail = humanLeagueInfo.rankDetail;
                }

                playerName.Find("MMR/Value").GetComponent<TextMeshProUGUI>().text = humanLeagueInfo.ratingPoint.ToString();
                enemyName.Find("MMR/Value").GetComponent<TextMeshProUGUI>().text = orcLeagueInfo.ratingPoint.ToString();

                var icons = AccountManager.Instance.resource.rankIcons;
                if (icons.ContainsKey(humanLeagueInfo.rankDetail.id.ToString())) {
                    playerName.Find("TierIcon").GetComponent<Image>().sprite = icons[humanLeagueInfo.rankDetail.id.ToString()];
                }
                else {
                    playerName.Find("TierIcon").GetComponent<Image>().sprite = icons["default"];
                }
                if (icons.ContainsKey(orcLeagueInfo.rankDetail.id.ToString())) {
                    enemyName.Find("TierIcon").GetComponent<Image>().sprite = icons[orcLeagueInfo.rankDetail.id.ToString()];
                }
                else {
                    enemyName.Find("TierIcon").GetComponent<Image>().sprite = icons["default"];
                }
            }
            else {
                //ai는 rankdetail 정보가 없음
                //임시로 나와 동일한 rank로 표기
                if (humanLeagueInfo.rankDetail == null) {
                    humanLeagueInfo.rankDetail = orcLeagueInfo.rankDetail;
                }

                playerName.Find("MMR/Value").GetComponent<TextMeshProUGUI>().text = orcLeagueInfo.ratingPoint.ToString();
                enemyName.Find("MMR/Value").GetComponent<TextMeshProUGUI>().text = humanLeagueInfo.ratingPoint.ToString();

                var icons = AccountManager.Instance.resource.rankIcons;
                if (icons.ContainsKey(orcLeagueInfo.rankDetail.id.ToString())) {
                    playerName.Find("TierIcon").GetComponent<Image>().sprite = icons[orcLeagueInfo.rankDetail.id.ToString()];
                }
                else {
                    playerName.Find("TierIcon").GetComponent<Image>().sprite = icons["default"];
                }
                if (icons.ContainsKey(humanLeagueInfo.rankDetail.id.ToString())) {
                    enemyName.Find("TierIcon").GetComponent<Image>().sprite = icons[humanLeagueInfo.rankDetail.id.ToString()];
                }
                else {
                    enemyName.Find("TierIcon").GetComponent<Image>().sprite = icons["default"];
                }
            }
        }
        else {
            playerName.Find("MMR").gameObject.SetActive(false);
            enemyName.Find("MMR").gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 로비 연결 끊어짐 (연결 성공 혹은 유저가 로비를 나갔을 때
    /// </summary>
    /// <param name="args"></param>
    /// <param name="id"></param>
    public void disconnected(object args, int? id, DequeueCallback callback) {
        callback();
    }

    public void entrance_complete(object args, int? id, DequeueCallback callback) {
        callback();
    }

    public void matched(object args, int? id, DequeueCallback callback) {
        var json = (JObject)args;
        matchKey = json["matchKey"].ToString();
        
        int _serverNum = -1;
        int.TryParse(json["serverNum"].ToString(), out _serverNum);
        if (_serverNum != -1) serverNum = _serverNum;
        
        JoinGame();
        callback();
    }

    public void join_complete(object args, int? id, DequeueCallback callback) {
        StopCoroutine(TimerOn());
        callback();
    }

    public void end_ready(object args, int? id, DequeueCallback callback) {
        bool isTest = PlayerPrefs.GetString("SelectedBattleType").CompareTo("test") == 0;
        if (isTest) {
            object value = JsonUtility.FromJson(PlayerPrefs.GetString("Editor_startState"), typeof(StartState));
            SendStartState(value);
        }
        callback();
    }

    public void begin_mulligan(object args, int? id, DequeueCallback callback) {
        TurnStart();
        callback();
    }

    public void mulligan_start(object args, int? id, DequeueCallback callback) {
        if(ScenarioGameManagment.scenarioInstance == null) {
            PlayMangement.instance.player.GetComponent<IngameTimer>().BeginTimer(30);
            StartCoroutine(PlayMangement.instance.GenerateCard(callback));
        }
        else {
            //chapter 0만 turn over 를 바로 보내고, chapter 1 이상은 muligun 단계를 거쳐야함
            if (PlayMangement.chapterData.chapter == 0) {
                callback();
                TurnOver();
            }
            else {
                // PlayMangement.instance.player.GetComponent<IngameTimer>().BeginTimer(30);
                StartCoroutine(PlayMangement.instance.GenerateCard(callback));
            }
        }
    }

    public void hand_changed(object args, int? id, DequeueCallback callback) {
        if(PlayMangement.instance == null) return;
        bool isHuman = PlayMangement.instance.player.isHuman;
        Card newCard = gameState.players.myPlayer(isHuman).newCard;
        HandchangeCallback(newCard.id, newCard.itemId, false);
        HandchangeCallback = null;
        callback();
    }

    public void end_mulligan(object args, int? id, DequeueCallback callback) {
        CardHandManager cardHandManager = PlayMangement.instance.cardHandManager;
        if(!cardHandManager.socketDone)
            cardHandManager.FirstDrawCardChange();        
        object[] param = new object[]{null, callback};
        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.END_TURN_BTN_CLICKED, this, param);
        PlayMangement.instance.SyncPlayerHp();
        if (ScenarioGameManagment.scenarioInstance == null) {
            PlayMangement.instance.player.GetComponent<IngameTimer>().EndTimer();
        }
    }

    public void begin_turn_start(object args, int? id, DequeueCallback callback) {
        PlayMangement.instance.SyncPlayerHp();
        callback();
    }
    
    public void end_turn_start(object args, int? id, DequeueCallback callback) {
        DebugSocketData.StartCheckMonster(gameState);
        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.END_BATTLE_TURN, this);
        callback();
    }

    public void begin_orc_pre_turn(object args, int? id, DequeueCallback callback) {
        if(!PlayMangement.instance.player.isHuman) TurnStart();
        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.BEGIN_ORC_PRE_TURN, this, null);
        callback();
    }

    public void orc_pre_turn_start(object args, int? id, DequeueCallback callback) {
        PlayerController player;
        player = PlayMangement.instance.player.isHuman ? PlayMangement.instance.enemyPlayer : PlayMangement.instance.player;
        if (ScenarioGameManagment.scenarioInstance == null && !stopTimer) {
            player.GetComponent<IngameTimer>().RopeTimerOn();
        }
        callback();
    }

    public void end_orc_pre_turn(object args, int? id, DequeueCallback callback) {
        PlayerController player;
        player = PlayMangement.instance.player.isHuman ? PlayMangement.instance.enemyPlayer : PlayMangement.instance.player;
        if(ScenarioGameManagment.scenarioInstance == null && !stopTimer) {
            player.GetComponent<IngameTimer>().RopeTimerOff();
        }
        object[] param = new object[]{TurnType.ORC, callback};
        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.END_TURN_BTN_CLICKED, this, param);
    }

    public void begin_human_turn(object args, int? id, DequeueCallback callback) {
        if(PlayMangement.instance.player.isHuman) TurnStart();
        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.BEGIN_HUMAN_TURN, this, null);
        callback();
    }

    public void human_turn_start(object args, int? id, DequeueCallback callback) {
        PlayerController player;
        player = PlayMangement.instance.player.isHuman ? PlayMangement.instance.player : PlayMangement.instance.enemyPlayer;
        if(ScenarioGameManagment.scenarioInstance == null && !stopTimer) {
            player.GetComponent<IngameTimer>().RopeTimerOn(70);
        }
        callback();
    }

    public void end_human_turn(object args, int? id, DequeueCallback callback) {
        PlayerController player;
        player = PlayMangement.instance.player.isHuman ? PlayMangement.instance.player : PlayMangement.instance.enemyPlayer;
        if(ScenarioGameManagment.scenarioInstance == null && !stopTimer) {
            player.GetComponent<IngameTimer>().RopeTimerOff();
        }
        object[] param = new object[]{TurnType.HUMAN, callback};
        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.END_TURN_BTN_CLICKED, this, param);
    }

    public void begin_orc_post_turn(object args, int? id, DequeueCallback callback) {
        if(!PlayMangement.instance.player.isHuman) TurnStart();
        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.BEGIN_ORC_POST_TURN, this, null);
        callback();
    }

    public void orc_post_turn_start(object args, int? id, DequeueCallback callback) {
        PlayerController player;
        player = PlayMangement.instance.player.isHuman ? PlayMangement.instance.enemyPlayer : PlayMangement.instance.player;
        if(ScenarioGameManagment.scenarioInstance == null && !stopTimer) {
            player.GetComponent<IngameTimer>().RopeTimerOn();
        }
        callback();
    }

    public void end_orc_post_turn(object args, int? id, DequeueCallback callback) {
        PlayerController player;
        player = PlayMangement.instance.player.isHuman ? PlayMangement.instance.enemyPlayer : PlayMangement.instance.player;
        if(ScenarioGameManagment.scenarioInstance == null && !stopTimer) {
            player.GetComponent<IngameTimer>().RopeTimerOff();
        }
        object[] param = new object[]{TurnType.SECRET, callback};
        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.END_TURN_BTN_CLICKED, this, param);
    }

    public Queue<int> unitSkillList = new Queue<int>();//일단 임시

    public void skill_activated(object args, int? id, DequeueCallback callback) {
        if(PlayMangement.instance.enemyPlayer.isHuman) {callback(); return;}
        var json = (JObject)args;
        int itemId = int.Parse(json["targets"][0]["args"][0].ToString());
        unitSkillList.Enqueue(itemId);
        callback();
    }

    public void begin_battle_turn(object args, int? id, DequeueCallback callback) {
        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.BEGIN_BATTLE_TURN, this, null);
        callback();        
    }

    public void end_battle_turn(object args, int? id, DequeueCallback callback) {
        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.END_BATTLE_TURN, this, null);
        PlayMangement.instance.CheckAtEndBattle();
        DebugSocketData.CheckBattleSynchronization(gameState);
        callback();
    }

    public void attack(object args, int? id, DequeueCallback callback) {
        JObject json = (JObject)args;
        AttackArgs message = dataModules.JsonReader.Read<AttackArgs>(args.ToString());
        PlayMangement.instance.StartBattle(message.attacker, message.affected , callback);
    }

    public void line_battle_start(object args, int? id, DequeueCallback callback) {
        JObject json = (JObject)args;
        int line = int.Parse(json["lineNumber"].ToString());
        PlayMangement.instance.SetBattleLineColor(true, line);
        callback();
    }

    public void line_battle_end(object args, int? id, DequeueCallback callback) {
        JObject json = (JObject)args;
        int line = int.Parse(json["lineNumber"].ToString());        
        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.LINE_BATTLE_FINISHED, this, line);

        if (line >= 4) TurnOver();

        DelayDequeueSocket(callback, 0.2f, delegate() { PlayMangement.instance.SetBattleLineColor(false, line); });        
    }

    public void line_battle(object args, int? id, DequeueCallback callback) {
        var json = (JObject)args;
        string line = json["lineNumber"].ToString();
        string camp = json["camp"].ToString();
        int line_num = int.Parse(line);
        
    }

    public void map_clear(object args, int? id, DequeueCallback callback) {
        if(args == null) {callback(); return;} //TODO : 유닛 소환이나 마법 카드로 피해 받을 떄에도 해당 메시지가 호출 되는데 line이 없어서 일시 스킵
        var json = (JObject)args;
        string[] itemIds = dataModules.JsonReader.Read<string[]>(json["cleared"].ToString());

        SkillResult[] skillResult = dataModules.JsonReader.Read<SkillResult[]>(json["skillResult"].ToString());

        if(skillResult != null && skillResult.Length > 0) {
            FieldUnitsObserver unitsObserver = PlayMangement.instance.UnitsObserver;
            foreach(SkillResult result in skillResult) {
                string from = result.from;
                for(int i = 0; i<result.to.Length; i++) {
                    GameObject target = unitsObserver.GetUnitToItemID(result.to[i]);
                    if (target.GetComponent<PlaceMonster>() != null) target.GetComponent<PlaceMonster>().UpdateGranted();
                }
            }
        }



        if (itemIds != null && itemIds.Length > 0) {
            var unitObserver = PlayMangement.instance.UnitsObserver;
            foreach (var itemID in itemIds) {
                GameObject targetUnitObject = unitObserver.GetUnitToItemID(itemID);
                PlaceMonster targetUnit = targetUnitObject.GetComponent<PlaceMonster>();
            
                targetUnit.UnitDead();
            }
        }
        shieldStack.ResetShield();

        callback();
    }

    IngameTimer ingameTimer;

    public void begin_shield_turn(object args, int? id, DequeueCallback callback) {
        DebugSocketData.CheckBattleSynchronization(gameState);
        if(PlayMangement.instance.player.HP.Value == 0 || PlayMangement.instance.enemyPlayer.HP.Value == 0) {
            callback();
            return;
        }

        TurnStart();
        var json = (JObject)args;
        string camp = json["camp"].ToString();
        bool isHuman = camp == "human" ? true : false;
        bool isPlayer;
        if (PlayMangement.instance.player.isHuman == isHuman)
            isPlayer = true;
        else
            isPlayer = false;


        if (isPlayer == true) {
            PlayMangement.instance.player.ActiveShield();
            PlayMangement.instance.heroShieldActive = true;
        }
        else {
            PlayMangement.instance.enemyPlayer.ActiveShield();
            IngameNotice.instance.SelectNotice();
        }


        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.HERO_SHIELD_ACTIVE, this, isPlayer);
        //human 실드 발동
        if (camp == "human") {
            if (!isHuman) {
                PlayMangement.instance.enemyPlayer.GetComponent<IngameTimer>()?.PauseTimer(25);
            }           
        }
        //orc 실드 발동
        else {
            if (isHuman) {
                PlayMangement.instance.enemyPlayer.GetComponent<IngameTimer>()?.PauseTimer(25);
            }
        }

        SoundManager.Instance.PlayIngameSfx(IngameSfxSound.SHIELDACTION);
        PlayMangement.instance.SocketAfterMessage(callback);
        StartCoroutine(PlayMangement.instance.DrawSpecialCard(isHuman));        
    }

    public void end_shield_turn(object args, int? id, DequeueCallback callback) { 
        PlayMangement.instance.heroShieldDone.Add(true);
        if(ingameTimer != null) {
            ingameTimer.ResumeTimer();
            ingameTimer = null;
        }
        var json = (JObject)args;
        string camp = json["camp"].ToString();
        bool isHuman = camp == "human" ? true : false;
        bool isPlayer = PlayMangement.instance.GetPlayerWithRace(isHuman);

        SocketFormat.Player socketPlayer = PlayMangement.instance.socketHandler.gameState.players.myPlayer(isHuman);


        if (isPlayer == true) {
            PlayMangement.instance.player.remainShieldCount = socketPlayer.hero.shieldCount;
            PlayMangement.instance.player.shieldStack.Value = 0;

        }
        else {
            PlayMangement.instance.enemyPlayer.remainShieldCount = socketPlayer.hero.shieldCount;
            PlayMangement.instance.enemyPlayer.shieldStack.Value = 0;
        }

        IngameNotice.instance.CloseNotice();
        callback();
    }

    bool isSurrender = false;

    public void surrender(object args, int? id, DequeueCallback callback) {
        var json = (JObject)args;
        string camp = json["camp"].ToString();
        //Logger.Log(camp + "측 항복");
        isSurrender = true;
        string result = "";
        bool isHuman = PlayMangement.instance.player.isHuman;

        if ((isHuman && camp == "human") || (!isHuman && camp == "orc")) {
            result = "lose";
        }
        else {
            result = "win";
        }
        callback();
    }

    IEnumerator SetResult(string result, bool isHuman) {
        yield return new WaitForSeconds(0.5f);
        PlayMangement.instance.resultManager.SetResultWindow(result, isHuman, PlayMangement.instance.socketHandler.result);
    }

    public void shield_gauge(object args, int? id, DequeueCallback callback) {
        var json = (JObject)args;
        string camp = json["camp"].ToString();
        string gauge = json["shieldGet"].ToString();
        ShieldCharge charge = new ShieldCharge();
        charge.shieldCount = int.Parse(gauge);
        charge.camp = camp;
        shieldStack.SavingShieldGauge(camp, int.Parse(gauge));
        callback();
    }

    public void begin_end_turn(object args, int? id, DequeueCallback callback) {
        JObject json = (JObject)args;
        bool isHuman = PlayMangement.instance.player.isHuman;


        //gameState.players.myPlayer(isHuman).newCard
        //json["draw"].Type != JTokenType.Null
        //json.TryGetValue("draw", out draw)
        if (json != null) {
            string itemID = json["draw"]["itemId"].ToString();
            SocketFormat.Card cardData = Array.Find(gameState.players.myPlayer(isHuman).deck.handCards, x=>x.itemId == itemID);
            PlayMangement.instance.EndTurnDraw(cardData);
        }

        callback();
    }
    



    public void end_end_turn(object args, int? id, DequeueCallback callback) {
        object[] param = new object[]{TurnType.BATTLE, callback};
        PlayMangement.instance.DistributeResource();
        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.END_TURN_BTN_CLICKED, this, param);
    }

    public LeagueData leagueData;
    public void begin_end_game(object args, int? id, DequeueCallback callback) {
        webSocket.Close();

        PlayMangement playMangement = PlayMangement.instance;
        playMangement.isGame = false;
        playMangement.openResult = true;
        GameResultManager resultManager = playMangement.resultManager;

        if (ScenarioGameManagment.scenarioInstance == null) {
            PlayMangement.instance.player.GetComponent<IngameTimer>().EndTimer();
            PlayMangement.instance.enemyPlayer.GetComponent<IngameTimer>().EndTimer();
        }
        JObject jobject = (JObject)args;
        result = JsonConvert.DeserializeObject<ResultFormat>(jobject.ToString());
        
        leagueData.prevLeagueInfo = leagueData
            .leagueInfo
            .DeepCopy(leagueData.leagueInfo);
        leagueData.leagueInfo = result.leagueInfo;
        
        AccountManager.Instance.RequestUserInfo();

        //상대방이 재접속에 최종 실패하여 게임이 종료된 경우
        if (isOpponentPlayerDisconnected) {
            if(reconnectModal != null) Destroy(reconnectModal);
            
            string _result = result.result;
            resultManager.gameObject.SetActive(true);
            StartCoroutine(resultManager.WaitResult(_result, playMangement.player.isHuman, result));
            callback();
            return;
        }

        resultManager.gameObject.SetActive(true);
        StartCoroutine(resultManager.WaitResult(result.result, playMangement.player.isHuman, result, isSurrender == true ? true : false));

        if(reconnectModal != null) Destroy(reconnectModal);
        
        callback();
    }

    public void end_end_game(object args, int? id, DequeueCallback callback) { }

    public void ping(object args, int? id, DequeueCallback callback) {
        SendMethod("pong");
        callback();
    }

    public void card_played(object args, int? id, DequeueCallback callback) {
        string enemyCamp = PlayMangement.instance.enemyPlayer.isHuman ? "human" : "orc";
        string cardCamp = gameState.lastUse.cardItem.camp;
        string cardType = gameState.lastUse.cardItem.type;
        bool isEnemyCard = cardCamp.CompareTo(enemyCamp) == 0;

        var json = (JObject)args;
        string itemID = json["itemId"].ToString();
        Debug.Log(itemID);

        if (isEnemyCard) {
            StartCoroutine(PlayMangement.instance.EnemyUseCard(gameState.lastUse, callback, args));
            IngameNotice.instance.CloseNotice();
        }
        else {
            if (cardType == "unit") {
                GameObject setMonster = PlayMangement.instance.UnitsObserver.GetUnitToItemID(gameState.lastUse.cardItem.itemId);
                if (setMonster != null) setMonster.GetComponent<PlaceMonster>().UpdateGranted();
                else Debug.LogError("해당 유닛이 없는데요");

                //MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
                //if(magicArgs.targets.Length > 1) PlayMangement.instance.cardActivate.Activate(gameState.lastUse.cardItem.cardId, args, callback);

                callback();
            }
            else {
                GameObject card = PlayMangement.instance.cardHandManager.FindCardWithItemId(itemID);
                card.GetComponent<MagicDragHandler>().StartCardUse(args, callback);
            }
        }
    }

    public void skill_effected(object args, int? id, DequeueCallback callback) {
        FieldUnitsObserver observer = PlayMangement.instance.UnitsObserver;
        JObject method = (JObject)args;
        var toList = method["to"].ToList<JToken>();
        string from = method["from"].ToString();
        string cardID;
        switch (method["trigger"].ToString()) {
            case "unit_skill":
                StartCoroutine(ShowSelectMove(toList, callback));
                break;
            case "sortie":
                UnitMove(toList, callback);
                break;
            case "before_card_play":
            case "after_card_play":
            case "toarms": //출격
            case "map_changed":
                cardID = gameState.map.allMonster.Find(x => x.itemId == from).origin.id;
                PlayMangement.instance.unitActivate.Activate_ToArms(cardID, args, callback);
                break;
            case "start_battle_turn":
            case "unambush":
                cardID = gameState.map.allMonster.Find(x => x.itemId == from).origin.id;
                PlayMangement.instance.unitActivate.Activate(cardID, args, callback);
                //for (int i = 0; i < toList.Count; i++) {
                //    string itemId = toList[i].ToString();
                //    PlaceMonster monster = observer.GetUnitToItemID(itemId).GetComponent<PlaceMonster>();
                //    if(monster.unit.cardId.CompareTo("ac10020") != 0) break;
                //    if(monster.isPlayer)
                //        monster.gameObject.AddComponent<CardUseSendSocket>().Init(false);
                //    else
                //        monster.gameObject.AddComponent<CardSelect>().EnemyNeedSelect();                    
                //}

                //callback();
                break;
            case "start_turn":
                string start_turn_cardID = gameState.map.allMonster.Find(x => x.itemId == from).origin.id;
                PlayMangement.instance.unitActivate.Activate(start_turn_cardID, args, callback);
                break;
            case "end_turn":
                string end_turn_cardID = gameState.map.allMonster.Find(x => x.itemId == from).origin.id;
                PlayMangement.instance.unitActivate.Activate(end_turn_cardID, args, callback);
                break;
            default :
                Debug.Log(method["trigger"]);
                callback();
                break;
        }
    }

    private IEnumerator ShowSelectMove(List<JToken> toList, DequeueCallback callback) {
        for(int i = 0; i< toList.Count; i++) {
            FieldUnitsObserver observer = PlayMangement.instance.UnitsObserver;
            string itemId = toList[i].ToString();
            GameObject toMonster = observer.GetUnitToItemID(itemId);
            Unit unit = gameState.map.allMonster.Find(x => string.Compare(x.itemId, itemId, StringComparison.Ordinal) == 0);
            CardSelect cardSelect = toMonster.GetComponent<CardSelect>();
            if(cardSelect != null)
                yield return cardSelect.enemyUnitSelect(unit.pos.col);
            observer.UnitChangePosition(toMonster, unit.pos, toMonster.GetComponent<PlaceMonster>().isPlayer, string.Empty, () => callback());
        }
    }

    private void UnitMove(List<JToken> toList, DequeueCallback callback) {
        for(int i = 0; i< toList.Count; i++) {
            FieldUnitsObserver observer = PlayMangement.instance.UnitsObserver;
            string itemId = toList[i].ToString();
            GameObject toMonster = observer.GetUnitToItemID(itemId);
            Unit unit = gameState.map.allMonster.Find(x => string.Compare(x.itemId, itemId, StringComparison.Ordinal) == 0);
            observer.UnitChangePosition(toMonster, unit.pos, toMonster.GetComponent<PlaceMonster>().isPlayer, string.Empty, () => callback());
        }
    }

    public void hero_card_kept(object args, int? id, DequeueCallback callback) {
        PlayMangement.instance.enemyPlayer.UpdateCardCount();
        callback();
    }

    //public void reconnect_game() { }

    public void begin_reconnect_ready(object args, int? id, DequeueCallback callback) {
        if (isOpponentPlayerDisconnected) {
            ReConnectReady();
            
            if(opponentWaitModal != null) Destroy(opponentWaitModal);
            string _message = PlayMangement.instance.uiLocalizeData["ui_ingame_popup_waitopponent"];
            opponentWaitModal = Modal.instantiateOpponentWaitingFinalModal(_message);
        }
        else {
            if (isForcedReconnectedFromMainScene) {
                SendMethod("current_state");
            }
            else {
                ResendMessage();
            }
        }
        callback();
    }

    public void current_state(object args, int? id, DequeueCallback callback) {
        __current_state(callback);
    }

    IEnumerator __current_state(DequeueCallback callback) {
        
        yield return 0;
    }

    public void resend_begin(object args, int? id, DequeueCallback callback) {
        Logger.Log("!! Resend_begin");
        callback();
    }

    public void resend_end(object args, int? id, DequeueCallback callback) {
        ReConnectReady();
        callback();
    }

    public void reconnect_fail(object args, int? accoudddid, DequeueCallback callback) {
        PlayerPrefs.DeleteKey("ReconnectData");
        
        var translator = AccountManager.Instance.GetComponent<Fbl_Translator>();
        
        if (webSocket != null) {
            webSocket.OnMessage -= ReceiveStart;
            webSocket.OnOpen -= OnOpen;
            webSocket.OnMessage -= ReceiveStart;
            webSocket.OnMessage -= ReceiveMessage;
            webSocket.OnClosed -= OnClosed;
            webSocket.OnError -= OnError;
        }
        PlayMangement playMangement = PlayMangement.instance;
        Logger.Log("<color=yellow>prevTime : " + prevTime + "</color>");
        if (prevTime != default) {
            Logger.Log("<color=yellow>check time interval after in background</color>");
            var currentTime = DateTime.Now;
            TimeSpan dateDiff = currentTime - prevTime;
            int diffSec = dateDiff.Seconds;
            Logger.Log("<color=yellow>diffSec</color>" + " : " + diffSec);
            if (diffSec > 30) {
                Logger.Log("diffSec > 30");
                Time.timeScale = 0;
                
                string _message = translator.GetLocalizedText("UIPopup", "ui_popup_main_losetobackground");
                string btnOk = playMangement.uiLocalizeData["ui_ingame_ok"];
                
                GameObject failureModal = Instantiate(Modal.instantiateReconnectFailModal(_message, btnOk));
                Button okBtn = failureModal.transform.Find("ModalWindow/Button").GetComponent<Button>();
                okBtn.onClick.RemoveAllListeners();
                okBtn.onClick.AddListener(() => {
                    Time.timeScale = 1;
                    FBL_SceneManager.Instance.LoadScene(FBL_SceneManager.Scene.MAIN_SCENE);
                });
                return;
            }
        }

        if(reconnectModal != null) Destroy(reconnectModal);
        //begin end game을 못 받은 경우
        if (!battleGameFinish) {
            //상대방이 끊어진 경우
            if (isOpponentPlayerDisconnected) {
                string _disconnectMsg = playMangement.uiLocalizeData["ui_ingame_popup_opdisconnect"];
                //3초간 모달 띄움
                Instantiate(Modal.instantiateAutoHideModal(_disconnectMsg, 3.0f));
            }
        }

        //둘이 동시에 게임을 나간 경우가 아니라면 보통 패배임..
        //TODO : 정밀한 승/패 판단이 필요한 경우 추가작업이 필요함.
        string _loseMessage = translator.GetLocalizedText("UIPopup", "ui_popup_main_losetoappoff");
        Modal.instantiate(_loseMessage, Modal.Type.CHECK, () => {
            Time.timeScale = 1; 
            FBL_SceneManager.Instance.LoadScene(FBL_SceneManager.Scene.MAIN_SCENE);
        });

        Time.timeScale = 0.0f;
        foreach (var gameObj in (GameObject[]) FindObjectsOfType(typeof(GameObject)))
        {
            if(gameObj.name == "ReconnectCanvas") {
                Destroy(gameObj);
            }
        }
    }

    public void reconnect_success(object args, int? id, DequeueCallback callback) {
        reconnectCount = 0;
        callback();
    }

    /// <summary>
    /// 양쪽 모두 reconnect가 되었을 때
    /// </summary>
    /// <param name="args"></param>
    public void end_reconnect_ready(object args, int? id, DequeueCallback callback) {
        if(opponentWaitModal != null) Destroy(opponentWaitModal);
        callback();
     }


    private GameObject opponentWaitModal;
    /// <summary>
    /// 상대방의 재접속을 대기 (상대가 튕김)
    /// </summary>
    /// <param name="args"></param>
    public void wait_reconnect(object args, int? id, DequeueCallback callback) {
        if(reconnectModal != null) Destroy(reconnectModal);
        // reconnectModal = Instantiate(Modal.instantiateReconnectModal());
        var translator = AccountManager.Instance.GetComponent<Fbl_Translator>();
        string _message = PlayMangement.instance.uiLocalizeData["ui_ingame_popup_tryreconnect"];
        _message = _message.Replace("|", "\n");
        opponentWaitModal = Modal.instantiateOpponentWaitingModal(_message);
        
        isOpponentPlayerDisconnected = true;
        callback();
    }

    public void x2_reward(object args, int? id, DequeueCallback callback) {
        var json = (JObject)args;
        PlayMangement playMangement = PlayMangement.instance;
        GameResultManager resultManager = playMangement.resultManager;
        resultManager.ExtraRewardReceived(json);
        callback();
    }

    private bool stopTimer = false;

    public void cheat(object args, int? id, DequeueCallback callback) {
        PlayMangement play = PlayMangement.instance;
        JObject argument = (JObject)args;
        string method = argument["method"].ToString();
        object value = argument["value"].ToObject<object>();
        bool myPlayer = (argument["camp"].ToString().CompareTo("human") == 0) == play.player.isHuman;
        switch(method) {
        case "shield_count" :
            if (myPlayer) {
                int val = Convert.ToInt32(value);
                play.player.remainShieldCount = val;
                //play.player.SetShieldStack(val);
            }
            else {
                int val = Convert.ToInt32(value);
                play.enemyPlayer.remainShieldCount = val;
                //play.enemyPlayer.SetShieldStack(val);
            }
            break;
        case "shield_gauge" :
            if (myPlayer) {
                int val = Convert.ToInt32(value);
                int stack = play.player.shieldStack.Value;
                play.player.ChangeShieldStack(play.player.shieldStack.Value, val - stack);
                play.player.shieldStack.Value = val;
            }
            else {
                int val = Convert.ToInt32(value);
                int stack = play.enemyPlayer.shieldStack.Value;
                play.enemyPlayer.ChangeShieldStack(play.player.shieldStack.Value, val - stack);
                play.enemyPlayer.shieldStack.Value = Convert.ToInt32(value);
            }
            break;
        case "resource" :
            if(myPlayer) play.player.resource.Value = Convert.ToInt32(value);
            else play.enemyPlayer.resource.Value = Convert.ToInt32(value);
            if(myPlayer == play.player.isHuman) play.player.ActivePlayer();
            else play.player.ActiveOrcTurn();
            break;
        case "hp" :
            if(myPlayer) play.player.SetHP(Convert.ToInt32(value));
            else play.enemyPlayer.SetHP(Convert.ToInt32(value));
            break;
        case "time_stop" :
            stopTimer = Convert.ToBoolean(value);
            if(stopTimer) {
                play.player.GetComponent<IngameTimer>().RopeTimerOff();
                play.enemyPlayer.GetComponent<IngameTimer>().RopeTimerOff();
            }
        break;
        case "free_card" :
            play.cheatFreeCard = Convert.ToBoolean(value);
            if(myPlayer == play.player.isHuman) play.player.ActivePlayer();
            else play.player.ActiveOrcTurn();
            break;
        case "draw" :
            string cardId = Convert.ToString(value);
            Card gameStateNewCard = gameState.players.myPlayer(PlayMangement.instance.player.isHuman).newCard;
            if(cardId.CompareTo(gameStateNewCard.cardId) != 0) break;
            DrawNewCard(gameStateNewCard.itemId);
            break;
        default :
            break;
        }
        callback();
    }

    public void begin_play(object args, int? id, DequeueCallback callback) { callback(); }
    public void battle_turn_start(object args, int? id, DequeueCallback callback) { callback(); }
    public void shield_turn_start(object args, int? id, DequeueCallback callback) { callback(); }
}


public class ShieldStack {
    Queue<int> human = new Queue<int>();
    Queue<int> orc = new Queue<int>();

    public void ResetShield() {
        human.Clear();
        orc.Clear();
    }

    public int GetShieldAmount(bool isHuman) {
        if (isHuman == true)
            return (human.Count > 0) ? human.Dequeue() : 0;
        else
            return (orc.Count > 0) ? orc.Dequeue() : 0;
    }
    
    public void SavingShieldGauge(string camp, int amount) {
        if (camp == "human")
            human.Enqueue(amount);
        else
            orc.Enqueue(amount);
    }

    public Queue<int> HitPerShield(string camp) {
        if (camp == "human") {
            return human;
        }
        else
            return orc;
    }
}
