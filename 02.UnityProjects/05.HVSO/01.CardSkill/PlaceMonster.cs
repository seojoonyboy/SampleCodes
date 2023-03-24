using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Events;
using System.Linq;
using TMPro;
using UnityEngine.UI;

public struct Granted {
    public string name;
    public int hp;
    public int attack;
    public int attackCount;
}

public class PlaceMonster : MonoBehaviour {
    public dataModules.Unit unit;
    public bool isPlayer;

    public int x { get; private set; }
    public int y { get; private set; }
    //public List<GameObject> myTarget;

    public Vector3 unitLocation;
    public int atkCount = 0;
    public int myUnitNum = 0;
    public string itemId = null;

    public bool buffEffect = false;
    
    public UnitSpine unitSpine;
    public HideUnit hideSpine;

    List<string> unitAttribute;
    List<string> unitAttackType;

    private Granted[] _granted;

    DequeueCallback afterAttackActionCall;
    GameObject arrow;

    public Granted[] granted {
        get { return _granted; }
        set { _granted = value; ChangeIcon(); }
    }
    
    public float atkTime {
        get { return unitSpine.atkDuration; }
    }

    private float appearTime {
        get { float time = 0; return time = (unitSpine != null) ? unitSpine.appearDuration : 1f;}
    }
    


    public int unitSoringOrder {
        set { unitSpine.transform.GetComponent<MeshRenderer>().sortingOrder = value; }
        get { return unitSpine.transform.GetComponent<MeshRenderer>().sortingOrder; }
    }


    struct buffStat {
        public bool running;
        public int atk;
        public int hp;
        public void init() {
            running = false;
            atk = 0;
            hp = 0;
        }
    }
    private buffStat buff = new buffStat();


    public enum UnitState {
        APPEAR,
        IDLE,
        ATTACK,
        HIT,
        MAGICHIT,
        DETECT,
        ANGRY,
        REPLACE,
        DEAD
    };

    void OnDestroy() {
        tintOnOff = false;
        if(PlayMangement.instance == null) return;
        if (isPlayer) {
            PlayMangement.instance.UnitsObserver
                .RefreshFields(
                    CardDropManager.Instance.unitLine, 
                    PlayMangement.instance.player.isHuman
                );
        }
        else {
            PlayMangement.instance.UnitsObserver
                .RefreshFields(
                    CardDropManager.Instance.enemyUnitLine, 
                    !PlayMangement.instance.player.isHuman
                );
        }
    }

    public void Init(dataModules.CollectionCard data) {
        x = transform.parent.GetSiblingIndex();
        y = transform.parent.parent.GetSiblingIndex();

        unitLocation = gameObject.transform.position;

        unitSpine = transform.Find("skeleton").GetComponent<UnitSpine>();
        //unitSpine.attackCallback += AttackTime;
        //unitSpine.takeMagicCallback += CheckHP;
        unitSpine.rarelity = unit.rarelity;

        InitAttribute();
        InitAttackProperty();


        if (CheckAttribute("ambush"))
            gameObject.AddComponent<ambush>();


        if (unit.attackRange == "distance" || unit.attackRange == "immediate") {
            GameObject arrow = Instantiate(unitSpine.arrow, transform);
            arrow.transform.position = gameObject.transform.position;
            

            if (isPlayer == false) {
                if (arrow.gameObject.name.Contains("Dog") == true)
                    arrow.transform.localScale = new Vector3(1, 1, 1);
                else
                    arrow.transform.localScale = new Vector3(1, -1, 1);
            }
            arrow.name = "arrow";
            arrow.SetActive(false);


            this.arrow = arrow;
            if (unit.attackRange == "immediate") unitSpine.SetImmediateObject();
        }

        if (isPlayer == true) 
            unit.ishuman = (PlayMangement.instance.player.isHuman == true) ? true : false;        
        else
            unit.ishuman = (PlayMangement.instance.enemyPlayer.isHuman == true) ? true : false;

        myUnitNum = PlayMangement.instance.unitNum++;
        StartCoroutine(SetupClickableUI());
        UpdateStat();        
    }

    public void UpdateGranted() {
        SocketFormat.Unit socketUnit = PlayMangement.instance.socketHandler.gameState.map.allMonster.Find(x => x.itemId == itemId);
        if(socketUnit == null) { Debug.LogError("problem about granted");  return; }
        this.granted = socketUnit.granted;
        ChangeIcon();
        unit.currentHp = socketUnit.currentHp;
        InstatiateBuff(socketUnit);
        ContinueBuff(socketUnit);
        unit.maxHp = socketUnit.maxHp;
        unit.attack = socketUnit.attack;
        UpdateStat();
    }

    private void InstatiateBuff(SocketFormat.Unit socketUnit) {

        if(unit.maxHp < socketUnit.maxHp || unit.attack < socketUnit.attack) {
            EffectSystem.Instance.ShowEffect(EffectSystem.EffectType.BUFF, transform.position);
        } 

        if(unit.maxHp > socketUnit.maxHp || unit.attack > socketUnit.attack) {
            EffectSystem.Instance.ShowEffect(EffectSystem.EffectType.DEBUFF, transform.position);
        }
    }

