using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ResocialViewCtrler : MonoBehaviour {
    private GameManager gameManager;
    private UIManager uiManager;
    private EventManager eventManager;
    private SoundManager soundManager;

    public Sprite[] 
        btnPressedImgArr,
        btnDefaultImgArr;

    public GameObject[] btns;

    private GameObject mainPanel;

    private Text 
        descTxt,
        costTxt,
        redBtnText,
        blueBtnText;

    private Color32 preColor;
    void Awake() {
        uiManager = UIManager.Instance;
        soundManager = SoundManager.Instance;

        btnPressedImgArr = uiManager.resocialPressedImgArr;
        btnDefaultImgArr = uiManager.resocialDefaultImgArr;

        preColor = btns[0].GetComponent<Image>().color;
        mainPanel = gameObject.transform.Find("MainPanel").gameObject;
        initialize();
    }

    void Start() {
        eventManager = EventManager.Instance;
        eventManager.addListener(EventType.RESOCIALIZE, OnEvent);
    }

    void initialize() {
        GameObject panel = gameObject.transform.Find("BackBtnPanel").gameObject;
        Button backBtn = panel.transform.Find("BackBtn").GetComponent<Button>();
        backBtn.onClick.AddListener(onBackBtn);

        panel = gameObject.transform.Find("DescPanel").gameObject;
        descTxt = panel.transform.Find("DescText").GetComponent<Text>();

        costTxt = panel.transform.Find("CostPanel/Text").GetComponent<Text>();
    }

    void refresh() {
        gameManager = GameManager.Instance;
        Grade grade = gameManager.grade;
        blueBtnText = mainPanel.transform.Find("LeftPanel/TalkBaloon/LeftText").GetComponent<Text>();
        redBtnText = mainPanel.transform.Find("RightPanel/TalkBaloon/RightText").GetComponent<Text>();

        if (grade.nextSocialization == null) {
            descTxt.text = grade.currentSocialization.dialog;
            costTxt.text = "최대 레벨에 도달하였습니다.";

            blueBtnText.text = "";
            redBtnText.text = "";
        }
        else {
            descTxt.text = grade.nextSocialization.dialog;
            costTxt.text = "비용 : " + grade.nextSocialization.cost.ToString("N0") + "<color=yellow> 곤대력</color>";

            blueBtnText.text = grade.nextSocialization.answer[0];
            redBtnText.text = grade.nextSocialization.answer[1];
        }
    }

    void OnEnable() {
        refresh();
        soundManager.BGM_MAIN.Stop();
        soundManager.BGM_RESOCIAL.Play();

        if (upgradable()) {
            for(int i=0; i<btns.Length; i++) {
                btns[i].GetComponent<Image>().color = preColor;
                btns[i].GetComponent<Button>().onClick.AddListener(OnPressedResocialBtn);
            }
        }
        else {
            if (gameManager.grade.nextSocialization == null) {
                descTxt.text = "";
            }
            else {
                descTxt.text = "자넨 아직 준비가 안됐어! \n어딜 감히 발을 들이밀어?";
                blueBtnText.text = "";
                redBtnText.text = "";
            }
            for(int i=0; i<btns.Length; i++) {
                btns[i].GetComponent<Image>().color = new Color32(120, 120, 120, 255);
                btns[i].GetComponent<Button>().onClick.AddListener(OnPressedDisabledBtn);
            }
        }
    }

    void OnDisable() {
        for (int i = 0; i < btns.Length; i++) {
            //btns[i].GetComponent<Button>().onClick.RemoveListener(OnPressedResocialBtn);
            btns[i].GetComponent<Button>().onClick.RemoveAllListeners();
        }
        soundManager.BGM_RESOCIAL.Stop();
        soundManager.BGM_MAIN.Play();
        btns[0].GetComponent<Image>().sprite = btnDefaultImgArr[0];
        btns[1].GetComponent<Image>().sprite = btnDefaultImgArr[1];
    }

    public void onBackBtn() {
        gameObject.SetActive(false);
        soundManager.SE_ON_BACK_BUTTON.Play();        
        OffMenuBtn();
    }

    public void OnPressedResocialBtn() {
        gameManager.resocializing();
        eventManager.postNotification(EventType.RESOCIALIZE, this);
        GameObject selectedBtn = EventSystem.current.currentSelectedGameObject;
        int index = selectedBtn.GetComponent<ListItem>().index;
        selectedBtn.GetComponent<Image>().sprite = btnPressedImgArr[index];
        OffMenuBtn();
        pressedAnim();
    }

    public void OnPressedDisabledBtn() {
        soundManager.SE_CANNOT_RESOCIAL.Play();
    }

    public void offLayout() {
        gameObject.SetActive(false);
        gameObject.transform.Find("AnimEffectPanel").gameObject.SetActive(false);
    }

    private void OffMenuBtn() {
        uiManager.menuBtns[1].GetComponent<Image>().sprite = uiManager.menuBtnDeActiveImgArr[1];
    }

    public void pressedAnim() {
        GetComponent<Animator>().Play("Resocial");
    }

    private bool upgradable() {
        if (gameManager.grade.nextSocialization != null) {
            if (gameManager.grade.current >= gameManager.grade.maxGrade && gameManager.gondaeReuk >= gameManager.grade.nextSocialization.cost) {
                return true;
            }
            else {
                return false;
            }
        }
        return false;
    }

    public void OnEvent(EventType type, Component sender, object param = null) {
        switch (type) {
            case EventType.RESOCIALIZE:                
                break;
        }
    }
}
