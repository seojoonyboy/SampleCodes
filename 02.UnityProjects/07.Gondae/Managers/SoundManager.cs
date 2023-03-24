using UnityEngine;
using System.Collections;

public class SoundManager : MonoBehaviour {
    private static SoundManager instance = null;

    public AudioSource SE_CANNOT_RESOCIAL;  //재사회화 버튼 클릭시 조건이 안되는 경우
    public AudioSource SE_ON_MENU_BUTTON;   //메뉴 버튼 클릭시
    public AudioSource SE_ON_BACK_BUTTON;   //뒤로가기 버튼 클릭시
    public AudioSource SE_GRADE_UP;         //직위 업그레이드시
    public AudioSource SE_DUTY_UP;          //업무 업그레이드시
    public AudioSource SE_TOUCH_CRITICAL;   //터치 극대화시

    public AudioSource BGM_MAIN;            //메인 테마곡
    public AudioSource BGM_DISTRESS_SKILL;  //갈구기 스킬 발동시 배경음
    public AudioSource BGM_ADVENT;          //강림 화면 전환시 배경음
    public AudioSource BGM_RESOCIAL;        //재사회화 화면 전환시 배경음
    public AudioSource[] BGM_STORY;         //스토리 화면 전환시 배경음 (전반부, 후반부)

    private EventManager eventManager;
    private GameManager gameManager;

    public static SoundManager Instance {
        get {
            return instance;
        }
    }

    void Awake() {
        if (instance != null && instance != this) {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        initialize();
    }

    void initialize() {
        eventManager = EventManager.Instance;
        gameManager = GameManager.Instance;

        eventManager.addListener(EventType.OPTION_CHANGE, OnEvent);

        GameObject audioSources = GameObject.Find("AudioSources").gameObject;

        SE_CANNOT_RESOCIAL = audioSources.transform.Find("Audio_Cannot_Resocial").GetComponent<AudioSource>();
        SE_ON_MENU_BUTTON = audioSources.transform.Find("Audio_Menu").GetComponent<AudioSource>();
        SE_ON_BACK_BUTTON = audioSources.transform.Find("Audio_Back_Button").GetComponent<AudioSource>();
        SE_GRADE_UP = audioSources.transform.Find("Audio_Grade_Up").GetComponent<AudioSource>();
        SE_DUTY_UP = audioSources.transform.Find("Audio_Duty_Up").GetComponent<AudioSource>();
        SE_TOUCH_CRITICAL = audioSources.transform.Find("Audio_Critical").GetComponent<AudioSource>();
        
        BGM_MAIN = audioSources.transform.Find("BGM_Main").GetComponent<AudioSource>();
        BGM_ADVENT = audioSources.transform.Find("BGM_Advent").GetComponent<AudioSource>();
        BGM_DISTRESS_SKILL = audioSources.transform.Find("BGM_Distress_Skill").GetComponent<AudioSource>();
        BGM_RESOCIAL = audioSources.transform.Find("BGM_Resocial").GetComponent<AudioSource>();
        BGM_STORY[0] = audioSources.transform.Find("BGM_Story_0").GetComponent<AudioSource>();
        BGM_STORY[1] = audioSources.transform.Find("BGM_Story_1").GetComponent<AudioSource>();
    }

    public void setBgm(bool isOn) {
        BGM_MAIN.mute = !isOn;
        BGM_ADVENT.mute = !isOn;
        BGM_DISTRESS_SKILL.mute = !isOn;
        BGM_RESOCIAL.mute = !isOn;
        BGM_STORY[0].mute = !isOn;
        BGM_STORY[1].mute = !isOn;
    }

    public void setSe(bool isOn) {
        SE_CANNOT_RESOCIAL.mute = !isOn;
        SE_ON_MENU_BUTTON.mute = !isOn;
        SE_ON_BACK_BUTTON.mute = !isOn;
        SE_DUTY_UP.mute = !isOn;
        SE_GRADE_UP.mute = !isOn;
        SE_TOUCH_CRITICAL.mute = !isOn;
    }

    public void OnEvent(EventType type, Component sender, object param = null) {
        switch (type) {
            case EventType.OPTION_CHANGE:
                bool SE = gameManager.config.sfx;
                bool BGM = gameManager.config.bgm;
                setBgm(BGM);
                setSe(SE);
                break;
        }
    }
}
