using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BuffViewCtrler : MonoBehaviour {
    public GameObject
        upgradePanel,
        listPanel,
        itemPanel;

    public Sprite[] iconArr;

    private GameManager gameManager;
    private UIManager uiManager;
    private SoundManager soundManager;
    private EventManager eventManager;

    private int
        skillIndex,
        buffListNum;

    private Line line;
    private BuffSkills buffskillLists;

    private Text myCurrBless;
    private GameObject getItemBtnObj;
    private Button getItemBtn;
    private Color btnColor;

    void Awake() {
        uiManager = UIManager.Instance;
        eventManager = EventManager.Instance;

        iconArr = uiManager.buffIconArr;
        Button cancelBtn = upgradePanel.transform.Find("ModalPanel/CancelBtn").gameObject.GetComponent<Button>();
        Button okBtn = upgradePanel.transform.Find("ModalPanel/OkBtn").gameObject.GetComponent<Button>();

        cancelBtn.onClick.AddListener(onCancelBtn);
        okBtn.onClick.AddListener(onOkBtn);        
    }

    void Start() {
        eventManager.addListener(EventType.ADVERTISE_FINISHED, OnEvent);
    }

    void OnEnable() {
        gameManager = GameManager.Instance;
        soundManager = SoundManager.Instance;

        line = gameManager.line;
        buffListNum = line.upgradedInfo.Length;
        buffskillLists = gameManager.buff;
        makeList();
        setCurrentBlessTxt();
        InvokeRepeating("checkAdIsReady", 0f, 1f);
    }

    void OnDisable() {
        gameObject.SetActive(false);
        upgradePanel.SetActive(false);
        CancelInvoke("checkAdIsReady");
    }

    private void setCurrentBlessTxt() {
        myCurrBless = transform.Find("TopPanel/CurrBlessText").GetComponent<Text>();
        myCurrBless.text = "곤대신의 가호 X " + gameManager.blessOfGondae;
    }

    private void checkAdIsReady() {
        getItemBtn.onClick.RemoveListener(gameManager.getBlessOfGondae);
        if (gameManager.adIsReady) {
            Debug.Log("Can click buff btn");
            getItemBtn.onClick.AddListener(gameManager.getBlessOfGondae);
            getItemBtnObj.GetComponent<Image>().color = btnColor;
        }
        else {
            Debug.Log("Can not click buff btn");
            getItemBtnObj.GetComponent<Image>().color = new Color(66 / 255f, 66 / 255f, 66 / 255f);
        }
    }

    private void makeList() {
        RectTransform rect;

        GameObject innerPanel = transform.Find("MaskLayout/InnerPanel").gameObject;
        foreach(Transform child in innerPanel.transform) {
            Destroy(child.gameObject);
        }
        for(int i=0; i<buffskillLists.unlockedMap.Length; i++) {
            if (buffskillLists[i].isUnlocked) {
                GameObject list = Instantiate(listPanel);
                list.transform.SetParent(innerPanel.transform);
                list.GetComponent<ListItem>().index = i;
                list.name = "List" + i;

                rect = list.GetComponent<RectTransform>();
                rect.localScale = new Vector3(1, 1, 0);

                Text nameTxt = list.transform.Find("NameText").GetComponent<Text>();
                Text costTxt = list.transform.Find("CostPanel/Text").GetComponent<Text>();

                nameTxt.text = buffskillLists[i].name;
                costTxt.text = "X " + buffskillLists[i].cost.ToString();

                GameObject imageObj = list.transform.Find("Image").gameObject;
                Image img = imageObj.GetComponent<Image>();

                img.sprite = iconArr[i];

                Button panelBtn = list.AddComponent<Button>();
                panelBtn.onClick.RemoveAllListeners();
                panelBtn.onClick.AddListener(onModalPanel);
            }
        }
        GameObject bottomPanel = gameObject.transform.Find("BottomPanel").gameObject;
        getItemBtnObj = bottomPanel.transform.Find("BlessBtn").gameObject;
        getItemBtn = getItemBtnObj.GetComponent<Button>();
        Button backBtn = bottomPanel.transform.Find("DescPanel/BackBtn").GetComponent<Button>();
        btnColor = getItemBtnObj.GetComponent<Image>().color;

        backBtn.onClick.RemoveAllListeners();
        backBtn.onClick.AddListener(onBackBtn);
    }

    public void onBackBtn() {
        gameObject.SetActive(false);
        soundManager.SE_ON_BACK_BUTTON.Play();
        uiManager.menuBtns[2].GetComponent<Image>().sprite = uiManager.menuBtnDeActiveImgArr[2];
    }

    public void onModalPanel() {
        GameObject modalPanel = upgradePanel.transform.Find("ModalPanel").gameObject;
        upgradePanel.SetActive(true);
        skillIndex = EventSystem.current.currentSelectedGameObject.GetComponent<ListItem>().index;

        Image img = modalPanel.transform.Find("Image").GetComponent<Image>();

        Text nameText = modalPanel.transform.Find("NameText").GetComponent<Text>();
        Text descText = modalPanel.transform.Find("DescText").GetComponent<Text>();
        Text costText = modalPanel.transform.Find("CostPanel/Text").GetComponent<Text>();

        img.sprite = iconArr[skillIndex];
        BuffSkill skill = buffskillLists[skillIndex];
        nameText.text = skill.name;
        descText.text = skill.desc + " / " + skill.duration + "초";
        costText.text = skill.cost + "개 소모";

        Button modalOkbtn = modalPanel.transform.Find("OkBtn").GetComponent<Button>();
        Button modalCancelBtn = modalPanel.transform.Find("CancelBtn").GetComponent<Button>();

        modalCancelBtn.onClick.RemoveAllListeners();
        modalOkbtn.onClick.RemoveAllListeners();

        modalOkbtn.onClick.AddListener(onOkBtn);
        modalCancelBtn.onClick.AddListener(onCancelBtn);
    }

    public void onClickList() {
        upgradePanel.SetActive(true);
    }

    public void onCancelBtn() {
        upgradePanel.SetActive(false);
    }

    public void onOkBtn() {
        int myBlessOfGondae = gameManager.blessOfGondae;
        int skillCost = buffskillLists[skillIndex].cost;
        if(myBlessOfGondae >= skillCost) {
            gameManager.blessOfGondae -= skillCost;
            gameManager.runBuff(buffskillLists[skillIndex]);
            //갈구기 스킬인 경우 사운드 효과 재생
            if (skillIndex == 0) {
                soundManager.BGM_MAIN.Stop();
                soundManager.BGM_DISTRESS_SKILL.Play();
            }
        }
        upgradePanel.SetActive(false);
        OnEnable();
    }

    public void OnEvent(EventType type, Component sender, object param = null) {
        switch (type) {
            case EventType.ADVERTISE_FINISHED:
                setCurrentBlessTxt();
                break;
        }
    }
}