    private void ContinueBuff(SocketFormat.Unit socketUnit) {
        if(socketUnit.origin.hp < socketUnit.maxHp || socketUnit.origin.attack < socketUnit.attack) {
            EffectSystem.Instance.ContinueEffect(EffectSystem.EffectType.CONTINUE_BUFF, transform);
        } 
        if(socketUnit.origin.hp > socketUnit.maxHp || socketUnit.origin.attack > socketUnit.attack) {
            EffectSystem.Instance.ContinueEffect(EffectSystem.EffectType.CONTINUE_DEBUFF, transform);
        }
        if(socketUnit.origin.hp == socketUnit.maxHp && socketUnit.origin.attack == socketUnit.attack) {
            EffectSystem.Instance.DisableEffect(EffectSystem.EffectType.CONTINUE_BUFF, transform);
            EffectSystem.Instance.DisableEffect(EffectSystem.EffectType.CONTINUE_DEBUFF, transform);
        }
        if (Array.Exists(socketUnit.granted, x => x.name == "poisoned"))
            EffectSystem.Instance.ContinueEffect(EffectSystem.EffectType.POISON_GET, transform, unitSpine.headbone);

        //if (Array.Exists(socketUnit.granted, x => x.name == "stun"))
        //    EffectSystem.Instance.ContinueEffect(EffectSystem.EffectType.STUN, gameObject.GetComponent<PlaceMonster>().unitSpine.headbone);
        //else
        //    EffectSystem.Instance.DisableEffect(EffectSystem.EffectType.STUN, gameObject.GetComponent<PlaceMonster>().unitSpine.headbone);

    }

    private IEnumerator SetupClickableUI() {
        yield return new WaitUntil(() => unitSpine.currentAnimationName == unitSpine.idleAnimationName);
        transform.Find("ClickableUI").position = unitSpine.bodybone.position;
        transform.Find("FightSpine").position = unitSpine.bodybone.position;
    }


    public void SetHiding() {
        if (CheckAttribute("ambush") == false) return;
        unitSpine.hidingObject = IngameResourceLibrary.gameResource.hideObject;
        GameObject hide = Instantiate(IngameResourceLibrary.gameResource.hideObject, transform);
        hide.transform.position = gameObject.transform.position;
        hideSpine = hide.GetComponent<HideUnit>();
        hideSpine.unitSpine = unitSpine;
        hideSpine.Init();
    }

    public void HideUnit() {
        transform.Find("Numbers").gameObject.SetActive(false);
        transform.Find("UnitAttackProperty").gameObject.SetActive(false);
        unitSpine.gameObject.SetActive(false);
        hideSpine.gameObject.SetActive(true);
        hideSpine.Appear();
    }

    public void DetectUnit() {
        transform.Find("Numbers").gameObject.SetActive(true);
        transform.Find("UnitAttackProperty").gameObject.SetActive(true);
        Destroy(gameObject.GetComponent<ambush>());
        //RemoveAttribute("ambush");
        SetState(UnitState.DETECT);
    }

    public void OverMask() {
        unitSoringOrder = 55;
    }
    
    public void ResetSorting() {
        unitSoringOrder = 50;
    }

    private void ChangeIcon() {
        if(_granted == null || _granted.Length == 0 || GetComponent<ambush>() != null) { transform.Find("UnitAttackProperty").gameObject.SetActive(false); return;}
        ResourceManager skills = AccountManager.Instance.resource;

        Granted[] check = Array.FindAll<Granted>(_granted, x=> boolGrantedSkill(x.name, skills));
        Debug.Log(check.Length);
        if(check.Length == 0) { transform.Find("UnitAttackProperty").gameObject.SetActive(false); return;}
        Array.ForEach(check, x=> Debug.Log(x.name));
        transform.Find("UnitAttackProperty").gameObject.SetActive(true);
        SpriteRenderer iconImage = transform.Find("UnitAttackProperty/StatIcon").GetComponent<SpriteRenderer>();
        if (check.Length > 1) iconImage.sprite = AccountManager.Instance.resource.GetSkillIcons("complex");
        else if (check.Length == 1 && check[0].name != "") iconImage.sprite = AccountManager.Instance.resource.GetSkillIcons(check[0].name);
        else iconImage.sprite = null;
    }

    private bool boolGrantedSkill(string name, ResourceManager skills) {
        return !string.IsNullOrEmpty(name) && name.CompareTo("ambush") != 0  && skills.FindSkillNames(name);
    }

    private void ChangeAttackIcon() {
        if (unitAttackType.Count <= 0) {
            transform.Find("UnitAttackProperty").gameObject.SetActive(false);
            return;
        }
        SpriteRenderer iconImage = transform.Find("UnitAttackProperty/StatIcon").GetComponent<SpriteRenderer>();
        if (iconImage != null)
            iconImage.sprite = (unitAttackType.Count > 1)
                ? AccountManager.Instance.resource.GetSkillIcons("complex")
                : AccountManager.Instance.resource.GetSkillIcons(unitAttackType[0]);
    }

