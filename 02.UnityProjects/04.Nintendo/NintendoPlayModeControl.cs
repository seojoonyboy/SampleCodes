using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using nn.hid;

using Ext;
using Ext.Async;

/// <summary>
/// Manage Nintendo Switch Play Mode
/// </summary>
namespace com.dalcomsoft.project.app.control.contents
{
    public class NintendoPlayModeControl : MonoBehaviour
    {
        //최대 2 Player
        private NpadId[] npadIds = { NpadId.Handheld, NpadId.No1, NpadId.No2 };

        //최대 4 Player
        //private NpadId[] npadIds = { NpadId.Handheld, NpadId.No1, NpadId.No2, NpadId.No3, NpadId.No4 };
        private NpadState npadState = new NpadState();

        public static NintendoPlayModeControl Instance { private set; get; }

        public delegate void OnNPadStyleChanaged(NpadStyle newPadStyle);
        private OnNPadStyleChanaged padStyleChangedCallback;

        private NpadStyle prevPadStyle;

        private Coroutine updateControllStateCoroutine;

        private bool isOpened = false;
        public bool IsOpened
        {
            get { return isOpened; }
            set { isOpened = value; }
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            
        }

        public void Open(OnNPadStyleChanaged callback = null)
        {
            DebugX.Log("Open NintendoPlayModeControl...");

            if (Instance == null)
            {
                DebugX.Log("NintendoPlayModeControl is null!");
                return;
            }

            this.CheckCurrentNPadStyle();

            this.padStyleChangedCallback += callback;

            CancellableSignal cancelSignal = new CancellableSignal();
            this.updateControllStateCoroutine = CoroutineTaskManager.AddTask(UpdateControllerState(cancelSignal));

            this.IsOpened = true;
        }

        public void Close()
        {
            this.IsOpened = false;

            this.CallbackReset();
        }

        public void AddPadStyleChangedEventListener(OnNPadStyleChanaged callback)
        {
            if (!IsOpened)
            {
                this.NotOpenedException();
                return;
            }

            this.padStyleChangedCallback += callback;
        }

        public void RemovePadStyleChangeEventListener(OnNPadStyleChanaged callback)
        {
            if (!IsOpened)
            {
                this.NotOpenedException();
                return;
            }

            this.padStyleChangedCallback -= callback;
        }

        private void NotOpenedException()
        {
            DebugX.LogError("NintendoPlayModeControl is not opened!");
        }

        public NpadStyle CheckCurrentNPadStyle()
        {
            NpadId npadId = NpadId.Handheld;
            NpadStyle npadStyle = NpadStyle.None;

            npadStyle = Npad.GetStyleSet(npadId);
            if (npadStyle != NpadStyle.Handheld)
            {
                npadId = NpadId.No1;
                npadStyle = Npad.GetStyleSet(npadId);
            }

            if(prevPadStyle != npadStyle)
            {
                this.padStyleChangedCallback?.Invoke(npadStyle);
            }

            this.prevPadStyle = npadStyle;
            //Debug.Log(string.Format("CheckCurrentNPadStyle : {0}", npadStyle));
            return npadStyle;
        }

        private Coroutine waitPressNPadButtonCoroutine = null;
        private Coroutine waitPressButtonCoroutine = null;

        public void WaitNPadButtonPressButton(CancellableSignal signal, NpadButton[] targetButtons, OnWaitPressButtonCallback callback)
        {
            this.waitPressNPadButtonCoroutine = CoroutineTaskManager
                .AddTask(_WaitNPadPressButton(signal, targetButtons, callback));
        }

        public delegate void OnWaitPressButtonCallback();
        private IEnumerator _WaitNPadPressButton(CancellableSignal signal, NpadButton[] targetButtons, OnWaitPressButtonCallback callback)
        {
            if (!this.IsOpened)
            {
                this.NotOpenedException();
                yield break;
            }

            while (true)
            {
                bool[] isPressedGroup = new bool[targetButtons.Length];

                for (int i = 0; i < npadIds.Length; i++)
                {
                    NpadId npadId = npadIds[i];
                    NpadStyle npadStyle = Npad.GetStyleSet(npadId);
                    if (npadStyle == NpadStyle.None) { continue; }

                    Npad.GetState(ref npadState, npadId, npadStyle);

                    
                    for(int j = 0; j < targetButtons.Length; j++)
                    {
                        if (npadState.GetButton(targetButtons[j]))
                        {
                            DebugX.Log(
                                string.Format("NPadButton {0} pressed", targetButtons[j])
                            );
                            isPressedGroup[j] = true;
                        }
                            
                    }
                }

                bool totalResult = true;
                for (int i = 0; i < isPressedGroup.Length; i++)
                {
                    if (isPressedGroup[i] == false)
                    {
                        //DebugX.Log(string.Format("Button {0} not pressed", targetButtons[i]));
                        totalResult = false;
                    }
                }

                if (totalResult)
                {
                    callback?.Invoke();
                    yield break;
                }

                if (!this.IsOpened) yield break;
                if (CancellableSignal.IsCanceled(signal)) yield break;

                yield return new WaitForEndOfFrame();
            }
        }

        public void WaitButtonPressButton(CancellableSignal signal, KeyCode[] targetButtons, OnWaitPressButtonCallback callback)
        {
            this.waitPressButtonCoroutine = CoroutineTaskManager
                .AddTask(_WaitKeyCodePressed(signal, targetButtons, callback));
        }

        private IEnumerator _WaitKeyCodePressed(CancellableSignal signal, KeyCode[] targetButtons, OnWaitPressButtonCallback callback)
        {
            if (!this.IsOpened)
            {
                this.NotOpenedException();
                yield break;
            }

            bool[] isPressedGroup = new bool[targetButtons.Length];

            while (true)
            {
                for(int i = 0; i < targetButtons.Length; i++)
                {
                    if (Input.GetKey(targetButtons[i])) 
                        isPressedGroup[i] = true;
                }

                bool totalResult = true;
                for(int i = 0; i < isPressedGroup.Length; i++)
                {
                    if (isPressedGroup[i] == false)
                    {
                        //DebugX.Log(string.Format("Button {0} not pressed", targetButtons[i]));
                        totalResult = false;
                    }
                }
                if (totalResult == true)
                {
                    callback?.Invoke();
                    yield break;
                }

                if (!this.IsOpened) yield break;
                if(CancellableSignal.IsCanceled(signal)) yield break;

                yield return new WaitForEndOfFrame();
            }
        }

        IEnumerator UpdateControllerState(CancellableSignal signal)
        {
            while (true)
            {
                yield return new WaitForSeconds(1.0f);
                this.CheckCurrentNPadStyle();

                if (CancellableSignal.IsCanceled(signal))
                    yield break;
            }
        }

        private void CallbackReset()
        {
            if (padStyleChangedCallback != null)
                padStyleChangedCallback = null;
        }

        private void OnDisable()
        {
            if (this.updateControllStateCoroutine != null)
                CoroutineTaskManager.RemoveTask(this.updateControllStateCoroutine);

            if(this.waitPressButtonCoroutine != null)
                CoroutineTaskManager.RemoveTask(this.waitPressButtonCoroutine);

            if(this.waitPressNPadButtonCoroutine != null)
                CoroutineTaskManager.RemoveTask(this.waitPressNPadButtonCoroutine);
        }
    }
}
