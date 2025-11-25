using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sirenix.OdinInspector;
using Tutorial;
using UnityEngine.Events;
using System;
using System.Linq;
using System.IO;
using dataModules;
using System.Text;

public class ScenarioManager : SerializedMonoBehaviour
{
    public static ScenarioManager Instance { get; private set; }
    public ShowSelectRace human, orc;
    public string heroID;
    public bool isIngameButtonClicked = false;
    public GameObject stageCanvas;
    public GameObject deckContent;

    public GameObject selectedDeckObject = null;
    public object selectedDeck;
    private int currentPageIndex = 0;   //현재 페이지
    public int maxPageIndex = 0;       //최대 페이지

    public GameObject headerMenu;
    public bool isHuman;
    
    public ChapterData selectedChapterData;
    public ChallengeData selectedChallengeData;
    public GameObject selectedChapterObject;

    //[SerializeField] GameObject orcDeckPrefab;
    //[SerializeField] GameObject humanDeckPrefab;

    [SerializeField] Image backgroundImage;
    [SerializeField] Dictionary<string, Sprite> stroyBackgroundImages;
    [SerializeField] Dictionary<string, Sprite> storyHeroPortraits;
    [SerializeField] MenuSceneController menuSceneController;

    //파일 읽어 세팅함
    public List<ChapterData> human_chapterDatas, orc_chapterDatas;
    public List<ChallengeData> human_challengeDatas, orc_challengeDatas;

    //읽어온 파일을 재분류함
    Dictionary<int, List<ChapterData>> pageHumanStoryList, pageOrcStoryList;

    public static UnityEvent OnLobbySceneLoaded = new UnityEvent();
    public ChapterChangeEvent OnChapterChanged = new ChapterChangeEvent();

    public class ChapterChangeEvent : UnityEvent<bool, int> { } 
    
    private void Awake() {
        Instance = this;
        OnLobbySceneLoaded.Invoke();
        isIngameButtonClicked = false;

        var humanArrowHandlers = human.StageCanvas.transform.Find("HUD").GetComponentsInChildren<StoryArrowHandler>();
        var orcArrowHandlers = orc.StageCanvas.transform.Find("HUD").GetComponentsInChildren<StoryArrowHandler>();

        foreach (var handler in humanArrowHandlers) {
            handler.Init();
        }

        foreach (var handler in orcArrowHandlers) {
            handler.Init();
        }
    }

    private void OnDestroy() {
        Instance = null;
    }

    [SerializeField] HUDController HUDController;
    void OnEnable() {
        ReadScenarioData();
        
        SetBackButton(1);
        EscapeKeyController.escapeKeyCtrl.AddEscape(OnBackButton);
        
        int prevChapter = int.Parse(PlayerPrefs.GetString("ChapterNum", "0"));
        int prevStageNumber = int.Parse(PlayerPrefs.GetString("StageNum", "0"));
        
        string prevRace = PlayerPrefs.GetString("SelectedRace").ToLower();

        if (MainSceneStateHandler.Instance.GetState("IsTutorialFinished")) {
            if (prevRace == "human") OnHumanCategories();
            else OnOrcCategories();
        
            SetSubStoryListInfo(prevChapter, prevStageNumber, prevRace);
        }
        else {
            OnHumanCategories();
        }
    }

    void OnDisable() {
        orc.StageCanvas.transform.Find("HUD/StageSelect/Buttons").gameObject.SetActive(false);
        human.StageCanvas.transform.Find("HUD/StageSelect/Buttons").gameObject.SetActive(false);
        EscapeKeyController.escapeKeyCtrl.RemoveEscape(OnBackButton);
    }
    
    private void OnDecksUpdated(Enum Event_Type, Component Sender, object Param) {
        
    }

    /// <summary>
    /// 휴먼튜토리얼 강제 호출시 Awake가 호출되지 않은 상태이기 때문에 MenuSceneController에서 호출함
    /// </summary>
    public void ReadScenarioData() {
        string dataAsJson = ((TextAsset)Resources.Load("TutorialDatas/HumanChapterDatas")).text;
        human_chapterDatas = JsonReader.Read<List<ChapterData>>(dataAsJson);

        dataAsJson = ((TextAsset)Resources.Load("TutorialDatas/OrcChapterDatas")).text;
        orc_chapterDatas = JsonReader.Read<List<ChapterData>>(dataAsJson);

        dataAsJson = ((TextAsset)Resources.Load("TutorialDatas/humanChallengeData")).text;
        human_challengeDatas = JsonReader.Read<List<ChallengeData>>(dataAsJson);

        dataAsJson = ((TextAsset)Resources.Load("TutorialDatas/orcChallengeData")).text;
        orc_challengeDatas = JsonReader.Read<List<ChallengeData>>(dataAsJson);

        MakeStoryPageList();
    }

