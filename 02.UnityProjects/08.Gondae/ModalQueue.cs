using System.Collections.Generic;

public enum ModalEventType { HPZERO, GRADEUP, STORY, RESOCIAL_RESULT_DIALOGUE };

public class ModalQueue {
    Queue<ModalQueueMessage> queue;
    public ModalQueue() {
        queue = new Queue<ModalQueueMessage>();
    }
    
    public void enqueue(ModalEventType type, System.Object payload) {
        ModalQueueMessage insertQueue = new ModalQueueMessage(type, payload);
        
        queue.Enqueue(insertQueue);
    }

    public void enqueue (ModalQueueMessage message){
        queue.Enqueue(message);
    }

    public ModalQueueMessage dequeue() {
        return queue.Dequeue();
    }

    public int Length() {
        return queue.Count;
    }
}

public class ModalQueueMessage {
    public ModalEventType type;
    public System.Object payload;
    public ModalQueueMessage(ModalEventType t, System.Object p = null){
        type = t;
        payload = p;
    }
}