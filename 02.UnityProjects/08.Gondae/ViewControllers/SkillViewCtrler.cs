using UnityEngine;

public class SkillViewCtrler : MonoBehaviour {
    public GameObject
        passiveLayout,
        activeLayout;

    private UIManager uiManager;

    void Start() {
        uiManager = UIManager.Instance;
    }

    public void onPassiveLayout() {
        activeLayout.SetActive(false);
        passiveLayout.SetActive(true);
    }

    public void onActiveLayout() {
        passiveLayout.SetActive(false);
        activeLayout.SetActive(true);
    }

    public void onBackBtn() {

    }
}
