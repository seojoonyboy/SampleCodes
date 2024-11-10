## FLUX 패턴 설계 예시

![image](https://github.com/user-attachments/assets/6e421d51-06a5-48e5-8e5b-4cd6d1bd78d8)

### 인벤토리 기준
---

> View에서 부품 장착 이벤트가 발생한 경우
> 해당 이벤트의 Action을 생성한다.
> ActionCreator.createAction(ActionTypes.GARAGE_ITEM_EQUIP) as equip_act;

<pre>
  <code>
    public void OnEquipButton() {
        Info info = parent.selectedItem.GetComponent<Info>();
        int itemGrade = info.grade;
        int myGrade = userStore.myData.status.rank;
        if(canEquip(itemGrade, myGrade)) {
            equip_act act = ActionCreator.createAction(ActionTypes.GARAGE_ITEM_EQUIP) as equip_act;
            act._type = equip_act.type.ITEM;
            act.id = info.id;
            gm.gameDispatcher.dispatch(act);

            close();
        }
        else {
            notifyModal.SetActive(true);
            notifyModal.transform.Find("InnerModal/Text").GetComponent<Text>().text = "등급이 낮아 장착할 수 없습니다.";
        }
    }
  </code>
</pre>

> equip_act 액션을 생성
<pre>
  <code>
    ... 중략...
    public static Actions createAction(ActionTypes _type) {
        case ActionTypes.GARAGE_ITEM_EQUIP:
            _return = new equip_act();
            break;
    }
  </code>
</pre>

<pre>
  <code>
    public class equip_act : NetworkAction {
      public enum type { CHAR, ITEM, USER }
      public type _type;
      public int id;
    }
  </code>
</pre>

> Dispatcher에게 생성한 Action을 전달한다.
> gm.gameDispatcher.dispatch(act);
> GameDispatcher의 Queue에 Action을 쌓고 하나씩 Dispatch 한다.

<pre>
  <code>
    public class QueueDispatcher<TPayload> : Dispatcher<TPayload> where TPayload: class{
        Queue<TPayload> dispatchQueue = new Queue<TPayload>();

        public new void dispatch(TPayload payload){
            if(this.isDispatching){
                this.dispatchQueue.Enqueue(payload);
                return;
            }
            while(true){
                base.dispatch(payload);
                if (dispatchQueue.Count == 0) break;
                payload = this.dispatchQueue.Dequeue();
            }
        }
        public new bool isDispatching{
            get{ return base.isDispatching || dispatchQueue.Count>0; }
        }
    }
  </code>
</pre>

> 해당 Action 이벤트를 Listen 하고 있는 Store에서 처리를 한다.
> BicycleInventory Store가 GARAGE_ITEM_INIT을 Listen 하고 있기 때문에 처리를 한다.

<pre>
  <code>
    protected override void _onDispatch(Actions action) {
        switch (action.type) {
            case ActionTypes.GARAGE_ITEM_EQUIP:
                equip_act equipAct = action as equip_act;
                if(equipAct._type == equip_act.type.ITEM) {
                    equip(action as equip_act);
                }
                break;
    
           ... 중략 ....
    
        }
        eventType = action.type;
    }
  </code>
</pre>

> equip 처리를 실제로 Server에 요청을 보내 처리한 이후 View를 갱신한다.
> _emitChange()

<pre>
  <code>
    private void equip(equip_act payload) {
        switch (payload.status) {
            case NetworkAction.statusTypes.REQUEST:
                var strBuilder = GameManager.Instance.sb;
                strBuilder.Remove(0, strBuilder.Length);
                strBuilder.Append(networkManager.baseUrl)
                    .Append("inventory/items/")
                    .Append(payload.id)
                    .Append("/equip");
                WWWForm form = new WWWForm();
                storeStatus = storeStatus.WAITING_REQ;
                networkManager.request("POST", strBuilder.ToString(), form, ncExt.networkCallback(dispatcher, payload));
                _emitChange();
                break;
            case NetworkAction.statusTypes.SUCCESS:
                storeStatus = storeStatus.NORMAL;

                //Debug.Log("아이템 장착 완료");

                MyInfo myInfoAct = ActionCreator.createAction(ActionTypes.MYINFO) as MyInfo;
                gm.gameDispatcher.dispatch(myInfoAct);
                _emitChange();
                break;
            case NetworkAction.statusTypes.FAIL:
                storeStatus = storeStatus.ERROR;
                _emitChange();
                break;
        }
    }
  </code>
</pre>