    private void InitAttackProperty() {
        if (unit.attackTypes.Length <= 0) {
            transform.Find("UnitAttackProperty").gameObject.SetActive(false);
            return;
        }
        if (unitAttackType == null) unitAttackType = new List<string>();
        unitAttackType.AddRange(unit.attackTypes.ToList());
        ChangeAttackIcon();
    }

    public void AddAttackProperty(string status) {
        if (unitAttackType == null) unitAttackType = new List<string>();
        unitAttackType.Add(status);
        unit.attackTypes = unitAttackType.ToArray();
        ChangeAttackIcon();
    }

    public void RemoveAttackProperty(string status) {
        if (CheckAttackProperty(status) == false) return;
        unitAttackType.Remove(status);
        ChangeAttackIcon();
    }


    public bool CheckAttackProperty(string atkProperty) {
        if (unitAttackType == null || unitAttackType.Count == 0) return false;
        if (unitAttackType.Exists(x => x == atkProperty)) return true;
        else return false;
    }


    private void InitAttribute() {
        if (unit.attributes.Length == 0) return;
        if (unitAttribute == null) unitAttribute = new List<string>();
        List<Granted> grantList = new List<Granted>();
        foreach (dataModules.Attr attr in unit.attributes) {
            unitAttribute.Add(attr.name);
            grantList.Add(new Granted { name = attr.name });
        }
        if(_granted == null)
            granted = grantList.ToArray();
    }

    public void AddAttribute(string newAttrName) {
        if (unitAttribute == null) unitAttribute = new List<string>();
        unitAttribute.Add(newAttrName);
        //unit.attributes = unitAttribute.ToArray();
    }

    public void RemoveAttribute(string attrName) {
        if (CheckAttribute(attrName)) {
            unitAttribute.Remove(attrName);
            //unit.attributes = unitAttribute.ToArray();
        }
    }

    public bool CheckAttribute(string attrName) {
        if (_granted == null || _granted.Length == 0) return false;
        if ( System.Array.Exists<Granted>(_granted, x => x.name == attrName)) return true;
        else return false;
    }



    public void SpawnUnit() {
        SetState(UnitState.APPEAR);
    }


    public void GetTarget(List<GameObject> targetList, DequeueCallback actionOver) {
        //stun이 있으면 공격을 못함
        //if (GetComponent<SkillModules.stun>() != null) {
        //    Destroy(GetComponent<SkillModules.stun>());
        //    actionOver.Invoke();
        //    return;
        //}
        if (unit.attack <= 0) {
            actionOver.Invoke();
            return;
        }

        //afterAttackActionCall = actionOver;

        List<GameObject> myTargetList;
        myTargetList = targetList;

        //if () {
        //    unitSpine.attackAction = delegate () {
        //        GameObject target = myTargetList.Find(x => x.GetComponent<PlayerController>() != null);
        //        target = (target == null) ? myTargetList[myTargetList.Count - 1] : target;
        //        Vector3 pos = (target.GetComponent<PlayerController>() != null) ? target.GetComponent<PlayerController>().wallPosition : target.transform.position;
        //        Hashtable hashset = iTween.Hash("x", pos.x, "y", pos.y, "z", pos.z, "time", 0.2f, "easetype", iTween.EaseType.easeOutExpo, "oncomplete", "PiercingAttack", "oncompleteparams", myTargetList);
        //        iTween.MoveTo(arrow, hashset);
        //    };
        //    UnitTryAttack();
        //}
        //else
        StartCoroutine(ExecuteAttack(myTargetList, actionOver));
    }

    protected IEnumerator PenetrateCharge(List<GameObject> myTargetList) {
        if (myTargetList.Count > 0) yield break;
        int takingToDamage = 0;
        List<GameObject> attackList = new List<GameObject>();

        while (takingToDamage < unit.attack) {
            PlaceMonster targetUnit = myTargetList[0].GetComponent<PlaceMonster>();
            if (targetUnit == null) {
                //SocketFormat.Players players = PlayMangement.instance.socketHandler.gameState.players;
                //SocketFormat.Player targetPlayer = (targetUnit.GetComponent<PlayerController>().isHuman) ? players.human : players.orc;
                //targetPlayer
                int amount = unit.attack.Value;
                takingToDamage += amount;
                attackList.Add(myTargetList[0]);
                myTargetList.RemoveAt(0);
            }
            else {
                SocketFormat.Unit socketUnit = PlayMangement.instance.socketHandler.gameState.map.allMonster.Find(x => x.itemId == targetUnit.itemId);
                int amount = (targetUnit.unit.currentHp > unit.attack.Value) ? unit.attack.Value : targetUnit.unit.currentHp;
                takingToDamage += amount;
                attackList.Add(myTargetList[0]);
                myTargetList.RemoveAt(0);
            }
        }

        unitSpine.attackAction = delegate () { PenetrateAttack(attackList); };
        UnitTryAttack();
        yield return new WaitForSeconds(atkTime + 0.5f);
        yield return PenetrateCharge(myTargetList);
    }

    protected IEnumerator DistanceAttack(List<GameObject> targetList, DequeueCallback actionOver = null) {
        if (targetList == null || targetList.Count == 0) yield break;
        yield return DistanceAttack(targetList, null);
        actionOver?.Invoke();
    }

