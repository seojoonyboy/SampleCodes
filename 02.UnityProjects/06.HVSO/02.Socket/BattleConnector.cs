using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine;
using BestHTTP.WebSocket;
using BestHTTP.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocketFormat;
using System.Threading.Tasks;
using System.Linq;

public partial class BattleConnector : MonoBehaviour {

    private string _lobbyUrl;
    private string _gameUrl;
    
    private string Url {
        get => _gameUrl;
        set => _gameUrl = NetworkManager.Instance.baseUrl + "game/" + value + "/socket";
    }

    private string LobbyUrl {
        get => NetworkManager.Instance.baseUrl + "lobby/socket";
    }

    public WebSocket GetWebSocket() {
        return webSocket;
    }
    
    WebSocket webSocket;
    [SerializeField] protected Text message;
    [SerializeField] protected Text timer;
    [SerializeField] protected GameObject machine;
    [SerializeField] protected Button returnButton, aiBattleButton;
    [SerializeField] protected GameObject textBlur;
    protected List<string> socketSendMessage = new List<string>();

    public UnityAction<string, string, bool> HandchangeCallback;
    protected Coroutine timeCheck;
    protected bool battleGameFinish = false;
    protected bool isQuit = false;
    protected int reconnectCount = 0;

    public static UnityEvent OnOpenSocket = new UnityEvent();

    private void Awake() {
        battleGameFinish = false;
        
        thisType = this.GetType();
        DontDestroyOnLoad(gameObject);
        Application.wantsToQuit += Quitting;
    }

    private void OnDestroy() {
        Application.wantsToQuit -= Quitting;
    }

    /// <summary>
    /// open lobby socket
    /// </summary>
    public virtual void OpenLobby() {
        reconnectCount = 0;

        Debug.Assert(!PlayerPrefs.GetString("SelectedRace").Any(char.IsUpper), "Race 정보는 소문자로 입력해야 합니다!");
        string race = PlayerPrefs.GetString("SelectedRace").ToLower();

        string url = string.Format("{0}", LobbyUrl);
        webSocket = new WebSocket(new Uri(string.Format("{0}?token={1}&camp={2}", url, AccountManager.Instance.TokenId, race)));
        webSocket.OnOpen += OnLobbyOpen;
        webSocket.OnMessage += ReceiveMessage;
        //webSocket.OnClosed += OnLobbyClosed;
        webSocket.OnError += Error;
        webSocket.Open();

        string findMessage = AccountManager.Instance.GetComponent<Fbl_Translator>().GetLocalizedText("MainUI", "ui_page_league_findopponent");

        message.text = findMessage;
        timeCheck = StartCoroutine(TimerOn());

        string battleType = PlayerPrefs.GetString("SelectedBattleType");

        if (battleType == "league" || battleType == "leagueTest") {
            returnButton.onClick.AddListener(BattleCancel);
            returnButton.gameObject.SetActive(true);
            aiBattleButton.gameObject.SetActive(true);
        }
    }


    void Error(WebSocket webSocket, Exception ex) {
        Logger.LogError(ex);
    }

    public void OnAiBattleButtonClicked() {
        if(webSocket != null) webSocket.Close();
        PlayerPrefs.SetString("SelectedBattleType", "leagueTest");
        aiBattleButton.gameObject.SetActive(false);

        OpenSocket();
    }
    
    /// <summary>
    /// open game socket (after lobby socket connected)
    /// </summary>
    public virtual void OpenSocket(bool isForcedReconnectedFromMainScene = false) {
        string battleType = PlayerPrefs.GetString("SelectedBattleType");
        
        this.isForcedReconnectedFromMainScene = isForcedReconnectedFromMainScene;
        reconnectCount = 0;

        //추후 유동적으로 값 변경될 예정
        Url = serverNum.ToString();
        
        if(battleType != "league") {
            AccountManager.Instance.RequestPickServer((request, response) => {
                if (response.IsSuccess) {
                    var json = JObject.Parse(response.DataAsText);
                    
                    int _serverNum = -1;
                    int.TryParse(json["number"].ToString(), out _serverNum);
                    if (_serverNum == -1) {
                        Time.timeScale = 0;
                        Modal.instantiate("Server Error -00100", Modal.Type.CHECK, () => {
                            Time.timeScale = 1;
                            FBL_SceneManager.Instance.LoadScene(FBL_SceneManager.Scene.MAIN_SCENE);
                        });
                        return;
                    }
                    serverNum = _serverNum;
                    
                    string url = string.Format("{0}", Url);
        
                    __OpenSocket(battleType);
                }
                else {
                    Logger.LogError("Server Num 가져오기 실패");
                }
            });
        }
        else {
             __OpenSocket(battleType);
        }
    }

