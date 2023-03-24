using UnityEngine;
using UnityEngine.UI;

public class OptionViewCtrler : MonoBehaviour {
    private GameManager gameManager;
    private EventManager eventManager;

    public GameObject modal;
    public Toggle 
        bgmToggle,
        emToggle,
        sleepToggle;
    void Start() {
        initialize();
        bgmToggle.onValueChanged.AddListener(delegate { onBgmBtn(); });
        emToggle.onValueChanged.AddListener(delegate { onSeBtn(); });
        sleepToggle.onValueChanged.AddListener(delegate { onSleepBtn(); });
    }

    void initialize() {
        gameManager = GameManager.Instance;
        eventManager = EventManager.Instance;

        eventManager.addListener(EventType.OPTION_CHANGE, OnEvent);

        setBtnState();
    }

    public void onModal() {
        modal.SetActive(true);
    }

    public void offModal() {
        modal.SetActive(false);
    }

    public void onBgmBtn() {
        gameManager.config.bgm = bgmToggle.isOn;
        eventManager.postNotification(EventType.OPTION_CHANGE, this);
    }

    public void onSeBtn() {
        gameManager.config.sfx = emToggle.isOn;
        eventManager.postNotification(EventType.OPTION_CHANGE, this);
    }

    public void onSleepBtn() {
        gameManager.configSleep(sleepToggle.isOn);
    }

    public void OnEvent(EventType type, Component sender, object param = null) {

    }

    private void setBtnState() {
        if (gameManager.config.bgm) {
            modal.transform.Find("ModalPanel/BGM_Toggle_Group/OnToggle").GetComponent<Toggle>().isOn = true;
            modal.transform.Find("ModalPanel/BGM_Toggle_Group/OffToggle").GetComponent<Toggle>().isOn = false;
        }
        else {
            modal.transform.Find("ModalPanel/BGM_Toggle_Group/OnToggle").GetComponent<Toggle>().isOn = false;
            modal.transform.Find("ModalPanel/BGM_Toggle_Group/OffToggle").GetComponent<Toggle>().isOn = true;
        }

        if (gameManager.config.sfx) {
            modal.transform.Find("ModalPanel/EM_Toggle_Group/OnToggle").GetComponent<Toggle>().isOn = true;
            modal.transform.Find("ModalPanel/EM_Toggle_Group/OffToggle").GetComponent<Toggle>().isOn = false;
        }
        else {
            modal.transform.Find("ModalPanel/EM_Toggle_Group/OnToggle").GetComponent<Toggle>().isOn = false;
            modal.transform.Find("ModalPanel/EM_Toggle_Group/OffToggle").GetComponent<Toggle>().isOn = true;
        }

        if (gameManager.config.sleep) {
            modal.transform.Find("ModalPanel/Sleep_Toggle_Group/OnToggle").GetComponent<Toggle>().isOn = true;
            modal.transform.Find("ModalPanel/Sleep_Toggle_Group/OffToggle").GetComponent<Toggle>().isOn = false;
        }
        else {
            modal.transform.Find("ModalPanel/Sleep_Toggle_Group/OnToggle").GetComponent<Toggle>().isOn = false;
            modal.transform.Find("ModalPanel/Sleep_Toggle_Group/OffToggle").GetComponent<Toggle>().isOn = true;
        }
    }
}