    public void OnBackButton() {
        SoundManager.Instance.PlaySound(UISfxSound.BUTTON1);
        PlayerPrefs.SetString("SelectedDeckId", "");
        PlayerPrefs.SetString("SelectedDeckType", "");
        PlayerPrefs.SetString("SelectedBattleType", "");
        PlayerPrefs.SetString("BattleMode", "");

        offAllGlowEffect();

        gameObject.SetActive(false);
        HUDController.SetHeader(HUDController.Type.SHOW_USER_INFO);
    }

    public void SetBackButton(int depth) {
        switch (depth) {
            case 1:
                HUDController.SetBackButton(() => {
                    OnBackButton();
                });
                HUDController.SetHeader(HUDController.Type.RESOURCE_ONLY_WITH_BACKBUTTON);
                break;
            case 2:
                HUDController.SetBackButton(() => {
                    CloseStoryDetail();
                    SetBackButton(1);
                });
                HUDController.SetHeader(HUDController.Type.ONLY_BAKCK_BUTTON);
                break;
            case 3:
                HUDController.SetBackButton(() => {
                    CloseDeckList();
                    SetBackButton(2);
                });
                HUDController.SetHeader(HUDController.Type.ONLY_BAKCK_BUTTON);
                break;
        }
    }

    void CloseStoryDetail() {
        stageCanvas.SetActive(false);
        EscapeKeyController.escapeKeyCtrl.RemoveEscape(CloseStoryDetail);
    }

    void CloseDeckList() {
        stageCanvas.transform.Find("DeckSelectPanel").gameObject.SetActive(false);
        stageCanvas.transform.Find("DeckSelectPanel/StagePanel/StartButton").gameObject.SetActive(false);
        
        EscapeKeyController.escapeKeyCtrl.RemoveEscape(CloseDeckList);
    }

    public void OnHumanCategories() {
        SoundManager.Instance.PlaySound(UISfxSound.BUTTON1);
        //heroID = "";
        isHuman = true;
        PlayerPrefs.SetString("SelectedRace", "human");
        ToggleUI();
    }
    
    public void OnOrcCategories() {
        SoundManager.Instance.PlaySound(UISfxSound.BUTTON1);
        //heroID = "";
        isHuman = false;
        PlayerPrefs.SetString("SelectedRace", "orc");
        ToggleUI();
    }

    /// <summary>
    /// 종족 선택시 UI 세팅
    /// </summary>
    private void ToggleUI() {
        SetSubStoryListInfo();

        var backgroundImages = AccountManager.Instance.resource.campBackgrounds;
        if (isHuman) {
            orc.raceButton.GetComponent<Image>().sprite = orc.deactiveSprite;
            orc.heroSelect.SetActive(false);
            orc.StageCanvas.SetActive(false);

            human.raceButton.GetComponent<Image>().sprite = human.activeSprite;
            human.heroSelect.SetActive(true);
            human.StageCanvas.SetActive(true);

            backgroundImage.sprite = backgroundImages["human"];
        }
        else {
            human.raceButton.GetComponent<Image>().sprite = human.deactiveSprite;
            human.heroSelect.SetActive(false);
            human.StageCanvas.SetActive(false);

            orc.raceButton.GetComponent<Image>().sprite = orc.activeSprite;
            orc.heroSelect.SetActive(true);
            orc.StageCanvas.SetActive(true);

            backgroundImage.sprite = backgroundImages["orc"];
        }
    }
    
    /// <summary>
    /// 페이지별 리스트 생성 (ex. 0챕터 리스트, 1챕터 리스트)
    /// </summary>
    private void MakeStoryPageList() {
        pageHumanStoryList = new Dictionary<int, List<ChapterData>>();
        pageOrcStoryList = new Dictionary<int, List<ChapterData>>();

        var queryPages =
            from _chapterData in human_chapterDatas
            group _chapterData by _chapterData.chapter into newGroup
            orderby newGroup.Key
            select newGroup;

        foreach(var newGroup in queryPages) {
            if (!pageHumanStoryList.ContainsKey(newGroup.Key)) pageHumanStoryList[newGroup.Key] = new List<ChapterData>();

            foreach(var chapter in newGroup) {
                pageHumanStoryList[newGroup.Key].Add(chapter);
            }
        }

        queryPages =
            from _chapterData in orc_chapterDatas
            group _chapterData by _chapterData.chapter into newGroup
            orderby newGroup.Key
            select newGroup;

        foreach(var newGroup in queryPages) {
            if (!pageOrcStoryList.ContainsKey(newGroup.Key)) pageOrcStoryList[newGroup.Key] = new List<ChapterData>();

            foreach(var chapter in newGroup) {
                pageOrcStoryList[newGroup.Key].Add(chapter);
            }
        }
    }

