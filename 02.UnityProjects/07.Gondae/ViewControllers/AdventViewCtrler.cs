using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ScottGarland;

public class AdventViewCtrler : MonoBehaviour {
    private GameManager gameManager;
    private UIManager uiManager;
    private SoundManager soundManager;

    private int adventIndex;
    private Advent[] adventLists;

    public GameObject 
        modal,
        shadow;
    private Text
        nameText, 
        costText,
        descText;
    private Button summonBtn;

    void Awake() {
        setModal();
    }

    void OnEnable() {
        gameManager = GameManager.Instance;
        uiManager = UIManager.Instance;
        soundManager = SoundManager.Instance;
        soundManager.BGM_MAIN.Stop();
        soundManager.BGM_ADVENT.Play();

        adventLists = gameManager.advents.list;
    }

    void OnDisable() {
        soundManager.BGM_ADVENT.Stop();
        soundManager.BGM_MAIN.Play();
        summonBtn.onClick.RemoveListener(onAdventBtnClick);
        modal.SetActive(false);
    }

    public void onBackBtn() {
        gameObject.SetActive(false);
        soundManager.SE_ON_BACK_BUTTON.Play();
        
        uiManager.menuBtns[4].GetComponent<Image>().sprite = uiManager.menuBtnDeActiveImgArr[4];
    }

    //버튼 클릭시
    public void OnClick() {
        GameObject selectedObj = EventSystem.current.currentSelectedGameObject.gameObject;
        adventIndex = selectedObj.GetComponent<ListItem>().index;
        shadow.transform.position = selectedObj.transform.position;
        shadow.GetComponent<Image>().sprite = selectedObj.GetComponent<Image>().sprite;
        float width = selectedObj.GetComponent<RectTransform>().rect.width * 1.2f;
        float height = selectedObj.GetComponent<RectTransform>().rect.height * 1.1f;
        shadow.GetComponent<RectTransform>().sizeDelta = new Vector3(width, height, 0);
        OnModal();
    }

    
    private void OnModal() {
        modal.SetActive(true);
        nameText.text = adventLists[adventIndex].name.ToString();
        descText.text = "곤대신 강림   <color=red>" + adventLists[adventIndex].level + "%</color>\n" + "패시브 획득 <color=red>  + " + adventLists[adventIndex].passiveBase + "</color>";

        string cost = adventLists[adventIndex].cost.ToString("N0");
        costText.text = cost;
    }

    private void setModal() {
        GameObject modalPanel = modal.transform.Find("ModalPanel").gameObject;

        nameText = modalPanel.transform.Find("NameText").GetComponent<Text>();
        descText = modalPanel.transform.Find("DescText").GetComponent<Text>();
        costText = modalPanel.transform.Find("CostBg/CostText").GetComponent<Text>();

        summonBtn = modalPanel.transform.Find("AdventBtn").GetComponent<Button>();
        summonBtn.onClick.AddListener(onAdventBtnClick);
    }

    private void onAdventBtnClick() {
        BigInteger cost = new BigInteger(adventLists[adventIndex].passiveBase);
        if(gameManager.gondaeReuk >= cost) {
            gameManager.adventLvUp(adventIndex);
            modal.SetActive(false);
        }
    }
    

    public void offModal() {
        modal.SetActive(false);
    }
}