    private void __OpenSocket(string battleType) {
        if (serverNum == null) {
            Time.timeScale = 0;
            Modal.instantiate("Server Error -00101", Modal.Type.CHECK, () => {
                Time.timeScale = 1;
                FBL_SceneManager.Instance.LoadScene(FBL_SceneManager.Scene.MAIN_SCENE);
            });
            return;
        }
        Url = serverNum.Value.ToString();
        string url = string.Format("{0}", Url);
        
        Logger.Log("<color=blue>OpenSocket URL : " + url + "</color>");
        webSocket = new WebSocket(new Uri(string.Format("{0}?token={1}", url, AccountManager.Instance.TokenId)));
        webSocket.StartPingThread = true;
        webSocket.OnOpen += OnOpen;
        webSocket.OnMessage += ReceiveStart;
        webSocket.OnMessage += ReceiveMessage;
        webSocket.OnClosed += OnClosed;
        webSocket.OnError += OnError;
        webSocket.Open();

        string findMessage = AccountManager.Instance.GetComponent<Fbl_Translator>().GetLocalizedText("MainUI", "ui_page_league_findopponent");
            
        if(message == null) return;
        
        message.text = findMessage;
        timeCheck = StartCoroutine(TimerOn());
        
        if (battleType == "league" || battleType == "leagueTest") {
            returnButton.onClick.AddListener(BattleCancel);
            returnButton.gameObject.SetActive(true);
        }
    }

    private void OnLobbyOpen(WebSocket webSocket) {
        //Logger.Log("OnLobbyOpen");
    }

    private void OnLobbyClosed(WebSocket webSocket, ushort code, string message) {
        OpenSocket();
    }

    //매칭 대기
    private IEnumerator TimerOn() {
        int time = 0;
        string countFormat = AccountManager.Instance.GetComponent<Fbl_Translator>().GetLocalizedText("MainUI", "ui_page_league_waitingsec");
        while(time < 60) {
            yield return new WaitForSeconds(1f);
            if(timer != null) timer.text = countFormat.Replace("{n}", time.ToString());
            ++time;
        }
        message.text = "대전상대를 찾지 못했습니다.";
        timer.text = "이전 메뉴로 돌아갑니다.";
        returnButton.gameObject.SetActive(false);
        webSocket.Close();
        yield return new WaitForSeconds(3f);
        FBL_SceneManager.Instance.LoadScene(FBL_SceneManager.Scene.MAIN_SCENE);
        Destroy(gameObject);
    }

    private void BattleCancel() {
        StopCoroutine(timeCheck);
        webSocket.Close();
        returnButton.onClick.RemoveListener(BattleCancel);
        FBL_SceneManager.Instance.LoadScene(FBL_SceneManager.Scene.MAIN_SCENE);
        Destroy(gameObject);
    }

    public void OnClosed(WebSocket webSocket, ushort code, string msg) {
        //Logger.LogWarning("Socket has been closed : " + code + "  message : " + msg);
        if(battleGameFinish) return;
        
        if(reconnectModal != null) Destroy(reconnectModal);
        reconnectModal = Instantiate(Modal.instantiateReconnectModal());
        TryReconnect();
    }

    public void OnError(WebSocket webSocket, Exception ex) {
        if(battleGameFinish) return;
        
        if(reconnectModal != null) Destroy(reconnectModal);
        reconnectModal = Instantiate(Modal.instantiateReconnectModal());
        //Logger.LogError("Socket Error message : " + ex);
        TryReconnect();
    }

    private bool Quitting() {
        //Logger.Log("quitting");
        isQuit = true;
        if(webSocket != null) {
            webSocket.OnOpen -= OnOpen;
            webSocket.OnMessage -= ReceiveMessage;
            webSocket.OnClosed -= OnClosed;
            webSocket.OnError -= OnError;
            webSocket.Close();
        }
        // PlayerPrefs.DeleteKey("ReconnectData");
        return true;
    }

