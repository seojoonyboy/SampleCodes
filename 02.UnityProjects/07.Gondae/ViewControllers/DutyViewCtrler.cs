using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DutyViewCtrler : MonoBehaviour {
    public GameObject 
        listPanel;
    public Sprite[] iconArr;
    private GameManager gameManager;
    private UIManager uiManager;
    private SoundManager soundManager;

    private int
        skillIndex,
        skillListNum;
    private GameObject[] panelLists;
    private DutySkill[] skillLists;

    private GameObject selectedPanel;

    void Start() {

    }

    void Update() {
        checkCanUpgrade();
    }

    void Awake() {
        uiManager = UIManager.Instance;
        iconArr = uiManager.dutyIconArr;
        initialize();
        makeList();
    }

    void OnEnable() {
        initialize();
        refreshData();
    }

    void initialize() {
        gameManager = GameManager.Instance;
        soundManager = SoundManager.Instance;

        skillLists = gameManager.dutySkills.skills;
        skillListNum = skillLists.Length;
    }

    private void makeList() {
        panelLists = new GameObject[skillListNum];
        GameObject mainPanel = transform.Find("MaskLayout/InnerPanel").gameObject;
        RectTransform rect;
        //rect.localScale = new Vector3(1, 1, 0);
        for (int i = 0; i < skillListNum; i++) {
            panelLists[i] = Instantiate(listPanel);
            panelLists[i].transform.SetParent(mainPanel.transform);
            panelLists[i].name = "List" + i;
            panelLists[i].transform.Find("NameText").GetComponent<Text>().text = skillLists[i].name;
            panelLists[i].transform.Find("Image/LvText").GetComponent<Text>().text = skillLists[i].level.ToString();
            panelLists[i].transform.Find("AmntPanel/TextBg/AmntText").GetComponent<Text>().text = "+ " + skillLists[i].touch.ToFormattedString();
            panelLists[i].transform.Find("CostPanel/TextBg/CostText").GetComponent<Text>().text = skillLists[i].upgradeCost.ToFormattedString();

            panelLists[i].GetComponent<ListItem>().index = i;
            panelLists[i].AddComponent<Button>();
            GameObject imageObj = panelLists[i].transform.Find("Image").gameObject;
            Image img = imageObj.GetComponent<Image>();
            //imageObj.GetComponent<LayoutElement>().preferredWidth = Screen.width / 4.0f;
            //imageObj.GetComponent<LayoutElement>().preferredWidth = 0;
            rect = panelLists[i].GetComponent<RectTransform>();
            rect.localScale = new Vector3(1, 1, 0);
            DutySkill skill = skillLists[i];

            img.sprite = iconArr[i];

            if (skill.level < 1) {
                panelLists[i].SetActive(false);
            }
        }
        Button backBtn = transform.Find("BottomPanel/BackBtn").GetComponent<Button>();
        backBtn.onClick.AddListener(onBackBtn);
    }



    private void checkCanUpgrade() {
        for (int i = 0; i < panelLists.Length; i++) {
            Image[] images = panelLists[i].GetComponentsInChildren<Image>();
            Button panelBtn = panelLists[i].GetComponent<Button>();

            panelBtn.onClick.RemoveAllListeners();

            if (gameManager.gondaeReuk >= skillLists[i].upgradeCost) {                
                panelBtn.onClick.AddListener(onClickList);
                foreach (Image img in images) {
                    img.color = new Color32(255, 255, 255, 255);
                }                    
            }
            else {
                panelBtn.onClick.RemoveListener(onClickList);
                foreach(Image img in images) {
                    img.color = new Color32(158, 158, 158, 255);
                }
            }
        }
    }

    public void onBackBtn() {
        gameObject.SetActive(false);
        soundManager.SE_ON_BACK_BUTTON.Play();
        uiManager.menuBtns[0].GetComponent<Image>().sprite = uiManager.menuBtnDeActiveImgArr[0];
    }

    public void onClickList() {
        selectedPanel = EventSystem.current.currentSelectedGameObject;
        skillIndex = selectedPanel.GetComponent<ListItem>().index;
        if (gameManager.gondaeReuk >= skillLists[skillIndex].upgradeCost) {
            soundManager.SE_DUTY_UP.Play();
            gameManager.dutySkillLevelUp(skillIndex);
            refreshData(skillIndex);
        }        
    }

    public void refreshData() {
        for (int i = 0; i < skillListNum; i++) {
            DutySkill skill = skillLists[i];
            if (skill.level >= 1) {
                panelLists[i].SetActive(true);
                panelLists[i].transform.Find("Image/LvText").GetComponent<Text>().text = skillLists[i].level.ToString();
            }
        }
    }

    public void refreshData(int index) {
        panelLists[index].transform.Find("NameText").GetComponent<Text>().text = skillLists[index].name;
        panelLists[index].transform.Find("Image/LvText").GetComponent<Text>().text = skillLists[index].level.ToString();
        panelLists[index].transform.Find("AmntPanel/TextBg/AmntText").GetComponent<Text>().text = "+ " + skillLists[index].touch.ToFormattedString();
        panelLists[index].transform.Find("CostPanel/TextBg/CostText").GetComponent<Text>().text = skillLists[index].upgradeCost.ToFormattedString();
    }

    
}