    public void NextPage() {
        currentPageIndex++;
        if (currentPageIndex > maxPageIndex) currentPageIndex = maxPageIndex;

        SetSubStoryListInfo(currentPageIndex);
    }

    public void PrevPage() {
        currentPageIndex--;
        if (currentPageIndex < 0) currentPageIndex = 0;

        SetSubStoryListInfo(currentPageIndex);
    }

    private void SetChapterHeaderAlert(string camp, ChapterData data, GameObject item) {
        bool isAlertExist = NewAlertManager.Instance.IsChapterAlertExist(camp, data.chapter, data.stage_number, data.require_level);
        item.transform.Find("Alert").gameObject.SetActive(isAlertExist);
    }

    private void SetSubStoryListInfo(int page = 0, int stageNumber = 0, string prevRace = null) {
        string camp = isHuman ? "human" : "orc";
        currentPageIndex = page;
        
        Transform canvas, content;
        List<ChapterData> selectedList;
        if (isHuman) {
            canvas = human.StageCanvas.transform;
            selectedList = pageHumanStoryList[page];
            content = human.stageContent.transform;

            maxPageIndex = pageHumanStoryList.Count - 1;
        }
        else {
            canvas = orc.StageCanvas.transform;
            selectedList = pageOrcStoryList[page];
            content = orc.stageContent.transform;

            maxPageIndex = pageOrcStoryList.Count - 1;
        }
        OnChapterChanged.Invoke(isHuman, page);

        AccountManager accountManager = AccountManager.Instance;
        var translator = accountManager.GetComponent<Fbl_Translator>();

        foreach (Transform child in content) {
            child.gameObject.SetActive(false);
            child.transform.Find("ClearCheckMask").gameObject.SetActive(false);
        }

        canvas
            .Find("HUD/ChapterSelect/BackGround/Text")
            .GetComponent<Text>()
            .text = "CHAPTER " + page;

        GameObject prevItem = null;
        for (int i=0; i < selectedList.Count; i++) {
            //if (selectedList[i].match_type == "testing") continue;
            GameObject item = content.GetChild(i).gameObject;

            string headerTxt = translator.GetLocalizedText("StoryLobby", selectedList[i].stage_Name);
            string str = string.Format("Stage {0}. {1}", selectedList[i].stage_number, headerTxt);
            item.transform.Find("StageName").GetComponent<TextMeshProUGUI>().text = str;
            //ShowReward(item ,selectedList[i]);
            StageButton stageButtonComp = item.GetComponent<StageButton>();
            stageButtonComp.Init(selectedList[i], isHuman, this, selectedList[i].require_level);

            var backgroundImage = GetStoryBackgroundImage(stageButtonComp.camp, stageButtonComp.chapter, stageButtonComp.stage);
            item.transform.Find("BackGround").GetComponent<Image>().sprite = backgroundImage;
            
            var clearedStageList = AccountManager.Instance.clearedStages;
            foreach (var list in clearedStageList) {
                if (list.chapterNumber == null) list.chapterNumber = 0;
            }
            
            if(clearedStageList.Exists(x => x.chapterNumber == stageButtonComp.chapter && x.camp == stageButtonComp.camp && x.stageNumber == stageButtonComp.stage)) {
                item.transform.Find("ClearCheckMask").gameObject.SetActive(true);
            }
            
            item.SetActive(true);
            
            string desc = translator.GetLocalizedText("StoryLobby", selectedList[i].description);

            SetStorySummaryText(
                desc, 
                item.transform.Find("StageScript").GetComponent<TextMeshProUGUI>()
            );

            if (item.transform.Find("Glow").gameObject.activeSelf)
                item.transform.Find("Glow").gameObject.SetActive(false);
            
            var lv = (int)accountManager.userData.lv;
            //default 선택 처리
            if (selectedList[i].chapter == page && selectedList[i].stage_number == stageNumber) {
                if (lv >= selectedList[i].require_level) {
                    item.GetComponent<StageButton>().OnClicked();    
                }
                else {
                    if(prevItem != null) prevItem.GetComponent<StageButton>().OnClicked();
                }
            }

            if (lv >= stageButtonComp.requireLevel) {
                var i1 = i;
                item.GetComponent<Button>().onClick.AddListener(() => {
                    NewAlertManager.Instance.CheckRemovable(
                        camp,
                        selectedList[i1].chapter, 
                        selectedList[i1].stage_number, 
                        selectedList[i1].require_level
                    );
                    item.transform.Find("Alert").gameObject.SetActive(false);
                });
            
                SetChapterHeaderAlert(camp, selectedList[i], item);
            }
            prevItem = item;
        }
        ShowTutoHand(isHuman ? "human" : "orc");
    }

