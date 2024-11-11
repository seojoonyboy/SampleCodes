using UnityEngine.Events;
using UnityEngine.Assertions;

namespace Flux{
    public abstract class Store<TPayload> where TPayload : class{
        UnityEvent changeEvent;
        string _dispatchToken;
        Dispatcher<TPayload> _dispatcher;
        bool _changed;
        string _className;


        public Store(Dispatcher<TPayload> dispatcher){
            _className = this.GetType().Name;
            _changed = false;
            _dispatcher = dispatcher;
            changeEvent = new UnityEvent();
            _dispatchToken = dispatcher.register(_invokeOnDispatch);
        }
        public void addListener(UnityAction callback){
            changeEvent.AddListener(callback);
        }
        public Dispatcher<TPayload> dispatcher{
            get{ return _dispatcher; }
        }
        public string dispatchToken{
            get{ return _dispatchToken; }
        }
        public bool changed{
            get{
                Assert.IsTrue(_dispatcher.isDispatching, _className+".changed: Must be invoked while dispatching.");
                return _changed;
            }
        }
        protected void _emitChange(){
            Assert.IsTrue(_dispatcher.isDispatching, _className+"._emmetChange(): Must be invoked while dispatching.");
            _changed = true;
        }
        protected void _invokeOnDispatch(TPayload payload){
            _changed = false;
            _onDispatch(payload);
            if(_changed){
                changeEvent.Invoke();
            }
        }
        protected abstract void _onDispatch(TPayload payload);
    }
}