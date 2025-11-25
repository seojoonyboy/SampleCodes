using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.EventSystems;


public class MyBossViewCtrler : MonoBehaviour {
    public GameObject
        upgradePanel,
        myBossList,
        buffInfoPanel;
    public int listPerRow;

    private int 
        lineIndex,
        upgradeInfoLength;
    private GameManager gameManager;
    private UIManager uiManager;
    private SoundManager soundManager;

    private GameObject[] panelLists;

    private Line lines;
    private int linePoint;

    private GameObject selectedList;

    void Start() {        
        Button backBtn = transform.Find("BottomPanel/BackBtn").GetComponent<Button>();
        backBtn.onClick.AddListener(onBackBtn);
    }

    void Awake() {
        uiManager = UIManager.Instance;
        soundManager = SoundManager.Instance;

        makeList();

        GameObject modalPanel = upgradePanel.transform.Find("ModalPanel").gameObject;
        Button cancelBtn = modalPanel.transform.Find("CancelBtn").gameObject.GetComponent<Button>();
        Button okBtn = modalPanel.transform.Find("OkBtn").gameObject.GetComponent<Button>();

        Text questionText = upgradePanel.transform.Find("ModalPanel/QuestionText").GetComponent<Text>();
        questionText.text = "정말 이 <color=red>라인</color>을 타시겠습니까?";

        cancelBtn.onClick.AddListener(onCancelModalBtn);
        okBtn.onClick.AddListener(onOkModalBtn);
    }
     
    void OnEnable() {
        gameManager = GameManager.Instance;
        
        lines = gameManager.line;
        linePoint = lines.linePoint;
        upgradeInfoLength = lines.lastLevelIndex;
        initialize();
    }

    void OnDisable() {
        buffInfoPanel.SetActive(false);
        upgradePanel.SetActive(false);
    }

    private void initialize() {
        for (int i = 0; i < listPerRow; i++) {
            GameObject activeHeaderTextObj = panelLists[i].transform.Find("ActiveHeaderText").gameObject;
            GameObject activeDescTextObj = panelLists[i].transform.Find("ActiveDescText").gameObject;
            GameObject passiveTextObj = panelLists[i].transform.Find("PassiveText").gameObject;

            Text activeHeaderText = activeHeaderTextObj.GetComponent<Text>();
            Text activeDescText = activeDescTextObj.GetComponent<Text>();
            Text passiveText = passiveTextObj.GetComponent<Text>();
            Text nameText = panelLists[i].transform.Find("NameText").GetComponent<Text>();

            Image charImg = panelLists[0].transform.Find("Image").GetComponent<Image>();
            List<LineItem>[] lineList = lines.lineList;
            if(upgradeInfoLength < lineList.Length) {
                if(lineList[upgradeInfoLength][i].effectType == EffectType.ACTIVE) {
                    BuffSkills skills = gameManager.buff;
                    activeHeaderTextObj.SetActive(true);
                    activeDescTextObj.SetActive(true);
                    passiveTextObj.SetActive(false);

                    activeHeaderText.text = skills[lineList[upgradeInfoLength][i].activeSkillID].name + " 스킬";
                    activeDescText.text = skills[lineList[upgradeInfoLength][i].activeSkillID].desc;
                }
                else {
                    activeHeaderTextObj.SetActive(false);
                    activeDescTextObj.SetActive(false);
                    passiveTextObj.SetActive(true);

                    passiveText.text = lines.lineList[upgradeInfoLength][i].desc;
                }
                nameText.text = lines.lineList[upgradeInfoLength][i].name;
                //DescText.text = lines.lineList[upgradeInfoLength][i].desc;

                charImg.sprite = uiManager.lineLeftBossImgArr[upgradeInfoLength];

                charImg = panelLists[1].transform.Find("Image").GetComponent<Image>();
                charImg.sprite = uiManager.lineRightBossImgArr[upgradeInfoLength];
            }
            else if (upgradeInfoLength >= lines.lineList.Length) {
                charImg.sprite = uiManager.lineLeftBossImgArr[upgradeInfoLength - 1];
                charImg = panelLists[1].transform.Find("Image").GetComponent<Image>();
                charImg.sprite = uiManager.lineRightBossImgArr[upgradeInfoLength - 1];
                //nameText.text = "최대 레벨 도달";
                //DescText.text = "최대 레벨 도달";
            }            

            Button btn = panelLists[i].GetComponent<Button>();
            btn.onClick.RemoveAllListeners();

            Image img = panelLists[i].GetComponent<Image>();
            
            GameObject deactiveObj = panelLists[i].transform.Find("DeactivePanel").gameObject;
            deactiveObj.SetActive(true);
            if (linePoint > 0) {
                btn.onClick.AddListener(onClickList);
                deactiveObj.SetActive(false);
                //img.color = activeColor;
            }            

            if(upgradeInfoLength < 3) {
                img.sprite = uiManager.lineBossPanelArr[0];
            }

            else if(upgradeInfoLength >= 3 && upgradeInfoLength < 7) {
                img.sprite = uiManager.lineBossPanelArr[1];
            }

            else if(upgradeInfoLength >= 7) {
                img.sprite = uiManager.lineBossPanelArr[2];
            }

            //exception
            else {
                img.sprite = uiManager.lineBossPanelArr[0];
            }
        }        
        
    }