    private string GetChapterNameLocalizeKeyword(int chapterNum, bool isHuman) {
        switch (chapterNum) {
            case 0:
                if (isHuman) return "txt_stage_lobby_h_tuto_chap_head";
                else return "txt_stage_lobby_o_tuto_chap_head";
            case 1:
                if (isHuman) return "txt_stage_lobby_h1_1_head";
                else return "txt_stage_lobby_o1_1_head";
        }
        return null;
    }

    private void offAllGlowEffect() {
        foreach (Transform item in human.stageContent.transform) {
            item.Find("Glow").gameObject.SetActive(false);
        }

        foreach (Transform item in orc.stageContent.transform) {
            item.Find("Glow").gameObject.SetActive(false);
        }
    }

    private void SetStorySummaryText(string data, TextMeshProUGUI targetTextComp) {
        int cutStandard = 45;
        StringBuilder cutStr = new StringBuilder();
        if(data.Length > cutStandard) {
            cutStr.Append(data.Substring(0, cutStandard));
            cutStr.Append("...");
        }
        else {
            cutStr.Append(data);
        }
        targetTextComp.text = cutStr.ToString();
    }

    private void ShowReward(GameObject item) {
        var stageButton = item.GetComponent<StageButton>();
        var rewards = stageButton.chapterData.scenarioReward;
        Color32 ReceivedBgColor = new Color32(140, 140, 140, 255);

        if (rewards == null) return;

        Transform rewardParent = stageCanvas.transform.Find("HUD/StagePanel/Rewards/HorizontalGroup");
        var clearedStageList = AccountManager.Instance.clearedStages;
        foreach(Transform tf in rewardParent) {
            tf.Find("Image").GetComponent<Image>().color = new Color32(255, 255, 255, 255);

            tf.gameObject.SetActive(false);
            tf.Find("ClearedMark").gameObject.SetActive(false);
        }
        
        for(int i=0; i<rewards.Length; i++) {
            string rewardType = rewards[i].reward;
            Sprite rewardImage = null;
            rewardParent.GetChild(i).gameObject.SetActive(true);
            rewardImage = AccountManager.Instance.resource.GetRewardIconWithBg(rewardType);
            rewardParent.GetChild(i).GetComponent<Button>().onClick.RemoveAllListeners();
            rewardParent.GetChild(i).GetComponent<Button>().onClick.AddListener(() => {
                RewardDescriptionHandler.instance.RequestDescriptionModalWithBg(rewardType);
            });

            rewardParent.GetChild(i).Find("Image").gameObject.SetActive(true);
            rewardParent.GetChild(i).Find("Image").GetComponent<Image>().sprite = rewardImage;
            rewardParent.GetChild(i).Find("Amount").GetComponent<TextMeshProUGUI>().text = "x" + rewards[i].count;
        }

        for (int i=rewards.Length; i<=4; i++) {
            rewardParent.GetChild(i).gameObject.SetActive(false);
        }

        if(clearedStageList.Exists(x => stageButton.chapter == 0 && x.camp == stageButton.camp && x.stageNumber == stageButton.stage)) {
            for (int i = 0; i < rewards.Length; i++) {
                rewardParent.GetChild(i).Find("ClearedMark").gameObject.SetActive(true);
                rewardParent.GetChild(i).Find("Image").GetComponent<Image>().color = ReceivedBgColor;
            }
        }
    }

    public void OnStageCloseBtn() {
        SoundManager.Instance.PlaySound(UISfxSound.BUTTON1);
        stageCanvas.SetActive(false);
    }

