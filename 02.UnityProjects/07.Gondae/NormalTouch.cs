using UnityEngine;

public class NormalTouch : MonoBehaviour {
    public void remove() {
        Destroy(gameObject.transform.parent.gameObject);
    }
}
