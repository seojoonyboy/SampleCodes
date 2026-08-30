카드마다 서로 다른 특수 효과(로켓, 폭탄, 미러볼, 종이비행기 등 특수 블록 생성형 스킬)를 갖고 있지만,
스킬마다 "발동 트리거가 다르고(수동 클릭 / 스코어 모드 자동 발동 / 이어하기 보상 등)" "적용 대상 셀도
다르다"는 문제가 있다. 스킬 종류가 늘어날 때마다 발동 로직을 새로 짜지 않도록, **일반 블록 셀 탐색 →
기존 블록 폭파 → 신규 특수 블록 생성/이펙트**라는 하나의 공통 파이프라인 위에 여러 종류의 카드 스킬을
얹는 구조로 되어 있다.

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

> 코드 곳곳에 남아있는 `// NOTE: 카드스킬은 턴을 소모하지 않는다` 주석은 실제 기획 규칙을 그대로 코드
> 옆에 남겨둔 흔적이다 — 카드 스킬은 퍼즐의 "턴"을 소모하는 일반 조작과는 다른 규칙을 갖고 있음을
> 구현 단계에서부터 명시하고 있다.

*발동 이후 공통 처리 — 셀 폭파와 특수 블록 생성*
> 세 진입점이 모두 도착하는 `SetCardSkillBlock`은 대상 셀을 무작위로 하나 뽑아 기존 블록을 터뜨리고,
> `Block.FactorySpecial(type)`으로 새 특수 블록을 만들어 그 자리에 채워 넣는다. `args`가 있을 때만(즉
> 실제 카드 스킬 발동일 때만) 카드에서 셀까지 날아가는 꼬리 이펙트를 먼저 재생하고, 이펙트 이동이 끝난
> 뒤에 블록을 교체한다는 순서가 중요하다.

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

> `retryCount`(이어하기 재시도 횟수)가 높을수록 보상으로 지급되는 특수 블록 종류가 늘어나는 것을 볼 수
> 있다(1회차: 턴만 추가, 2회차: 로켓 1개 추가, 3회차: 종이비행기/폭탄/미러볼 3종 추가) — 재시도를 여러
> 번 할수록 다음 시도가 더 유리해지도록 보상 곡선이 설계되어 있다.

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

관련 코드: [BlockMatchLogic.md](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/BlockMatchLogic.md) · [07.BlockControl](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/07.BlockControl)