    private void makeList() {
        panelLists = new GameObject[listPerRow];

        GameObject innerPanel = transform.Find("MaskLayout/InnerPanel/MyBossPanel").gameObject;

        for (int i = 0; i < listPerRow; i++) {
            if (panelLists[i] == null) {
                panelLists[i] = Instantiate(myBossList);
                panelLists[i].transform.SetParent(innerPanel.transform);
                panelLists[i].GetComponent<ListItem>().index = i;
                panelLists[i].AddComponent<Button>();

                RectTransform rect = panelLists[i].GetComponent<RectTransform>();
                rect.localScale = new Vector3(1, 1, 0);
            }
        }
    }

    IEnumerator LateCall() {
        yield return new WaitForSeconds(3.0f);
        Image img = buffInfoPanel.GetComponent<Image>();
        for (float i=1f; i>=0f; i -= 0.05f) {
            Color color = new Vector4(1, 1, 1, i);
            img.color = color;
            yield return 0;
        }
        img.color = new Vector4(1, 1, 1, 1);
        buffInfoPanel.SetActive(false);
    }

    public void onClickList() {
        selectedList = EventSystem.current.currentSelectedGameObject;
        lineIndex = selectedList.GetComponent<ListItem>().index;
        upgradePanel.SetActive(true);
        soundManager.SE_ON_MENU_BUTTON.Play();
    }

    public void onCancelModalBtn() {
        upgradePanel.SetActive(false);
        soundManager.SE_ON_BACK_BUTTON.Play();
    }

    public void onOkModalBtn() {
        upgradePanel.SetActive(false);
        gameManager.lineUpgrade(upgradeInfoLength, lineIndex);
        soundManager.SE_DUTY_UP.Play();

        OnEnable();
    }

    public void onBackBtn() {
        gameObject.SetActive(false);
        buffInfoPanel.SetActive(false);
        //uiManager.callLayout("MAIN");
        uiManager.menuBtns[3].GetComponent<Image>().sprite = uiManager.menuBtnDeActiveImgArr[3];
        soundManager.SE_ON_BACK_BUTTON.Play();
    }

    //현재 효과 보기
    public void showBuffDesc() {
        buffInfoPanel.SetActive(true);
        string passiveLists = "";
        string touchLists = "";
        string activeLists = "";
        string tmp = "";
        if(upgradeInfoLength == 0) {
            tmp = "효과 없음";
        }
        else {
            tmp = lines.summary.ToString();
            int touchVal = 0;
            int passiveVal = 0;
            for (int i=0; i<lines.upgradedInfo.Length; i++) {
                LineItem item = lines.lineList[i][lines.upgradedInfo[i]];
                if (item.effectType == EffectType.PASSIVE) {
                    if(item.passiveField == "touchGDR") {
                        touchVal += item.passiveValue;
                    }
                    else if(item.passiveField == "passiveGDR") {
                        passiveVal += item.passiveValue;
                    }
                }
                else if(item.effectType == EffectType.ACTIVE) {
                    activeLists += item.desc + "\n\n";
                }
            }
            touchLists = "곤대력 " + touchVal + "% 상승";
            passiveLists = "곤대력 " + passiveVal + "% 상승";
        }
        buffInfoPanel.transform.Find("ModalPanel/PassiveDescText").GetComponent<Text>().text = passiveLists;
        buffInfoPanel.transform.Find("ModalPanel/ActiveDescText").GetComponent<Text>().text = activeLists;
        buffInfoPanel.transform.Find("ModalPanel/TouchDescText").GetComponent<Text>().text = touchLists;

        //StartCoroutine(LateCall());
    }

    public void offBuffDesc() {
        buffInfoPanel.SetActive(false);
    }
}
