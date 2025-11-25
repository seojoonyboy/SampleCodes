using UnityEngine;
using System;
using System.IO;
using System.Collections;
using ScottGarland;
using UnityEngine.Advertisements;

public class GameManager : MonoBehaviour {
    private static GameManager instance = null;
	private static UIManager uIManager;
    private static EventManager eventManager;
    private SoundManager soundManager;

	private string saveDataPath;
    private float passiveTimer;

    private PassiveMeter[] passiveMeterList;

    public static GameManager Instance {
        get {
            return instance;
        }
    }

    private int _maxHealthPoint = 2000;
	public int maxHealthPoint{
        get{
            var inc = line.summary.healthIncrease;
            if(inc > 0){
                return _maxHealthPoint*(100+inc)/100;
            }
            return _maxHealthPoint;
        }
    }
	public int healthPoint;
	public float healthPointRecoverSec = 20f;
    public bool adIsReady = false;
    public BigInteger gondaeReuk;
    public DutySkills dutySkills;
    public Grade grade;
    public Line line;
    public BuffSkills buff;
    public Advents advents;
    public Story story;
    public Dialogues dialogues;
    public Config config;
    private long? pauseTimestamp = null;
    public BigInteger passiveGDR {
        get{
            decimal ret = 0m;
            for(var i=0; i<passiveMeterList.Length-1; i++){
                if(passiveMeterList[i].isActivate) {
                    ret+= decimal.Parse(dutySkills[i].touch.ToString())/(decimal)passiveMeterList[i].max;
                }
            }
            ret += advents.passiveSummary;
            return new BigInteger(ret) * (100 + line.summary.passiveGDR + buff.summary.passiveGDR) / 100;
        }
    }
    private BigInteger passiveGDRWithoutBuff{
        get{
            decimal ret = 0m;
            for(var i=0; i<grade.gradeList.Length; i++){
                var g = grade.gradeList[i];
                if(g.minionsLevel>0){
                    ret = decimal.Parse(dutySkills[i].touch.ToString())/(decimal)g.passiveRunTime;
                }
            }
            ret += advents.passiveSummary;
            return new BigInteger(ret);
        }
    }
    public BigInteger touchGDR {
        get{
            var onTouch = dutySkills.gdrSummary;
            // line & buff 보너스 추가
            var bonus = 100;
            bonus += line.summary.touchGDR + buff.summary.touchGDR;
            onTouch = onTouch * bonus;
            onTouch = onTouch / 100;
            return onTouch;
        }
    }
    public int blessOfGondae = 0;
    public decimal passiveSummary{
        get{
            decimal ret = 0m;
            for(var i=0; i<passiveMeterList.Length-1; i++){
                if(passiveMeterList[i].isActivate) {
                    ret+= decimal.Parse(dutySkills[i].touch.ToString())/(decimal)passiveMeterList[i].max;
                }
            }
            ret += advents.passiveSummary;
            return ret;
        }
    }

    void Start() {
        // Debug.Log("start");
        eventManager = EventManager.Instance;
        uIManager = UIManager.Instance;
        soundManager = SoundManager.Instance;

        saveDataPath = Application.persistentDataPath + "/savedata.json";
        eventManager.addListener(EventType.HEALTH_CHANGE, OnEvent);
        eventManager.addListener(EventType.RESOCIALIZE, OnEvent);
        eventManager.addListener(EventType.ADVERTISE_FINISHED, OnEvent);
        eventManager.addListener(EventType.GRADE_CHANGE, OnEvent);

        loadFromFile();

		InvokeRepeating("recoverHealth", 0, healthPointRecoverSec);
        configSleep(config.sleep);

        eventManager.postNotification(EventType.OPTION_CHANGE, this);
        InvokeRepeating("checkAdIsReady",5f, 60f);
    }
    void checkAdIsReady(){
        if(adIsReady == false){
            adIsReady = Advertisement.IsReady("rewardedVideo");
        }
    }
	void recoverHealth(){
        if (healthPoint < maxHealthPoint) {
            healthPoint++;
            eventManager.postNotification(EventType.HEALTH_CHANGE, this);
        }
	}