    private void CreateTutorialDeck(bool isHuman) {
        Deck dummyDeck = new Deck();
        dummyDeck.deckValidate = true;

        Transform deck = deckContent.transform.GetChild(0);
        deck.gameObject.SetActive(true);
        string deckName = "";
        if (isHuman) {
            deckName = AccountManager.Instance.GetComponent<Fbl_Translator>().GetLocalizedText("SampleDeck", "sampledeck_human_kingdomguards");
            dummyDeck.heroId = "h10001";
        }
        else {
            deckName = AccountManager.Instance.GetComponent<Fbl_Translator>().GetLocalizedText("SampleDeck", "sampledeck_orc_shamantribe");
            dummyDeck.heroId = "h10002";
        }

        //GameObject setDeck = Instantiate(deckPrefab, deckContent.transform);

        deck.GetComponent<Button>().onClick.AddListener(() => {
            OnDeckSelected(deck.gameObject, dummyDeck, true);
        });
        deck.Find("DeckName").GetComponent<TextMeshProUGUI>().text = deckName;
        deck.Find("HeroImg").GetComponent<Image>().sprite = AccountManager.Instance.resource.deckPortraite[dummyDeck.heroId];
        deck.Find("HeroImg").GetComponent<Image>().color = new Color32(255, 255, 255, 255);
        deck.Find("CardNum/Value").GetComponent<TextMeshProUGUI>().text = "40/";

        if (isHuman) {
            deck.Find("RaceFlag/Human").gameObject.SetActive(true);
            deck.Find("RaceFlag/Orc").gameObject.SetActive(false);
        }
        else {
            deck.Find("RaceFlag/Human").gameObject.SetActive(false);
            deck.Find("RaceFlag/Orc").gameObject.SetActive(true);
        }
        
        var deckCountText = stageCanvas.transform.Find("DeckSelectPanel/StagePanel/Header/Count").GetComponent<TextMeshProUGUI>();
        deckCountText.text = "1/1";
    }

    private void LoadMyDecks(bool isHuman) {
        List<Deck> totalDecks = new List<Deck>();
        AccountManager accountManager = AccountManager.Instance;

        switch (isHuman) {
            case true:
                totalDecks.AddRange(accountManager.humanDecks);
                break;
            case false:
                totalDecks.AddRange(accountManager.orcDecks);
                break;
            default:
                totalDecks = null;
                break;
        }

        if (totalDecks == null) return;
        PlayerPrefs.SetString("SelectedDeckId", "");

        int deckIndex = 0;

        for (int i = 0; i < totalDecks.Count; i++) {
            Deck deck = totalDecks[deckIndex];
            Transform setDeck = deckContent.transform.GetChild(i);

            setDeck.Find("HeroImg").gameObject.SetActive(true);
            if (totalDecks[deckIndex].bannerImage == "custom")
                setDeck.Find("HeroImg").GetComponent<Image>().sprite = AccountManager.Instance.resource.deckPortraite[deck.heroId];
            else {
                setDeck.Find("HeroImg").GetComponent<Image>().sprite = AccountManager.Instance.resource.deckPortraite[deck.bannerImage];
            }

            setDeck.transform.Find("DeckName").GetComponent<TextMeshProUGUI>().text = deck.name;

            var cardNumValue = setDeck.Find("CardNum/Value").GetComponent<TextMeshProUGUI>();
            setDeck.transform.Find("CardNum/Value").GetComponent<TextMeshProUGUI>().text = totalDecks[deckIndex].totalCardCount.ToString() + "/";

            Image heroImg = setDeck.Find("HeroImg").GetComponent<Image>();
            if (totalDecks[deckIndex].totalCardCount < 40) {
                heroImg.transform.Find("Block").gameObject.SetActive(true);
                heroImg.color = new Color32(60, 60, 60, 255);
                cardNumValue.color = new Color32(255, 0, 0, 255);
            }
            else {
                heroImg.transform.Find("Block").gameObject.SetActive(false);
                heroImg.color = new Color32(255, 255, 255, 255);
                cardNumValue.color = new Color32(255, 255, 255, 255);
            }

            setDeck.GetComponent<StringIndex>().Id = totalDecks[deckIndex].id;
            
            if (isHuman) {
                setDeck.Find("RaceFlag/Human").gameObject.SetActive(true);
                setDeck.Find("RaceFlag/Orc").gameObject.SetActive(false);
            }
            else {
                setDeck.Find("RaceFlag/Human").gameObject.SetActive(false);
                setDeck.Find("RaceFlag/Orc").gameObject.SetActive(true);
            }

            int temp = deckIndex;
            setDeck.GetComponent<Button>().onClick.AddListener(() => {
                Instance.OnDeckSelected(setDeck.gameObject, totalDecks[temp], true);
            });
            setDeck.gameObject.SetActive(true);
            deckIndex++;
        }

        var deckCountText = stageCanvas.transform.Find("DeckSelectPanel/StagePanel/Header/Count").GetComponent<TextMeshProUGUI>();
        deckCountText.text = totalDecks.Count + "/8";
    }