    protected IEnumerator DistanceAttack(List<GameObject> targetList, bool[] targetDead, List<GameObject> attackList, int attackerAtk, DequeueCallback actionOver = null) {
        if (targetList == null) yield break;

        bool charge = Array.Exists(granted, x => x.name == "charge");
        bool penetrate = Array.Exists(granted, x => x.name == "penetrate");


        for (int i = 0; i < targetList.Count; i++) {
            if (targetDead[i] == true) continue;
            int targetHP = targetList[i].GetComponent<PlaceMonster>() ? targetList[i].GetComponent<PlaceMonster>().unit.currentHp : targetList[i].GetComponent<PlayerController>().HP.Value;
            targetDead[i] = targetHP - attackerAtk > 0 ? false : true;
            attackerAtk -= targetHP;
            attackList.Add(targetList[i]);

            int from = -1;
            int to = -1;

            if ((penetrate == false && (targetDead[i] == true || targetDead[i] == false)) || (penetrate == true && (targetList[i].GetComponent<PlayerController>() != null || targetDead[i] == false))) {
                unitSpine.attackAction = delegate () { PenetrateAttack(attackList); };
                UnitTryAttack();
                yield return new WaitForSeconds(atkTime + 0.2f);
                attackerAtk = unit.attack.Value;
                from = targetList.IndexOf(targetList.Find(x => x == attackList[0]));
                to = i;


                if (targetList[i].GetComponent<PlayerController>() != null) targetDead[i] = true;
                attackList.Clear();
            }

            if (targetDead[i] == true)
                yield return DistanceAttack(targetList, targetDead, attackList, attackerAtk, null);


            int range = to - from;
            if (to != -1 && to < targetList.Count - 1) {
                targetList.RemoveRange(from, range);
            }
        }
        actionOver?.Invoke();
    }


    protected IEnumerator ExecuteAttack(List<GameObject> myTargetList, DequeueCallback actionOver = null) {
        if (unit.attackRange == "distance") {
            bool[] checkDead = new bool[myTargetList.Count];
            checkDead.Initialize();
            List<GameObject> attackList = new List<GameObject>();
            yield return DistanceAttack(myTargetList, checkDead, attackList, unit.attack.Value, null);

            //if (granted.Length > 0 && Array.Exists(granted, x => x.name == "penetrate")) {
            //    if (Array.Exists(granted, x => x.name == "charge"))
            //        yield return PenetrateCharge(myTargetList);
            //    else {
            //        unitSpine.attackAction = delegate () { PenetrateAttack(myTargetList); };
            //        UnitTryAttack();
            //        yield return new WaitForSeconds(atkTime + 0.5f);
            //    }
            //    //FinishAttack(false);
            //    actionOver.Invoke();
            //}

            //else {
            //    while (myTargetList.Count > 0) {
            //        unitSpine.attackAction = delegate () { DistanceAttack(myTargetList[0]); };
            //        UnitTryAttack();
            //        yield return new WaitForSeconds(atkTime + 0.2f);
            //        myTargetList.RemoveAt(0);

            //        if (myTargetList.Count == 0)
            //            break;
            //    }
            //    //FinishAttack(false);
            //    actionOver.Invoke();
            //}
            actionOver.Invoke();
        }
        else if (unit.attackRange == "immediate") {
            while (myTargetList.Count > 0) {
                unitSpine.attackAction = delegate () { CloserAttack(myTargetList[0]); };
                arrow.transform.position = (myTargetList[0].GetComponent<PlayerController>() != null) ? myTargetList[0].GetComponent<PlayerController>().wallPosition :myTargetList[0].transform.position;
                UnitTryAttack();
                yield return new WaitForSeconds(atkTime + 0.5f);
                myTargetList.RemoveAt(0);
                if (myTargetList.Count == 0)
                    break;
            }
            //FinishAttack(false);
            actionOver.Invoke();
        }
        else {
            while (myTargetList.Count > 0) {
                GameObject target = myTargetList[0];
                unitSpine.attackAction = delegate () { CloserAttack(target); };
                CloserTarget(target);
                yield return new WaitForSeconds(atkTime + 0.35f);
                target.GetComponent<PlaceMonster>()?.ReturnPosition(false);
                myTargetList.RemoveAt(0);

                if (myTargetList.Count == 0) {
                    yield return new WaitForSeconds(0.05f);
                    ReturnPosition(true);
                    yield return new WaitForSeconds(0.3f);                                       
                    break;
                }
                
            }
            //FinishAttack(false);

            actionOver.Invoke();
        }
        yield return null;
    }


    // 공격을 할려면 어찌됐든 여기로.
    protected void UnitTryAttack() {
        if (unit.attack <= 0) return;
        SetState(UnitState.ATTACK);
        SoundManager.Instance.PlayAttackSound(unit.cardId);
        VoiceType attackVoice;

        if (unitSpine.arrow == null)
            attackVoice = VoiceType.ATTACK;
        else if (unitSpine.arrow != null && unitSpine.arrow.name.Contains("magic"))
            attackVoice = VoiceType.CHARGE;
        else
            attackVoice = VoiceType.ATTACK;
        SoundManager.Instance.PlayUnitVoice(unit.cardId, attackVoice);
    }


