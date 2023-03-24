using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class StoryViewCtrler : MonoBehaviour {
    private GameManager gameManager;
    private UIManager uiManager;
    private SoundManager soundManager;
    private EventManager eventManager;

    private GameObject parentOfLists;
    private Chapter[] chapter;

    public GameObject
        mainListsPref,
        subLists;

    public GameObject[] lists;

    private bool listExists = false;

    void Awake() {
        soundManager = SoundManager.Instance;
        uiManager = UIManager.Instance;
        gameManager = GameManager.Instance;
        eventManager = EventManager.Instance;
    }
    
    void Start() {
        if (!listExists) {
            makeList();
            listExists = true;
            refresh();
        }
                    
    }

    void OnEnable() {
        initialize();
        if (listExists) {
            refresh();
        }
        setBGM();
    }

    void OnDisable() {
        soundManager.SE_ON_BACK_BUTTON.Play();
        soundManager.BGM_MAIN.Play();
        foreach(AudioSource src in soundManager.BGM_STORY) {
            src.Stop();
        }
    }

    public void onBackBtn() {
        gameObject.SetActive(false);
        //uiManager.callLayout("MAIN");
        uiManager.menuBtns[5].GetComponent<Image>().sprite = uiManager.menuBtnDeActiveImgArr[5];        
    }

    private void makeList() {
        Chapter[] chapter = gameManager.story.chapters;
        //메인, 서브 스토리 gameobject를 담기 위한 배열 => 이후 스토리 해금 이벤트 발생시 해당 gameobject 접근
        lists = new GameObject[chapter.Length];
        GameObject listObj = null;

        Transform parent = null;

        int 
            mainChapterIndex = 0,
            subChapterIndex = 0;

        for (int i=0; i<chapter.Length; i++) {
            //메인 스토리
            if (chapter[i].subChapter == 0) {
                subChapterIndex = 0;
                parent = parentOfLists.transform;
                listObj = Instantiate(mainListsPref);
                listObj.name = "MainList" + i;
                listObj.transform.SetParent(parent);                
                listObj.GetComponent<RectTransform>().transform.localScale = new Vector3(1, 1, 0);                

                GameObject mainstory = listObj.transform.Find("MainStory").gameObject;
                mainstory.GetComponent<ListItem>().index = i;

                Text labelText = mainstory.transform.Find("LabelText").GetComponent<Text>();

                labelText.text = "EP" + mainChapterIndex;
                parent = listObj.transform.Find("VerticalLayout").transform;

                mainstory.GetComponent<Image>().sprite = uiManager.mainStoryImgArr[mainChapterIndex];
                lists[i] = listObj;
                mainChapterIndex++;
            }
            
            //서브 스토리
            else if(chapter[i].subChapter != 0) {                
                GameObject subListObj = Instantiate(subLists);
                subListObj.transform.SetParent(parent);
                subListObj.name = "SubList" + i;
                subListObj.GetComponent<ListItem>().index = i;

                Text labelText = subListObj.transform.Find("Text").GetComponent<Text>();
                labelText.text = "단서 #" + subChapterIndex;

                subListObj.GetComponent<RectTransform>().transform.localScale = new Vector3(1, 1, 0);
                subChapterIndex++;
                //Debug.Log(chapter[i].isPlayed);
                lists[i] = subListObj;
            }
        }
    }

    private void initialize() {
        parentOfLists = gameObject.transform.Find("MaskLayout/InnerPanel").gameObject;
    }

    public void onModal() {
        int index = EventSystem.current.currentSelectedGameObject.GetComponent<ListItem>().index;
        Chapter selectedChapter = chapter[index];
        ModalQueueMessage message = new ModalQueueMessage(ModalEventType.STORY, selectedChapter);
        eventManager.receiveModalEvent(message);
    }

    private void setBGM() {
        int level = gameManager.grade.socializationLevel;
        if (level < 3) {
            soundManager.BGM_STORY[0].Play();
        }
        else if(level >= 3) {
            soundManager.BGM_STORY[1].Play();
        }
        else {
            soundManager.BGM_STORY[0].Play();
        }
        soundManager.BGM_MAIN.Stop();
        //Debug.Log("Resocial LV : " + level);
    }

    //재사회화 추가 해금된 정보 적용
    public bool refresh() {
        gameManager = GameManager.Instance;
        chapter = gameManager.story.chapters;

        if (chapter == null || lists == null) {
            return false;
        }

        else {
            if(chapter.Length != lists.Length) {
                return false;
            }
            for(int i=0; i<chapter.Length; i++) {
                if (chapter[i].isPlayed) {
                    if(lists[i].tag == "MainStory") {
                        lists[i].transform.Find("MainStory/LockPanel").gameObject.SetActive(false);
                        lists[i].transform.Find("MainStory").GetComponent<Button>().onClick.AddListener(onModal);
                    }

                    else if(lists[i].tag == "SubStory") {
                        lists[i].transform.Find("LockPanel").gameObject.SetActive(false);
                        lists[i].transform.GetComponent<Button>().onClick.AddListener(onModal);
                    }
                }
                else if (!chapter[i].isPlayed) {
                    if(lists[i].tag == "MainStory") {
                        
                    }

                    else if(lists[i].tag == "SubStory") {
                        lists[i].transform.GetComponent<Button>().onClick.RemoveListener(onModal);
                    }
                }
            }
            return true;
        }        
    }
}