    public void OnDeckSelected(GameObject selectedDeckObject, dataModules.Deck data, bool isTutorial) {
        SoundManager.Instance.PlaySound(UISfxSound.BUTTON1);
        if (this.selectedDeckObject != null) {
            this.selectedDeckObject.transform.Find("FrontEffect").gameObject.SetActive(false);
            this.selectedDeckObject.transform.Find("Glow").gameObject.SetActive(false);
        }
        //selectedDeckObject.transform.Find("Outline").gameObject.SetActive(true);
        this.selectedDeckObject = selectedDeckObject;
        this.selectedDeckObject.transform.Find("FrontEffect").gameObject.SetActive(true);
        this.selectedDeckObject.transform.Find("Glow").gameObject.SetActive(true);
        //GameObject twinkle = selectedDeckObject.transform.Find("Deck/Twinkle").gameObject;
        //twinkle.SetActive(true);
        //twinkle.GetComponent<DeckClickSpine>().Click();
        object[] selectedInfo = new object[] { isTutorial, data };
        PlayerPrefs.SetString("SelectedDeckId", data.id);
        PlayerPrefs.SetString("selectedHeroId", data.heroId);

        selectedDeck = selectedInfo;
        GameObject startButton = stageCanvas.transform.Find("DeckSelectPanel/StagePanel/StartButton").gameObject;
        if (!startButton.activeSelf) {
            startButton.SetActive(true);
        }
    }

    private void ClearDeckList() {
        foreach (Transform child in deckContent.transform) {
            child.Find("FrontEffect").gameObject.SetActive(false);
            child.Find("Glow").gameObject.SetActive(false);
            child.gameObject.SetActive(false);
        }
    }

    public void OnCloseBtn() {
        SoundManager.Instance.PlaySound(UISfxSound.BUTTON1);
        stageCanvas.SetActive(false);
        HUDController.gameObject.SetActive(true);
    }

    public void OnClickStage() {
        SoundManager.Instance.PlaySound(UISfxSound.BUTTON1);
        if (isHuman) {
            orc.StageCanvas.transform.Find("HUD/StageSelect/Buttons").gameObject.SetActive(false);
            human.StageCanvas.transform.Find("HUD/StageSelect/Buttons").gameObject.SetActive(true);
        }
        else {
            human.StageCanvas.transform.Find("HUD/StageSelect/Buttons").gameObject.SetActive(false);
            orc.StageCanvas.transform.Find("HUD/StageSelect/Buttons").gameObject.SetActive(true);
        }

        offAllGlowEffect();
        selectedChapterObject.transform.Find("Glow").gameObject.SetActive(true);
    }

    public void OpenStoryDetailWindow() {
        SetBackButton(2);
        EscapeKeyController.escapeKeyCtrl.AddEscape(CloseStoryDetail);

        var stageButton = selectedChapterObject.GetComponent<StageButton>();
        if (stageButton == null) return;

        stageCanvas.SetActive(true);
        bool isTutorial = stageButton.isTutorial;

        Image background = stageCanvas.transform.Find("HUD/BackGround").GetComponent<Image>();
        Image descBackground = stageCanvas.transform.Find("HUD/StagePanel/Body").GetComponent<Image>();
        Image victoryBackground = stageCanvas.transform.Find("HUD/StagePanel/VictoryConditions/Portrait").GetComponent<Image>();

        var translator = AccountManager.Instance.GetComponent<Fbl_Translator>();

        if (isHuman) {
            background.sprite = human.background;
            descBackground.sprite = human.readyCanvasBg;
        }
        else {
            background.sprite = orc.background;
            descBackground.sprite = orc.readyCanvasBg;
        }
        
        var heroNamekey = GetHeroLocalizeKey(stageButton.chapterData.enemyHeroId);
        stageCanvas
            .transform
            .Find("HUD/StagePanel/VictoryConditions/HeroName")
            .gameObject
            .GetComponent<TextMeshProUGUI>().text = translator.GetLocalizedText("Hero", heroNamekey);

        if (storyHeroPortraits.ContainsKey(stageButton.chapterData.enemyHeroId)) {
            victoryBackground.sprite = storyHeroPortraits[stageButton.chapterData.enemyHeroId];
        }

        stageCanvas
                 .transform
                 .Find("HUD/StagePanel/Body/StageName")
                 .gameObject
                 .GetComponent<TextMeshProUGUI>().text = translator.GetLocalizedText("StoryLobby", stageButton.stageName);

        stageCanvas
            .transform
            .Find("HUD/StagePanel/Body/Description")
            .gameObject
            .GetComponent<Text>().text = translator.GetLocalizedText("StoryLobby", stageButton.description);

        ShowReward(selectedChapterObject);

        stageCanvas
            .transform
            .Find("HUD/StagePanel/VictoryConditions/Description")
            .gameObject
            .GetComponent<TextMeshProUGUI>().text = stageButton.chapterData.specialRule;

        var storyEnemyHeroInfo = stageCanvas
            .transform
            .Find("HUD/HeroInfo")
            .GetComponent<StoryEnemyHeroInfo>();
        object[] data = new object[] { isHuman, stageButton };
        storyEnemyHeroInfo.SetData(data);
    }

