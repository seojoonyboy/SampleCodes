using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.UI.Extensions;
using System;
using SkillModules;
using System.Linq;
using System.Text;
using UnityEngine.Events;

public partial class MagicDragHandler : CardHandler, IBeginDragHandler, IDragHandler, IEndDragHandler {
    public bool isPlayer;

    public void OnBeginDrag(PointerEventData eventData) {
        if (heroCardActivate) {
            if(PlayMangement.instance.isTutorial == true) { 
                if(gameObject.transform.Find("drag") != null) 
                    gameObject.transform.Find("drag").gameObject.SetActive(false);
            }
            ShowCardsHandler showCardsHandler = GetComponentInParent<ShowCardsHandler>();
            showCardsHandler.Selecting(gameObject);

            heroCardInfo.SetActive(false);
            transform.localScale = Vector3.zero;
            if (cardData.skills != null)
                CardInfoOnDrag.instance.SetCardDragInfo(null, mouseLocalPos.localPosition, cardData.skills.desc);
            else
                CardInfoOnDrag.instance.SetCardDragInfo(null, mouseLocalPos.localPosition);
            CardDropManager.Instance.ShowMagicalSlot(cardData.targets, cardData.type);
            object[] parms1 = new object[] { true, gameObject };
            PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.BEGIN_CARD_PLAY, this, parms1);
            return;
        }
        if (!PlayMangement.dragable) return;
        if (firstDraw || PlayMangement.instance.isMulligan) return;
        if (Input.touchCount > 1) return;
        if (PlayMangement.instance.player.dragCard) return;
        if (ScenarioGameManagment.scenarioInstance != null) {
            ScenarioMask.Instance.OffDeckCardGlow();
            ScenarioMask.Instance.StopEveryHighlight();
        }


        StartDragCard();
        if (cardData.skills != null)
            CardInfoOnDrag.instance.SetCardDragInfo(null, mouseLocalPos.localPosition, cardData.skills.desc);
        else
            CardInfoOnDrag.instance.SetCardDragInfo(null, mouseLocalPos.localPosition);
        itsDragging = gameObject;
        blockButton = PlayMangement.instance.player.dragCard = true;
        PlayMangement.instance.player.isPicking.Value = true;

        CardDropManager.Instance.ShowMagicalSlot(cardData.targets, cardData.type);

