Unity 기반 전략 카드 게임
==========================
> 피드백루프 게임 클라이언트 개발   
> 개발 기간 : 2018.04 ~ 2020.06 [약 2년]   
> 출시 여부 : 출시 안함

개발 환경
==========================
엔진 : Unity 2018.4.12f1      
플랫폼 : Android, iOS   
버전 관리 : Git, Gitlab   
이슈 관리 : Mentis   


프로젝트 소개
==========================
*HVSO 특징*   
실시간 턴제 기반 전략 카드 게임.   
유저와 유저를 서로 매칭하여 실시간으로 PVP를 즐길 수 있고, 
AI와 대전하는 PVE 컨텐츠가 있음.    
게임 방식은 기존 EA사의 플랜츠 vs 좀비와 유사하다.    

프로젝트 관리
===========================
Gitlab으로 관리하였으며, develop/feature/release 브랜치로 나누어 관리.    
이슈는 Mentis를 활용하여 관리.   

![Gitlab_Commit](https://github.com/user-attachments/assets/ae71fab3-a993-4b82-a90f-5ea2542cf672)   

***
Socket TCP 통신을 통한 실시간 플레이어와 매칭.    
또한, 게임 도중 네트워크가 불안정해지거나, 앱이 종료되는 경우 재접속 관련 처리.    
C# Reflection을 활용하여 Server-> Client Socket Message를 받아 처리함.    

BattleConnector.cs 의 코드 일부.    
> PlayerPrefab에 ReconnectData가 존재하는 경우, 재접속 로직을 타게 된다.
> ReconnectData는 게임이 종료되는 시점에 게임종료 Socket Message를 전달받은 이후에 제거되기 때문에,   
> 게임종료 Socket Message를 받지 못한 상황은 중간에 네트워크가 끊어진 상황으로 간주함.

<pre>
  <code>
    private void SocketConnected() 
    {
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
  </code>
</pre>

BattleConnector_receiver.cs 의 코드 일부   
Server->Client로 전달받는 Socket Message는 C# Reflection을 활용한다.   

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

***
카드를 드래그 하여 스킬 사용시, Singleton 형태의 PlayManagement의 Activate 함수를 호출한다.   

<pre>
  <code>
    IEnumerator UseSkillCard(object[] parms, object args, DequeueCallback callback) {
      int cardNum = transform.parent.GetSiblingIndex();
      PlayMangement.dragable = false;
      PlayMangement.instance.LockTurnOver();
      PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.MAGIC_USED, this, cardData.id);
      yield return PlayMangement.instance.cardHandManager.ShowUsedCard(cardNum, gameObject);
      if (cardData.isHeroCard == true) {
          HideCardImage();
          yield return EffectSystem.Instance.HeroCutScene(PlayMangement.instance.player.heroID);            
      }
      PlayMangement.instance.cardActivate.Activate(cardData.id, args, callback);
      SoundManager.Instance.PlayMagicSound(cardData.id);
      highlighted = false;
      CardDropManager.Instance.HighLightMagicSlot(highlightedSlot, highlighted);       
      highlightedSlot = null;        
      ShowCardsHandler showCardsHandler = transform.root.GetComponentInChildren<ShowCardsHandler>();

      if (showCardsHandler.CheckShieldTurnCard(gameObject) == false)
          PlayMangement.instance.player.cdpm.DestroyCard(cardNum);
      showCardsHandler.FinishPlay(gameObject);
      handManager.SortHandPosition();        
      PlayMangement.instance.UnlockTurnOver();
      PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.END_CARD_PLAY, this, parms);
      PlayMangement.dragable = true;
      //GetComponentInParent<ShowCardsHandler>().RemoveCard(gameObject);
  }
  </code>
</pre>

C#의 Reflection 특성을 활용하여 해당하는 카드 스킬의 함수를 실제로 호출한다.   

<pre>
  <code>
    public void Activate(string cardId, object args, DequeueCallback callback) {
        MethodInfo theMethod = this.GetType().GetMethod(cardId);
        object[] parameter = new object[] { args, callback };
        unitObserver = unitObserver == null ? PlayMangement.instance.UnitsObserver : unitObserver;
        if (theMethod == null) {
            Logger.Log(cardId + "해당 카드는 아직 준비가 안되어있습니다.");
            callback();
            return;
        }
        theMethod.Invoke(this, parameter);
    }
  </code>
</pre>

카드별로 구현되어 있는 실제 함수 일부   

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