    public void OpenDeckListWindow() {
        ClearDeckList();

        SetBackButton(3);
        EscapeKeyController.escapeKeyCtrl.AddEscape(CloseDeckList);

        stageCanvas.gameObject.SetActive(true);
        stageCanvas.transform.Find("DeckSelectPanel").gameObject.SetActive(true);
        var stageButton = selectedChapterObject.GetComponent<StageButton>();
        bool isTutorial = stageButton.isTutorial;

        if (isTutorial) {
            CreateTutorialDeck(isHuman);
        }
        else {
            LoadMyDecks(isHuman);
        }

        Image background = stageCanvas.transform.Find("DeckSelectPanel/BackGround").GetComponent<Image>();
        Image headerImage = stageCanvas.transform.Find("DeckSelectPanel/StagePanel/Header").GetComponent<Image>();

        if (isHuman) {
            background.sprite = human.background;
            headerImage.sprite = human.headerBg;
        }
        else {
            background.sprite = orc.background;
            headerImage.sprite = orc.headerBg;
        }
    }

    public void OnStartBtn() {
        if (isIngameButtonClicked) {
            Logger.Log("이미 대전 시작 버튼이 눌려진 상태");
            return;
        }
        AccountManager.Instance.startSpread = true;
        AccountManager.Instance.beforeBox = AccountManager.Instance.userResource.supplyBox;
        AccountManager.Instance.beforeSupply = AccountManager.Instance.userResource.supply;
        PlayerPrefs.SetString("SelectedBattleType", "story");
        string race = PlayerPrefs.GetString("SelectedRace").ToLower();

        if(selectedDeck == null) {
            Modal.instantiate("덱을 선택해 주세요", Modal.Type.CHECK);
            SoundManager.Instance.PlaySound(SoundType.FIRST_TURN);
            return;
        }

        object[] selectedDeckInfo = (object[])selectedDeck;
        bool isTutorial = (bool)selectedDeckInfo[0];
        if (selectedChapterData.chapter == 0) {
            FBL_SceneManager.Instance.LoadScene(FBL_SceneManager.Scene.CONNECT_MATCHING_SCENE);
            PlayMangement.chapterData = selectedChapterData;
            PlayerPrefs.SetString("BattleMode", selectedChapterData.match_type);
        }
        else {
            string selectedDeckId = PlayerPrefs.GetString("SelectedDeckId").ToLower();
            Deck selectedDeck = (dataModules.Deck)selectedDeckInfo[1];

            if (race != null && !string.IsNullOrEmpty(selectedDeckId)) {
                if (selectedDeck.deckValidate) {
                    isIngameButtonClicked = true;

                    FBL_SceneManager.Instance.LoadScene(FBL_SceneManager.Scene.CONNECT_MATCHING_SCENE);
                    PlayMangement.chapterData = selectedChapterData;
                }
                else {
                    if(selectedDeck.totalCardCount < 40) {
                        var translator = AccountManager.Instance.GetComponent<Fbl_Translator>();
                        string message = translator.GetLocalizedText("UIPopup", "ui_popup_cantusedeck");
                        string okBtn = translator.GetLocalizedText("UIPopup", "ui_popup_check");
                        string header = translator.GetLocalizedText("UIPopup", "ui_popup_check");

                        Modal.instantiate(
                            message,
                            Modal.Type.CHECK,
                            btnTexts: new string[] { okBtn },
                            headerText: header
                        );
                    }
                }
            }
            else {
                if (race == "none") Logger.Log("종족을 선택해야 합니다.");
                if (string.IsNullOrEmpty(selectedDeckId)) Logger.Log("덱을 선택해야 합니다.");

                if (race == "none") {
                    Modal.instantiate("종족을 선택해 주세요.", Modal.Type.CHECK);
                }
                else if (string.IsNullOrEmpty(selectedDeckId)) {
                    Modal.instantiate("덱을 선택해 주세요.", Modal.Type.CHECK);
                }
            }
        }
        SoundManager.Instance.PlaySound(SoundType.FIRST_TURN);
    }

