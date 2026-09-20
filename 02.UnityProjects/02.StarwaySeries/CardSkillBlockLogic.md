카드 스킬 블록 — 발동 트리거가 달라도 하나의 파이프라인으로
=======================================================
> STARWAY 의 카드는 카드마다 고유한 스킬을 갖는다. 스킬을 쓰면 판 위의 일반 블록 하나가 **카드 스킬 블록(또는 로켓·폭탄 같은 특수 블록)으로 바뀌고**, 그 블록을 터치하면 스킬 효과가 터진다.
> 그런데 스킬은 **발동 트리거가 다르고**(수동 클릭 / 스코어 모드 자동 발동 / 이어하기 보상 / 부스터 아이템) **적용 대상 셀도 다르다.**
> 스킬 종류가 늘어날 때마다 로직을 새로 짜지 않도록, **일반 블록 셀 탐색 → 기존 블록 폭파 → 신규 특수 블록 생성/이펙트**라는 하나의 공통 골격 위에 여러 스킬을 얹는 구조로 되어 있다.
>
> 스킬의 흐름은 두 단계로 나뉜다. **① 스킬 블록 배치**(이 문서의 `CardSkillController`) → **② 스킬 블록 발동**(터치했을 때 `StageController` 가 `CardSkillMatch.Analyse3` 로 대상 셀을 구해 터뜨린다, 문서 하단 참고).

샘플 코드: [`04.CardSkill/CardSkillController.cs`](./04.CardSkill/CardSkillController.cs)