    //protected void ImmediateToTarget() {
    //    //arrow.transform.position = myTarget[0].transform.position;
    //    UnitTryAttack();
    //}

    //itween이 함수 검사가 모두 끝나고 실행시킴. 더러운것....
    protected void CloserTarget(GameObject myTarget) {
        PlaceMonster target = myTarget.GetComponent<PlaceMonster>();
        Vector3 playerPos, enemyPos;
        if (target != null) {
            if (isPlayer == true) {
                unitSoringOrder = 51;
                playerPos = new Vector3(gameObject.transform.position.x - 0.75f, myTarget.transform.position.y, gameObject.transform.position.z);
                enemyPos = new Vector3(myTarget.transform.position.x + 0.75f, myTarget.transform.position.y, myTarget.transform.position.z);
            }
            else {
                playerPos = new Vector3(gameObject.transform.position.x + 0.75f, myTarget.transform.position.y, gameObject.transform.position.z);
                enemyPos = new Vector3(myTarget.transform.position.x - 0.75f, myTarget.transform.position.y, myTarget.transform.position.z);
            }
            iTween.MoveTo(gameObject, iTween.Hash("x", playerPos.x, "y", playerPos.y, "z", playerPos.z, "time", 0.3f, "easetype", iTween.EaseType.easeInOutExpo, "oncomplete", "UnitTryAttack", "oncompletetarget", gameObject));
            iTween.MoveTo(myTarget, iTween.Hash("x", enemyPos.x, "y", enemyPos.y, "z", enemyPos.z, "time", 0.2f, "easetype", iTween.EaseType.easeInOutExpo));
        }
        else {
            iTween.MoveTo(gameObject, iTween.Hash("x", gameObject.transform.position.x, "y", myTarget.GetComponent<PlayerController>().unitClosePosition.y, "z", gameObject.transform.position.z, "time", 0.3f, "easetype", iTween.EaseType.easeInOutExpo, "oncomplete", "UnitTryAttack", "oncompletetarget", gameObject));
        }
    }
    
    protected void DistanceAttack(GameObject myTarget) {
        arrow.transform.position = transform.position;
        arrow.SetActive(true);
        PlaceMonster targetMonster = myTarget.GetComponent<PlaceMonster>();
        if (targetMonster != null)
            iTween.MoveTo(arrow, iTween.Hash("x", gameObject.transform.position.x, "y", myTarget.transform.position.y, "z", gameObject.transform.position.z, "time", 0.2f, "easetype", iTween.EaseType.easeOutExpo, "oncomplete", "distanceAttack", "oncompleteparams", myTarget, "oncompletetarget", gameObject));
        else
            iTween.MoveTo(arrow, iTween.Hash("x", gameObject.transform.position.x, "y", myTarget.GetComponent<PlayerController>().wallPosition.y, "z", gameObject.transform.position.z, "time", 0.2f, "easetype", iTween.EaseType.easeOutExpo, "oncomplete", "distanceAttack", "oncompleteparams", myTarget, "oncompletetarget", gameObject));
    }

    protected void PenetrateAttack(List<GameObject> myTargetList) {
        GameObject target = myTargetList.Find(x => x.GetComponent<PlayerController>() != null);
        GameObject arrow = transform.Find("arrow").gameObject;
        arrow.SetActive(true);
        Vector3 pos;

        if (target != null) {
            pos = target.GetComponent<PlayerController>().wallPosition;
            pos.x = gameObject.transform.position.x;
            pos.z = gameObject.transform.position.z;
        }
        else
            pos = myTargetList[myTargetList.Count - 1].transform.position;

        Hashtable hashset = iTween.Hash("x", pos.x, "y", pos.y, "z", pos.z, "time", 0.2f, "easetype", iTween.EaseType.easeOutExpo, "oncomplete", "PiercingAttack", "oncompleteparams", myTargetList, "oncompletetarget", gameObject);
        iTween.MoveTo(arrow, hashset);
    }

    protected void CloserAttack(GameObject myTarget) {
        PlaceMonster targetMonster = myTarget.GetComponent<PlaceMonster>();
        BattleConnector battleConnector = PlayMangement.instance.socketHandler;
        int damage = (unit.attack != null) ? (int)unit.attack : 0;
        if (unit.attack > 0) {
            if (targetMonster != null)
                RequestAttackUnit(myTarget, damage);
            else
                myTarget.GetComponent<PlayerController>().PlayerTakeDamage();
            AttackEffect(myTarget);
        }
        myTarget.GetComponent<PlaceMonster>()?.ReturnPosition();
    }

    protected void distanceAttack(GameObject myTarget) {
        PlaceMonster targetMonster = myTarget.GetComponent<PlaceMonster>();
        BattleConnector battleConnector = PlayMangement.instance.socketHandler;
        int damage = (unit.attack != null) ? (int)unit.attack : 0;
        if (unit.attack > 0) {
            if (targetMonster != null)
                RequestAttackUnit(myTarget, damage);
            else
                myTarget.GetComponent<PlayerController>().PlayerTakeDamage();
            AttackEffect(myTarget);
        }
        if (unit.attackRange != "immediate") {
            arrow.transform.position = transform.position;
            arrow.SetActive(false);
        }
    }


    


