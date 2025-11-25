using UnityEngine;
using UnityEngine.UI;

public class PortraitViewCtrler : MonoBehaviour {
    private GameManager gameManager;
    private UIManager uiManager;
    private EventManager eventManager;

    private Button btn;
    private Text gradeTxt;
    private Image portraitImg;

    void Awake() {
        initialize();

        eventManager.addListener(EventType.GRADE_CHANGE, OnEvent);
        eventManager.addListener(EventType.RESOCIALIZE, OnEvent);
    }

    void initialize() {
        gameManager = GameManager.Instance;
        uiManager = UIManager.Instance;
        eventManager = EventManager.Instance;

        btn = gameObject.transform.Find("OkBtn").GetComponent<Button>();
        btn.onClick.AddListener(offModal);

        GameObject modalPanel = gameObject.transform.Find("ModalPanel").gameObject;
        gradeTxt = modalPanel.transform.Find("GradeText").GetComponent<Text>();
        portraitImg = modalPanel.transform.Find("Portrait").GetComponent<Image>();
        chanageInfo();
    }

    public void onModal() {        
        gameObject.SetActive(true);
    }

    public void offModal() {
        gameObject.SetActive(false);
    }

    private void chanageInfo() {
        Grade grade = gameManager.grade;

        gradeTxt.text = grade.currentGradeName.ToString();
        
        int gradeIndex = grade.current;
        
        int portraitIndex = 0;

        if (gradeIndex >= 0 && gradeIndex <= 2) {
            portraitIndex = 0;
        }
        else if (gradeIndex > 2 && gradeIndex <= 4) {
            portraitIndex = 1;
        }
        else if (gradeIndex > 4 && gradeIndex <= 6) {
            portraitIndex = 2;
        }
        else if(gradeIndex > 6 && gradeIndex <= 7) {
            portraitIndex = 3;
        }
        else {
            portraitIndex = 4;
        }
        portraitImg.sprite = uiManager.myCardsPortraitImgArr[portraitIndex];
    }

    public void OnEvent(EventType type, Component sender, object param = null) {
        switch (type) {
            case EventType.GRADE_CHANGE:
                chanageInfo();
                break;

            case EventType.RESOCIALIZE:
                chanageInfo();
                break;
        }
    }
}