![image](https://github.com/user-attachments/assets/9b09ca53-a03e-4db9-adae-ad3b7e41f85e)

*스킬 적용 대상이 될 정상 블록 셀 탐색*
> 스테이지 전체를 순회하며 "살아있는 셀 + 일반 블록 + 고정 상태"인 셀만 후보로 모은다.
> 이 목록은 카드 스킬뿐 아니라 부스터 아이템, 이어하기 보상에서도 동일하게 재사용된다.

```csharp
    // 일반블록을 가진 셀을 모두 얻고
    private List<Cell> getNormalCells()
    {
        List<Cell> cells = new List<Cell>();
        // 일반블록을 가진 셀을 모두 얻고
        for (int r = 0; r < this.stage.rowCount; r++)
            for (int c = 0; c < this.stage.colCount; c++)
                if (null != this.stage.cells[r, c].block &&
                    CellType.Alive == this.stage.cells[r, c].type &&
                    this.stage.cells[r, c].block.IsNormal &&
                    BlockState.Fixed == this.stage.cells[r, c].block.state)
                    cells.Add(this.stage.cells[r, c]);
        return cells;
    }
```

*카드 스킬 발동 진입점 — 수동 클릭 vs 스코어 모드 자동 발동*
> `OnClickCardSkill`은 UI에서 카드를 직접 클릭했을 때 호출되며, 카드 발사 이펙트(꼬리 이펙트, 글로우)를
> 함께 넘긴다. 반면 `OnClickCardSkillForAutoplay`는 스코어 모드에서 피버 게이지가 찼을 때 자동으로
> 호출되는 경로로, 진입 시점에 `this.stage.skillFever = 0`으로 게이지를 즉시 초기화한다. 두 경로 모두
> 최종적으로는 같은 `Analyse()` → `AttackCardSkill()` → `SetCardSkillBlock()` 파이프라인으로 합류한다 —
> "무엇이 스킬을 발동시켰는가"만 다르고 "발동 이후 처리"는 하나로 공유하는 구조다.

```csharp
    public void OnClickCardSkill(
        int cardSkillCode, 
        CardSkillSet cardSkillSet, 
        Transform cardPos, 
        GameObject tailPrefab, 
        GameObject glowPrefab,
        Texture2D skillBlockTexture,
        Texture2D skillBlockBeforeRemoveEffectTexture)
    {
        var result = this.Analyse(cardSkillCode);
        
        AttackCardSkillArgs args = new AttackCardSkillArgs();
        args.match = result;
        args.cardSkillCode = cardSkillCode;
        args.cardSkillSet = cardSkillSet;
        
        args.skillBlockTexture = skillBlockTexture;
        args.skillBlockBeforeRemoveEffectTexture = skillBlockBeforeRemoveEffectTexture;
        
        args.tf = cardPos;
        args.glowPrefab = glowPrefab;
        args.tailPrefab = tailPrefab;
        StartCoroutine(this.AttackCardSkill(args));
    }
    
    public void OnClickCardSkill(int cardSkillCode)
    {
        var result = this.Analyse(cardSkillCode);
        // TODO: 여기에 카드스킬 소모되었음을 넣어줘야 한다.
        // NOTE: 카드스킬은 턴을 소모하지 않는다.
        // 셀내에 있는 블록을 어덱차감 적용한다.
        AttackCardSkillArgs args = new AttackCardSkillArgs();
        args.match = result;
        StartCoroutine(this.AttackCardSkill(args));
    }

    public void OnClickCardSkillForAutoplay(int type)
    {
        var result = this.Analyse(type, 1);
        // TODO: 여기에 카드스킬 소모되었음을 넣어줘야 한다.
        this.stage.skillFever = 0;
        // NOTE: 카드스킬은 턴을 소모하지 않는다.
        // 셀내에 있는 블록을 어덱차감 적용한다.
        AttackCardSkillArgs args = new AttackCardSkillArgs();
        args.match = result;
        StartCoroutine(this.AttackCardSkill(args));
    }
```

> 위 세 진입점 중 **카드 발사 이펙트 정보를 함께 넘기는 것은 첫 번째(전체 인자) 오버로드뿐**이다. 나머지 두 개(`OnClickCardSkill(int)`, `OnClickCardSkillForAutoplay`)는 `args.tf`, `args.tailPrefab`,
> `args.glowPrefab` 을 채우지 않은 채 `args != null` 로 넘기므로, 이 파일 기준으로는 `SetCardSkillBlock` 의 `args.tf.gameObject` 에서 `NullReferenceException` 이 날 수 있는 형태다.
> 호출부가 샘플에 없어 두 경로가 실제로 쓰였는지는 확인하지 못했다(아래 "한계와 개선 방향").
> 또한 진입점마다 `Analyse()` 로 `CardSkillMatchResult` 를 구하지만, `SetCardSkillBlock` 은 이 결과(`args.match`)를 쓰지 않고 `getNormalCells()` 에서 무작위로 셀을 고른다.
>
> 코드 곳곳에 남아있는 `// NOTE: 카드스킬은 턴을 소모하지 않는다` 주석은 실제 기획 규칙을 그대로 코드
> 옆에 남겨둔 흔적이다 — 카드 스킬은 퍼즐의 "턴"을 소모하는 일반 조작과는 다른 규칙을 갖고 있음을
> 구현 단계에서부터 명시하고 있다.

*발동 이후 공통 처리 — 셀 폭파와 특수 블록 생성*
> `SetCardSkillBlock`은 대상 셀을 무작위로 하나 뽑아 기존 블록을 터뜨리고,
> `Block.FactorySpecial(type)`으로 새 특수 블록을 만들어 그 자리에 채워 넣는다. `args`가 있으면 카드에서 셀까지 날아가는
> 꼬리 이펙트를 먼저 재생하고, 이펙트 이동이 끝난 뒤에 블록을 교체한다는 순서가 중요하다(**연출이 끝난 뒤 데이터를 바꾼다**).
> `args == null` 로 호출되는 경로는 테스트 버튼(`OnClickTEST`)뿐이고, 이 경우 이펙트와 카드 등급 정보 없이 블록만 교체한다.

```csharp
    private IEnumerator SetCardSkillBlock(BlockType type, AttackCardSkillArgs args)
    {
        List<Cell> cells = this.getNormalCells();
        StageController sc = stageController;
        if (0 < cells.Count) {
            // 랜덤으로 셀을 하나 얻고
            Cell cell = cells[Random.Next(0, cells.Count)];
            Block oldBlock = cell.block;

            oldBlock.state = BlockState.Floating;
            try {
                // 기존 블록을 폭파시키고
                BlockController controller = sc.FindBlockController(oldBlock);
                
                // TODO: 카드스킬 소요됨을 메모리에 넣는다.
                if (args != null)
                {
                    bool isFinishedMove = false;
                    IngameCardSkillEffectView.Params skillEffectParams = new IngameCardSkillEffectView.Params();
                    skillEffectParams.tailPrefab = args.tailPrefab;
                    
                    Vector2 targetPos = GetPositionByMatrix(cell.row, cell.col);
                    skillEffectParams.moveTo = new Vector3(targetPos.x, targetPos.y, 0);
                    
                    skillEffectParams.from = args.tf.gameObject.transform.position;
                    skillEffectParams.parent = sc.transform;
                    
                    GameObject effectPrefab = Resources.Load("Prefabs/Game/Scene/Ingame/Effect") as GameObject;
                    GameObject effectObj = Instantiate(effectPrefab);
                    IngameCardSkillEffectView effectView = effectObj.GetComponent<IngameCardSkillEffectView>();
                    
                    effectView.GetComponent<IngameCardSkillEffectView>()
                        .OnMove(skillEffectParams, () =>
                        {
                            isFinishedMove = true;
                            Destroy(effectObj);
                        });
                    yield return new WaitUntil(() => isFinishedMove);
                }
                
                yield return StartCoroutine(controller.Explode());
                cell.block = null;

                // 카드 스킬 블록을 생성한 후.
                Block newBlock = Block.FactorySpecial(type);

                // 셀에 넣어준다.
                newBlock.skillBlockTexture = args?.skillBlockTexture;
                newBlock.skillBlockBeforeRemoveEffectTexture = args?.skillBlockBeforeRemoveEffectTexture;

                var newBlockController = BlockController.Create(newBlock, cell.row, cell.col);
                if (args != null)
                {
                    GameObject glowEffectObj = Instantiate(args.glowPrefab, newBlockController.transform.GetChild(0).transform);
                    glowEffectObj.name = "glowEffectObj";
                    // SelfDestroy selfDestroy = glowEffectObj.AddComponent<SelfDestroy>();
                    // selfDestroy.interval = 3.0f;

                    newBlock.cardGrade = args.cardSkillSet.GradeType;
                    newBlock.cardSkillCode = args.cardSkillCode;
                }
                
                this.stage.AddBlock(cell, newBlock);

            } finally {
                oldBlock.state = BlockState.Fixed;
            }
        }
    }
```

> `newBlock.cardGrade = args.cardSkillSet.GradeType;` 한 줄이 핵심이다. 생성되는 블록의 등급은 코드에
> 하드코딩된 값이 아니라 `CardSkillSet`이라는 데이터 시트 레코드에서 그대로 옮겨온다. 즉 카드 스킬을
> 새로 추가할 때 이 함수 자체는 손댈 필요가 없고, 데이터 시트에 등급/코드 값을 채워 넣는 것만으로 새
> 스킬이 같은 파이프라인을 타게 된다.

*여러 이펙트가 동시에 끝나기를 기다리는 처리*
> `async/await`가 아닌 코루틴 기반 코드라, 여러 개의 셀을 동시에 타격하는 이펙트가 모두 끝날 때까지
> 기다리는 로직을 `bool[]` 배열로 직접 구현하고 있다.

```csharp
    IEnumerator TailEffectTask(AttackCardSkillArgs args)
    {
        StageController sc = stageController;

        bool[] finishedList = new bool[args.match.cells.Count];
        int index = 0;
        foreach (Cell cell in args.match.cells)
        {
            finishedList[index] = false;
            
            Vector2 targetPos = GetPositionByMatrix(cell.row, cell.col);
            var tmpIndex = index;

            GameObject effectPrefab = Resources.Load("Prefabs/Game/Scene/Ingame/Effect") as GameObject;
            GameObject effectObj = Instantiate(effectPrefab);
            IngameCardSkillEffectView effectView = effectObj.GetComponent<IngameCardSkillEffectView>();
            
            IngameCardSkillEffectView.Params skillEffectParams = new IngameCardSkillEffectView.Params();
            skillEffectParams.tailPrefab = args.tailPrefab;
            skillEffectParams.moveTo = new Vector3(targetPos.x, targetPos.y, 0);
            skillEffectParams.from = args.tf.gameObject.transform.position;
            skillEffectParams.parent = sc.transform;
            
            effectView.GetComponent<IngameCardSkillEffectView>()
                .OnMove(skillEffectParams, () =>
                {
                    finishedList[tmpIndex] = true;
                    Destroy(effectObj);
                });
            index++;
        }

        bool isAllFinished = false;
        while (!isAllFinished)
        {
            var isNotFinishedExist = finishedList.ToList().Exists(x => x == false);
            if (!isNotFinishedExist)
                isAllFinished = true;
            
            yield return new WaitForEndOfFrame();
        }

        yield return new WaitUntil(() => isAllFinished);
    }
```

> 이 함수(`TailEffectTask`)는 **이 파일 안에서는 호출되지 않는다.** 여러 셀을 동시에 타격하는 스킬을 위한 fan-out/join 코드로 남아있는 것이고, 현재 배치 경로(`SetCardSkillBlock`)는 셀 하나에만 이펙트를 보낸다.
>
> 콜백이 올 때마다 인덱스별 `bool`을 켜고, 매 프레임 "아직 안 끝난 항목이 있는지"를 검사하는 방식으로
> 사실상의 fan-out/join을 코루틴만으로 구현했다. `System.Random`을 필드로 하나 두고 재사용하는 것도
> (`private System.Random Random = new System.Random();`) 호출마다 새로 생성하지 않기 위한 선택이다.

*같은 폭파-재생성 패턴의 재사용 — 부스터 아이템과 이어하기 보상*
> 카드 스킬 블록뿐 아니라, 게임 실패 후 "이어하기"를 선택했을 때 지급되는 보너스 특수 블록(`SetContinueItem`)과
> 상점에서 구매한 부스터 아이템(`SetBoosterItem`, 코드 생략)도 "셀을 고르고 → 기존 블록을 `Explode()`로
> 지우고 → `Block.FactorySpecial(type)`로 새 블록을 만들어 넣는다"는 동일한 골격을 그대로 재사용한다.

```csharp
    public void OnClickContinue(int retryCount)
    {
        // 일반블록 전체 목록을 얻고
        List<Cell> normalCells = this.getNormalCells();

        // 추가할 턴수는 5개, 일반블록 중에 하나를 종이비행기로 교환
        int appendTurnCount = 0;
        BlockType[] types = new BlockType[] { BlockType.None, BlockType.None, BlockType.None };
        Cell[] cells = new Cell[3] { null, null, null };

        int tmpIndex;
        switch (retryCount) {
            case 1:
                appendTurnCount = 5;
                break;
            case 2:
                appendTurnCount = 5;
                if (0 < normalCells.Count) {
                    tmpIndex = Random.Next(0, normalCells.Count);
                    types[1] = BlockType.Rocket;
                    cells[1] = normalCells[Random.Next(0, normalCells.Count)];
                    normalCells.RemoveAt(tmpIndex);
                }
                break;
            case 3:
                appendTurnCount = 5;
                // ... 중략: case 2와 동일한 방식으로 PaperPlane, Bomb, Mirrorball 세 종류를 배분 ...
                break;
        }

        // 추가 턴을 넣어주고
        this.stage.totalTurn += appendTurnCount;
        stageController.UpdateDashboard();

        for (int i = 0; i < 3; i++)
            if (BlockType.None != types[i])
                StartCoroutine(this.SetContinueItem(types[i], cells[i]));
    }

    private IEnumerator SetContinueItem(BlockType type, Cell cell)
    {
        StageController sc = stageController;
        Block oldBlock = cell.block;
        oldBlock.state = BlockState.Floating;
        try {
            // 기존 블록을 폭파시키고
            BlockController controller = sc.FindBlockController(oldBlock);
            yield return StartCoroutine(controller.Explode());
            cell.block = null;

            // 새 블록을 하나 메모리에 생성 후
            Block newBlock = Block.FactorySpecial(type);

            // 셀에 넣어준다.
            BlockController.Create(newBlock, cell.row, cell.col);
            this.stage.AddBlock(cell, newBlock);
        } finally {
            oldBlock.state = BlockState.Fixed;
        }
    }
```

> 위 발췌의 `case 2` 에는 **버그가 있다.** `tmpIndex` 로 뽑은 셀을 지우면서(`RemoveAt(tmpIndex)`) 정작 `cells[1]` 에는 `Random.Next(...)` 를 한 번 더 호출한 값을 넣는다.
> 그래서 지운 셀과 실제로 쓰는 셀이 달라, 같은 셀이 두 번 선택될 수 있다(생략한 `case 3` 의 미러볼 배정도 같은 형태다). 후보 셀이 3개 미만일 때는 `case 3` 에서 빈 리스트를 인덱싱할 수도 있다.
> `cells[i] = normalCells[tmpIndex]` 로 통일하고 후보 수를 검사하는 것이 맞다.
>
> `retryCount`(이어하기 재시도 횟수)가 높을수록 보상으로 지급되는 특수 블록 종류가 늘어나는 것을 볼 수
> 있다(1회차: 턴만 추가, 2회차: 로켓 1개 추가, 3회차: 종이비행기/폭탄/미러볼 3종 추가) — 재시도를 여러
> 번 할수록 다음 시도가 더 유리해지도록 보상 곡선이 설계되어 있다.

*② 배치된 스킬 블록이 터지는 곳 — `StageController` (참고)*
> 스킬 블록을 터치하면 `StageController` 의 특수 블록 처리 분기(`case BlockType.CardSkill`)가 실행된다. 여기서 `CardSkillMatch.Analyse3(me)` 로 **스킬이 영향을 줄 셀 목록**을 구하고,
> 제거 직전 깜빡임 이펙트(0.8초) → 카드 사용 이벤트 방송(`"com.snowballs.UseSkillCard"`, 카드가 회색으로 바뀌는 UI 갱신용) → 셀별 폭발 이펙트 → 블록 제거 순서로 진행한다.

```csharp
case BlockType.CardSkill: {
    CardSkillMatch match2 = new CardSkillMatch(this.stage, excludedBlocks);
    CardSkillMatchResult result2 = match2.Analyse3(me);       // 영향 셀 목록
    _AttackBottomBlock(me);
    _AddLock(result2.cells);                                   // 처리 중인 셀 잠금
    _beginLock();
    try {
        // 제거 직전 깜빡이 효과 → await UniTask.Delay(0.8초)
        BroadcastTunnel<string, int>.Notify("com.snowballs.UseSkillCard", me.block.skillCardIndex.Value);
        // 셀별 폭발 이펙트, 스킬 블록 제거 ...
    } finally { _UnlockAll(); }
}
```
> 즉 **"어디에 스킬 블록을 놓을지"(`CardSkillController`)와 "스킬이 어떤 셀을 터뜨리는지"(`CardSkillMatch` + `StageController`)가 분리**되어 있다. 카드 스킬 종류를 늘리는 작업은 대부분 후자(대상 셀 계산)에서 일어나고,
> 이 문서의 배치 파이프라인은 그대로 재사용된다. (`Analyse3` 의 내부는 이 샘플 범위에 포함하지 않았다.)

설계 포인트
------------
> `getNormalCells → Explode → FactorySpecial → AddBlock`이라는 동일한 골격이 `SetCardSkillBlock`,
> `SetBoosterItem`, `SetContinueItem` 세 곳에서 반복된다. `SetCardSkillBlock`만 이펙트/카드 등급 정보를
> 추가로 다루는 상위 호환 버전이라, 실제로는 세 함수를 하나의 파라미터 객체로 합쳐 완전히 통합할 수
> 있는 여지가 남아 있다 — 여러 종류의 카드 스킬/보너스 블록을 겪으며 구조가 점진적으로 공통화되어 온
> 과정이 코드에 그대로 드러난다.   
> 발동 트리거는 수동 클릭(`OnClickCardSkill`)과 스코어 모드의 자동 발동(`OnClickCardSkillForAutoplay`,
> 피버 게이지 초기화 포함)으로 나뉘지만, 트리거 이후의 처리는 `AttackCardSkillArgs` 하나로 캡슐화해
> 같은 코루틴에 흘려보낸다. 즉 "무엇이 스킬을 트리거했는가"와 "스킬 발동 결과를 어떻게 반영하는가"를
> 분리해, 새로운 발동 조건(예: 신규 모드)이 추가돼도 결과 처리 로직은 그대로 재사용할 수 있다.

---

한계와 개선 방향
------------------
> * **같은 골격이 세 번 복사되어 있다.** `SetCardSkillBlock`, `SetBoosterItem`, `SetContinueItem` 이 "셀 선택 → `Explode` → `FactorySpecial` → `AddBlock`" 을 각자 구현한다. `args`(이펙트/카드 정보)를 옵션으로 받는
>   하나의 `ReplaceBlock(cell, type, options)` 로 합칠 수 있다.
> * **`OnClickContinue` 의 셀 배정 버그.** `case 2`/`case 3`(미러볼)에서 `RemoveAt(tmpIndex)` 와 실제 사용 셀의 인덱스가 다르다. 후보 셀이 적을 때는 빈 리스트 접근도 가능하다(위 본문 참고).
> * **사용하지 않는 코드가 남아있다.** `TailEffectTask` 는 호출부가 없다. `Analyse()` 결과(`args.match`)도 배치 경로에서는 쓰이지 않는다. 또한 `OnClickBoosterItem`, `OnClickCardSkill` 안에는
>   "TODO: 사용한 아이템/카드스킬 소모 처리" 주석이 남아있어, 소모 처리를 호출부에서 하는지 이 클래스에서 해야 하는지 코드만으로는 알 수 없다.
> * **`args` 를 부분적으로만 채운 진입점이 있다.** 1인자 `OnClickCardSkill` 과 `OnClickCardSkillForAutoplay` 는 `args.tf` 등을 채우지 않는 채로 `args != null` 로 넘긴다. 호출부가 있다면
>   `NullReferenceException` 위험이 있고, 없다면 삭제 대상이다. 옵션 객체를 `null` 대신 "이펙트 없음" 값 객체로 표현하면 이 종류의 실수를 막을 수 있다.
> * **`Random`, `Resources.Load("Prefabs/Game/Scene/Ingame/Effect")`, `Instantiate` 가 함수 안에서 직접 호출된다.** 이펙트는 이미 오브젝트 풀링 구조가 있는 게임이므로 풀을 통해 재사용하는 것이 맞고,
>   난수는 주입 가능하게 만들어야 셀 선택 로직을 테스트할 수 있다.
> * **대기 코드가 비효율적이다.** `TailEffectTask` 는 매 프레임 `finishedList.ToList().Exists(...)` 로 리스트를 새로 만들고, 루프가 끝난 뒤 불필요한 `WaitUntil` 을 한 번 더 건다. 완료 카운터(`int remaining`)로 충분하다.

관련 문서: [BlockMatchLogic.md](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/BlockMatchLogic.md) · [07.BlockControl](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/07.BlockControl) (블록 재배치·힌트) · [PuzzleInit.md](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/PuzzleInit.md)
