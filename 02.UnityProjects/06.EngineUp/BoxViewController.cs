using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BoxViewController : MonoBehaviour {
    public Text numOfBoxLabel;

    private GameManager gm;
    private SoundManager sm;
    private Box_Inventory boxStore;
    private TweenPosition tP;

    public Sprite defaultSlotImg;

    public User userStore;

    public GameObject
        notifyModal,
        singleOpenModal,
        multiOpenModal;

    public GameObject _openEffect;
    private bool
        isReverse_tp,
        canClick = true;

    public MainViewController mV;
    public SpritesManager spriteManager;

    private Color32[] grades = new Color32[]{
        new Color32(166, 166, 166, 255), 
        new Color32(151, 197, 58, 255),
        new Color32(58, 133, 197, 255),
        new Color32(166, 98, 185, 255)
        };

    void Awake() {
        gm = GameManager.Instance;
        sm = SoundManager.Instance;

        boxStore = gm.boxInvenStore;

        tP = GetComponent<TweenPosition>();
    }

    void OnEnable() {
        tweenPos();

        isReverse_tp = false;
    }

    void OnDisable() {
        tP.ResetToBeginning();
    }

    public void tweenPos() {
        if (!isReverse_tp) {
            tP.PlayForward();
        }
        else {
            //swap
            Vector3 tmp;
            tmp = tP.to;
            tP.to = tP.from;
            tP.from = tmp;

            tP.ResetToBeginning();
            tP.PlayForward();
            StopCoroutine("openEffect");
        }
    }

    public void tPFinished() {
        if (isReverse_tp) {
            gameObject.SetActive(false);
            gameObject.transform.Find("TopPanel").gameObject.SetActive(false);
        }

        else {
            gameObject.transform.Find("TopPanel").gameObject.SetActive(true);

            MyInfo act = ActionCreator.createAction(ActionTypes.MYINFO) as MyInfo;
            gm.gameDispatcher.dispatch(act);
        }

        isReverse_tp = true;
    }

    public void onUserStoreListener() {
        if(userStore.eventType == ActionTypes.MYINFO) {
            if(userStore.storeStatus == storeStatus.NORMAL) {
                string boxNum = userStore.myData.boxes.ToString();
                numOfBoxLabel.text = boxNum;
            }
        }
    }

    public void onBoxStoreListener() {
        if (boxStore.eventType == ActionTypes.BOX_OPEN) {
            if (boxStore.storeStatus == storeStatus.NORMAL) {
                //박스 열기 정상 동작시

                var items = boxStore.openedItem;
                int itemCount = items.Length;

                //박스 1개 열기
                if(itemCount == 1) {
                    sm.playEffectSound(4);
                    singleOpenModal.SetActive(true);
                    Text name = singleOpenModal.transform.Find("InnerModal/Name").GetComponent<Text>();
                    Image image = null;

                    init();

                    var openedItem = boxStore.openedItem;
                    string type = openedItem[0].type;
                    if (type == "item") {
                        Debug.Log("Item");
                        var tmp = spriteManager.stage_items[openedItem[0].item.id - 1];
                        if (openedItem[0].item.parts == "FR") {
                            image = singleOpenModal.transform.Find("InnerModal/Frame").GetComponent<Image>();
                        }
                        else if (openedItem[0].item.parts == "WH") {
                            image = singleOpenModal.transform.Find("InnerModal/Wheel/Image").GetComponent<Image>();
                        }
                        else if (openedItem[0].item.parts == "DS") {
                            image = singleOpenModal.transform.Find("InnerModal/Engine").GetComponent<Image>();
                        }
                        image.enabled = true;

                        if (tmp == null) {
                            image.sprite = spriteManager.default_slots[openedItem[0].item.grade];
                        }
                        else {
                            image.sprite = spriteManager.stage_items[openedItem[0].item.id - 1];
                        }
                        name.text = openedItem[0].item.name;
                    }
                    else if (type == "character") {
                        Debug.Log("CHAR");
                        image = singleOpenModal.transform.Find("InnerModal/Character").GetComponent<Image>();
                        image.enabled = true;

                        image.sprite = mV.characters_slots[openedItem[0].character.id - 1].images[0];
                        name.text = openedItem[0].character.name;
                    }

                    Image grade_img = singleOpenModal.transform.Find("InnerModal/Grade").GetComponent<Image>();
                    int grade = openedItem[0].item.grade;
                    grade_img.color = grades[grade];
                    name.color = grades[grade];

                    canClick = true;
                }
                
                //다중 열기
                else {
                    multiOpenModal.SetActive(true);
                    GameObject table = multiOpenModal.transform.Find("InnerModal/Grid").gameObject;
                    List<Transform> list = new List<Transform>();

                    foreach(Transform tr in table.transform) {
                        list.Add(tr);
                    }
                    init(list);
                    StartCoroutine(openEffect(list, itemCount, items, multiOpenModal));
                }
            }
        }
    }

    //공구함 열기 버튼 클릭시
    //박스 열기 Action 이전 Animation 발생시킴
    //Animation 마지막에 open 함수 실행
    public void startAnim(GameObject obj) {
        offBoxOpenModal(obj);
        int index = obj.GetComponent<ButtonIndex>().index;
        open(index);
    }

    //박스 열기 Action 전송
    public void open(int index) {
        //button index 판별
        if(!canClick) {
            return;
        }
        canClick = false;

        int boxNum = userStore.myData.boxes;
        int openNum = 0;
        garage_box_open act = ActionCreator.createAction(ActionTypes.BOX_OPEN) as garage_box_open;
        switch (index) {
            case 0:
                openNum = 1;
                break;
            case 1:
                openNum = 10;
                break;
        }
        //더 이상 열 수 있는 박스가 없는 경우 Modal 활성화
        if(boxNum >= openNum) {
            act.num = openNum;
            gm.gameDispatcher.dispatch(act);
            if(openNum == 10) {
                multiOpenModal.transform.Find("InnerModal/BottonPanel/ConfirmButton").gameObject.GetComponent<Button>().enabled = false;
                multiOpenModal.transform.Find("InnerModal/BottonPanel/CancelButton").gameObject.GetComponent<Button>().enabled = false;
            }
            //singleOpenButton.GetComponent<UIPlaySound>().Play();
        }
        else {
            onNotifyModal();
            //notifyModal.GetComponent<UIPlaySound>().Play();
        }
    }

    //박스가 없는 경우
    public void onNotifyModal() {
        notifyModal.SetActive(true);
    }

    public void offNotifyModal() {
        notifyModal.SetActive(false);
    }

    public void offBoxOpenModal(GameObject obj) {
        obj.SetActive(false);
    }

    IEnumerator openEffect(List<Transform> list, int itemCount, Box_Inventory.boxOpenCallback[] items, GameObject modal) {
        int cnt = 0;
        foreach (Transform item in list) {
            if (cnt < itemCount) {
                //effect
                GameObject effect = Instantiate(_openEffect);
                effect.transform.SetParent(item.transform, false);
                //effect.transform.localPosition = new Vector3(0f, 55f, 0f);
                //effect.transform.localScale = Vector3.one;
                sm.playEffectSound(4);
                //setUI
                string type = items[cnt].type;
                Text label = item.Find("Name").GetComponent<Text>();
                Image sprite = null;
                string parts = items[cnt].item.parts;
                item.Find("Grade").GetComponent<Image>().enabled = true;
                if (type == "item") {
                    var tmp = spriteManager.stage_items[items[cnt].item.id - 1];
                    if (tmp == null) {
                        sprite = item.Find("Frame").GetComponent<Image>();
                        sprite.enabled = true;
                        sprite.sprite = spriteManager.default_slots[items[cnt].item.grade];
                    }
                    else {
                        if(parts == "FR") {
                            sprite = item.Find("Frame").GetComponent<Image>();
                            
                        }
                        else if(parts == "DS") {
                            sprite = item.Find("Engine").GetComponent<Image>();
                        }
                        else if(parts == "WH") {
                            sprite = item.Find("Wheel/Image").GetComponent<Image>();
                        }
                        sprite.enabled = true;
                        sprite.sprite = spriteManager.stage_items[items[cnt].item.id - 1];
                    }
                    label.text = items[cnt].item.name;
                }
                else if (type == "character") {
                    sprite = item.Find("Character").GetComponent<Image>();
                    sprite.enabled = true;

                    sprite.sprite = mV.characters_slots[items[cnt].character.id - 1].images[0];
                    label.text = items[cnt].character.name;
                }

                Image grade_img = item.transform.Find("Grade").GetComponent<Image>();
                int grade = items[cnt].item.grade;
                grade_img.color = grades[grade];
                label.color = grades[grade];
                cnt++;
            }

            yield return new WaitForSeconds(1.0f);
        }
        modal.transform.Find("InnerModal/BottonPanel/ConfirmButton").gameObject.GetComponent<Button>().enabled = true;
        modal.transform.Find("InnerModal/BottonPanel/CancelButton").gameObject.GetComponent<Button>().enabled = true;

        canClick = true;
    }

    public void init(List<Transform> list = null) {
        if(list != null) {
            foreach (Transform item in list) {
                item.Find("Frame").GetComponent<Image>().enabled = false;
                item.Find("Wheel/Image").GetComponent<Image>().enabled = false;
                item.Find("Engine").GetComponent<Image>().enabled = false;
                item.Find("Character").GetComponent<Image>().enabled = false;
                item.Find("Name").GetComponent<Text>().text = "";
                item.Find("Grade").GetComponent<Image>().enabled = false;
            }
        }
        else {
            singleOpenModal.transform.Find("InnerModal/Frame").GetComponent<Image>().enabled = false;
            singleOpenModal.transform.Find("InnerModal/Wheel/Image").GetComponent<Image>().enabled = false;
            singleOpenModal.transform.Find("InnerModal/Engine").GetComponent<Image>().enabled = false;
            singleOpenModal.transform.Find("InnerModal/Character").GetComponent<Image>().enabled = false;
        }
    }
}
