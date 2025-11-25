using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MainViewCtrler : MonoBehaviour {
    public GameObject 
        gradeListPrefab,
        skyPrefab,
        groundPrefab,
        upgradePanelBg,
        upgradePanelFg,
        hpExhaustPanel,
        unlockListPrefabFg,
        unlockListPrefabBg;    

    private GameObject selectedPanel;

    public GameObject[] panelLists;
    private GradeItem[] gradeLists;   
     
    private GameManager gameManager;
    private UIManager uiManager;
    private EventManager eventManager;
    private SoundManager soundManager;

    private int
        gradeListNum,
        gradeIndex,
        currentGrade;

    private bool 
        canEffect = true,
        canTouchMyChar = true;

    private float time = 0;

    void Update() {
        //checkCanUpgrade();
        time += Time.deltaTime;

        if(time > 5) {
            underOverDialogueEffect();
            time = 0;
            offMyCharBtn();
        }
    }

    void Awake() {        
        initialize();
    }

    private void initialize() {
        gameManager = GameManager.Instance;
        uiManager = UIManager.Instance;
        eventManager = EventManager.Instance;
        soundManager = SoundManager.Instance;

        soundManager.BGM_MAIN.Play();

        gradeListNum = gameManager.grade.currentSocialization.maxGrade;
        gradeLists = gameManager.grade.gradeList;
        panelLists = new GameObject[gradeListNum + 1];

        currentGrade = gameManager.grade.current;
        Button upgradeCancelBtn = upgradePanelBg.transform.Find("ModalPanel/CancelBtn").GetComponent<Button>();
        Button upgradeBtn = upgradePanelBg.transform.Find("ModalPanel/OkBtn").GetComponent<Button>();
        upgradeCancelBtn.onClick.RemoveListener(onCanelInUpgradeModal);
        upgradeBtn.onClick.RemoveListener(onOkInUpgradeModal);

        upgradeCancelBtn.onClick.AddListener(onCanelInUpgradeModal);
        upgradeBtn.onClick.AddListener(onOkInUpgradeModal);

        Button hpGodSixBtn = hpExhaustPanel.transform.Find("ModalPanel/OkBtn").GetComponent<Button>();
        Button hpCloseBtn = hpExhaustPanel.transform.Find("ModalPanel/CancelBtn").GetComponent<Button>();

        hpGodSixBtn.onClick.AddListener(onOkInHpModal);
        hpCloseBtn.onClick.AddListener(onCancelInHpModal);

        makeList();
    }

    void Start() {
        eventManager.addListener(EventType.RESOCIALIZE, OnEvent);
        eventManager.addListener(EventType.GRADE_CHANGE, OnEvent);

        InvokeRepeating("checkCanUpgrade", 1.0f, 1.0f);
    }

    private void makeList() {
        //Debug.Log(gameManager.grade.currentSocialization.maxGrade);
        Transform parent = transform.Find("InnerPanel").transform;

        GameObject skyPanel = Instantiate(skyPrefab);
        skyPanel.transform.SetParent(parent);
        skyPanel.GetComponent<RectTransform>().transform.localPosition = new Vector3(0, 0, 0);

        Button skyBtn = skyPanel.GetComponent<Button>();
        RectTransform rect = skyBtn.GetComponent<RectTransform>();
        rect.localScale = new Vector3(1, 1, 0);
        skyBtn.onClick.RemoveListener(gameManager.onMainTouchEvent);
        skyBtn.onClick.AddListener(gameManager.onMainTouchEvent);
        int gradeIndex = gameManager.grade.current;
        //Debug.Log(gradeIndex);
        for (int i = gradeListNum; i >= 0; i--) {
            int floorNum = i + 1;
            panelLists[i] = Instantiate(gradeListPrefab);
            panelLists[i].transform.SetParent(parent);
            panelLists[i].name = "Floor" + floorNum;
            panelLists[i].GetComponent<ListItem>().index = i;
            panelLists[i].GetComponent<RectTransform>().transform.localPosition = new Vector3(0, 0, 0);

            GameObject roomObj = panelLists[i].transform.Find("Room").gameObject;

            Image roomImg = roomObj.GetComponent<Image>();
            roomImg.sprite = uiManager.floorImgArr[i];

            GameObject spriteObj = roomObj.transform.Find("Char/Sprite").gameObject;
            spriteObj.GetComponent<Image>().sprite = uiManager.minionImageArr[i];
            GameObject talkBalloonObj = roomObj.transform.Find("TalkBalloon").gameObject;

            CharAnimCtrler animCtrler = spriteObj.GetComponent<CharAnimCtrler>();
            bool isMyChar = false;

            if (uiManager.reverseMyCharSprite[i] == 1) {
                talkBalloonObj.GetComponent<Image>().sprite = uiManager.talkBalloonArr[0];
            }
            else if(uiManager.reverseMyCharSprite[i] == 0) {
                talkBalloonObj.GetComponent<Image>().sprite = uiManager.talkBalloonArr[1];
            }
            //내 캐릭터
            if (i == gradeIndex) {
                roomObj.transform.Find("Char/Loc").gameObject.SetActive(true);
                isMyChar = true;
                if (uiManager.reverseMyCharSprite[gradeIndex] == 1) {
                    spriteObj.GetComponent<Image>().transform.localRotation = Quaternion.Euler(0, 180, 0);
                }
            }

            rect = panelLists[i].GetComponent<RectTransform>();
            rect.localScale = new Vector3(1, 1, 0);

            if (currentGrade + 1 == i) {
                Text upgradeCostText = roomObj.transform.Find("Cost/CostTxt").GetComponent<Text>();
                roomObj.transform.Find("Cost").gameObject.SetActive(true);
                roomObj.transform.Find("CostIcon").gameObject.SetActive(true);
                upgradeCostText.text = gradeLists[i].upgradeCost.ToFormattedString();
            }

            Text gradeNameText = roomObj.transform.Find("GradeNameText").GetComponent<Text>();
            gradeNameText.text = gameManager.grade.gradeList[i].name.ToString();

            Button panelBtn = panelLists[i].AddComponent<Button>();

            panelBtn.onClick.RemoveListener(gameManager.onMainTouchEvent);
            panelBtn.onClick.RemoveListener(onMyCharBtn);

            panelBtn.onClick.AddListener(gameManager.onMainTouchEvent);
            panelBtn.onClick.AddListener(onMyCharBtn);
            
            animCtrler.setAnim(i, isMyChar);
        }
        GameObject room = panelLists[0].transform.Find("Room").gameObject;

        GameObject groundPanel = Instantiate(groundPrefab);
        groundPanel.transform.SetParent(parent);
        groundPanel.GetComponent<RectTransform>().transform.localPosition = new Vector3(0, 0, 0);

        rect = groundPanel.GetComponent<RectTransform>();
        rect.localScale = new Vector3(1, 1, 0);

        Button groundBtn = groundPanel.GetComponent<Button>();
        groundBtn.onClick.RemoveListener(gameManager.onMainTouchEvent);
        groundBtn.onClick.AddListener(gameManager.onMainTouchEvent);
    }

    //재사회화를 하는 경우 발동되는 함수
    private void resocializing() {        
        Transform parent = transform.Find("InnerPanel").transform;
        foreach (Transform child in parent) {
            Destroy(child.gameObject);
        }
        initialize();
    }

    //every per frame
    private void checkCanUpgrade() {
        currentGrade = gameManager.grade.current;
        if (currentGrade == gameManager.grade.maxGrade) {
            return;
        }
        else {
            int nextGrade = currentGrade + 1;
            Button panel = panelLists[nextGrade].GetComponent<Button>();
            GameObject nextRoom = panelLists[nextGrade].transform.Find("Room").gameObject;
            Image nextImg = nextRoom.GetComponent<Image>();
            panel.onClick.RemoveListener(onUpgradeModalPanel);
            if (gradeLists[nextGrade].upgradeCost <= gameManager.gondaeReuk) {
                //직위 업그레이드가 가능한 경우
                panel.onClick.RemoveListener(gameManager.onMainTouchEvent);
                panel.onClick.AddListener(onUpgradeModalPanel);
                if (canEffect) {
                    nextRoom.GetComponent<Animator>().Play("RoomFlick");
                    canEffect = false;
                }
            }
            else {
                //직위 업그레이드가 불가한 경우
                nextRoom.GetComponent<Animator>().Play("Default");
                canEffect = true;
                panel.onClick.RemoveListener(onUpgradeModalPanel);
            }
        }
    }

    public void onUpgradeModalPanel() {
        int nextGrade = gameManager.grade.current + 1;

        soundManager.SE_ON_MENU_BUTTON.Play();

        upgradePanelBg.SetActive(true);
        upgradePanelFg.SetActive(true);

        GradeItem grade = gradeLists[nextGrade];
        int[] unlockSkills = grade.unlockSkills;
        string[] str = gradeLists[nextGrade].desc.Split('|');
        string desc = "";
        for (int i = 0; i < str.Length; i++) {
            desc = desc + str[i] + "\n";
        }
        upgradePanelBg
            .transform
            .Find("ModalPanel/QuestionText")
            .GetComponent<Text>().text = "<color=red>" + grade.name.ToString() + "</color>"
            + "으로 승진가능!!";

        upgradePanelBg
            .transform
            .Find("ModalPanel/CostBg/InfoText")
            .GetComponent<Text>().text = grade.upgradeCost.ToFormattedString();
        
        GameObject BgUnlockDescPanel = upgradePanelBg.transform.Find("ScrollView/UnlockDescPanel").gameObject;
        GameObject FgUnlockDescPanel = upgradePanelFg.transform.Find("ScrollView/UnlockDescPanel").gameObject;

        foreach (Transform child in BgUnlockDescPanel.transform) {
            Destroy(child.gameObject);
        }

        foreach(Transform child in FgUnlockDescPanel.transform) {
            Destroy(child.gameObject);
        }

        for (int i=0; i< unlockSkills.Length; i++) {
            GameObject tmp = Instantiate(unlockListPrefabBg);
            tmp.transform.SetParent(BgUnlockDescPanel.transform);
            //tmp.GetComponent<RectTransform>().transform.localPosition = new Vector3(0, 0, -10f);
            tmp.GetComponent<RectTransform>().localScale = new Vector3(1, 1, 0);
            tmp.GetComponent<RectTransform>().localPosition = new Vector3(0, 0, 0);

            if (unlockSkills.Length >= 2) {
                tmp.transform.Find("ErekiCore").GetComponent<ParticleSystem>().startSize = 1f;
            }

            tmp = Instantiate(unlockListPrefabFg);
            tmp.transform.SetParent(FgUnlockDescPanel.transform);
            tmp.GetComponent<RectTransform>().localScale = new Vector3(1, 1, 0);
            tmp.GetComponent<RectTransform>().localPosition = new Vector3(0, 0, 0);

            Image img = tmp.transform.Find("Image").GetComponent<Image>();
            img.sprite = uiManager.gradeUpDutyUnlockImgArr[unlockSkills[i]];

            Text text = tmp.transform.Find("Text").GetComponent<Text>();
            text.text = "" + gameManager.dutySkills.skills[unlockSkills[i]].name;
        }
    }

    private void onOkInUpgradeModal() {
        int gradeIndex = gameManager.grade.current;
        int nextUpgradeAreaIndex = gradeIndex + 2;
        int nextMyGrade = gradeIndex + 1;

        //업그레이드를 한 뒤 이전 내 직위에 대한 처리
        GameObject roomObj = panelLists[nextMyGrade].transform.Find("Room").gameObject;
        roomObj.transform.Find("Cost/CostTxt").GetComponent<Text>().text = "";
        roomObj.GetComponent<Animator>().Play("Default");
        canEffect = true;
        roomObj.transform.Find("Cost").gameObject.SetActive(false);
        roomObj.transform.Find("CostIcon").gameObject.SetActive(false);

        //업그레이드를 한 뒤 내 직위에 대한 처리
        panelLists[nextMyGrade].GetComponent<Button>().onClick.AddListener(gameManager.onMainTouchEvent);
        panelLists[nextMyGrade].GetComponent<Button>().onClick.RemoveListener(onUpgradeModalPanel);

        panelLists[gradeIndex].transform.Find("Room/Char/Loc").gameObject.SetActive(false);

        roomObj = panelLists[nextMyGrade].transform.Find("Room").gameObject;
        roomObj.transform.Find("Char/Loc").gameObject.SetActive(true);

        upgradePanelBg.SetActive(false);
        upgradePanelFg.SetActive(false);

        gameManager.gradeUp();
        soundManager.SE_GRADE_UP.Play();
        
        if(gameManager.grade.current != gameManager.grade.maxGrade) {
            GameObject nextPanel = panelLists[nextUpgradeAreaIndex].gameObject;
            nextPanel.transform.Find("Room/Cost").gameObject.SetActive(true);
            nextPanel.transform.Find("Room/CostIcon").gameObject.SetActive(true);
            nextPanel.transform.Find("Room/Cost/CostTxt").GetComponent<Text>().text = gradeLists[nextUpgradeAreaIndex].upgradeCost.ToFormattedString();
        }   
        eventManager.canDequeue = true;        
    }

    private void onCanelInUpgradeModal() {
        upgradePanelBg.SetActive(false);
        upgradePanelFg.SetActive(false);

        soundManager.SE_ON_BACK_BUTTON.Play();
        eventManager.canDequeue = true;
    }

    private void onOkInHpModal() {
        //곤대신의 가호를 1개 소모하여 체력을 가득 채움
        eventManager.canDequeue = true;
        if(gameManager.blessOfGondae > 0) {
            gameManager.healthPoint += gameManager.maxHealthPoint;
            gameManager.blessOfGondae--;
        }
        hpExhaustPanel.SetActive(false);
    }

    private void onCancelInHpModal() {
        hpExhaustPanel.SetActive(false);
        eventManager.canDequeue = true;
    }

    public void onMyCharBtn() {
        if (canTouchMyChar) {
            canTouchMyChar = false;
            int index = gameManager.grade.current;
            GameObject talkBalloon = panelLists[index].transform.Find("Room/TalkBalloon").gameObject;
            talkBalloon.SetActive(true);
            talkBalloon.GetComponentInChildren<Text>().text = gameManager.dialogues.getDialogue("hero", index);
        }
    }

    private void offMyCharBtn() {
        int index = gameManager.grade.current;
        GameObject talkBalloon = panelLists[index].transform.Find("Room/TalkBalloon").gameObject;
        talkBalloon.SetActive(false);
        canTouchMyChar = true;
    }

    private void initializeDialogue() {
        foreach(GameObject obj in panelLists) {
            obj.transform.Find("Room/TalkBalloon").gameObject.SetActive(false);
        }
    }

    private void underOverDialogueEffect() {
        int currentGrade = gameManager.grade.current;
        if(currentGrade != gameManager.grade.maxGrade) {
            int index = currentGrade + 1;
            GameObject talkBalloon = panelLists[index].transform.Find("Room/TalkBalloon").gameObject;
            talkBalloon.GetComponentInChildren<Text>().text = gameManager.dialogues.getDialogue("overHero", index);
            talkBalloon.SetActive(true);
        }        
        if(currentGrade != 0) {
            int index = currentGrade - 1;
            GameObject talkBalloon = panelLists[index].transform.Find("Room/TalkBalloon").gameObject;
            talkBalloon.GetComponentInChildren<Text>().text = gameManager.dialogues.getDialogue("underHero", index);
            talkBalloon.SetActive(true);
        }
    }

    private void changeAnim() {
        int currentGrade = gameManager.grade.current;
        GameObject roomObj = panelLists[currentGrade].transform.Find("Room").gameObject;

        GameObject spriteObj = roomObj.transform.Find("Char/Sprite").gameObject;
        spriteObj.GetComponentInChildren<CharAnimCtrler>().setAnim(currentGrade, true);
        if (uiManager.reverseMyCharSprite[currentGrade] == 1) {
            spriteObj.GetComponent<Image>().transform.localRotation = Quaternion.Euler(0, 180, 0);
        }
        int prevIndex = gameManager.grade.current - 1;

        string animName = "Grade" + prevIndex + "_Idle";
        spriteObj = panelLists[prevIndex].transform.Find("Room/Char/Sprite").gameObject;
        spriteObj.GetComponent<CharAnimCtrler>().GetComponent<Animator>().Play(animName);
        spriteObj.GetComponent<Image>().transform.localRotation = Quaternion.Euler(0, 0, 0);
        if (uiManager.reverseMyCharSprite[prevIndex] == 0) {
            spriteObj.GetComponent<Image>().transform.localRotation = Quaternion.Euler(0, 180, 0);
        }        
    }

    public void OnEvent(EventType type, Component sender, object param = null) {
        switch (type) {
            case EventType.RESOCIALIZE:
                initializeDialogue();
                resocializing();
                break;
            case EventType.GRADE_CHANGE:
                initializeDialogue();
                changeAnim();
                break;
        }
    }
}