    public void SelectChallengeData(int chapterNum, int stageNum, string camp) {
        try {
            List<ChallengeData> list;
            list = camp == "human" ? human_challengeDatas : orc_challengeDatas;
            selectedChallengeData = list.Find(x => x.chapterNum == chapterNum && x.stageNum == stageNum);
            ScenarioGameManagment.challengeDatas = selectedChallengeData.challenges;
        }
        catch(NullReferenceException ex) { }
    }

    private QuestTutorial questTutorial;

    public void SetTutoQuest(Quest.QuestContentController quest, int stage) {
        questTutorial = new QuestTutorial();
        questTutorial.quest = quest;
        questTutorial.stage = stage;
        if(Instance == null) return;
        ShowTutoHand("human");
    }

    

    public class QuestTutorial {
        public Quest.QuestContentController quest;
        public int stage;
        [HideInInspector] public GameObject handUI;
    }

    public void ShowTutoHand(string camp) {
        if(questTutorial == null) return;
        if(questTutorial.handUI != null) {
            DestroyImmediate(questTutorial.handUI);
            questTutorial.handUI = null;
        }
        
        bool isClear = AccountManager.Instance.clearedStages.Exists(x=>(
            x.chapterNumber == null && 
            x.stageNumber == questTutorial.stage && 
            String.Compare(x.camp, camp, StringComparison.Ordinal) == 0)
        );
        
        if(isClear) return;
        StageButton[] stages = transform.GetComponentsInChildren<StageButton>();
        StageButton stage = Array.Find(stages, x => (x.chapter == 0 && x.stage == questTutorial.stage && x.camp.CompareTo(camp) == 0));
        if(stage == null) return;
        questTutorial.handUI = Instantiate(questTutorial.quest.manager.handSpinePrefab, stage.transform, false);
    }

    public TutorialSerializedList tutorialSerializedList;

    [Serializable] public class TutorialSerializedList {
        public ScenarioManager scenarioManager;
    }

    public Sprite GetStoryBackgroundImage(string camp, int chapterNumber, int stageNumber) {
        
        string defaultKey = camp + "_default";
        //Logger.Log("defaultKey : " + defaultKey);
        Sprite selectedImage = null;
        stroyBackgroundImages
            .TryGetValue(camp + "_" + chapterNumber + "-" + stageNumber, out selectedImage);
        if(selectedImage == null) selectedImage = stroyBackgroundImages[defaultKey];
        return selectedImage;
    }

    public string GetHeroLocalizeKey(string heroId) {
        string result = heroId
            .Contains("qh") ? "hero_npc_" + heroId + "_name" : "hero_pc_" + heroId + "_name";
        return result;
    }
}

namespace Tutorial {
    [System.Serializable]
    public class ShowSelectRace {
        public GameObject raceButton;
        public Sprite activeSprite;
        public Sprite deactiveSprite;
        public Sprite victoryConditionBg;
        public GameObject heroSelect;
        public GameObject StageCanvas;
        public GameObject heroContent;
        public GameObject stageContent;
        public Sprite readyCanvasBg;
        public Sprite headerBg;
        public Sprite background;
        public GameObject chapterHeader;
    }

    public class ScenarioButton : MonoBehaviour {
        protected ScenarioManager scenarioManager;

        private void Start() {
            scenarioManager = ScenarioManager.Instance;
        }

        public virtual void OnClicked() { }
    }

    public class ChapterData {
        public int chapter;
        public int stage_number;
        public int require_level;
        public string stage_Name;
        public string match_type;
        public string map;
        public string myHeroId;
        public string enemyHeroId;

        [MultiLineProperty(10)] public string description;
        [MultiLineProperty(5)] public string specialRule;
        public List<ScriptData> scripts;
        public ScenarioReward[] scenarioReward;

        public int stageSerial;
    }

    public class CommonTalking {
        public string talkingTiming;
        public List<ScriptData> scripts;
    }

    public class ScriptEndChapterDatas {
        public int chapter;
        public int stage_number;
        public int isWin;
        public List<Method> methods;
    }

    public class ScriptData {
        [MultiLineProperty(10)] public string Print_text;
        public List<Method> methods;

        public bool isExecute;
    }

    public class Method {
        public string name;
        public List<string> args;
    }

    public class ScenarioReward {
        public string reward;
        public int count;
    }


}