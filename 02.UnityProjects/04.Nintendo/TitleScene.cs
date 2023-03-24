#if !UNITY_EDITOR && UNITY_SWITCH
using nn;
using nn.hid;
#else
using nn.hid;
#endif

using UnityEngine;

using com.dalcomsoft.project.app.control;
using com.dalcomsoft.project.app.control.based;
using com.dalcomsoft.project.app.control.contents;
using com.dalcomsoft.project.app.view.scene;
using com.dalcomsoft.project.app.view.switch_popup;
using com.dalcomsoft.project.client.model.type;

using Ext.Async;
using Ext;

namespace com.dalcomsoft.project.app.scene
{
    public class TitleScene : Scene
    {
        public enum STATE
        {
            Intro,
            Loading,
            LoadingDone,
            LoginAuto
        }

        private TitleView sceneView;
        private AssetControl.STATE loadingState;

        protected override void OnOpen()
        {
            this.sceneView = (TitleView)this.SceneViewComponent;
            BgmPlayer.PlaySingle(AssetControl.BGM_MAIN);

            this.SetStateIntro();
        }

        protected override void OnClose() { }

        protected override void OnBack() { }

        void SetStateLoading()
        {
            this.SetState(STATE.Loading);

            this.sceneView.ShowLoading();
            this.sceneView.LoadingText.text = "불러오는 중...";

            AssetControl.Open(false);
            this.AddStateTask(() => {
                switch (AssetControl.State)
                {
                    case AssetControl.STATE.Done:
                        this.SetStateLoadingDone();
                        break;
                }
            });
        }

        void SetStateLoadingDone()
        {
            DebugX.Log("SetStateLoadingDone...");

            this.SetState(STATE.LoadingDone);

            NintendoPlayModeControl.Instance.Open();
            this.ExecuteLogin();
        }

        void SetStateIntro()
        {
            this.SetState((int)STATE.Intro);

            this.sceneView.ShowIntro();

            //타이틀 배경음 재생
            //SoundFx.Play(AssetControl.SOUND_TITLE);

            //TODO : 한번만 호출되야 하는데 SetStateLoadingDone 계속 호출됨....
            this.AddStateTask(() =>
            {
                if (this.sceneView.IsIntroAnimCompleted)
                {
                    this.SetState((int)STATE.LoadingDone);

                    this.sceneView.HideLoading();
                    this.SetStateLoading();
                }
            });
        }

        /// <summary>
        /// 로그인 처리
        /// </summary>
        private void ExecuteLogin()
        {
            //추후 로그인 추가 과정이 생길 수 있음
            //우선은 자동 로그인 처리 강제
            this.SetStateLoginAuto();
        }

        private void SetStateLoginAuto()
        {
            this.SetState(STATE.LoginAuto);

            DebugX.Log("SetStateLoginAuto...");

            LoginControl.Instance.LoginAuto((responseCode, list) =>
            {
                switch (responseCode)
                {
                    case ResponseCode.OK:
                        InventoryControl.Open();

                        if (UserControl.ProfileCode == -1)
                        {
                            //TODO : 닌텐도 계정의 국가, 언어 정보에 따라 default 세팅
                            UserControl.Language = Language.ko;
                            UserControl.Country = Country.KR;

                            NicknameInputPopup.Params nicknameParm = new NicknameInputPopup.Params();
                            nicknameParm.NintendoNickname = UserControl.Nickname;

                            ViewControl.OpenPopup("Custom/Profile/NicknameInputPopup", nicknameParm, (pop, btn) => {
                                ProfileSelectPopup.Params profileImageParm = new ProfileSelectPopup.Params();
                                profileImageParm.isFirstAccess = true;

                                ViewControl.OpenPopup("Custom/Profile/ProfileSelectPopup", profileImageParm, (_pop, _btn) =>
                                {
                                    if (_btn.isOk)
                                    {
                                        UserControl.SaveData();

                                        this.WaitInputToLobby();
                                    }
                                });
                            });
                        }
                        else
                        {
                            WaitInputToLobby();
                        }
                        break;
                    case ResponseCode.NO_USER_DATA:
                    case ResponseCode.ALREADY_MOUNT_DATA:
                        //TODO : 닌텐도 계정의 국가, 언어 정보에 따라 default 세팅
                        UserControl.Language = Language.ko;
                        UserControl.Country = Country.KR;

                        NicknameInputPopup.Params _nicknameParm = new NicknameInputPopup.Params();
                        _nicknameParm.NintendoNickname = UserControl.Nickname;

                        ViewControl.OpenPopup("Custom/Profile/NicknameInputPopup", _nicknameParm, (pop, btn) => {
                            ProfileSelectPopup.Params profileImageParm = new ProfileSelectPopup.Params();
                            profileImageParm.isFirstAccess = true;

                            ViewControl.OpenPopup("Custom/Profile/ProfileSelectPopup", profileImageParm, (_pop, _btn) =>
                            {
                                if (_btn.isOk)
                                {
                                    UserControl.SaveData();
                                    WaitInputToLobby();
                                }
                            });
                        });
                        break;
                    case ResponseCode.EXCEPT:
                        this.WaitInputToLobby();
                        Debug.Log("Login Failed !");
                        break;
                }
            });
        }

