using UnityEngine;

public class ManagersLoader : MonoBehaviour {
    public GameObject 
        gameManager,
        uiManager,
        eventManager,
        soundManager;
    void Awake() {
        if (EventManager.Instance == null) {
            Instantiate(eventManager);
        }
        if (GameManager.Instance == null){
            Instantiate(gameManager);
        }
        if(UIManager.Instance == null) {
            Instantiate(uiManager);
        }        
        if(SoundManager.Instance == null) {
            Instantiate(soundManager);
        }
    }
}