    public async void TryReconnect() {
        isDisconnected = true;
        
        await Task.Delay(2000);
        if (battleGameFinish) {
            if(reconnectModal != null) Destroy(reconnectModal);
            return;
        }
        if(isQuit) return;
        if(reconnectCount >= 15) {
            PlayMangement playMangement = PlayMangement.instance;
            PlayerPrefs.DeleteKey("ReconnectData");

            ReconnectController controller = FindObjectOfType<ReconnectController>();
            if(controller != null) {
                Destroy(controller);
                Destroy(controller.gameObject);
            }

            if (playMangement) {
                string message = playMangement.uiLocalizeData["ui_ingame_popup_gotitle"];
                string btnOk = playMangement.uiLocalizeData["ui_ingame_ok"];
                
                GameObject failureModal = Instantiate(Modal.instantiateReconnectFailModal(message, btnOk));
                Button okBtn = failureModal.transform.Find("ModalWindow/Button").GetComponent<Button>();
                okBtn.onClick.RemoveAllListeners();
                okBtn.onClick.AddListener(() => {
                    NetworkManager.Instance.RestartToLogin();
                });
            }
            else FBL_SceneManager.Instance.LoadScene(FBL_SceneManager.Scene.MAIN_SCENE);
            return;
        }
        reconnectCount++;

        Url = serverNum.ToString();
        Logger.Log("<color=blue>Re OpenSocket URL : " + Url + "</color>");
        
        webSocket = new WebSocket(new Uri(string.Format("{0}?token={1}", Url, AccountManager.Instance.TokenId)));
        webSocket.StartPingThread = true;
        webSocket.OnOpen += OnOpen;
        webSocket.OnMessage += ReceiveStart;
        webSocket.OnMessage += ReceiveMessage;
        webSocket.OnClosed += OnClosed;
        webSocket.OnError += OnError;
        webSocket.Open();
    }

    //Connected
    private void OnOpen(WebSocket webSocket) { }

    private void SocketConnected() {
        object message;
        string reconnect = PlayerPrefs.GetString("ReconnectData", null);
        if(!string.IsNullOrEmpty(reconnect)) {
            NetworkManager.ReconnectData data = JsonConvert.DeserializeObject<NetworkManager.ReconnectData>(reconnect);
            bool isSameType = String.Compare(data.battleType, PlayerPrefs.GetString("SelectedBattleType"), StringComparison.Ordinal) == 0;
            if(isSameType) {
                message = SetJoinGameData(data);
                SendMethod("reconnect_game", message);
                return;
            }
            PlayerPrefs.DeleteKey("ReconnectData");
            //재연결 실패단계?
        }
        message = SetJoinGameData();
        SendMethod("join_game", message);
        OnOpenSocket.Invoke();
    }

    private void JoinGame() {
        webSocket.Close();
        OpenSocket();

        object message;
        PlayerPrefs.SetString("SelectedBattleType", "league");

        message = SetJoinGameData();
        SendMethod("join_game", message);
    }

    private object SetJoinGameData(NetworkManager.ReconnectData data) {
        JObject args = new JObject();
        args["type"] = data.battleType.CompareTo("leagueTest") == 0 ? "league" : data.battleType;
        args["camp"] = data.camp;
        args["gameId"] = data.gameId;
        return args;
    }

    private object SetJoinGameData() {
        //matchKey = "55555";
        JObject args = new JObject();
        string battleType = PlayerPrefs.GetString("SelectedBattleType");

        Debug.Assert(!PlayerPrefs.GetString("SelectedRace").Any(char.IsUpper), "Race 정보는 소문자로 입력해야 합니다!");
        string race = PlayerPrefs.GetString("SelectedRace").ToLower();
        args["type"] = battleType;
        args["camp"] = race;
        if(String.Compare(battleType, "story", StringComparison.Ordinal) == 0) {
            var chapterNum =  int.Parse(PlayerPrefs.GetString("ChapterNum", "0"));
            args["stage"] = int.Parse(PlayerPrefs.GetString("StageNum"));
            args["chapter"] = chapterNum;
            if(chapterNum >= 1) args["deckId"] = int.Parse(PlayerPrefs.GetString("SelectedDeckId"));
        }
        else {
            args["deckId"] = int.Parse(PlayerPrefs.GetString("SelectedDeckId"));
            if(battleType.Contains("league")) {
                args["gameId"] = matchKey;
                
                //첫 리그 데이터 구별
                if(leagueData.leagueInfo.winningStreak == 0 && leagueData.leagueInfo.losingStreak == 0) 
                    PlayerPrefs.SetInt("isLeagueFirst", 1);
                else 
                    PlayerPrefs.SetInt("isLeagueFirst", 0);
            }
        }
        return args;
    }


    public void ChangeCard(string itemId) {
        JObject item = new JObject();
        item["itemId"] = itemId;
        SendMethod("hand_change", item);
    }


    public void KeepHeroCard(string itemId) {
        JObject args = new JObject();
        args["itemId"] = itemId;
        Debug.Log(args);
        SendMethod("keep_hero_card", args);
    }

    public void ResendMessage() {
        JObject args = new JObject();
        
        Logger.Log("lastQueueId : " + lastQueueId);
        
        var lastId = lastQueueId;
        if(lastId == null) return;
        
        args["lastMsgId"] = lastId;
        SendMethod("resend_msg", args);
    }

