## Server-Client 카드 정규화 테이블 작성과 C# Reflection을 활용한 카드 기능 구현

---

기획팀에서 작성한 테이블 원형(data_card_0627.xlsx)을 바탕으로 Client, Server Socket Message로 활용할 수 있는 **개발용 테이블**을 새로 작성   

[기획 테이블 원형]   
![image](https://github.com/user-attachments/assets/861e8f3b-11fe-4940-972f-ec618b6b2fda)


[개발용 테이블 작성]   
![image](https://github.com/user-attachments/assets/0eb8e4ce-dbf5-4753-9733-f6206d5cba6e)

### Client에서 Server로 Socket Message를 보내는 경우 처리 예시

***

> 카드를 드래그 하여 유닛 소환시(Drag & Drop 처리가 완료된 이후) UnitDragHandler의 SummonUnit 함수를 호출한다.
> unitPref.AddComponent<CardUseSendSocket>().Init();

<pre>
  <code>
    IEnumerator SummonUnit(Transform slot) {
        PlayMangement.dragable = false;

        //yield return PlayMangement.instance.cardHandManager.ShowUsedCard(transform.parent.GetSiblingIndex(), gameObject);
        GameObject unitPref = CardDropManager.Instance.DropUnit(gameObject, slot);
        if (unitPref != null) {
            var cardData = GetComponent<CardHandler>().cardData;

            object[] parms = new object[] { true, unitPref };
            unitPref.AddComponent<CardUseSendSocket>().Init();
            PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.END_CARD_PLAY, this, parms);
            PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.FIELD_CHANGED, null, null);
        }
        PlayMangement.dragable = true;
        yield return 0;
    }
  </code>
</pre>

***

> CardUseSendSocket.cs의 Init 함수를 호출
> SendSocket 함수를 호출한다.

<pre>
  <code>
    public async void Init(bool isEndCardPlay = true) {
        SetUnitorMagic();
        await CheckSelect(isEndCardPlay);
        Debug.Log("sending Socket");
        if (isEndCardPlay) {
            SendSocket();
            DestroyMyCard();
        }
        else SendSkillActivate();
        PlayMangement.instance.UnlockTurnOver();
        Destroy(this);
    }
  </code>
</pre>


***

<pre>
  <code>
    public void SendSocket() {
        BattleConnector connector = PlayMangement.instance.socketHandler;
        MessageFormat format = MessageForm(true);
        connector.UseCard(format);
    }
  </code>
</pre>

<pre>
  <code>
    public void UseCard(object args) {
        SendMethod("play_card", args);
    }
  </code>
</pre>

> SendMethod에서 **개발용 테이블** 형태에 의거한 형태로 메시지를 json 형태로 보낸다.

<pre>
  <code>
    protected void SendMethod(string method, object args = null) {
        if (args == null) args = new string[] { };
        SendFormat format = new SendFormat(method, args);
        string json = JsonConvert.SerializeObject(format);
        PutSendMessage(method, json);
        Debug.Log("<color=red>소켓으로 보내는 메시지!</color> : " + json);
        webSocket.Send(json);
    }
  </code>
</pre>

### Server에서 Client로 Socket Message를 받을 때, Client 처리 흐름

> BattleConnector_receiver.cs 에서 C# Reflection을 활용하여 Socket Message에 해당하는 함수를 문자열로 찾아 호출한다.
> 메시지를 받는 최초 함수
> 전달 받는 Message는 Queue에 쌓고, 하나씩 Message를 해석하여 처리한다.

<pre>
  <code>
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
  </code>
</pre>

> SocketMessage를 Queue에서 Dequeue 하여 해석한다.
> ExecuteSocketMessage(result);
<pre>
  <code>
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
  </code>
</pre>

> 전달 받은 메시지를 C# Reflection을 활용하여 해당하는 이름의 함수를 찾아 호출한다.

<pre>
  <code>
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
  </code>
</pre>

> ac10006이 호출되는 경우, ac10006이 Socket Message의 cardID로 담겨있을 것이며, 관련한 파라미터 정보를 **개발용 테이블**에 맞추어 함께 전달해 준다.    
> 구현된 카드 함수 일부

> ac10006인 경우 마법 카드이기 때문에 다음과 같은 구조의 파라미터 정보를 갖는다.
<pre>
  <code>
    public class MagicArgs {
        public string itemId;
        public Target[] targets;
        public object skillInfo;
    }
  </code>
</pre>

<pre>
  <code>
    public void ac10006(object args, DequeueCallback callback) {
        JObject jObject = args as JObject;
        string itemId = jObject["targets"][0]["args"][0].ToString();

        GameObject targetUnit = PlayMangement.instance.UnitsObserver.GetUnitToItemID(itemId);
        EffectSystem.Instance.ShowEffect(EffectSystem.EffectType.BLESS_AC10006, targetUnit.transform.position);
        SoundManager.Instance.PlayMagicSound("ac10006_1");
        SoundManager.Instance.PlayMagicSound("ac10006_2");

        targetUnit.GetComponent<PlaceMonster>().UpdateGranted();
        callback();
    }

    //긴급 보급
    public void ac10007(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] itemIds = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());
        bool isHuman = magicArgs.targets[0].args[0] == "human";
        PlayerController player = PlayMangement.instance.player;
        BattleConnector socket = PlayMangement.instance.SocketHandler;
        if (player.isHuman != isHuman)
            player.StartCoroutine(PlayMangement.instance.EnemyMagicCardDraw(itemIds.Length, callback));
        else
            socket.DrawNewCards(itemIds, callback);
    }

    //재배치
    public void ac10015(object args, DequeueCallback callback) {
        JObject jObject = args as JObject;
        Debug.Log(jObject["targets"][0]["args"][0]);
        string itemId = (string)jObject["targets"][0]["args"][0];
        Debug.Log(itemId);
        GameObject monster = unitObserver.GetUnitToItemID(itemId);
        Unit unit = PlayMangement.instance.socketHandler.gameState.map.allMonster.Find(x => string.Compare(x.itemId, itemId, StringComparison.Ordinal) == 0);
        EffectSystem.ActionDelegate skillAction;
        skillAction = delegate () { monster.GetComponent<PlaceMonster>().UpdateGranted(); callback(); };
        unitObserver.UnitChangePosition(monster, unit.pos, monster.GetComponent<PlaceMonster>().isPlayer, string.Empty, () => skillAction());
    }
  </code>
</pre>
