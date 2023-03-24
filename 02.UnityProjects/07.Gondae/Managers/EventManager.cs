using UnityEngine;
using System.Collections.Generic;
using System;

public enum EventType { HEALTH_CHANGE, GDR_CHANGE, RESOCIALIZE, GRADE_CHANGE, OPTION_CHANGE, ADVERTISE_FINISHED };

public class EventManager : MonoBehaviour {
    public delegate void OnEvent(EventType type, Component sender, object param = null);
    private Dictionary<EventType, List<OnEvent>> Listeners = new Dictionary<EventType, List<OnEvent>>();
    private static EventManager instance = null;

    private ModalQueue modalQueue;
    public bool canDequeue;

    private UIManager uiManager;

    public static EventManager Instance {
        get {
            return instance;
        }
    }

    void Start() {
        modalQueue = new ModalQueue();
        canDequeue = true;

        uiManager = UIManager.Instance;
    }

    void Awake() {
        if (instance != null && instance != this) {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update() {
        if (canDequeue && modalQueue.Length() > 0) {
            ModalQueueMessage message = modalQueue.dequeue();
            sendModalEvent(message);
            canDequeue = false;
        }
    }

    public void addListener(EventType type, OnEvent Listener) {
        List<OnEvent> ListenList = null;

        if (Listeners.TryGetValue(type, out ListenList)) {
            ListenList.Add(Listener);
            return;
        }

        //해당 Type의 List가 존재하지 않는 경우 그 Type에 대한 새로운 List를 생성한다.
        ListenList = new List<OnEvent>();
        ListenList.Add(Listener);
        Listeners.Add(type, ListenList);
    }

    public void postNotification(EventType type, Component sender, object param = null) {
        List<OnEvent> ListenList = null;

        if (!Listeners.TryGetValue(type, out ListenList))
            return;

        //Listeners 항목이 존재하는 경우 적합한 리스너에게 알려준다.
        for (int i = 0; i < ListenList.Count; i++) {
            //delegate를 통해 메시지를 보낸다
            if (!ListenList[i].Equals(null))
                ListenList[i](type, sender, param);
        }
    }

    public void receiveModalEvent(ModalQueueMessage message){
        modalQueue.enqueue(message);
    }

    private void sendModalEvent(ModalQueueMessage message) {
        ModalEventType eventType = message.type;
        System.Object msg = message.payload;
        switch(eventType){
            case ModalEventType.STORY:
                Chapter chapter = msg as Chapter;
                uiManager.onStoryModal(chapter);
                break;
            case ModalEventType.HPZERO:
                uiManager.onHpZeroModal();
                break;
            case ModalEventType.RESOCIAL_RESULT_DIALOGUE:
                if(msg != null) {
                    Type type = msg.GetType();
                    if (type.Equals(typeof(string))) {
                        uiManager.onResoicalResultDialogueModal(true);
                    }
                }
                else {
                    uiManager.onResoicalResultDialogueModal();
                }                
                break;
            default:
                break;
        }
    }
}