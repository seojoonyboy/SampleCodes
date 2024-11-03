스코어 모드, 일반 스테이지 모드에 따라 서로 다른 하위 클래스를 활용
> 상위 클래스 BaseIngameCardSkillController에서 기본 카드 클릭에 대한 처리를 하고
> 스코어 모드인 경우 IngameRotationCardSkillController에서 카드 스킬을 게이지가 찰 때마다 자동 발동 되면서 카드를 순환시키는 처리를 구현   

![image](https://github.com/user-attachments/assets/9b09ca53-a03e-4db9-adae-ad3b7e41f85e)

카드 클릭시 최초 호출
> 스코어모드 여부에 따라 다른 Controller에 접근합니다.

<pre>
  <code>
    private void OnClickCard(PuzzleCard.IngameParams cardParam)
    {
        // 정지상태
        if(!InGameController.isScoreModeSelected)
            BaseController.stageController.isSkillPlaying = true;
        
        if(this.isWorkCardSkill) return;        //카드 스킬 연속 클릭 방지

        this.canShowGaugeEffect = false;        //스킬게이지 이펙트를 비활성화 시킨다.
        this.isWorkCardSkill = true;
        //빈카드 사용에 대한 처리
        if (cardParam == null)
        {
            this.isWorkCardSkill = false;
            this.canShowGaugeEffect = true;

            return;
        }

        if (!isScoreModeSelected)
        {
            if (cardParam.cardSkillCode.HasValue)
            {
                this.normalCardSkillController.OnClick(
                    cardParam.cardIndex,
                    cardParam.cardSkillCode.Value,
                    cardParam.cardSkillSetSon,
                    cardParam.cardTransform,
                    cardParam.blockGlowPrefab,
                    cardParam.skillBlockTexture,
                    cardParam.skillBlockBeforeRemoveEffectTexture,
                    InGameController.isScoreModeSelected
                );
                this.isWorkCardSkill = false;
                this.canShowGaugeEffect = true;
            }
        }
        else
        {
            this.SkillActiveAnim(
                () =>
                {
                    if (cardParam.cardSkillCode.HasValue)
                    {
                        this.stage.skillFever = 0;
                        
                        this.rotationCardSkillController.OnClickCard(
                            cardParam.cardSkillCode.Value,
                            cardParam.cardSkillSetSon,
                            cardParam.cardTransform,
                            cardParam.blockGlowPrefab,
                            cardParam.skillBlockTexture,
                            cardParam.skillBlockBeforeRemoveEffectTexture,
                            InGameController.isScoreModeSelected
                        );
                        this.isWorkCardSkill = false;
                        this.canShowGaugeEffect = true;
                        this.isSoundInvoked = false;
                    }
                }
            );
        }
    }
  </code>
</pre>

스코어 모드의 카드 스킬 발동 처리
<pre>
  <code>
    private void OnClickCard(GameObject cardObj, PuzzleCard.IngameParams obj)
    {
        CommonProcessController.PlayVoice(AudioController.VoiceBundle.UseCardSkill);
        
        int cardIndex = -100;
        int.TryParse(cardObj.name, out cardIndex);
        if (cardIndex != this.cardDeck[0])
        {
            SBDebug.Log("첫번째 카드 클릭이 아님");
            return;
        }
        
        if (obj != null)
        {
            obj.ingameController.isSoundInvoked = false;
            if(!obj.ingameController.CanSkillActive) return;
        }

        this.onClickCard?.Invoke(obj);
        this.RotateLeft();
    }
  </code>
</pre>

카드 사용 이후 순환 처리
<pre>
  <code>
    private IEnumerator ShiftTransform(int prevFirstCardID)
        {
            yield return DisappearFrontCard(prevFirstCardID);
            
            for (int i = 0; i < this.cardDeck.Length; i++)
            {
                var targetCardObj = this.cardObjs.ToList().Find(x => x.name == this.cardDeck[i].ToString());
                if (targetCardObj != null)
                {
                    targetCardObj.gameObject.SetActive(true);

                    var cardView = targetCardObj.GetComponent<PuzzleCard>();
                    
                    if (i == 0)
                    {
                        cardView.IsUsed = false;
                        
                        targetCardObj.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                        targetCardObj.transform.SetParent(this.frontArea.GetChild(0));
                        float val = targetCardObj.transform.localPosition.x;
                        DOTween
                            .To(
                                () => val, 
                                x => val = x, 
                                0, 
                                0.2f)
                            .OnUpdate(() =>
                            {
                                targetCardObj.transform.localPosition = new Vector3(val, 0, 0);
                            });
                    }
                    else
                    {
                        cardView.IsUsed = true;
                        
                        targetCardObj.transform.localScale = new Vector3(1.24f, 1.24f, 1.24f);
                        targetCardObj.transform.SetParent(
                            this.deckArea
                                .Find((i - 1).ToString()
                            )
                        );
                        if (this.cardDeck[i] == prevFirstCardID)
                        {
                            targetCardObj.transform.localPosition = new Vector3(200, 0, 0);
                        }
                        
                        float val = targetCardObj.transform.localPosition.x;
                        DOTween
                            .To(
                                () => val, 
                                x => val = x, 
                                0, 
                                0.2f)
                            .OnUpdate(() =>
                            {
                                targetCardObj.transform.localPosition = new Vector3(val, 0, 0);
                            });
                    }
                }

                yield return new WaitForSeconds(0.02f);
            }
        }
  </code>
</pre>
