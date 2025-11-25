using Flux;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Box_Inventory : AjwStore {
    //store status
    public storeStatus storeStatus = storeStatus.NORMAL;
    //store message
    public string message;
    public Box_Inventory(QueueDispatcher<Actions> _dispatcher) : base(_dispatcher) { }

    NetworkManager networkManager = NetworkManager.Instance;
    NetworkCallbackExtention ncExt = new NetworkCallbackExtention();

    public boxOpenCallback[] openedItem;

    public ActionTypes eventType;

    protected override void _onDispatch(Actions action) {
        switch (action.type) {
            case ActionTypes.BOX_OPEN:
                open(action as garage_box_open);
                break;
        }
        eventType = action.type;
    }

    //박스 오픈
    private void open(garage_box_open payload) {
        switch (payload.status) {
            case NetworkAction.statusTypes.REQUEST:
                storeStatus = storeStatus.WAITING_REQ;
                var strBuilder = GameManager.Instance.sb;
                strBuilder.Remove(0, strBuilder.Length);
                if (payload.num == 1) {
                    strBuilder.Append(networkManager.baseUrl)
                    .Append("inventory/open_box");
                }
                else if(payload.num == 10) {
                    strBuilder.Append(networkManager.baseUrl)
                    .Append("inventory/open_box/10");
                }
                Debug.Log(strBuilder.ToString());
                networkManager.request("POST", strBuilder.ToString(), ncExt.networkCallback(dispatcher, payload));
                break;
            case NetworkAction.statusTypes.SUCCESS:
                storeStatus = storeStatus.NORMAL;
                message = "박스를 성공적으로 오픈하였습니다.";
                Debug.Log(payload.response.data);
                openedItem = JsonHelper.getJsonArray<boxOpenCallback>(payload.response.data);

                MyInfo act = ActionCreator.createAction(ActionTypes.MYINFO) as MyInfo;
                dispatcher.dispatch(act);

                _emitChange();
                break;
            case NetworkAction.statusTypes.FAIL:
                storeStatus = storeStatus.ERROR;
                Debug.Log(payload.response.data);
                message = "박스를 오픈하는 과정에서 문제가 발생하였습니다.";
                _emitChange();
                break;
        }
    }

    [System.Serializable]
    public class boxOpenCallback {
        public string type;
        public subBoxOpen_Item item;
        public subBoxOpen_Char character;
    }

    [System.Serializable]
    public class subBoxOpen_Item {
        public int id;
        public string name;
        public string desc;
        public int grade;
        public int gear;
        public string parts;
        public int limit_rank;
    }

    [System.Serializable]
    public class subBoxOpen_Char {
        public int id;
        public string name;
        public string desc;
    }
}