    protected void PiercingAttack(List<GameObject> myTarget) {
        PlayerController targetPlayer;

        if (myTarget.Exists(x => x.GetComponent<PlayerController>() != null))
            targetPlayer = myTarget.Find(x => x.GetComponent<PlayerController>() != null).GetComponent<PlayerController>();
        else
            targetPlayer = null;


        GameObject arrow = transform.Find("arrow").gameObject;

        SocketFormat.GameState gameState = PlayMangement.instance.socketHandler.gameState;
        int leftAttack = unit.attack.Value;


        for(int i =0; i<myTarget.Count; i++) {
            if (myTarget[i].GetComponent<PlayerController>() != null) {
                //int amount = 0;
                //if (targetPlayer.isPlayer == true)
                //    amount = PlayMangement.instance.socketHandler.gameState.players.myPlayer(targetPlayer.isHuman).hero.currentHp;
                //else
                //    amount = PlayMangement.instance.socketHandler.gameState.players.enemyPlayer(targetPlayer.isHuman).hero.currentHp;
                //amount = targetPlayer.HP.Value - amount;
                targetPlayer.PlayerTakeDamage();
                AttackEffect(targetPlayer.gameObject);
                leftAttack -= leftAttack;
            }
            else {
                //int amount = 0;
                SocketFormat.Line line = PlayMangement.instance.socketHandler.gameState.map.lines[x];
                //SocketFormat.Unit socketUnit;
                PlaceMonster clientUnit = myTarget[i].GetComponent<PlaceMonster>();
                //socketUnit = System.Array.Find((targetPlayer.isHuman == true) ? line.human : line.orc, x => x.itemId == myTarget[i].GetComponent<PlaceMonster>().itemId);
                //amount = (socketUnit != null) ? socketUnit.currentHp : 0;
                //amount = myTarget[i].GetComponent<PlaceMonster>().unit.currentHp - amount;

                int amount = clientUnit.unit.currentHp;
                RequestAttackUnit(myTarget[i], leftAttack);
                AttackEffect(myTarget[i]);
                leftAttack -= amount;
            }            
        }
        arrow.transform.position = transform.position;
        arrow.SetActive(false);               
    }

    public void ChangePositionMagicEffect() {
        unitSpine.transform.gameObject.SetActive(true);
        SetState(UnitState.APPEAR);
        gameObject.transform.position = unitLocation;
    }

    public void ChangePosition() {
        iTween.MoveTo(gameObject, unitLocation, 1.0f);
    }


    public void ChangePosition(int x, int y, Vector3 unitLocation, string cardID) {
        this.x = x;
        this.y = y;

        Vector3 portalPosition = new Vector3(unitLocation.x, unitSpine.headbone.transform.position.y, unitLocation.z);
        this.unitLocation = unitLocation;        

        switch (cardID) {
            case "ac10028":
                EffectSystem.Instance.ShowEffectOnEvent(EffectSystem.EffectType.PORTAL, portalPosition, delegate() { ChangePositionMagicEffect(); });
                break;
            case "ac10015":
                ChangePositionMagicEffect();
                break;
            default:
                ChangePosition();
                break;
        }
    }


    //사용 안해도 될것 같은데...
    protected async void FinishAttack(bool wait = false) {
        if (wait == true) await System.Threading.Tasks.Task.Delay(500);
        afterAttackActionCall?.Invoke();
        afterAttackActionCall -= afterAttackActionCall;
    }

    public void AttackEffect(GameObject myTarget = null) {
        PlaceMonster targetMonster = myTarget.GetComponent<PlaceMonster>();
        Vector3 targetPos = (targetMonster != null) ? targetMonster.unitSpine.bodybone.position : new Vector3(gameObject.transform.position.x, myTarget.GetComponent<PlayerController>().wallPosition.y, 0);
        SoundManager.Instance.PlayHitSound(unit.cardId);
        if (unit.attack <= 3) {
            EffectSystem.Instance.ShowEffect(EffectSystem.EffectType.HIT_LOW, targetPos);
            StartCoroutine(PlayMangement.instance.cameraShake(0.4f, 1));
        }
        else if (unit.attack > 3 && unit.attack <= 6) {
            EffectSystem.Instance.ShowEffect(EffectSystem.EffectType.HIT_MIDDLE, targetPos);
            StartCoroutine(PlayMangement.instance.cameraShake(0.4f, 4));
        }
        else {
            EffectSystem.Instance.ShowEffect(EffectSystem.EffectType.HIT_HIGH, targetPos);
            StartCoroutine(PlayMangement.instance.cameraShake(0.4f, 10));
        }        
    }
    


