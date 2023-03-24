using UnityEngine;
using UnityEngine.UI;
using com.dalcomsoft.project.app.control;
using com.dalcomsoft.project.app.control.contents;

namespace com.dalcomsoft.project.app.view.switch_popup
{
    public class InputModeSelectPopup : SwitchPopup
    {
        [Header("SubMenu Root")]
        [SerializeField] GameObject Menu1, Menu2;

        [Header("SubMenu's Buttons")]
        [SerializeField] Toggle[] MenuButtons1, MenuButtons2;

        [SerializeField]
        Font globalFont, korFont;

        //메뉴 대분류 Index ...
        int menuIndex = 0;
        public int MenuIndex
        {
            get
            {
                return menuIndex;
            }
            set
            {
                menuIndex = value;
            }
        }

        //메뉴 소분류 Index ...
        int selectIndex = 0;
        public int SelectIndex
        {
            get
            {
                return selectIndex;
            }
            set
            {
                Toggle[] toggles = MenuIndex == 0 ? MenuButtons1 : MenuButtons2;
                if (value < 0) value = 0;
                if (value >= toggles.Length) value = toggles.Length - 1;
                selectIndex = value;

                Debug.Log("Index : " + selectIndex);
            }
        }

        protected override void OnBuild()
        {
            base.OnBuild();

            Menu1.SetActive(true);
            MenuIndex = 0;
            SelectIndex = 0;
            MenuButtons1[SelectIndex].isOn = true;

            StartCoroutine(WaitEvent());
        }

        System.Collections.IEnumerator WaitEvent()
        {
            yield return new WaitForSeconds(0.2f);
            AddControllerListenrs();
        }
         
        private void AddControllerListenrs()
        {
            SwitchInputControl.Parm p = new SwitchInputControl.Parm();
            p.caller = gameObject;
            p.consecutiveBtnCall = true;
            p.LeftWheelMoved = ClickedLeftWheel;
            p.RightWheelMoved = ClickedRightWheel;
            p.OnButtonClicked = ClickedButton;

            SwitchInputControl.inst.AddListener(p);
        }

        private void RemoveControllerListeners()
        {
            if (SwitchInputControl.inst == null) return;

            SwitchInputControl.inst.RemoveListener(gameObject);
        }

        private void ClickedButton(SwitchInputControl.Keys key)
        {
            switch (key)
            {
                case SwitchInputControl.Keys.A:
                    OnSelect();
                    break;
                case SwitchInputControl.Keys.B:
                    OnCancel();
                    break;
            }
        }

        private void ClickedRightWheel(Direction dir)
        {
            Toggle[] toggles = MenuIndex == 0 ? MenuButtons1 : MenuButtons2;
            switch (dir)
            {
                case Direction.LEFT:
                    SoundFx.Play(AssetControl.SOUND_EQUIP);
                    SelectIndex--;
                    toggles[SelectIndex].isOn = true;
                    break;
                case Direction.RIGHT:
                    SoundFx.Play(AssetControl.SOUND_EQUIP);
                    SelectIndex++;
                    toggles[SelectIndex].isOn = true;
                    break;
            }
        }

        private void ClickedLeftWheel(Direction wheel)
        {
            Toggle[] toggles = MenuIndex == 0 ? MenuButtons1 : MenuButtons2;
            switch (wheel)
            {
                case Direction.LEFT:
                    SoundFx.Play(AssetControl.SOUND_EQUIP);
                    SelectIndex--;
                    toggles[SelectIndex].isOn = true;
                    break;
                case Direction.RIGHT:
                    SoundFx.Play(AssetControl.SOUND_EQUIP);
                    SelectIndex++;
                    toggles[SelectIndex].isOn = true;
                    break;
            }
        }

        protected override void TriggerOnOk()
        {
            RemoveControllerListeners();
            base.TriggerOnOk();
        }

        protected override void TriggerOnX()
        {          
            RemoveControllerListeners();
            base.TriggerOnX();
        }

        private void OnCancel()
        {
            if(MenuIndex == 0)
            {
                SoundFx.Play(AssetControl.SOUND_CLOSE);
                TriggerOnX();
            }
            else
            {
                Menu2.SetActive(false);
                Menu1.SetActive(true);
                MenuIndex = 0;
                SelectIndex = 0;
                MenuButtons1[SelectIndex].isOn = true;
            }
        }

        private void OnSelect()
        {
            //인풋 모드 선택완료
            if(MenuIndex == 0)
            {
#if UNITY_EDITOR
                SoundFx.Play(AssetControl.SOUND_DECIDE);
                RemoveControllerListeners();
                SwitchConfig.KeyInput = SelectIndex;

                Menu1.SetActive(false);
                Menu2.SetActive(true);

                SelectIndex = 0;
                MenuIndex++;
                StartCoroutine(WaitEvent());

#elif !UNITY_EDITOR && UNITY_SWITCH
                //Motion을 선택했는데 NPadStyle이 Handheld인 경우
                //Joycon 분리 유도
                var nPadStyle = NintendoPlayModeControl.Instance.CheckCurrentNPadStyle();
                if((nPadStyle == nn.hid.NpadStyle.Handheld || nPadStyle == nn.hid.NpadStyle.None) && SelectIndex == 1)
                {
                    AlertPopup.Params p = new AlertPopup.Params();

                    p.activeTime = 2.0f;
                    p.rewardType = AlertPopup.RewardType.RhythmPoint;
                    p.context = "Joycon을 분리해 주세요!";

                    ViewControl.OpenPopup("System/AlertPopup", p, (pop, btn) => { });
                }
                else
                {
                    SoundFx.Play(AssetControl.SOUND_DECIDE);
                    RemoveControllerListeners();
                    SwitchConfig.KeyInput = SelectIndex;

                    Menu1.SetActive(false);
                    Menu2.SetActive(true);

                    SelectIndex = 0;
                    MenuIndex++;
                    StartCoroutine(WaitEvent());
                }
#endif
            }

            //싱글 멀티 모드 선택완료
            else if(MenuIndex == 1)
            {
                SoundFx.Play(AssetControl.SOUND_DECIDE);

#if UNITY_EDITOR
                SwitchConfig.MULTI_MODE = SelectIndex == 0 ? false : true;
                TriggerOnOk();
#elif !UNITY_EDITOR && UNITY_SWITCH
                var nPadStyle = NintendoPlayModeControl.Instance.CheckCurrentNPadStyle();
                if((nPadStyle == nn.hid.NpadStyle.Handheld || nPadStyle == nn.hid.NpadStyle.None) && SelectIndex == 1)
                {
                    AlertPopup.Params p = new AlertPopup.Params();

                    p.activeTime = 2.0f;
                    p.rewardType = AlertPopup.RewardType.RhythmPoint;
                    p.context = "Joycon을 분리해 주세요!";

                    ViewControl.OpenPopup("System/AlertPopup", p, (pop, btn) => { });
                }
                else{
                    SwitchConfig.MULTI_MODE = SelectIndex == 0 ? false : true;
                    TriggerOnOk();
                }
#endif
            }
        }
    }
}
