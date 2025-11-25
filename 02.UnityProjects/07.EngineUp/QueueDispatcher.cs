using System.Collections.Generic;

namespace Flux{
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
}