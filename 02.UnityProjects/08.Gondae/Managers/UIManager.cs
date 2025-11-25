using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.EventSystems;

public class UIManager : MonoBehaviour {

    private GameManager gameManager;
    private EventManager eventManager;
    private SoundManager soundManager;
    private static UIManager instance = null;

    private GameObject
        canvasPanels,
        menuPanels,
        resocialResultModal,
        hpExhaustionModal,      //체력고갈시 등장하는 Modal
        storyModal,             //스토리를 보여주는 Modal
        optionModal,            //옵션 버튼 클릭시 등장하는 Modal
        ciModal,                //회사 로고
        hpSliderObj,
        topPanel;

    private int charAnimNum;            //메인화면 캐릭터 애니메이션 종류

    public int 
        sceneNum,
        menuBtnNum,
        modalNum;
                
    public GameObject[] 
        scenes,
        menuBtns;

    public Sprite[] 
        minionImageArr,
        talkBalloonArr,         //캐릭터가 어느 방향을 바라보는지에 따라 다른 말풍선 이미지 사용. 이미지를 뒤집는 경우 내부 텍스트 또한 뒤집어지는 문제가 있음.
        menuBtnActiveImgArr,    //메뉴버튼 활성화된 경우의 이미지 배열
        menuBtnDeActiveImgArr,  //메뉴버튼 비활성화된 경우의 이미지 배열
        dutyIconArr,            //업무 아이콘 이미지 배열
        buffIconArr,            //버프 아이콘 이미지 배열
        resocialPressedImgArr,  //재사회화 버튼 눌렀을 때 이미지 배열
        resocialDefaultImgArr,  //재사회화 버튼 기본 이미지 배열
        gradeUpDutyUnlockImgArr,//직위 업그레이드시 해금되는 업무에 대한 Icon 배열
        lineLeftBossImgArr,     //라인 왼쪽 라인 상사 초상화 이미지 배열
        lineRightBossImgArr,    //라인 오른쪽 라인 상사 초상화 이미지 배열
        lineMinionImgArr,       //라인 부하 초상화 이미지 배열
        lineBossPanelArr,       //라인 상사 패널 이미지 배열
        lineMinionPanelArr,     //라인 부하 패널 이미지 배열
        topPanelPortraitImgArr, //메인화면 상단 주인공 초상화 이미지 배열
        myCardsPortraitImgArr,  //주인공 초상화 클릭시 등장하는 사원증에 표시되는 이미지 배열
        mainStoryImgArr,        //스토리 메뉴에서 메인 스토리 아이콘 배경 배열
        mainStoryBgArr,         //메인 스토리 모달 배경 배열
        subStoryBgArr,          //서브 스토리 모달 배경 배열
        floorImgArr;            //각 층 배경 이미지 배열

    public GameObject 
        touchCriticalPref,      //터치 극대화 효과 프리팹
        touchPref;              //일반 터치 효과 프리팹

    public Slider hpSlider;
    public Text 
        hpSlideText,
        passivePerTouchText,
        passivePerSecText,
        levelText,
        totalGdrAmntText;
    public string[] charAnimArr;    //메인화면 캐릭터 애니메이션 Trigger명 배열
    public int[] reverseMyCharSprite;  //메인 화면 내 캐릭터가 좌측(true) 혹은 우측(false)을 향할지 결정
    private string str;

    public AnimationClip[] minionAnimArr;
    public static UIManager Instance {
        get {
            return instance;
        }
    }

    void Start() {
        initialize();
        scenes[0].SetActive(true);
        
        #if UNITY_ANDROID
        ciModal.SetActive(true);
        ciModal.GetComponent<Animator>().Play("CI");
        #endif
    }

    void Update() {
        str = gameManager.gondaeReuk.ToFormattedString();
        totalGdrAmntText.text = str;
        passivePerSecText.text = "+ " + gameManager.passiveGDR.ToFormattedString();
        levelText.text = gameManager.grade.currentGradeName;
        passivePerTouchText.text = "+ " + gameManager.touchGDR.ToFormattedString();
        hpSlideText.text = gameManager.healthPoint.ToString("N0") + " / " + gameManager.maxHealthPoint.ToString("N0");
        hpSlider.value = gameManager.healthPoint;
    }

