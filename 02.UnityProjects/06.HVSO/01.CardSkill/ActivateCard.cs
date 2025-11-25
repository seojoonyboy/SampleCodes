using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using SocketFormat;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class ActiveCard {

    public class CardPlayArgs {

    }

    public delegate void AfterCallBack();
    FieldUnitsObserver unitObserver;



    // 카드 별로 구현시에는 나중에 좀 큰일이 될수 있으니 정리 예정
    public delegate void SkillAction(object args, DequeueCallback callback);
    SkillAction cardSkill;


    public void Activate(string cardId, object args, DequeueCallback callback) {
        MethodInfo theMethod = this.GetType().GetMethod(cardId);
        object[] parameter = new object[] { args, callback };
        unitObserver = unitObserver == null ? PlayMangement.instance.UnitsObserver : unitObserver;
        if (theMethod == null) {
            Logger.Log(cardId + "해당 카드는 아직 준비가 안되어있습니다.");
            callback();
            return;
        }
        theMethod.Invoke(this, parameter);
    }


    protected async void AfterCallAction(float time = 0f, AfterCallBack callAction = null, DequeueCallback callback = null) {
        await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(time));
        callAction?.Invoke();
        callAction = null;
        callback?.Invoke();
    }



    //축복
    public void ac10006(object args, DequeueCallback callback) {
        JObject jObject = args as JObject;
        string itemId = jObject["targets"][0]["args"][0].ToString();

        GameObject targetUnit = PlayMangement.instance.UnitsObserver.GetUnitToItemID(itemId);
        EffectSystem.Instance.ShowEffect(EffectSystem.EffectType.BLESS_AC10006, targetUnit.transform.position);
        SoundManager.Instance.PlayMagicSound("ac10006_1");
        SoundManager.Instance.PlayMagicSound("ac10006_2");

        targetUnit.GetComponent<PlaceMonster>().UpdateGranted();
        callback();
    }

    //긴급 보급
    public void ac10007(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] itemIds = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());
        bool isHuman = magicArgs.targets[0].args[0] == "human";
        PlayerController player = PlayMangement.instance.player;
        BattleConnector socket = PlayMangement.instance.SocketHandler;
        if (player.isHuman != isHuman)
            player.StartCoroutine(PlayMangement.instance.EnemyMagicCardDraw(itemIds.Length, callback));
        else
            socket.DrawNewCards(itemIds, callback);
    }

    //재배치
    public void ac10015(object args, DequeueCallback callback) {
        JObject jObject = args as JObject;
        Debug.Log(jObject["targets"][0]["args"][0]);
        string itemId = (string)jObject["targets"][0]["args"][0];
        Debug.Log(itemId);
        GameObject monster = unitObserver.GetUnitToItemID(itemId);
        Unit unit = PlayMangement.instance.socketHandler.gameState.map.allMonster.Find(x => string.Compare(x.itemId, itemId, StringComparison.Ordinal) == 0);
        EffectSystem.ActionDelegate skillAction;
        skillAction = delegate () { monster.GetComponent<PlaceMonster>().UpdateGranted(); callback(); };
        unitObserver.UnitChangePosition(monster, unit.pos, monster.GetComponent<PlaceMonster>().isPlayer, string.Empty, () => skillAction());
    }

    //피의 분노
    public void ac10016(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        JObject jObject = JObject.FromObject(magicArgs.skillInfo);
        AttackArgs info = jObject.ToObject<AttackArgs>();
        PlaceMonster attacker = unitObserver.GetUnitToItemID(info.attacker).GetComponent<PlaceMonster>();
        List<GameObject> affected = unitObserver.GetAfftecdList(attacker.unit.ishuman, info.affected);
        EffectSystem effectSystem = EffectSystem.Instance;
        EffectSystem.ActionDelegate skillAction;
        skillAction = delegate () { attacker.GetTarget(affected, callback); };
        effectSystem.ShowEffectAfterCall(EffectSystem.EffectType.ANGRY, attacker.unitSpine.headbone, skillAction);
    }

    //전쟁의 외침
    public void ac10017(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] itemIds = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());
        bool isHuman = magicArgs.targets[0].args[0] == "human";
        PlayerController player = PlayMangement.instance.player;
        BattleConnector socket = PlayMangement.instance.SocketHandler;
        if (player.isHuman != isHuman)
            PlayMangement.instance.StartCoroutine(PlayMangement.instance.EnemyMagicCardDraw(itemIds.Length, callback));
        else
            socket.DrawNewCards(itemIds, callback);
    }

    //투석 공격
    public void ac10021(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] itemIds = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());
        bool userIsHuman = magicArgs.itemId[0] == 'H';
        PlayerController targetPlayer = PlayMangement.instance.player.isHuman == userIsHuman ? PlayMangement.instance.enemyPlayer : PlayMangement.instance.player;
        BattleConnector socket = PlayMangement.instance.SocketHandler;
        int line = int.Parse(magicArgs.targets[0].args[0]);
        Unit[] units = (targetPlayer.isHuman == true) ? socket.gameState.map.lines[line].human : socket.gameState.map.lines[line].orc;
        AfterCallBack afterCallBack = delegate () {  };
        EffectSystem effectSystem = EffectSystem.Instance;
        EffectSystem.ActionDelegate skillAction;
        //socket.gameState.map.line        
        for (int i = 0; i < itemIds.Length; i++) {
            skillAction = null;
            if (itemIds[i].Contains("hero")) {               
                skillAction = delegate () { targetPlayer.TakeIgnoreShieldDamage(true, "ac10021"); targetPlayer.MagicHit(); /*PlayMangement.instance.CheckLine(line);*/};
                effectSystem.ShowEffectOnEvent(EffectSystem.EffectType.TREBUCHET, targetPlayer.bodyTransform.position, skillAction);
            }
            else {
                PlaceMonster unit = unitObserver.GetUnitToItemID(itemIds[i]).GetComponent<PlaceMonster>();
                Unit socketUnit = Array.Find(units, x => x.itemId == itemIds[i]);
                skillAction = delegate () { unit.RequestChangeStat(0, -(unit.unit.currentHp - socketUnit.currentHp), "ac10021"); unit.Hit(); };
                effectSystem.ShowEffectOnEvent(EffectSystem.EffectType.TREBUCHET, unit.gameObject.transform.position, skillAction);                
            }
        }
        AfterCallAction(1.2f, null, callback);

    }

    public void ac10055(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string targetID = magicArgs.targets[0].args[0];
        GameObject affected = unitObserver.GetUnitToItemID(targetID);
        affected.GetComponent<PlaceMonster>().RequestChangeStat(-1, -1, "ac10055");
        callback();
    }


    //한파
    public void ac10022(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] itemIds = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());
        bool isHuman = magicArgs.itemId[0] == 'H' ? true : false;

        PlaceMonster targetUnit = unitObserver.GetUnitToItemID(magicArgs.targets[0].args[0]).GetComponent<PlaceMonster>();
        targetUnit.UpdateGranted();
        targetUnit.gameObject.AddComponent<SkillModules.stun>();

        PlayerController player = PlayMangement.instance.player;
        EffectSystem.Instance.ShowEffect(EffectSystem.EffectType.COLDWAVE_AC10022, targetUnit.transform.position);

        if (player.isHuman != isHuman)
            PlayMangement.instance.StartCoroutine(PlayMangement.instance.EnemyMagicCardDraw(itemIds.Length, callback));
        else
            PlayMangement.instance.socketHandler.DrawNewCards(itemIds, callback);

    }

    //독성부여
    public void ac10027(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        bool isHuman = magicArgs.targets[0].args[0] == "orc" ? false : true;
        List<GameObject> affectedList = unitObserver.GetAllFieldUnits(isHuman);
        for (int i = 0; i < affectedList.Count; i++)
            affectedList[i].GetComponent<PlaceMonster>().UpdateGranted();
        callback?.Invoke();
    }

    //습격용 포탈
    public void ac10028(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        bool isHuman = magicArgs.itemId[0] == 'O' ? false : true;

        JObject jObject = JObject.FromObject(magicArgs.skillInfo);
        AttackArgs info = jObject.ToObject<AttackArgs>();
        string itemId = info.attacker;




        GameObject monster = unitObserver.GetUnitToItemID(itemId);
        Unit unit = PlayMangement.instance.socketHandler.gameState.map.allMonster.Find(x => string.Compare(x.itemId, itemId, StringComparison.Ordinal) == 0);

        PlaceMonster attacker = monster.GetComponent<PlaceMonster>();
        attacker.UpdateGranted();
        List<GameObject> affected = unitObserver.GetAfftecdList(monster.GetComponent<PlaceMonster>().unit.ishuman, info.affected);
        EffectSystem effectSystem = EffectSystem.Instance;
        EffectSystem.ActionDelegate skillAction;
        skillAction = delegate () { attacker.GetTarget(affected, callback); };

        if (unitObserver.CheckEmptySlot(isHuman) == true)
            unitObserver.UnitChangePosition(monster, unit.pos, monster.GetComponent<PlaceMonster>().isPlayer, "ac10028", () => skillAction());
        else
            skillAction();
    }

    //암흑수정구
    public void ac10025(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] itemIds = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());
        bool isHuman = magicArgs.targets[0].args[0] == "human";
        PlayerController player = PlayMangement.instance.player;
        BattleConnector socket = PlayMangement.instance.SocketHandler;
        if (player.isHuman != isHuman)
            player.StartCoroutine(PlayMangement.instance.EnemyMagicCardDraw(itemIds.Length, callback));
        else
            socket.DrawNewCards(itemIds, callback);
    }

    //송환
    public void ac10023(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string targetItemID = (string)magicArgs.skillInfo;
        bool isHuman = magicArgs.targets[0].args[0] == "orc" ? false : true;
        bool isPlayer = PlayMangement.instance.GetPlayerWithRace(isHuman);
        BattleConnector socket = PlayMangement.instance.SocketHandler;

        PlaceMonster targetUnit = unitObserver.GetUnitToItemID(targetItemID).GetComponent<PlaceMonster>();
        string cardID = targetUnit.unit.cardId;
        EffectSystem.Instance.ShowEffect(EffectSystem.EffectType.GETBACK, targetUnit.transform.position);
        UnityEngine.Object.Destroy(targetUnit.gameObject);

        if (isPlayer == true)
            socket.DrawNewCard(targetItemID);
        else
            PlayMangement.instance.enemyPlayer.UpdateCardCount();


        callback();
    }
    //사기진작
    public void ac10024(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] itemIds = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());
        for (int i = 0; i < itemIds.Length; i++) {
            GameObject targetUnit = PlayMangement.instance.UnitsObserver.GetUnitToItemID(itemIds[i]);
            targetUnit.GetComponent<PlaceMonster>().UpdateGranted();
            EffectSystem.Instance.ShowEffect(EffectSystem.EffectType.BOOSTMORALE_AC10024, targetUnit.transform.position);
        }
        callback();
    }

    //성장폭주
    public void ac10026(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] itemIds = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());
        for (int i = 0; i < itemIds.Length; i++) {
            GameObject targetUnit = PlayMangement.instance.UnitsObserver.GetUnitToItemID(itemIds[i]);
            targetUnit.GetComponent<PlaceMonster>().UpdateGranted();
            EffectSystem.Instance.ShowEffect(EffectSystem.EffectType.GROWTHRANAWAY_AC10026, targetUnit.transform.position);
        }
        callback();
    }

    //전승 지식
    public void ac10035(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] itemIds = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());
        bool isHuman = magicArgs.targets[0].args[0] == "human";
        PlayerController player = PlayMangement.instance.player;
        BattleConnector socket = PlayMangement.instance.SocketHandler;
        if (player.isHuman != isHuman)
            player.StartCoroutine(PlayMangement.instance.EnemyMagicCardDraw(itemIds.Length, callback));
        else
            socket.DrawNewCards(itemIds, callback);

        SoundManager.Instance.PlayMagicSound("ac10035_1");
        SoundManager.Instance.PlayMagicSound("ac10035_2");
    }


    //마력주입
    public void ac10036(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] itemIds = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());
        bool userIsHuman = magicArgs.itemId[0] == 'H';


        for (int i = 0; i < itemIds.Length; i++) {
            GameObject targetUnit = unitObserver.GetUnitToItemID(itemIds[i]);
            PlaceMonster targetUnitData = targetUnit.GetComponent<PlaceMonster>();
            EffectSystem.Instance.ShowEffect(EffectSystem.EffectType.MANAINSERT_AC10036, targetUnitData.transform.position);
            targetUnitData.UpdateGranted();
        }
        SoundManager.Instance.PlayMagicSound("ac10036_1");
        AfterCallAction(0f, null, callback);
    }



    //불의파도
    public void ac10034(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] itemIds = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());
        string mainTarget = magicArgs.targets[0].args[0];

        BattleConnector socket = PlayMangement.instance.SocketHandler;

        bool isHuman = magicArgs.targets[0].args[1] == "orc" ? false : true;
        Player playerData = (isHuman) ? socket.gameState.players.human : socket.gameState.players.orc;

        EffectSystem.ActionDelegate mainAction;
        EffectSystem.ActionDelegate afterAction;


        GameObject mainUnit = unitObserver.GetUnitToItemID(mainTarget);
        PlaceMonster mainUnitData = mainUnit.GetComponent<PlaceMonster>();
        int line = mainUnitData.x;



        for (int i = 0; i < itemIds.Length; i++) {
            if (itemIds[i] == mainTarget) continue;

            GameObject subUnit = unitObserver.GetUnitToItemID(itemIds[i]);
            PlaceMonster subUnitData = subUnit.GetComponent<PlaceMonster>();
            line = subUnitData.x;
            mainAction = delegate () { subUnitData.RequestChangeStat(0, -1); };
            //afterAction = delegate () { subUnitData.CheckHP(); };

            EffectSystem.Instance.ShowEffectOnEvent(EffectSystem.EffectType.FIRE_WAVE, subUnit.transform.position, mainAction, false, null, null);

            mainAction = null;
            afterAction = null;
        }
        mainAction = delegate () { mainUnitData.RequestChangeStat(0, -3); };
        afterAction = delegate () {  callback(); };

        EffectSystem.Instance.ShowEffectOnEvent(EffectSystem.EffectType.FIRE_WAVE, mainUnit.transform.position, mainAction, true, null, afterAction);
    }

    //연쇄번개
    public void ac10037(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] itemIds = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());
        bool userIsHuman = magicArgs.itemId[0] == 'H';
        PlayerController targetPlayer = PlayMangement.instance.player.isHuman == userIsHuman ? PlayMangement.instance.enemyPlayer : PlayMangement.instance.player;
        BattleConnector socket = PlayMangement.instance.SocketHandler;

        EffectSystem.ActionDelegate mainAction;
        EffectSystem.ActionDelegate afterAction = null;

        for (int i = 0; i < itemIds.Length; i++) {
            GameObject targetUnit = unitObserver.GetUnitToItemID(itemIds[i]);
            PlaceMonster targetUnitData = targetUnit.GetComponent<PlaceMonster>();
            int line = targetUnitData.x;

            mainAction = delegate () { targetUnitData.RequestChangeStat(0, -5); };
            //afterAction = delegate () { targetUnitData.CheckHP(); };

            if (i == itemIds.Length - 1) afterAction += delegate () { callback(); };
            EffectSystem.Instance.ShowEffectOnEvent(EffectSystem.EffectType.CHAIN_LIGHTNING, targetUnit.transform.position, mainAction, false, null, afterAction);
        }
    }
    //숲의 축복
    public void ac10044(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] itemIds = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());
        for (int i = 0; i < itemIds.Length; i++)
            PlayMangement.instance.UnitsObserver.GetUnitToItemID(itemIds[i]).GetComponent<PlaceMonster>().UpdateGranted();
        callback();
    }

    ////마법대학 수석
    //public void ac10032(object args, DequeueCallback callback) {
    //    MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
    //    if(magicArgs.targets[1].method == "unit") {
    //        GameObject targetUnit = unitObserver.GetUnitToItemID(magicArgs.targets[1].args[0]);
    //        PlaceMonster targetUnitData = targetUnit.GetComponent<PlaceMonster>();
    //        targetUnitData.UpdateGranted();
    //    }
    //    callback();
    //}

    //툴카드 감옥
    public void ac10050(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        var targets = magicArgs.targets;

        int line = int.Parse(targets[0].args[0]);
        EffectSystem.Instance.SetUpToolLine("ac10050", line, delegate () { PlayMangement.instance.CheckLineGranted(line); }, callback);
    }

    public void ac10077(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        int line = int.Parse(magicArgs.targets[0].args[0]);
        EffectSystem.Instance.SetUpToolLine("ac10077", line, delegate () { PlayMangement.instance.CheckLineGranted(line); }, callback);
    }

    public void ac10091(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        int line = int.Parse(magicArgs.targets[0].args[0]);
        EffectSystem.Instance.ClearToolLine(line, delegate () { PlayMangement.instance.CheckLineGranted(line); }, callback);
    }

    public void ac10045(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] itemIds = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());
        bool isHuman = magicArgs.itemId[0] == 'O' ? false : true;
        bool isPlayer = PlayMangement.instance.GetPlayerWithRace(isHuman);
        PlayerController player = PlayMangement.instance.player;
        BattleConnector socket = PlayMangement.instance.SocketHandler;


        if (player.isHuman != isHuman) {
            PlayMangement.instance.enemyPlayer.HP.Value = socket.gameState.players.orc.hero.currentHp;
            PlayMangement.instance.StartCoroutine(PlayMangement.instance.EnemyMagicCardDraw(itemIds.Length, callback));
        }
        else {
            PlayMangement.instance.enemyPlayer.HP.Value = socket.gameState.players.orc.hero.currentHp;
            player.ActiveOrcTurn();
            socket.DrawNewCards(itemIds, callback);

        }
    }


    //힘줄절단
    public void ac10046(object args, DequeueCallback callback) {
        Debug.Log(args);
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string targetItemID = magicArgs.targets[0].args[0];

        GameObject targetUnitObject = unitObserver.GetUnitToItemID(targetItemID);
        PlaceMonster targetUnit = targetUnitObject.GetComponent<PlaceMonster>();

        SoundManager.Instance.PlayMagicSound("ac10046_1");
        SoundManager.Instance.PlayMagicSound("ac10046_2");

        EffectSystem.Instance.ShowEffectAfterCall(EffectSystem.EffectType.CUTSTRING_AC10046, targetUnit.transform, delegate () { targetUnit.RequestChangeStat(-4, -2); callback(); });
        
    }


    //법률제정
    public void ac10047(object args, DequeueCallback callback) {
        Debug.Log(args);
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string targetItemID = magicArgs.targets[0].args[0];

        GameObject targetUnitObject = unitObserver.GetUnitToItemID(targetItemID);
        PlaceMonster targetUnit = targetUnitObject.GetComponent<PlaceMonster>();
        SoundManager.Instance.PlayMagicSound("ac10047_2");


        AfterCallBack afterAction = delegate () { SoundManager.Instance.PlayMagicSound("ac10047_1"); };
        EffectSystem.Instance.ShowEffectAfterCall(EffectSystem.EffectType.LEGISLATION_AC10047, targetUnit.transform, delegate() { targetUnit.UpdateGranted(); callback(); });
        AfterCallAction(0.9f, afterAction, null);
        //targetUnit.RequestChangeStat(-2, 1);
        //callback();
    }


    //체포
    public void ac10049(object args, DequeueCallback callback) {
        Debug.Log(args);
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string targetItemID = magicArgs.targets[0].args[0];

        GameObject targetUnitObject = unitObserver.GetUnitToItemID(targetItemID);
        PlaceMonster targetUnit = targetUnitObject.GetComponent<PlaceMonster>();
        targetUnitObject.AddComponent<SkillModules.Arrest>().amount = 1;

        EffectSystem.Instance.ShowEffectAfterCall(EffectSystem.EffectType.ARREST_AC10049, targetUnit.transform, delegate () { targetUnit.UpdateGranted(); callback(); });
        //targetUnit.RequestChangeStat(0, -1);
        //callback();
    }
    //깨우침
    public void ac10054(object args, DequeueCallback callback) {
        Debug.Log(args);
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string targetItemID = magicArgs.targets[0].args[0];

        GameObject targetUnitObject = unitObserver.GetUnitToItemID(targetItemID);
        PlaceMonster targetUnit = targetUnitObject.GetComponent<PlaceMonster>();

        targetUnit.UpdateGranted();
        EffectSystem.Instance.ShowEffect(EffectSystem.EffectType.DEBUFF, targetUnitObject.transform.position);
        callback();
    }

    public void ac10081(object args, DequeueCallback callback) {
        Debug.Log(args);
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string targetItemID = magicArgs.targets[0].args[0];

        GameObject targetUnitObject = unitObserver.GetUnitToItemID(targetItemID);
        PlaceMonster targetUnit = targetUnitObject.GetComponent<PlaceMonster>();

        targetUnit.UpdateGranted();
        EffectSystem.Instance.ShowEffect(EffectSystem.EffectType.FAKECONTRACT_AC10081, targetUnit.unitSpine.bodybone.position);
        callback();
    }

    //무지함
    public void ac10061(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string targetItemID = magicArgs.targets[0].args[0];

        GameObject targetUnitObject = unitObserver.GetUnitToItemID(targetItemID);
        PlaceMonster targetUnit = targetUnitObject.GetComponent<PlaceMonster>();

        targetUnit.UpdateGranted();
        
        EffectSystem.ActionDelegate skillAction;
        skillAction = delegate() {
            callback();
        };
        
        EffectSystem.Instance.ShowEffectOnEvent(EffectSystem.EffectType.IGNORANCE, targetUnit.transform.position, "ac10061", skillAction);
    }

    //과부하
    public void ac10065(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] skillInfoArray = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());

        string targetID = skillInfoArray[0];
        GameObject targetUnitObject = unitObserver.GetUnitToItemID(targetID);
        PlaceMonster targetUnit = targetUnitObject.GetComponent<PlaceMonster>();

        targetUnit.gameObject.AddComponent<SkillModules.stun>();
        targetUnit.UpdateGranted();

        callback();
    }

    public void ac10067(object args, DequeueCallback callback) {
        Debug.Log(args);
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string targetItemID = magicArgs.targets[0].args[0];

        GameObject targetUnitObject = unitObserver.GetUnitToItemID(targetItemID);
        targetUnitObject.AddComponent<SkillModules.Arrest>().amount = 1;
        PlaceMonster targetUnit = targetUnitObject.GetComponent<PlaceMonster>();

        targetUnit.UpdateGranted();
        EffectSystem.Instance.ShowEffect(EffectSystem.EffectType.DEBUFF, targetUnitObject.transform.position);
        callback();
    }

    //마력폭주
    public void ac10068(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());

        var targets = magicArgs.targets;
        bool isHeroTarget = magicArgs.targets[0].args[0] == "hero" ? true : false;
        EffectSystem.ActionDelegate skillAction;

        if (isHeroTarget) {
            bool isHuman = magicArgs.targets[0].args[1] == "human";
            PlayerController targetPlayer = PlayMangement.instance.player.isHuman == isHuman ? PlayMangement.instance.player : PlayMangement.instance.enemyPlayer;
            skillAction = delegate () {
                targetPlayer.TakeIgnoreShieldDamage(true);
                targetPlayer.MagicHit();

                Logger.Log("마력 폭주 ShowEffectOnEvent Callback!");
                callback();
            };

            EffectSystem.Instance.ShowEffectOnEvent(
                EffectSystem.EffectType.MAGIC_OVERWHELMED,
                targetPlayer.bodyTransform.position,
                null,
                true,
                null,
                skillAction
            );
        }
        else {
            string[] skillInfoTargets = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());

            BattleConnector socket = PlayMangement.instance.SocketHandler;
            var units = socket.gameState.map.allMonster;

            bool isHuman = magicArgs.itemId[0] == 'H' ? true : false;

            foreach (var target in skillInfoTargets) {
                GameObject targetUnitObject = unitObserver.GetUnitToItemID(target);
                PlaceMonster targetUnit = targetUnitObject.GetComponent<PlaceMonster>();

                skillAction = delegate () {
                    targetUnit.UpdateGranted();
                };

                //메인 타겟
                if (target.Equals(magicArgs.targets[0].args[0])) {
                    EffectSystem.Instance.ShowEffectOnEvent(
                        EffectSystem.EffectType.MAGIC_OVERWHELMED,
                        targetUnit.transform.position,
                        skillAction,
                        true
                    );
                }
                else EffectSystem.Instance.ShowEffectOnEvent(EffectSystem.EffectType.MAGIC_OVERWHELMED, targetUnit.transform.position, skillAction);
            }

            callback();
        }
    }

    //어둠의 가시
    public void ac10074(object args, DequeueCallback callback) {
        Debug.Log(args);
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] targets = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());
        string targetItemID = targets[0];
        bool isHuman = magicArgs.itemId[0] == 'H' ? true : false;
        PlayerController targetPlayer = PlayMangement.instance.player.isHuman == isHuman ? PlayMangement.instance.enemyPlayer : PlayMangement.instance.player;
        EffectSystem.ActionDelegate skillAction;

        if (targetItemID.Contains("hero")) {
            skillAction = delegate () { targetPlayer.TakeIgnoreShieldDamage(true, "ac10021"); targetPlayer.MagicHit(); callback(); };
            EffectSystem.Instance.ShowEffectOnEvent(EffectSystem.EffectType.DARK_THORN, targetPlayer.bodyTransform.position, skillAction);
        }
        else {          
            GameObject targetUnitObject = unitObserver.GetUnitToItemID(targetItemID);
            PlaceMonster targetUnit = targetUnitObject.GetComponent<PlaceMonster>();
            targetUnit.UpdateGranted();
            skillAction = delegate () { callback(); };
            EffectSystem.Instance.ShowEffectOnEvent(EffectSystem.EffectType.DARK_THORN, targetUnitObject.transform.position, skillAction);
        }
    }

    //암수 살인
    public void ac10084(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] targets = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());

        string targetItemID = targets[0];
        GameObject targetUnitObject = unitObserver.GetUnitToItemID(targetItemID);
        PlaceMonster targetUnit = targetUnitObject.GetComponent<PlaceMonster>();


        EffectSystem.Instance.ShowEffectAfterCall(EffectSystem.EffectType.MURDER_AC10084, targetUnit.unitSpine.bodybone, delegate () { callback(); });

        //targetUnit.UnitDead();
        //callback();
    }

    //종의 멸망
    public void ac10094(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] targets = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());

        BattleConnector socket = PlayMangement.instance.SocketHandler;

        bool userIsHuman = magicArgs.itemId[0] == 'H';
        PlayerController targetPlayer = PlayMangement.instance.player.isHuman == userIsHuman ? PlayMangement.instance.enemyPlayer : PlayMangement.instance.player;

        var units = targetPlayer.isHuman ? socket.gameState.map.GetHumanMonsters : socket.gameState.map.GetOrcMonsters;

        EffectSystem.ActionDelegate skillAction;
        foreach (var targetID in targets) {
            GameObject targetUnitObject = unitObserver.GetUnitToItemID(targetID);
            PlaceMonster targetUnit = targetUnitObject.GetComponent<PlaceMonster>();

            Unit socketUnit = units.Find(x => string.Equals(x.itemId, targetID, StringComparison.Ordinal));
            skillAction = delegate () {
                targetUnit.RequestChangeStat(0, -(targetUnit.unit.currentHp - socketUnit.currentHp));
                targetUnit.Hit();
            };

            targetUnit.RequestChangeStat(0, -(targetUnit.unit.currentHp - socketUnit.currentHp));
            //TODO : spine animation 이름이 animation이 아님
            EffectSystem.Instance.ShowEffectOnEvent(EffectSystem.EffectType.DISTINCTION, targetUnit.transform.position, skillAction);
        }

        callback();
    }

    public void ac10075(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string targetItemID = magicArgs.targets[0].args[0];
        
        GameObject targetUnitObject = unitObserver.GetUnitToItemID(targetItemID);
        if (targetUnitObject != null) {
            targetUnitObject.AddComponent<SkillModules.Arrest>().amount = 1;
            PlaceMonster targetUnit = targetUnitObject.GetComponent<PlaceMonster>();
            targetUnit.UpdateGranted();
        }
        else {
            bool isHuman = magicArgs.itemId[0] == 'H' ? true : false;
            PlayerController targetPlayer = PlayMangement.instance.player.isHuman == isHuman ? PlayMangement.instance.enemyPlayer : PlayMangement.instance.player;
            targetPlayer.TakeIgnoreShieldDamage();
        }
        EffectSystem.Instance.ShowEffect(EffectSystem.EffectType.MANAEXTRACTION_AC10075, targetUnitObject.transform.position);
        callback();
    }

    //탐지 결계
    public void ac10029(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string targetItemID = magicArgs.targets[0].args[0];
        GameObject targetUnitObject = unitObserver.GetUnitToItemID(targetItemID);

        EffectSystem.Instance.ShowEffectAfterCall(EffectSystem.EffectType.DETECT, targetUnitObject.transform, delegate() { callback(); });
    }

    // 완벽보호
    public void ac10043(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        //string[] targetArray = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());
        bool isHuman = magicArgs.itemId[0] == 'H' ? true : false;

        List<GameObject> unitList = unitObserver.GetAllFieldUnits(isHuman);

        for (int i = 0; i < unitList.Count; i++)
            unitList[i].AddComponent<SkillModules.guarded>();

        GameObject hero = PlayMangement.instance.player.isHuman == isHuman ? PlayMangement.instance.player.gameObject : PlayMangement.instance.enemyPlayer.gameObject;
        hero.AddComponent<SkillModules.guarded>();

        //for (int i = 0; i<targetArray.Length; i++) {
        //    if (targetArray[i].Contains("hero")) {
        //        GameObject hero;
        //        if(targetArray[i] == "hero_human") 
        //            hero = (PlayMangement.instance.player.isHuman == true) ? PlayMangement.instance.player.gameObject : PlayMangement.instance.enemyPlayer.gameObject;
        //        else
        //            hero = (PlayMangement.instance.player.isHuman == false) ? PlayMangement.instance.player.gameObject : PlayMangement.instance.enemyPlayer.gameObject;
        //        EffectSystem.Instance.ContinueEffect(EffectSystem.EffectType.NO_DAMAGE, hero.transform, hero.GetComponent<PlayerController>().bodyTransform);
        //    }
        //    else {
        //        GameObject unit = unitObserver.GetUnitToItemID(targetArray[i]);
        //        EffectSystem.Instance.ContinueEffect(EffectSystem.EffectType.NO_DAMAGE, unit.transform, unit.GetComponent<PlaceMonster>().unitSpine.bodybone);
        //    }
        //}
        callback();
    }

    //보존
    public void ac10058(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string targetItemID = magicArgs.targets[0].args[0];
        unitObserver.GetUnitToItemID(targetItemID).GetComponent<PlaceMonster>().UpdateGranted();
        callback();
    }
    //잠복근무
    public void ac10088(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string targetItemID = magicArgs.targets[0].args[0];
        unitObserver.GetUnitToItemID(targetItemID).GetComponent<PlaceMonster>().UpdateGranted();
        unitObserver.GetUnitToItemID(targetItemID).AddComponent<ambush>();
        callback();
    }

    public void ac10038(object args, DequeueCallback callback){
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] itemIds = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());

        for(int i = 0; i<itemIds.Length; i++) {
            unitObserver.GetUnitToItemID(itemIds[i]).GetComponent<PlaceMonster>().UpdateGranted();
        }

        SoundManager.Instance.PlayMagicSound("ac10038_1");
        SoundManager.Instance.PlayMagicSound("ac10038_2");
        SoundManager.Instance.PlayMagicSound("ac10038_3");
        callback();
    }

    public void ac10078(object args, DequeueCallback callback) {
        MagicArgs magicArgs = dataModules.JsonReader.Read<MagicArgs>(args.ToString());
        string[] itemIds = dataModules.JsonReader.Read<string[]>(magicArgs.skillInfo.ToString());
        for (int i = 0; i < itemIds.Length; i++) {
            unitObserver.GetUnitToItemID(itemIds[i]).GetComponent<PlaceMonster>().UpdateGranted();
        }
        callback();
    }


}