        object[] parms = new object[] { true, gameObject };
        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.BEGIN_CARD_PLAY, this, parms);
    }

    public void OnDrag(PointerEventData eventData) {
        if (heroCardActivate) {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos = new Vector3(mousePos.x, mousePos.y, 0);
            transform.position = mousePos;
            mouseLocalPos.position = transform.position;
            CheckLocation();
            CardInfoOnDrag.instance.SetInfoPosOnDrag(mouseLocalPos.localPosition);
            CheckMagicHighlight();
            return;
        }
        if (!PlayMangement.dragable) {
            eventData.pointerDrag = null;
            eventData.dragging = false;
            OnEndDrag(null);
            return;
        }


        if (firstDraw) return;
        if (Input.touchCount > 1) return;
        if (gameObject != itsDragging) return;
        OnDragCard();
        CheckLocation();
        CardInfoOnDrag.instance.SetInfoPosOnDrag(mouseLocalPos.localPosition);
        CheckMagicHighlight();
    }

    public void ForceToHandHeroCards() {
        ShowCardsHandler showCardsHandler = GetComponentInParent<ShowCardsHandler>();
        if (PlayMangement.instance.isTutorial == true && ScenarioGameManagment.scenarioInstance.canHeroCardToHand == false) {
            cardUsed = false;
            transform.localScale = new Vector3(1, 1, 1);
            transform.localPosition = new Vector3(0, 0, 0);
            transform.parent.parent.Find("HeroCardGuide").gameObject.SetActive(true);

            transform.Find("CardInfoWindow").gameObject.SetActive(false);
            showCardsHandler.CancelSelecting();
        }
        else {
            showCardsHandler.FinishPlay(gameObject, true);
            handManager.AddHeroCard(gameObject);
            heroCardActivate = false;
        }
    }


    public void OnEndDrag(PointerEventData eventData) {
        EffectSystem.Instance.HideEveryDim();
        if (heroCardActivate) {
            HeroCard();
            return;
        }

        if (firstDraw) return;
        if (gameObject != itsDragging) return;
        CheckLocation(true);
        blockButton = PlayMangement.instance.player.dragCard = false;
        cardUsed = false;
        PlayMangement.instance.player.isPicking.Value = false;
        if (isDropable == true && CheckMagicSlot() != null && (PlayMangement.instance.player.resource.Value >= cardData.cost || PlayMangement.instance.cheatFreeCard) && turnMachine.isPlayerTurn()) {
            cardUsed = true;
            gameObject.AddComponent<CardUseSendSocket>().Init();
        }
        else {
            highlighted = false;
            CardDropManager.Instance.HighLightMagicSlot(highlightedSlot, highlighted);
            highlightedSlot = null;
        }
        handManager.transform.SetParent(mouseXPos.parent);

        if (!cardUsed) {            
            if (PlayMangement.instance.isTutorial == true)
                SendEvent();
        }

        BackToDeckCard();
        CardDropManager.Instance.HideMagicSlot();
        CardInfoOnDrag.instance.OffCardDragInfo();
        PlayMangement.instance.infoOn = false;
    }

    void SendEvent() {
        //튜토리얼에서 drop 이 실패하여 다시 핸드로 돌아온 경우 튜토리얼 재호출 처리
        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.UNIT_DROP_FAIL, this);
    }

    //IEnumerator UseSkillCardExceptInfo(object[] parms) {
    //    PlayMangement.instance.LockTurnOver();
    //    yield return EffectSystem.Instance.HeroCutScene(PlayMangement.instance.player.heroID);
    //    PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.END_CARD_PLAY, this, parms);
    //    highlighted = false;
    //    CardDropManager.Instance.HighLightMagicSlot(highlightedSlot, highlighted);
    //    highlightedSlot = null;
    //}

    public void StartCardUse(object args, DequeueCallback callback) {
        gameObject.SetActive(true);
        transform.Find("GlowEffect").gameObject.SetActive(false);
        if (!PlayMangement.instance.cheatFreeCard) PlayMangement.instance.player.resource.Value -= cardData.cost;
        bool isHuman = PlayMangement.instance.player.isHuman;

        if (isHuman && PlayMangement.instance.currentTurn == TurnType.HUMAN) 
            PlayMangement.instance.player.ActivePlayer();        
        else if(isHuman == false && PlayMangement.instance.currentTurn == TurnType.ORC || PlayMangement.instance.currentTurn == TurnType.SECRET)
            PlayMangement.instance.player.ActiveOrcTurn();


        SoundManager.Instance.PlaySound(UISfxSound.CARDCHOICE_UNIT);
        object[] parms = new object[] { true, gameObject };
        StartCoroutine(UseSkillCard(parms, args ,callback));
    }

    IEnumerator UseSkillCard(object[] parms, object args, DequeueCallback callback) {
        int cardNum = transform.parent.GetSiblingIndex();
        PlayMangement.dragable = false;
        PlayMangement.instance.LockTurnOver();
        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.MAGIC_USED, this, cardData.id);
        yield return PlayMangement.instance.cardHandManager.ShowUsedCard(cardNum, gameObject);
        if (cardData.isHeroCard == true) {
            HideCardImage();
            yield return EffectSystem.Instance.HeroCutScene(PlayMangement.instance.player.heroID);            
        }
        PlayMangement.instance.cardActivate.Activate(cardData.id, args, callback);
        SoundManager.Instance.PlayMagicSound(cardData.id);
        highlighted = false;
        CardDropManager.Instance.HighLightMagicSlot(highlightedSlot, highlighted);       
        highlightedSlot = null;        
        ShowCardsHandler showCardsHandler = transform.root.GetComponentInChildren<ShowCardsHandler>();

        if (showCardsHandler.CheckShieldTurnCard(gameObject) == false)
            PlayMangement.instance.player.cdpm.DestroyCard(cardNum);
        showCardsHandler.FinishPlay(gameObject);
        handManager.SortHandPosition();        
        PlayMangement.instance.UnlockTurnOver();
        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.END_CARD_PLAY, this, parms);
        PlayMangement.dragable = true;
        //GetComponentInParent<ShowCardsHandler>().RemoveCard(gameObject);
    }


    IEnumerator UseSkillCard(object[] parms) {
        PlayMangement.dragable = false;
        PlayMangement.instance.LockTurnOver();
        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.MAGIC_USED, this, cardData.id);
        yield return PlayMangement.instance.cardHandManager.ShowUsedCard(transform.parent.GetSiblingIndex(), gameObject);
        if (cardData.isHeroCard == true) {
            HideCardImage();
            yield return EffectSystem.Instance.HeroCutScene(PlayMangement.instance.player.heroID);
        }

        gameObject.AddComponent<CardUseSendSocket>().Init();
        PlayMangement.instance.EventHandler.PostNotification(IngameEventHandler.EVENT_TYPE.END_CARD_PLAY, this, parms);
        SoundManager.Instance.PlayMagicSound(cardData.id);
        highlighted = false;
        CardDropManager.Instance.HighLightMagicSlot(highlightedSlot, highlighted);
        highlightedSlot = null;
        ShowCardsHandler showCardsHandler = transform.root.GetComponentInChildren<ShowCardsHandler>();
        showCardsHandler.FinishPlay(gameObject);
        CardInfoOnDrag.instance.OffCardDragInfo();
        //GetComponentInParent<ShowCardsHandler>().RemoveCard(gameObject);
    }


    public override void OnTurnChanged(Enum Event_Type, Component Sender, object Param) {
        if (!isMyTurn) {
            //영웅 마법 카드 선택단계인경우 둘중 무작위 한장 선택하여 핸드로 가져오기
            if (heroCardActivate) {
                OnEndDrag(null);

                PlayMangement.instance.showCardsHandler.TimeoutShowCards();
            }
        }
    }

    private void HideCardImage() {
        transform.Find("GlowEffect").gameObject.SetActive(false);
        transform.Find("Portrait").gameObject.SetActive(false);
        transform.Find("BackGround").gameObject.SetActive(false);
        transform.Find("Cost").gameObject.SetActive(false);
    }


    private void HeroCard() {
        ShowCardsHandler showCardsHandler = GetComponentInParent<ShowCardsHandler>();
        bool pass = false;
        heroCardInfo.SetActive(true);
        //영웅 카드를 핸드로 가져오는 부분
        if (transform.position.y < -3.5f) {
            if (ScenarioGameManagment.scenarioInstance != null && ScenarioGameManagment.scenarioInstance.canHeroCardToHand == false) {
                pass = true;
                BackToDeckCard();
                //if (heroCardActivate) {
                //    transform.parent.parent.Find("HeroCardGuide").gameObject.SetActive(true);
                //}
                transform.Find("CardInfoWindow").gameObject.SetActive(false);
                showCardsHandler.CancelSelecting();
                //gameObject.transform.Find("drag").gameObject.SetActive(true);
                SendEvent();
            }
            else {
                if (ScenarioGameManagment.scenarioInstance == null) {
                    IngameTimer timer = isPlayer ? PlayMangement.instance.player.GetComponent<IngameTimer>() : PlayMangement.instance.enemyPlayer.GetComponent<IngameTimer>();
                    timer.OnTimeout.RemoveListener(PlayMangement.instance.showCardsHandler.TimeoutShowCards);
                    timer.EndTimer();
                }

                ForceToHandHeroCards();
            }

        }
        else {
            CheckLocation(true);
            cardUsed = false;
            //영웅 실드 발동시 나온 카드를 사용 할 때만 여기로 들어옴
            if (CheckMagicSlot() != null) {
                cardUsed = true;
                GameObject oppositeCard = showCardsHandler
                    .GetOppositeCard(gameObject);

                oppositeCard
                    .transform
                    .localPosition = new Vector3(4000f, 0);
                //var abilities = GetComponents<MagicalCasting>();
                //foreach (MagicalCasting ability in abilities) ability.RequestUseMagic();
                object[] parms = new object[] { true, gameObject };
                transform.Find("GlowEffect").gameObject.SetActive(false);
                transform.Find("CardInfoWindow").gameObject.SetActive(false);
                showCardsHandler.hideShowBtn.SetActive(false);
                SoundManager.Instance.PlaySound(UISfxSound.CARDCHOICE_HERO);
                
                gameObject.AddComponent<CardUseSendSocket>().Init();
                oppositeCard.GetComponent<CardHandler>().heroCardActivate = false;
                showCardsHandler.RemoveCard(oppositeCard);
                PlayMangement.dragable = false;
                //StartCoroutine(UseSkillCard(parms));

                if (ScenarioGameManagment.scenarioInstance == null) {
                    IngameTimer timer = isPlayer ? PlayMangement.instance.player.GetComponent<IngameTimer>() : PlayMangement.instance.enemyPlayer.GetComponent<IngameTimer>();
                    timer.OnTimeout.RemoveListener(PlayMangement.instance.showCardsHandler.TimeoutShowCards);
                    timer.EndTimer();
                }
            }
            else {
                highlighted = false;
                CardDropManager.Instance.HighLightMagicSlot(highlightedSlot, highlighted);
                highlightedSlot = null;
            }



            if (cardUsed == false) {
                Invoke("SendEvent", 0.3f);
                BackToDeckCard();
                transform.Find("CardInfoWindow").gameObject.SetActive(false);
                showCardsHandler.CancelSelecting();
                if (PlayMangement.instance.isTutorial == true) {
                    if (gameObject.transform.Find("drag") != null)
                        gameObject.transform.Find("drag").gameObject.SetActive(true);
                }
            }
        }
        CardDropManager.Instance.HideMagicSlot();
        CardInfoOnDrag.instance.OffCardDragInfo();
        if (pass == false)
            PlayMangement.instance.player.ConsumeShieldStack();
    }



}