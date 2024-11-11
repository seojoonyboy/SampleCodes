using Flux;
using System.Text;
using UnityEngine;

public class GameManager : Singleton<GameManager> {
    protected GameManager(){}
    public QueueDispatcher<Actions> gameDispatcher;
    public StringBuilder sb;
    [System.NonSerialized]
    public string deviceId;
    public User userStore;
    public Riding ridingStore;
    public Friends friendsStore;
    public Groups groupStore;
    public Locations locationStore;
    public Char_Inventory charInvenStore;
    public BicycleItem_Inventory bicycleInventStore;
    public Box_Inventory boxInvenStore;

    void Awake() {
        gameDispatcher = new QueueDispatcher<Actions>();
        sb = new StringBuilder();
        userStore = new User(gameDispatcher);
        ridingStore = new Riding(gameDispatcher);
        friendsStore = new Friends(gameDispatcher);
        groupStore = new Groups(gameDispatcher);
        locationStore = new Locations(gameDispatcher);
        charInvenStore = new Char_Inventory(gameDispatcher);
        bicycleInventStore = new BicycleItem_Inventory(gameDispatcher);
        boxInvenStore = new Box_Inventory(gameDispatcher);

        deviceId = SystemInfo.deviceUniqueIdentifier;
        Debug.Log("deviceId : " + deviceId);
        //Debug.Log("GameManager Awake");
    }
}