        private void WaitInputToLobby()
        {
            DebugX.Log("WaitInputToLobby");
#if UNITY_EDITOR
            bool isWaitFinished = false;
            CancellableSignal signal = new CancellableSignal(() => isWaitFinished);
            KeyCode[] targetButtons = new KeyCode[] { KeyCode.Space };

            NintendoPlayModeControl.Instance.WaitButtonPressButton(signal, targetButtons, () => {
                isWaitFinished = true;
                this.SetStateOpenScene(SceneControl.SCENE_TYPE.Lobby);
            });
            this.sceneView.LoadingText.text = "Space 버튼을 눌러주세요";
#elif !UNITY_EDITOR && UNITY_SWITCH
            this.ShowSwitchControllerApplet();
#endif
        }

#if !UNITY_EDITOR && UNITY_SWITCH
        private void ShowSwitchControllerApplet()
        {
            // Normal start.
            ControllerSupportArg supportArg = new ControllerSupportArg();
            supportArg.SetDefault();
        
            NpadJoy.SetHoldType(NpadJoyHoldType.Horizontal);
            Npad.SetSupportedIdType(new NpadId[] { NpadId.No1 });
        
            Result res = ControllerSupport.Show(supportArg);
            if(res.IsSuccess()) {
                NpadId npadId = NpadId.Handheld;
                NpadStyle npadStyle = Npad.GetStyleSet(npadId);
                if (npadStyle != NpadStyle.Handheld)
                {
                    this.SetStateOpenScene(SceneControl.SCENE_TYPE.Lobby);
                }
                else{
                    this.ShowSwitchControllerApplet();
                }
            }
            Debug.Log("OpenApplet Result : " + res.innerValue);
        }
#endif

        private void OnPadStyleChanged(NpadStyle nPadStyle)
        {
            bool isWaitFinished = false;

            if (nPadStyle == NpadStyle.JoyDual)
            {
                DebugX.Log("NpadStyle JoyDual detected");

                CancellableSignal signal = new CancellableSignal(() => isWaitFinished);
                NpadButton[] targetButtons = new NpadButton[] { NpadButton.L, NpadButton.R };
                NintendoPlayModeControl.Instance.WaitNPadButtonPressButton(signal, targetButtons, () => {
                    isWaitFinished = true;
                    NintendoPlayModeControl.Instance.RemovePadStyleChangeEventListener(this.OnPadStyleChanged);

                    this.SetStateOpenScene(SceneControl.SCENE_TYPE.Lobby);
                });

                this.sceneView.LoadingText.text = "L + R 버튼을 눌러주세요";
            }
            else
            {
                this.sceneView.LoadingText.text = "Joycon을 분리해 주세요";
            }
        }
    }
}