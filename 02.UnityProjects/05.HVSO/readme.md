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


프로젝트 소개
==========================
*HVSO 특징*   
실시간 턴제 기반 전략 카드 게임.   
유저와 유저를 서로 매칭하여 실시간으로 PVP를 즐길 수 있고, 
AI와 대전하는 PVE 컨텐츠가 있음.    
게임 방식은 기존 EA사의 플랜츠 vs 좀비와 유사하다.    

프로젝트 관리
===========================
Gitlab으로 관리하였으며, develop/feature/release 브랜치로 나누어 관리함.    

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