    void Awake() {
        if (instance != null && instance != this) {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        makeCharDirArr();
        
        //GameObject.Find("CIModal").GetComponent<Animator>().Play("CI");
    }

    void makeCharDirArr() {
        reverseMyCharSprite = new int[11];
        string str = "1, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0";
        string[] tokens = str.Split(',');


        reverseMyCharSprite = Array.ConvertAll<string, int>(tokens, int.Parse);
    }

    void initialize() {
        eventManager = EventManager.Instance;
        gameManager = GameManager.Instance;
        soundManager = SoundManager.Instance;

        scenes = new GameObject[sceneNum];
        menuBtns = new GameObject[menuBtnNum];
       
        eventManager.addListener(EventType.HEALTH_CHANGE, OnEvent);
        eventManager.addListener(EventType.GRADE_CHANGE, OnEvent);
        eventManager.addListener(EventType.RESOCIALIZE, OnEvent);

        canvasPanels = GameObject.Find("Canvas/Panel").gameObject;
        menuPanels = canvasPanels.transform.Find("MainLayout/MenuPanel").gameObject;
        hpExhaustionModal = canvasPanels.transform.Find("HealthExaustionModal").gameObject;
        optionModal = canvasPanels.transform.Find("OptionModal").gameObject;
        storyModal = canvasPanels.transform.Find("StoryModal").gameObject;
        resocialResultModal = canvasPanels.transform.Find("ResocialResultModal").gameObject;
        ciModal = canvasPanels.transform.Find("CIModal").gameObject;

        Button resocialResultBtn = resocialResultModal.transform.Find("CloseBtn").GetComponent<Button>();
        resocialResultBtn.onClick.AddListener(offResocialDialogueModal);

        Button storyCloseBtn = storyModal.transform.Find("CloseBtn").GetComponent<Button>();
        storyCloseBtn.onClick.AddListener(offStoryModal);

        string[] layoutNames = new string[]{
                                    "MainLayout",
                                    "DutyLayout",
                                    "ResocializeLayout",
                                    "BuffLayout",
                                    "LineLayout",
                                    "SummonLayout",
                                    "StoryLayout" };

        string[] menuBtnNames = new string[] {
                                    "DutyBtn",
                                    "ResocialBtn",
                                    "BuffBtn",
                                    "LineBtn",
                                    "SummonBtn",
                                    "StoryBtn" };

        //메인, 캐릭터, 업무, 이직, 버프, 내상사, 내부하, 강림씬 순
        for (int i=0; i<sceneNum; i++) {
            scenes[i] = canvasPanels.transform.Find(layoutNames[i]).gameObject;
        }

        //메뉴, 캐릭터, 업무, 이직, 버프, 내상사, 내부하, 강림, 뒤로가기 버튼 순
        for (int i=0; i<menuBtnNum; i++) {
            menuBtns[i] = menuPanels.transform.Find(menuBtnNames[i]).gameObject;
            menuBtns[0].GetComponent<Button>().onClick.RemoveAllListeners();
        }
        menuBtns[0].GetComponent<Button>().onClick.AddListener(callLayout);
        menuBtns[1].GetComponent<Button>().onClick.AddListener(callLayout);
        menuBtns[2].GetComponent<Button>().onClick.AddListener(callLayout);
        menuBtns[3].GetComponent<Button>().onClick.AddListener(callLayout);
        menuBtns[4].GetComponent<Button>().onClick.AddListener(callLayout);
        menuBtns[5].GetComponent<Button>().onClick.AddListener(callLayout);
        //menuBtns[6].GetComponent<Button>().onClick.AddListener(offMenu);

        GameObject mainPanel = scenes[0].transform.Find("MainPanel").gameObject;
        mainPanel.AddComponent<Button>();
        //mainPanel.GetComponent<Button>().onClick.AddListener(onTouchScreen);
        //setMenuBtnSize(canvasPanels);

        hpSliderObj = GameObject.Find("HpSlide").gameObject;
        hpSlider = hpSliderObj.GetComponent<Slider>();
        hpSlider.maxValue = gameManager.maxHealthPoint;

        hpSlider.value = gameManager.healthPoint;
        hpSlideText = hpSliderObj.transform.Find("HealthText").GetComponent<Text>();
        hpSlideText.text = gameManager.healthPoint + " / " + gameManager.maxHealthPoint;

        topPanel = GameObject.Find("TopPanel").gameObject;
        passivePerTouchText = topPanel.transform.Find("Touch/PerTouchText").GetComponent<Text>();
        passivePerSecText = topPanel.transform.Find("Passive/PerSecText").GetComponent<Text>();
        levelText = topPanel.transform.Find("Grade/LvText").GetComponent<Text>();
        totalGdrAmntText = topPanel.transform.Find("GDRSlide/GdrTotalAmntTxt").GetComponent<Text>();

        changePortrait();

        charAnimNum = gameManager.grade.gradeList.Length;
        charAnimArr = new string[charAnimNum];
        for(int i=0; i<charAnimNum; i++) {
            charAnimArr[i] = "Grade" + i;
        }
    }

    public void callLayout() {
        soundManager.SE_ON_MENU_BUTTON.Play();
        GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
        int btnIndex = -1;
        if (selectedObj.tag == "MenuBtn") {
            btnIndex = selectedObj.GetComponent<ListItem>().index;
        }
        if(btnIndex == -1) {
            return;
        }
        //Debug.Log("Btn Index : " + btnIndex);
        int sceneIndex = btnIndex + 1;
        //Debug.Log("Scene Index : " + sceneIndex);
        if (scenes[sceneIndex].activeInHierarchy == true) {
            offMenu();
        }
        else if(scenes[sceneIndex].activeInHierarchy == false) {
            offMenu();
            onMenu(btnIndex);
        }
    }

    private void onMenu(int index) {
        scenes[index + 1].SetActive(true);
        menuBtns[index].GetComponent<Image>().sprite = menuBtnActiveImgArr[index];
        
    }

    private void offMenu() {
        foreach(GameObject btn in menuBtns) {
            int tmpIdx = btn.GetComponent<ListItem>().index;
            scenes[tmpIdx + 1].SetActive(false);
            menuBtns[tmpIdx].GetComponent<Image>().sprite = menuBtnDeActiveImgArr[tmpIdx];
        }
    }

	public void onCritical(){
        Vector3 pos = Vector3.zero;
        GameObject effectObj = Instantiate(touchCriticalPref);
        #if UNITY_EDITOR
        pos = Input.mousePosition;
        pos.z = 10.0f;
        #endif

        #if UNITY_IPHONE || UNITY_ANDROID
        if (Input.touchCount > 0) {
            pos = Input.GetTouch(0).position;
            pos.z = 10.0f;
        }
        #endif
        effectObj.transform.position = Camera.main.ScreenToWorldPoint(pos);
    }

    public void onMainTouch() {
        GameObject effectObj = Instantiate(touchPref);
        effectObj.transform.SetParent(GameObject.Find("CanvasFG").transform);
        effectObj.GetComponent<RectTransform>().transform.localScale = new Vector3(1, 1, 0);
        effectObj.transform.Find("Image").GetComponent<RectTransform>().transform.localScale = new Vector3(1, 1, 0);
        GameObject textObj = effectObj.transform.Find("Text").gameObject;
        textObj.GetComponent<Text>().text = " + " + gameManager.touchGDR.ToFormattedString();
        #if UNITY_IPHONE || UNITY_ANDROID
        textObj.GetComponent<RectTransform>().sizeDelta = new Vector3(Screen.width / 5f, Screen.width / 5f, 0);
        #endif
        Vector3 pos = Vector3.zero;
        #if UNITY_EDITOR
        pos = Input.mousePosition;
        pos.y += 100f;
        pos.z = 10.0f;
        #endif

        #if UNITY_IPHONE || UNITY_ANDROID
        if (Input.touchCount > 0) {
            pos = Input.GetTouch(0).position;
            pos.y += 100f;
            pos.z = 10.0f;
        }
        #endif
        effectObj.transform.position = Camera.main.ScreenToWorldPoint(pos);
    }

    public void onHpZeroModal() {
        hpExhaustionModal.SetActive(true);
        Text blessAmnt = hpExhaustionModal.transform.Find("ModalPanel/CostPanel/Text").GetComponent<Text>();
        blessAmnt.text = "X " + gameManager.blessOfGondae + "개 보유중";
    }

    public void onOptionModal() {
        optionModal.SetActive(true);
    }

    public void onStoryModal(Chapter chapter) {
        storyModal.SetActive(true);
        GameObject textObj = storyModal.transform.Find("MaskLayout/InnerPanel/Text").gameObject;
        Text storyText = textObj.GetComponent<Text>();
        storyText.text = chapter.playStoryContent().text;

        Image img = storyModal.transform.Find("BackGround").GetComponent<Image>();
        RectTransform rect = storyModal.transform.Find("BackGround").GetComponent<RectTransform>();
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        if (chapter.subChapter == 0) {
            textRect.transform.localScale = new Vector3(1.0f, 1.0f);
            img.sprite = mainStoryBgArr[chapter.chapterNum - 1];
            rect.transform.localScale = new Vector3(1.0f, 1.0f);
            storyText.fontSize = 15;
            storyText.resizeTextMinSize = storyText.fontSize;
        }
        else if(chapter.subChapter != 0) {
            textRect.transform.localScale = new Vector3(0.7f, 0.7f);
            img.sprite = subStoryBgArr[0];
            rect.transform.localScale = new Vector2(0.7f, 0.7f);
            storyText.fontSize = 60;
            storyText.resizeTextMinSize = storyText.fontSize;
        }
        //storyText.text = gameManager.story.chapters[mainInex].playStoryContent().ToString();
        //scenes[5].GetComponent<StoryViewCtrler>().unlockStory();
    }

    public void offStoryModal() {
        storyModal.SetActive(false);
        eventManager.canDequeue = true;
    }

    public void onResoicalResultDialogueModal(bool isFirstMaxGrade = false) {
        resocialResultModal.SetActive(true);
        GameObject myBossPanelObj = resocialResultModal.transform.Find("MyBossPanel").gameObject;
        GameObject finalText = resocialResultModal.transform.Find("FinalText").gameObject;
        if (isFirstMaxGrade) {
            string message = "자네가 새로 승진한 대리로군. 반갑네 나는 백과장이라고 하네. 슬슬 지치고 열정이 떨어질 텐데, 회사 차원의 스트레스 관리 복지 시스템이 있으니 한번 이용해 보게나. 재사회화실로 오게나.";
            myBossPanelObj.SetActive(true);
            myBossPanelObj.transform.Find("Text").GetComponent<Text>().text = message;
        }

        else {
            if (gameManager.grade.nextSocialization == null) {
                myBossPanelObj.SetActive(false);
                finalText.SetActive(true);
                finalText.GetComponent<Text>().text = gameManager.grade.currentSocialization.result;
            }
            else {
                finalText.SetActive(false);
                myBossPanelObj.SetActive(true);
                myBossPanelObj.transform.Find("Text").GetComponent<Text>().text = gameManager.grade.currentSocialization.result;
            }
        }        
    }

    public void offResocialDialogueModal() {
        resocialResultModal.SetActive(false);
        eventManager.canDequeue = true;
    }

    public void OnEvent(EventType type, Component sender, object param = null) {
        switch (type) {
            case EventType.HEALTH_CHANGE:
                break;

            case EventType.GRADE_CHANGE:
                changePortrait();
                break;

            case EventType.RESOCIALIZE:
                changePortrait();
                break;
        }
    }

    IEnumerator moveUp(GameObject obj) {
        Vector2 pos = obj.transform.position;
        for (float i=3; i>=0; i -= 0.05f) {
            yield return 0;
            pos.y += 0.01f;
            obj.transform.position = pos;
        }
        Destroy(obj);
    }

    private void changePortrait() {
        int gradeIndex = gameManager.grade.current;
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
        else if (gradeIndex > 6 && gradeIndex <= 7) {
            portraitIndex = 3;
        }
        else {
            portraitIndex = 4;
        }
        topPanel.transform.Find("Portrait").GetComponent<Image>().sprite = topPanelPortraitImgArr[portraitIndex];        
    }
}