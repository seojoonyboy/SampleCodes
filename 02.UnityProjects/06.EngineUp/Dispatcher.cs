using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Text;

namespace Flux{
    public class Dispatcher<TPayload> where TPayload: class{
        public delegate void CallbackDelegate(TPayload payload);
        class Callback {    // Nested Class
            public CallbackDelegate callback;
            public bool isHandled;
            public bool isPending;
            public Callback(CallbackDelegate _callback){
                callback = _callback;
                isHandled = false;
                isPending = false;
            }
        }
        Dictionary<string, Callback> _callbacks;
        bool _isDispatching = false;
        uint _lastId = 1;
        TPayload _pendingPayload;
        string _prefix = "ID_";
        StringBuilder sb;
        public Dispatcher(){
            _callbacks = new Dictionary<string, Callback>();
            sb = new StringBuilder();
        }

        public string register(CallbackDelegate _callback){
            Assert.IsFalse(_isDispatching, "Dispatcher.register(...): Cannot register in the middle of a dispatch.");
            sb.Remove(0,sb.Length)
                .Append(_prefix)
                .Append(_lastId++);
            var id = sb.ToString();
            _callbacks[id] = new Callback(_callback);
            return id;
        }

        public void unregister(string id){
            Assert.IsFalse(_isDispatching, "Dispatcher.unregister(...): Cannot unregister in the middle of a dispatch.");
            Assert.IsFalse(_callbacks.ContainsKey(id),"Dispatcher.unregister(...): `"+id+"` does not map to a registered callback.");
            _callbacks.Remove(id);
        }

        public void waitFor(string[] ids){
            Assert.IsTrue(_isDispatching, "Dispatcher.waitFor(...): Must be invoked while dispatching.");
            for (var i=0; i<ids.Length; i++){
                var id = ids[i];
                if (_callbacks[id].isPending){
                    Assert.IsTrue(_callbacks[id].isHandled,"Dispatcher.waitFor(...): Circular dependency detected while waiting for `"+id+"`.");
                    continue;
                }
                Assert.IsTrue(_callbacks.ContainsKey(id), "Dispatcher.waitFor(...): `"+id+"` does not map to a registered callback.");
                _invokeCallback(id);
            }
        }

        public void dispatch(TPayload payload){
            Assert.IsFalse(_isDispatching,"Dispatch.dispatch(...): Cannot dispatch in the middle of a dispatch.");
            _startDispatching(payload);
            try {
                foreach (var cb in _callbacks){
                    if (cb.Value.isPending){ continue; }
                    _invokeCallback(cb.Key);
                }
            } finally {
                _stopDispatching();
            }
        }

        public bool isDispatching{
            get{ return _isDispatching; }
        }
        void _invokeCallback(string id){
            var cb = _callbacks[id];
            cb.isPending = true;
            cb.callback(_pendingPayload);
            cb.isHandled = true;
        }
        void _startDispatching(TPayload payload){
            foreach(var cb in _callbacks.Values){
                cb.isHandled = false;
                cb.isPending = false;
            }
            _pendingPayload = payload;
            _isDispatching = true;
        }
        void _stopDispatching(){
            _pendingPayload = null;
            _isDispatching = false;
        }
    }


}