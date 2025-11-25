using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BicycleDetailViewController : MonoBehaviour {
    private GameManager gm;
    private SoundManager sm;
    private BicycleItem_Inventory bicycleItemStore;
    private User userStore;

    private Animator animator;
    private int id = -1;

    public BicycleListViewController parent;

    public SpritesManager spriteManager;

    public GameObject[] specs;

    public GameObject 
        notifyModal,
        equipButton,
        unEquipButton,
        tierImg,
        tierGrid;

    private Color32 increaseColor = new Color32(31, 196, 97, 255);
    private Color32 decreaseColor = new Color32(201, 48, 48, 255);
    private Color32 defaultColor = new Color32(255, 255, 255, 255);

    private Color32[] gradesColors = new Color32[]{
        new Color32(166, 166, 166, 255),
        new Color32(151, 197, 58, 255),
        new Color32(58, 133, 197, 255),
        new Color32(166, 98, 185, 255)
        };

    private int[] limitRank = new int[4] { 1, 10, 20, 30 };

    void Awake() {
        gm = GameManager.Instance;
        sm = SoundManager.Instance;
        userStore = gm.userStore;
        bicycleItemStore = gm.bicycleInventStore;

        animator = GetComponent<Animator>();
    }

    void OnEnable() {
        Invoke("playSlideIn", 0.2f);
    }

    void OnDisable() {
        init();
    }

    void playSlideIn() {
        animator.Play("SlideIn");
    }

    public void slideFinished(AnimationEvent animationEvent) {
        int boolParm = animationEvent.intParameter;

        //slider in
        if (boolParm == 1) {
            var itemStatus = userStore.itemSpects;

            int new_str = itemStatus.Char_strength;
            int new_speed = itemStatus.Char_speed;
            int new_end = itemStatus.Char_endurance;
            int new_rec = itemStatus.Char_regeneration;

            //Debug.Log("캐릭터 스피드 : " + itemStatus.Char_speed);
            Info info = parent.selectedItem.GetComponent<Info>();

            if(info == null) { return; }

            id = info.id;
            int grade = info.grade;
            for(int i=0; i<grade; i++) {
                GameObject tier = Instantiate(tierImg);
                tier.transform.SetParent(tierGrid.transform, false);
                tier.GetComponent<Image>().color = gradesColors[grade - 1];
            }

            gameObject.transform.Find("Name").GetComponent<Text>().text = info.name;
            var type = info.parts;

            if (type == "FR") {
                Image img = gameObject.transform.Find("Image_FR").GetComponent<Image>();
                var tmp = spriteManager.stage_items[info.imageId - 1];
                if (tmp != null) {
                    img.enabled = true;
                    img.sprite = tmp;
                }
                else {
                    img.enabled = false;
                }
            }
            else if(type == "WH") {
                Image img = gameObject.transform.Find("Image_WH_mask/Image_WH").GetComponent<Image>();
                var tmp = spriteManager.stage_items[info.imageId - 1];
                if(tmp != null) {
                    img.enabled = true;
                    img.sprite = tmp;
                }
                else {
                    img.enabled = false;
                }
            }
            else if(type == "DS") {
                Image img = gameObject.transform.Find("Image_EG").GetComponent<Image>();
                var tmp = spriteManager.stage_items[info.imageId - 1];
                if (tmp != null) {
                    img.enabled = true;
                    img.sprite = tmp;
                }
                else {
                    img.enabled = false;
                }
            }
            int pre_str = itemStatus.Char_strength + itemStatus.Item_strength;
            int pre_speed = itemStatus.Char_speed + itemStatus.Item_speed;
            int pre_end = itemStatus.Char_endurance + itemStatus.Item_endurance;
            int pre_rec = itemStatus.Char_regeneration + itemStatus.Item_regeneration;
            
            for(int i=0; i<bicycleItemStore.equipedItemIndex.Length; i++) {
                if(bicycleItemStore.equipedItemIndex[i] == null || bicycleItemStore.equipedItemIndex[i].item.parts == type) {
                    continue;
                }
                new_str += bicycleItemStore.equipedItemIndex[i].item.strength;
                new_end += bicycleItemStore.equipedItemIndex[i].item.endurance;
                new_rec += bicycleItemStore.equipedItemIndex[i].item.regeneration;
                new_speed += bicycleItemStore.equipedItemIndex[i].item.speed;
            }

            new_str += info.strength;
            new_end += info.endurance;
            new_rec += info.recovery;
            new_speed += info.speed;

            int diff_str = new_str - pre_str;
            int diff_speed = new_speed - pre_speed;
            int diff_end = new_end - pre_end;
            int diff_rec = new_rec - pre_rec;

            Text pre_str_text = specs[0].transform.Find("PreVal").GetComponent<Text>();
            Text pre_end_text = specs[1].transform.Find("PreVal").GetComponent<Text>();
            Text pre_rec_text = specs[2].transform.Find("PreVal").GetComponent<Text>();
            Text pre_speed_text = specs[3].transform.Find("PreVal").GetComponent<Text>();

            pre_str_text.text = pre_str.ToString();
            pre_end_text.text = pre_end.ToString();
            pre_rec_text.text = pre_rec.ToString();
            pre_speed_text.text = pre_speed.ToString();

            Text new_str_text = specs[0].transform.Find("ChangeVal").GetComponent<Text>();
            Text new_end_text = specs[1].transform.Find("ChangeVal").GetComponent<Text>();
            Text new_rec_text = specs[2].transform.Find("ChangeVal").GetComponent<Text>();
            Text new_speed_text = specs[3].transform.Find("ChangeVal").GetComponent<Text>();

            new_str_text.text = new_str.ToString();
            new_end_text.text = new_end.ToString();
            new_rec_text.text = new_rec.ToString();
            new_speed_text.text = new_speed.ToString();

            //이전보다 근력 증가
            if (diff_str > 0) {
                new_str_text.color = increaseColor;
            }
            else if(diff_str == 0) {
                new_str_text.color = defaultColor;
            }
            else if(diff_str < 0) {
                new_str_text.color = decreaseColor;
            }

            if (diff_speed > 0) {
                new_speed_text.color = increaseColor;
            }
            else if(diff_speed == 0) {
                new_speed_text.color = defaultColor;
            }
            else if(diff_speed < 0) {
                new_speed_text.color = decreaseColor;
            }

            if (diff_end > 0) {
                new_end_text.color = increaseColor;
            }
            else if(diff_end == 0) {
                new_end_text.color = defaultColor;
            }
            else if(diff_end < 0) {
                new_end_text.color = decreaseColor;
            }

            if (diff_rec > 0) {
                new_rec_text.color = increaseColor;
            }
            else if(diff_rec == 0) {
                new_rec_text.color = defaultColor;
            }
            else if(diff_rec < 0) {
                new_rec_text.color = decreaseColor;
            }

            if (info.is_equiped) {
                unEquipButton.SetActive(true);
            }
            else {
                equipButton.SetActive(true);
            }
        }

        //slider out
        else if (boolParm == 0) {
            gameObject.SetActive(false);
        }
    }

    public void onBackButton() {
        animator.Play("SlideOut");
    }

    public void OnEquipButton() {
        Info info = parent.selectedItem.GetComponent<Info>();
        int itemGrade = info.grade;
        int myGrade = userStore.myData.status.rank;
        if(canEquip(itemGrade, myGrade)) {
            equip_act act = ActionCreator.createAction(ActionTypes.GARAGE_ITEM_EQUIP) as equip_act;
            act._type = equip_act.type.ITEM;
            act.id = info.id;
            gm.gameDispatcher.dispatch(act);

            close();
        }
        else {
            notifyModal.SetActive(true);
            notifyModal.transform.Find("InnerModal/Text").GetComponent<Text>().text = "등급이 낮아 장착할 수 없습니다.";
        }
    }

    private bool canEquip(int grade, int userRank) {
        bool result = false;
        if(limitRank[grade - 1] <= userRank) {
            result = true;
        }
        else {
            result = false;
        }
        return result;
    }

    public void OnSellButton() {
        Info info = parent.selectedItem.GetComponent<Info>();
        garage_sell_act act = ActionCreator.createAction(ActionTypes.GARAGE_SELL) as garage_sell_act;
        act.id = info.id;
        gm.gameDispatcher.dispatch(act);

        notifyModal.SetActive(true);
        notifyModal.transform.Find("InnerModal/Text").GetComponent<Text>().text = "총 " + info.gear  + " 기어를 획득하였습니다.";
    }

    public void OnUnequipButton() {
        unequip_act act = ActionCreator.createAction(ActionTypes.GARAGE_ITEM_UNEQUIP) as unequip_act;
        Info info = parent.selectedItem.GetComponent<Info>();
        int index = info.id;
        act.id = index;
        gm.gameDispatcher.dispatch(act);

        close();
    }

    public void close() {
        onBackButton();
        parent.onBackButton();
    }

    public void offNotifyModal() {
        notifyModal.SetActive(false);
        close();
    }

    private void init() {
        gameObject.transform.Find("Image_EG").GetComponent<Image>().enabled = false;
        gameObject.transform.Find("Image_FR").GetComponent<Image>().enabled = false;
        gameObject.transform.Find("Image_WH_mask/Image_WH").GetComponent<Image>().enabled = false;

        foreach(GameObject obj in specs) {
            Text text = obj.transform.Find("PreVal").GetComponent<Text>();
            text.text = "";
            text.color = defaultColor;

            text = obj.transform.Find("ChangeVal").GetComponent<Text>();
            text.text = "";
            text.color = defaultColor;
        }

        unEquipButton.SetActive(false);
        equipButton.SetActive(false);
        notifyModal.SetActive(false);

        foreach(Transform child in tierGrid.transform) {
            Destroy(child.gameObject);
        }
    }

    private class spec {
        public int diff_strength = 0;
        public int diff_speed = 0;
        public int diff_endurance = 0;
        public int diff_recovery = 0;

        public int pre_str = 0;
        public int pre_end = 0;
        public int pre_speed = 0;
        public int pre_rec = 0;
    }
}