    public void RequestAttackUnit(GameObject target, int amount) {
        PlaceMonster targetMonster = target.GetComponent<PlaceMonster>();

        if (targetMonster != null) {
            targetMonster.UnitTakeDamage(amount);

            object[] parms = new object[] { !isPlayer, targetMonster.gameObject };
            PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.BEGIN_ATTACK, this, parms);
        }
    }

    public void UnitTakeDamage(int amount) {
        if(GetComponent<SkillModules.guarded>() != null) amount = 0;
        EffectSystem.Instance.ShowDamageText(transform.position, -amount);


        if (unit.currentHp >= amount)
            unit.currentHp -= amount;
        else
            unit.currentHp = 0;

        UpdateStat();
        UpdateGranted();
        SetState(UnitState.HIT);
        //EffectSystem.Instance.DisableEffect(EffectSystem.EffectType.NO_DAMAGE, transform);
    }

    public void RequestChangeStat(int power = 0, int hp = 0, string magicId = null) {
        //StartCoroutine(buffEffectCoroutine(power, hp, magicId, isMain));
        unit.attack += power;
        if (unit.attack < 0) unit.attack = 0;
        unit.currentHp += hp;        
        UpdateStat();
    }

    public void InitUnitStat() {
        SocketFormat.Unit unitInfo = PlayMangement.instance.socketHandler.gameState.map.allMonster.Find(x => x.itemId == unit.itemId);
        unit.currentHp = unitInfo.currentHp;
        unit.attack = unitInfo.attack;
        UpdateStat();
        //CheckHP();
    }

    //private IEnumerator buffEffectCoroutine(int power, int hp, string magicId = null, bool isMain = false){
    //    buff.atk += power;
    //    buff.hp += hp;
    //    if(buff.running) yield break;
    //    else buff.running = true;
    //    yield return null;

    //    if(buff.atk == 0 && buff.hp == 0) {
    //        buff.init();
    //        yield break;
    //    }
    //    else {
    //        //투석공격
    //        if (magicId == "ac10021") {
    //            EffectSystem.Instance.ShowEffectOnEvent(EffectSystem.EffectType.TREBUCHET, transform.position, delegate() { Hit(); } );              
    //        }
    //        //어둠의 가시
    //        else if (magicId == "ac10074") {
    //            EffectSystem.Instance.ShowEffectOnEvent(EffectSystem.EffectType.DARK_THORN, transform.position, delegate () { Hit(); });
    //        }
    //        else if (magicId == "ac10037") {
    //            EffectSystem.Instance.ShowEffectOnEvent(EffectSystem.EffectType.CHAIN_LIGHTNING, unitSpine.rootbone.position, delegate () { Hit(); });
    //        }
    //        else if (magicId == "ac10034") {
    //            EffectSystem.Instance.ShowEffectOnEvent(EffectSystem.EffectType.FIRE_WAVE, unitSpine.rootbone.position, delegate () { Hit(); }, isMain);
    //        }
    //        //버프 혹은 디버프 효과 부여
    //        else {
    //            GetComponent<UnitBuffHandler>()
    //                .AddBuff(new UnitBuffHandler.BuffStat(power, hp));
                

    //            if(buff.hp < 0) {
    //                EffectSystem.Instance.ShowEffect(EffectSystem.EffectType.DEBUFF, transform.position);
    //            }
    //            else {
    //                EffectSystem.Instance.ShowEffect(EffectSystem.EffectType.BUFF, transform.position);
    //            }
    //        }
    //    }
    //    buff.init();
    //}

    public void Hit() {
        SetState(UnitState.HIT);
    }

    public void UpdateStat() {
        Text hpText = transform.Find("Numbers/HP").GetComponentInChildren<Text>();
        Text atkText = transform.Find("Numbers/ATK").GetComponentInChildren<Text>();

        if (unit.currentHp > 0)
            hpText.text = unit.currentHp.ToString();
        else
            hpText.text = 0.ToString();


        int grantedHP = 0;
        int grantedAtk = 0;
        if (granted != null && granted.Length > 0) { foreach (Granted grant in granted) { if (grant.hp != 0) grantedHP += grant.hp; if (grant.attack != 0) grantedAtk += grant.attack; } };
        int hpPos = unit.hp.Value + grantedHP;
        int atkPos = unit.originalAttack + grantedAtk;


        if (unit.currentHp < hpPos)
            hpText.color = Color.red;
        else if (unit.currentHp > hpPos)
            hpText.color = Color.green;
        else
            hpText.color = Color.white;

        atkText.text = unit.attack.ToString();

        if (unit.attack < atkPos)
            atkText.color = Color.red;
        else if (unit.attack > atkPos)
            atkText.color = Color.green;
        else
            atkText.color = Color.white;
    }

    private void ReturnPosition(bool isAttacker = false) {
        unitSoringOrder = 50;
        Hashtable hashset;
        //"oncomplete", "FinishAttack", "oncompleteparams", false
        hashset = iTween.Hash("x", unitLocation.x, "y", unitLocation.y, "z", unitLocation.z, "time", 0.2f, "easetype", iTween.EaseType.easeInOutExpo);

        //if (isAttacker == true) {
        //    hashset = iTween.Hash("x", unitLocation.x, "y", unitLocation.y, "z", unitLocation.z, "time", 0.2f, "delay", 0.3f, "easetype", iTween.EaseType.easeInOutExpo);
        //}
        //else
            
        iTween.MoveTo(gameObject, hashset);
    }

    public void CheckHP() {
        if (unit.currentHp <= 0) 
            UnitDead();        
    }

    public void CheckGranted() {
        if (Array.Exists(granted, x => x.name == "poisoned")) UnitDead();
    }



    public void UnitDead() {
        PlayMangement playMangement = PlayMangement.instance;
        GameObject[,] slots = null;
        bool isHuman = playMangement.player.isHuman;

        if (isPlayer) {
            if (isHuman) slots = playMangement.UnitsObserver.humanUnits;
            else slots = playMangement.UnitsObserver.orcUnits;

            if(slots[x, y] == null)
                return;
        }
        else {
            if (isHuman) slots = playMangement.UnitsObserver.orcUnits;
            else slots = playMangement.UnitsObserver.humanUnits;

            if (slots[x, y] == null)
                return;
        }
        unit.currentHp = 0;
        PlayMangement.instance.cardInfoCanvas.Find("CardInfoList").GetComponent<CardListManager>().RemoveUnitInfo(myUnitNum);
        GameObject tomb;
        tomb = IngameResourceLibrary.gameResource.deadObject;

        GameObject dropTomb = Instantiate(tomb);
        dropTomb.transform.position = transform.position;

        //Logger.Log("X : " + x);
        //Logger.Log("Y : " + y);

        if (isPlayer) {
            PlayMangement.instance.UnitsObserver.UnitRemoved(new FieldUnitsObserver.Pos(x, y), isHuman);
        }
        else {
            PlayMangement.instance.UnitsObserver.UnitRemoved(new FieldUnitsObserver.Pos(x, y), !isHuman);
        }

        dropTomb.GetComponent<DeadSpine>().target = gameObject;
        dropTomb.GetComponent<DeadSpine>().StartAnimation(unit.ishuman);
        SoundManager.Instance.PlayUnitVoice(unit.cardId, VoiceType.DIE);
        object[] parms = new object[]{isPlayer, gameObject};

        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.FIELD_CHANGED, null, null);
        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.DIE, this, parms);
    }
    

    
    protected void SetState(UnitState state) {

        switch (state) {
            case UnitState.APPEAR:
                unitSpine.Appear();
                break;
            case UnitState.IDLE: {
                    if (unitSpine.enabled == true)
                        unitSpine.Idle();
                    else
                        hideSpine.Idle();
                }
                break;
            case UnitState.ATTACK:                
                unitSpine.Attack();
                break;
            case UnitState.HIT:
                SoundManager.Instance.PlayUnitVoice(unit.cardId, VoiceType.DAMAGE);
                unitSpine.Hit();
                break;
            case UnitState.MAGICHIT:
                unitSpine.MagicHit();
                break;
            case UnitState.DETECT:
                hideSpine.Disappear();
                break;
            case UnitState.REPLACE:
                unitSpine.Appear();
                
                break;
            case UnitState.DEAD:
                break;
        }
    }

    public void TintAnimation(bool onOff) {
        tintOnOff = onOff;
        if(tintOnOff) StartCoroutine(PingPongTween());
    }
    
    bool tintOnOff = false;
    


    private IEnumerator PingPongTween() {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        MeshRenderer meshRenderer = unitSpine.GetComponent<MeshRenderer>();
        unitSoringOrder = 55;
        string glowPower = "_GlowPower";
        //string toggle = "_UseGlow";
        //string colorProperty = "_Color";

        //block.SetInt(toggle, 1);
        //block.SetColor(colorProperty, Color.white);
		//string blackTintProperty = "_Black";
        while (tintOnOff) {
            float random = Mathf.PingPong(Time.time, 1f) + 1f;
            block.SetFloat(glowPower, random);
            //block.SetColor(blackTintProperty, showColor);
            meshRenderer.SetPropertyBlock(block);
            yield return null;
        }
        block.SetFloat(glowPower, 1f);
        //block.SetInt(toggle, 0);
        //block.SetColor(blackTintProperty, Color.black);
        meshRenderer.SetPropertyBlock(block);
        unitSoringOrder = 50;
        EffectSystem.Instance.HideEveryDim();
    }

    private IEnumerator UnitMovement(Transform targetPos) {
        float runningTime = 0f;
        float maxTime = 0.3f;
        float percentage = 0f;

        while (percentage < 1f) {         
            float x = MovementSpeed(gameObject.transform.position.x, targetPos.position.x, percentage);
            float y = MovementSpeed(gameObject.transform.position.y, transform.position.y, percentage);
            float z = MovementSpeed(gameObject.transform.position.z, transform.position.z, percentage);

            gameObject.transform.position = new Vector3(x, y, z);

            runningTime += Time.deltaTime;
            percentage = runningTime / maxTime;
        }
        yield return null;
    }

    private float MovementSpeed(float start, float end, float percentage) {
        end -= start;
        if(percentage < 1) return end * 0.5f * Mathf.Pow(2, 10 * (percentage - 1)) + start;
        percentage--;
        return end * 0.5f * (-Mathf.Pow(2, -10 * percentage) + 2) + start;
    }

}