    void Update() {
        #if UNITY_ANDROID
        if (Input.GetKeyDown(KeyCode.Escape)) {
            Application.Quit();
        }
        #endif
        operatePassiveMeter();
        runStoryChapterByType("gdr");
    }

    void runStoryChapterByType(string type){
        Chapter chapter;
        if(type == "socLv"){
            chapter = story.getChepterBySocLv();
        } else if(type == "grade") {
            var chapters = story.getChaptersByGrade();
            chapter = Array.Find(chapters, c=>checkStoryCondition(c));
        } else {
            chapter = story.getChapterByTypeMinValue(type);        
        }
        if(chapter != null && !chapter.isPlayed && checkStoryCondition(chapter)){
            chapter.isPlayed = true;
            ModalQueueMessage message = new ModalQueueMessage(ModalEventType.STORY, chapter);
            eventManager.receiveModalEvent(message);
        }
    }

    public void configSleep(bool conf){
        if(conf){
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        } else {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
        config.sleep = conf;
    }

    void operatePassiveMeter(){
        PassiveMeter pm;
        float timeDelta = Time.time - passiveTimer;
        passiveTimer = Time.time;
        for(int i=0; i<passiveMeterList.Length; i++){
            pm = passiveMeterList[i];
            if(pm.isActivate){
                pm.current += timeDelta;
                if(pm.current > pm.max){
                    int m = (int)(pm.current/pm.max);
                    pm.current %= pm.max;
                    runPassive(i, m);
                }
            }
        }
    }

    void runPassive(int passiveIndex, int multiple){
        BigInteger passiveAdd = null;
        if(passiveIndex < passiveMeterList.Length-1){
            passiveAdd = dutySkills[passiveIndex].touch;
        } else {
            passiveAdd = new BigInteger(advents.passiveSummary);
        }
        passiveAdd = passiveAdd * (100 + line.summary.passiveGDR + buff.summary.passiveGDR) / 100;
        gondaeReuk += passiveAdd * multiple;
    }

    void Awake() {
        if(instance != null && instance != this) {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void OnEvent(EventType type, Component sender, object param = null) {
        switch (type) {
            case EventType.HEALTH_CHANGE:
                break;

            case EventType.RESOCIALIZE:
                break;
        }
    }

    public void onMainTouchEvent() { // 메인화면 터치 이벤트시 실행되는 함수
		if(healthPoint == 0 && buff.summary.healthful != 1){
            ModalQueueMessage message = new ModalQueueMessage(ModalEventType.HPZERO);
            eventManager.receiveModalEvent(message);
            return;
        }
        if(buff.summary.healthful != 1){
            healthPoint--;
        }
		
        eventManager.postNotification(EventType.HEALTH_CHANGE, this);

        var onTouch = touchGDR;

		// Critical chance
		int chance = 1 + line.summary.criticalChance + buff.summary.criticalChance;
        uIManager.onMainTouch();
        if (UnityEngine.Random.value*100f < chance) {	// critical hit!!
			onTouch = onTouch * 120 / 100;
			uIManager.onCritical();
            soundManager.SE_TOUCH_CRITICAL.Play();
        }
		gondaeReuk += onTouch;
    }

    void saveToFile() { // 게임의 현재 데이터를 파일로 보존
        SaveData saveData = new SaveData(this);
        string json = saveData.toJson();
        #if UNITY_EDITOR
            File.WriteAllText(saveDataPath, json);
        #else
            DESCrypto dc = new DESCrypto();
            byte[] writeData = dc.Encrypt(json);
            File.WriteAllBytes(saveDataPath, writeData);
        #endif
    }

    void loadFromFile() { // 파일로부터 게임 데이터를 읽어옴
        SaveData saveData = null;
        if (File.Exists(saveDataPath)) {
            #if UNITY_EDITOR
                string json = File.ReadAllText(saveDataPath);
            #else
                DESCrypto dc = new DESCrypto();
                byte[] readData = File.ReadAllBytes(saveDataPath);
                string json = dc.Decrypt(readData);
            #endif
            saveData = new SaveData(json);
        }
        float timeStampDiff = 0f;
        if(saveData !=null){
            gondaeReuk = new BigInteger(saveData.gdr);
            grade = new Grade(saveData.minionsNumList);
            grade.current = saveData.grade;
            grade.socializationLevel = saveData.socializationLevel;
            line.loadLine(saveData.lineSelected, saveData.linePoint);
            buff.loadBuffSkills(saveData.buffUnlocked);
            dutySkills = new DutySkills(saveData.passiveLevels);
            advents = new Advents(saveData.adventLevels);
            story.chapterIsPlayedList = saveData.storyPlayed;
            healthPoint = saveData.healthPoint;
            config = saveData.config;
            timeStampDiff = (float)(DateTime.Now.Ticks - saveData.saveTimestamp) / 10000000f;
            healthPoint += (int)(timeStampDiff / healthPointRecoverSec);
            healthPoint = healthPoint > maxHealthPoint ? maxHealthPoint : healthPoint;
            blessOfGondae = saveData.blessOfGondae;
        } else {
            gondaeReuk = new BigInteger(0);
            grade = new Grade();
            line.loadLine(null,1);
            dutySkills = new DutySkills();
            dutySkills[0].level = 1;
            advents = new Advents();
            healthPoint = maxHealthPoint;
            grade.socializationLevel = 0;
            config = new Config();
            runStoryChapterByType("start");
            buff.loadBuffSkills(null);
        }

        int len = grade.gradeList.Length;
        passiveMeterList = new PassiveMeter[len];
        var madd = line.summary.minionAdd + buff.summary.minionAdd;
        var maddr = line.summary.minionAddRate + buff.summary.minionAddRate;
        var pspeed = line.summary.passiveSpeed + buff.summary.passiveSpeed;
        for(int i = 0; i<passiveMeterList.Length-1; i++){
            passiveMeterList[i] = new PassiveMeter();
            passiveMeterList[i].isActivate = (grade[i].minionsLevel > 0);
            passiveMeterList[i].max = grade[i].passiveRunTimeAdd(madd, maddr, pspeed);
        }
        passiveMeterList[len-1] = new PassiveMeter();
        passiveMeterList[len-1].isActivate = true;
        passiveMeterList[len-1].max = 1f*(100-pspeed)/100;
        passiveTimer = Time.time;
        
        if(saveData != null){
            len = saveData.buffRemainTime.Length;
            for(var i=0; i<len; i++){
                if(saveData.buffRemainTime[i] != 0){
                    var bf = buff[i];
                    runBuff(bf,saveData.buffRemainTime[i]);
                }
            }
        }
        gondaeReuk += passiveGDRWithoutBuff * new BigInteger(timeStampDiff);
    }

    void OnApplicationQuit(){
        saveToFile();
    }

    void OnApplicationPause(bool pauseStatus) {
        // Debug.Log("is Paused? " + pauseStatus);
        if (pauseStatus){
            saveToFile();
            pauseTimestamp = DateTime.Now.Ticks;
        } else {
            if(pauseTimestamp == null) return;
            var timeDelta = (float)(DateTime.Now.Ticks - pauseTimestamp) / 10000000f;
            healthPoint += (int)(timeDelta / healthPointRecoverSec);
            healthPoint = healthPoint > maxHealthPoint ? maxHealthPoint : healthPoint;
            gondaeReuk += passiveGDRWithoutBuff * new BigInteger(timeDelta);
        }
    }
    public void lineUpgrade(int listIndex, int itemIndex){
        line.linePoint--;
        LineItem l = line.lineList[listIndex][itemIndex];
        l.isUpgraded = true;
        if(l.effectType == EffectType.ACTIVE){
            buff[l.activeSkillID].isUnlocked = true;
        }
        setPassiveTimer();
    }

    void setPassiveTimer(){
        var madd = line.summary.minionAdd + buff.summary.minionAdd;
        var maddr = line.summary.minionAddRate + buff.summary.minionAddRate;
        var pspeed = line.summary.passiveSpeed + buff.summary.passiveSpeed;
        var len = passiveMeterList.Length;
        for(var i=0; i<len; i++){
            var pm = passiveMeterList[i];
            if(i != len-1){
                pm.max = grade[i].passiveRunTimeAdd(madd, maddr, pspeed);
            } else {
                pm.max = 1.0f*(100-pspeed)/100;
            }
        }
    }

    public void runBuff(BuffSkill b, int? runTime=null){
        if(b.isUnlocked == false){
            return;
        }
        b.isActivated = true;
        if(runTime == null ){
            b.activateTime = DateTime.Now;
        } else {
            var beforeTime = TimeSpan.FromSeconds((double)(b.duration - runTime));
            b.activateTime = DateTime.Now - beforeTime;
        }
        setPassiveTimer();
        StartCoroutine(deactiveBuff(b, runTime));
    }

    private IEnumerator deactiveBuff(BuffSkill b, int? runTime){
        if(runTime == null){
            yield return new WaitForSeconds((float)b.duration);
        } else {
            yield return new WaitForSeconds((float)runTime.Value);
        }
        b.isActivated = false;
        setPassiveTimer();
    }

    public bool dutySkillLevelUp(int skillIndex){
        DutySkill skill = dutySkills[skillIndex];
        if(skill.upgradeCost <= gondaeReuk ){
            gondaeReuk = gondaeReuk - skill.upgradeCost;
            skill.level++;
            return true;
        } else {
            return false;
        }
    }

    public bool gradeUp(){
        if( grade.upgradeCost <= gondaeReuk && grade.current+1 <= grade.maxGrade ){
            gondaeReuk = gondaeReuk - grade.upgradeCost;
            grade.current++;
            int[] unlock = grade.currentGrade.unlockSkills;
            for( int i=0; i<unlock.Length; i++){
                var sk = dutySkills[unlock[i]];
                if(sk.level == 0){
                    sk.level++;
                }
            }
            if(grade.prevSocialization == null || grade.prevSocialization.maxGrade < grade.current) {
                line.linePoint++;
            }
            runStoryChapterByType("grade");
            eventManager.postNotification(EventType.GRADE_CHANGE, this);

            if(grade.socializationLevel == 0 && grade.current == grade.maxGrade) {
                string str = "FirstMaxGrade";
                ModalQueueMessage message = new ModalQueueMessage(ModalEventType.RESOCIAL_RESULT_DIALOGUE, str);
                eventManager.receiveModalEvent(message);
            }

            return true;
        } else {
            return false;
        }
    }

    public bool minionLvUp(int minionIndex){
        BigInteger hireCost = new BigInteger(grade[minionIndex].minionLvupCost);
        if (gondaeReuk >= hireCost && grade.minionsLvMax > grade[minionIndex].minionsLevel) {
            gondaeReuk -= hireCost;
            grade[minionIndex].minionsLevel += 1;
            passiveMeterList[minionIndex].isActivate = true;
            var madd = line.summary.minionAdd + buff.summary.minionAdd;
            var maddr = line.summary.minionAddRate + buff.summary.minionAddRate;
            var pspeed = line.summary.passiveSpeed + buff.summary.passiveSpeed;
            passiveMeterList[minionIndex].max = grade[minionIndex].passiveRunTimeAdd(madd, maddr, pspeed);
            return true;
        } else {
            return false;
        }
    }

    public void resocializing() {
        if(grade.nextSocialization != null) {
            ModalQueueMessage message = new ModalQueueMessage(ModalEventType.RESOCIAL_RESULT_DIALOGUE);
            eventManager.receiveModalEvent(message);

            gondaeReuk -= grade.nextSocialization.cost;
            grade.socializationLevel++;
            grade.current = 0;

            runStoryChapterByType("socLv");
            healthPoint = maxHealthPoint;
        }        
    }

    public bool adventLvUp(int adventIndex){
        Advent upAdvent = advents.list[adventIndex];
        BigInteger lvUpCost = new BigInteger(upAdvent.cost);
        if (upAdvent.level < Advents.lvLimit && gondaeReuk >= lvUpCost){
            gondaeReuk -= lvUpCost;
            upAdvent.level ++;
            if(Array.TrueForAll(advents.levels,lev=>lev==100)){
                Debug.Log("advent 100");
                runStoryChapterByType("clear");
            }
            return true;
        } else {
            return false;
        }
    }
    public bool checkStoryCondition(Chapter chapter){
        var check = true;
        foreach(StoryCondition con in chapter.conditions){
            switch(con.type){
                case "socLv":
                    check = check && checkSocLv(con.val);
                    break;
                case "gdr":
                    check = check && checkGdr(con.val); 
                    break;
                case "grade":
                    check = check && checkGrade(con.val);
                    break;
            }
            if(!check) break;
        }

        return check;
    }

    public bool checkSocLv(ulong socLv){
        return grade.socializationLevel >= (int)socLv;
    }

    public bool checkGdr(ulong gdr){
        var comp = new BigInteger(gdr);
        return gondaeReuk >= comp;
    }
    public bool checkGrade(ulong g){
        return grade.current >= (int)g;
    }

    public void getBlessOfGondae(){
        if (Advertisement.IsReady("rewardedVideo"))
        {
            var options = new ShowOptions { resultCallback = HandleShowResult };
            Advertisement.Show("rewardedVideo", options);
        }else{
            adIsReady = false;
        }
    }
    private void HandleShowResult(ShowResult result)
    {
        switch (result)
        {
        case ShowResult.Finished:
            Debug.Log("The ad was successfully shown.");
            blessOfGondae++;
            eventManager.postNotification(EventType.ADVERTISE_FINISHED, this);
            break;
        case ShowResult.Skipped:
            Debug.Log("The ad was skipped before reaching the end.");
            break;
        case ShowResult.Failed:
            Debug.LogError("The ad failed to be shown.");
            break;
        }
    }
}

class SaveData {
    public string gdr;
    public int grade;
    public string[] minionsNumList;
    public int[] passiveLevels;
    public long saveTimestamp;
    public int[] lineSelected;
    public bool[] buffUnlocked;
    public int[] adventLevels;
    public int linePoint;
	public int healthPoint;
	public int socializationLevel;
    public bool[] storyPlayed;
    public Config config;
    public int[] buffRemainTime;
    public int blessOfGondae;
    public SaveData(GameManager manager) {
        DutySkills p = manager.dutySkills;
        gdr = manager.gondaeReuk.ToString();
        grade = manager.grade.current;
        minionsNumList = Array.ConvertAll<int, string>(manager.grade.minionsLvList,b=>b.ToString());
        passiveLevels = p.skillsLevelList;
        saveTimestamp = DateTime.Now.Ticks;
        lineSelected = manager.line.upgradedInfo;
        buffUnlocked = manager.buff.unlockedMap;
        linePoint = manager.line.linePoint;
		healthPoint = manager.healthPoint;
		socializationLevel = manager.grade.socializationLevel;
        adventLevels = manager.advents.levels;
        storyPlayed = manager.story.chapterIsPlayedList;
        config = manager.config;
        buffRemainTime = manager.buff.remainTime;
        blessOfGondae = manager.blessOfGondae;
    }
    public SaveData(string json) {
        JsonUtility.FromJsonOverwrite(json, this);
    }
    public string toJson(){
        string json = JsonUtility.ToJson(this);
        return json;
    }
}

class PassiveMeter {
    public bool isActivate = false;
    public float max = 0;
    public float current = 0;
}

[Serializable]
public class Config{
    public bool bgm = true;
    public bool sfx = true;
    public bool sleep = false;
}
