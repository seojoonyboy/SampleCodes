using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ScottGarland;

public class MinionViewCtrler : MonoBehaviour {
    public GameObject listPanel;
    private GameManager gameManager;
    private UIManager uiManager;
    private SoundManager soundManager;
    private EventManager eventManager;

    private int
        minionIndex,
        minionListNum;
    private GameObject[] panelLists;
    private Grade gradeLists;

    private GameObject selectedPanel;

    void Awake() {
        uiManager = UIManager.Instance;
        soundManager = SoundManager.Instance;
        eventManager = EventManager.Instance;

        gameManager = GameManager.Instance;
        gradeLists = gameManager.grade;
        
        makeList();
    }

    void Start() {
        eventManager.addListener(EventType.RESOCIALIZE, OnEvent);
        eventManager.addListener(EventType.GRADE_CHANGE, OnEvent);
    }

    void Update() {
        checkCanUpgrade();
    }

    public void onBackBtn() {
        gameObject.SetActive(false);
    }

    private void makeList() {
        int prevMaxGrade = 0;
        GameObject mainPanel = transform.Find("MaskLayout/InnerPanel/MinionPanel").gameObject;
        foreach (Transform child in mainPanel.transform) {
            Destroy(child.gameObject);
        }

        int currentMaxGrade = gradeLists.currentSocialization.maxGrade;
        if(gradeLists.socializationLevel == 0) {
            prevMaxGrade = 0;
        }
        else if(gradeLists.socializationLevel != 0) {
            prevMaxGrade = gradeLists.prevSocialization.maxGrade;
        }
        panelLists = new GameObject[currentMaxGrade];

        for (int i = 0; i < currentMaxGrade; i++) {
            panelLists[i] = Instantiate(listPanel);
            panelLists[i].transform.SetParent(mainPanel.transform);
            panelLists[i].name = "List" + i;
            if(gradeLists.current <= prevMaxGrade) {
                if(i < prevMaxGrade) {
                    panelLists[i].SetActive(true);
                }
            }
            else if(gradeLists.current > prevMaxGrade) {
                if(i < gradeLists.current) {
                    panelLists[i].SetActive(true);
                }
            }
            RectTransform rect = panelLists[i].GetComponent<RectTransform>();
            rect.localScale = new Vector3(1, 1, 0);

            Text DescTxt = panelLists[i].transform.Find("DescText").GetComponent<Text>();
            Text PassiveAmntTxt = panelLists[i].transform.Find("PassivePanel/PassiveAmountText").GetComponent<Text>();

            Text CostAmntTxt = panelLists[i].transform.Find("CostAmountPanel/CostAmountText").GetComponent<Text>();

            Text LvTxt = panelLists[i].transform.Find("LVText").GetComponent<Text>();
            Text passiveDescTxt = panelLists[i].transform.Find("PassiveDescText").GetComponent<Text>();

            int level = gradeLists[i].minionsLevel;
            LvTxt.text = level.ToString();

            DescTxt.text = gradeLists[i].name + "급";
            if(level == 0) {
                PassiveAmntTxt.text = " - ";
            }
            else {
                PassiveAmntTxt.text = gradeLists[i].passiveRunTime + " 초";
            }            
            passiveDescTxt.text = gameManager.dutySkills.skills[i].name.ToString();

            string str = "";
            string cost = gradeLists.gradeList[i].minionLvupCost.ToString();
            char[] tmp = cost.ToCharArray();
            int cnt = 0;
            for (int j = tmp.Length - 1; j > -1; j--) {
                if (cnt == 3) {
                    str = "," + str;
                    cnt = 0;
                }
                str = tmp[j] + str;
                cnt++;
            }
            CostAmntTxt.text = str;

            panelLists[i].GetComponent<ListItem>().index = i;
            panelLists[i].AddComponent<Button>();

            Image img = panelLists[i].transform.Find("Image").GetComponent<Image>();
            img.sprite = uiManager.lineMinionImgArr[i];

            img = panelLists[i].GetComponent<Image>();
            if (i < 3) {
                img.sprite = uiManager.lineMinionPanelArr[0];
            }

            else if(3 <= i && i < 7) {
                img.sprite = uiManager.lineMinionPanelArr[1];
            }

            else if(i >= 7) {
                img.sprite = uiManager.lineMinionPanelArr[2];
            }
        }
    }
    
    private void refreshTxt(int index) {
        gradeLists = gameManager.grade;

        Text DescTxt = panelLists[index].transform.Find("DescText").GetComponent<Text>();

        DescTxt.text = gradeLists[index].name + "급";

        Text PassiveAmntTxt = panelLists[index].transform.Find("PassivePanel/PassiveAmountText").GetComponent<Text>();
        Text CostAmntTxt = panelLists[index].transform.Find("CostAmountPanel/CostAmountText").GetComponent<Text>();

        PassiveAmntTxt.text = gradeLists[index].passiveRunTime + " 초";

        string str = "";
        string cost = gradeLists.gradeList[index].minionLvupCost.ToString();
        char[] tmp = cost.ToCharArray();
        int cnt = 0;
        for(int i=tmp.Length - 1; i>-1; i--) {
            if(cnt == 3) {
                str = "," + str;
                cnt = 0;
            }
            str = tmp[i] + str;
            cnt++;
        }
        CostAmntTxt.text = str;

        Text LvTxt = panelLists[index].transform.Find("LVText").GetComponent<Text>();
        LvTxt.text = gradeLists[index].minionsLevel.ToString();
    }

    public void unlockMinion() {
        int index = gradeLists.current - 1;
        panelLists[index].SetActive(true);
    }

    public void checkCanUpgrade() {
        for (int i = 0; i < panelLists.Length; i++) {
            if(panelLists[i].activeSelf) {
                Button panelBtn = panelLists[i].GetComponent<Button>();
                //곤대력이 충분하면 버튼 리스너에 클릭 이벤트 등록
                panelBtn.onClick.RemoveAllListeners();
                GameObject deactivePanel = panelLists[i].transform.Find("DeactivePanel").gameObject;

                if (gameManager.gondaeReuk >= new BigInteger(gradeLists[i].minionLvupCost)) {
                    panelBtn.onClick.AddListener(onClickList);
                    deactivePanel.SetActive(false);
                }
                //충분하지 않으면 버튼 이벤트 제거
                else {
                    panelBtn.onClick.RemoveListener(onClickList);
                    deactivePanel.SetActive(true);
                }
            }
        }
    }

    public void onClickList() {
        selectedPanel = EventSystem.current.currentSelectedGameObject;
        minionIndex = selectedPanel.GetComponent<ListItem>().index;

        soundManager.SE_DUTY_UP.Play();

        if (gameManager.minionLvUp(minionIndex)) {
            refreshTxt(minionIndex);
        }
    }

    public void OnEvent(EventType type, Component sender, object param = null) {
        switch (type) {
            case EventType.RESOCIALIZE:
                makeList();
                break;
            case EventType.GRADE_CHANGE:
                unlockMinion();
                break;
        }
    }
}