    /// <summary>
    /// resend_end 이후에 재접속 완료했다는 메시지
    /// </summary>
    public void ReConnectReady() {
        SendMethod("reconnect_ready");
        ResendSendMessage();
    }

    void OnDisable() {
        if(webSocket != null) Quitting();
    }

    public void StartBattle() {
        if (PlayerPrefs.GetString("SelectedBattleType") != "story")
            UnityEngine.SceneManagement.SceneManager.LoadScene("IngameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("TutorialScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }


    public void DrawNewCards(string[] itemId, DequeueCallback callback = null) {
        PlayMangement playMangement = PlayMangement.instance;
        bool isHuman = playMangement.player.isHuman;
        int cardNum = gameState.players.myPlayer(isHuman).deck.handCards.Length - 1;

        Card[] cardDatas = new Card[itemId.Length];
        for(int i = 0; i<itemId.Length; i++) 
            cardDatas[i] = Array.Find(gameState.players.myPlayer(isHuman).deck.handCards, x => x.itemId == itemId[i]);
        StartCoroutine(PlayMangement.instance.player.cdpm.AddMultipleCard(cardDatas, callback));
    }


    public void DrawNewCard(string itemId, DequeueCallback callback = null) {
        PlayMangement playMangement = PlayMangement.instance;
        bool isHuman = playMangement.player.isHuman;
        int cardNum = gameState.players.myPlayer(isHuman).deck.handCards.Length - 1;

        Card[] cardDatas = new Card[1];
        cardDatas[0] = Array.Find(gameState.players.myPlayer(isHuman).deck.handCards, x => x.itemId == itemId);
        StartCoroutine(PlayMangement.instance.player.cdpm.AddMultipleCard(cardDatas, callback));
    }


    public IEnumerator DrawCardIEnumerator(string itemId, DequeueCallback callback = null) {
        PlayMangement playMangement = PlayMangement.instance;
        bool isHuman = playMangement.player.isHuman;
        yield return PlayMangement.instance.player.cdpm.AddMultipleCard(gameState.players.myPlayer(isHuman).deck.handCards, callback);
    }
}

public partial class BattleConnector {
    public delegate void SetMessage(object args = null);
    public SetMessage MessageComponent;


    public enum SendMessageList {
        join_game = 20000,
        client_ready,
        pong,
        reconnect_ready,

        hand_change = 25000,
        turn_start,
        turn_over,
        play_card,
        start_state,
        unit_skill_activate,
        keep_hero_card,

        player_surrender = 30000,
        end_story_game,
        end_game,
        claim_2x_reward,
        cheat
    }

    public void SettingMethod(string method, object args = null) {
        SendMethod(method, args);
    }

    protected void SendMethod(string method, object args = null) {
        if (args == null) args = new string[] { };
        SendFormat format = new SendFormat(method, args);
        string json = JsonConvert.SerializeObject(format);
        PutSendMessage(method, json);
        Debug.Log("<color=red>소켓으로 보내는 메시지!</color> : " + json);
        webSocket.Send(json);
    }

    public static readonly string[] checkSendList = new string[]{"turn_start","turn_over","play_card","unit_skill_activate","keep_hero_card"};

    protected void PutSendMessage(string method, string json) {
        if(checkSendList.Contains(method)) {
            socketSendMessage.Add(json);
        }
    }

    protected void CheckSendMessage() {
        if(socketSendMessage.Count != 0 && !isDisconnected) { 
            socketSendMessage.Clear();
        }
    }

    protected void ResendSendMessage() {
        foreach(string json in socketSendMessage) {
            webSocket.Send(json);
        }
        socketSendMessage.Clear();
    }



    private void TurnStart() {
        SendMethod("turn_start");
    }

    public void TurnOver() {
        SendMethod("turn_over");
    }

    public void UseCard(object args) {
        SendMethod("play_card", args);
    }

    public void SendStartState(object args) {
        SendMethod("start_state", args);
    }

    public void UnitSkillActivate(object args) {
        SendMethod("unit_skill_activate", args);
    }

    public void ClientReady() {
        SendMethod("client_ready");
    }

    public void TutorialEnd() {
        SendMethod("end_story_game");

        var isHuman = PlayMangement.instance.player.isHuman;
        if (isHuman) {
            PlayerPrefs.SetString("PrevTutorial", "Human_Tutorial");
        }
        else {
            PlayerPrefs.SetString("PrevTutorial", "Orc_Tutorial");
        }
    }

    public void Surrend(object args) {
        SendMethod("player_surrender", args);
    }
}