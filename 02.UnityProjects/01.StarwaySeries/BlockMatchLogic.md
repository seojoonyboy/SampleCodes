### 일반 블록 제거와 채워지는 과정 구현

> 제거 대상이 되는 일반 블록 목록을 만든다. [match.Analyse 함수]
> NormalMatch 생성자에서 특수블록 생성 조건에 규합하는 일반 블록 조합이 있는지 배열에 순서대로 담는다.

<pre>
  <code>
    NormalMatch match = new NormalMatch(this.stage);
    NormalMatchResult result = match.Analyse(youNormal);
    BlockType targetBlockType = youNormal.block.type;
    youNormal.block.attr = BlockAttr.Movable;
    List<Block> excludedBlocks = null;
  </code>
</pre>

<pre>
  <code>
    public NormalMatch(Stage stage, List<Block> excludedBlocks = null)
    {
        this.s = stage;
        // 매칭에 포함 안되어야 할 블럭들
        this.uniq = new HashSet<Block>();
        if (null != excludedBlocks)
            foreach (Block block in excludedBlocks) 
                if (null != block)
                    this.uniq.Add(block);
          
        this.getters = new Getter[8] {
            new Getter(this.GetSpecialFinaleMatch),
            new Getter(this.GetSpecialLaserMatch),
            new Getter(this.GetSpecialBombWith4Match),
            new Getter(this.GetSpecialBombMatch),
            new Getter(this.GetSpecialCrossMatch),
            new Getter(this.GetSpecialTargetWith3Match),
            new Getter(this.GetSpecialTargetMatch),
            new Getter(this.GetNormalMatch),
        };
    }
  </code>
</pre>

---   

> 순서대로 담은 배열[getters]을 순회하며 매칭되는(제거될) 일반 블록을 총합한다.   
<pre>
  <code>
    public NormalMatchResult Analyse(Cell cell)
    {
        Cell[] outputs;
        MatchType matchType;
        foreach (Getter getter in this.getters) {
            Zone z = this.FindZone(cell);
            if (null != z && this.IsValidCell(cell) && getter(z, cell, out outputs, out matchType))
                return new NormalMatchResult(cell.block.type, outputs, matchType);
        }
        this.s.matchCount++;
        return null;
    }
  </code>
</pre>

---

> 일반 블록을 실제로 제거한다.
<pre>
  <code>
    if (null != result) {
        // 노말블럭을 삭제해주고
        Block newSpecialBlock = this.AttackNormal(result);
        excludedBlocks = new List<Block>() { newSpecialBlock };
        await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
    }
  </code>
</pre>

---

> 그 이후에 특수 블록도 제거한다.
<pre>
  <code>
    if (null != meSpecial.block) {
        meSpecial.block.attr = BlockAttr.Movable;
        await this.AttackSpecial(meSpecial, targetBlockType, excludedBlocks);
    }
  </code>
</pre>

제거 이후 빈칸을 블록들이 이동하는 처리를 진행한다. [MatchAndGravity]
<pre>
  <code>
    this.coMatchAndGravity = this.MatchAndGravity();
  </code>
</pre>

<pre>
  <code>
    public async UniTask MatchAndGravity()
    {
        this.dirtyCount++;
        this.gravityStackCount++;
        try {
            // 앞서 이동 진행을 마무리 한 후, 재 계산으로 들어간다.
            await UniTask.WaitUntil(() => 0 == BlockController.gravityCount);

            for (;;) {

                this.hintController.Unselect();

                if (this.isGraviting)
                    throw new OperationCanceledException();
                    //yield break;

                this.isGraviting = true;
                try {

                    ////////// MATCHING //////////
                    
                    // 트로피가 밑에까지 왔는지 검출한다.
                    foreach (Cell cell in this.stage.throphyTerminalCells) {
                        if (null != cell.block && BlockType.Trophy == cell.block.type) {
                            if (this.stage.ClearCountdown(ClearType.Throphy))
                            {
                                CommonProcessController.MuteEffectSound("Ingame", 0);
                                CommonProcessController.PlayEffectSound("Ingame", 20);
                                
                                await UniTask.Delay(TimeSpan.FromSeconds(0.4f));
                                BlockController controller = this.FindBlockController(cell.block);
                                if (null != controller)
                                {
                                    controller.Explode().Forget();
                                }

                                this.stage.RemoveBlock(cell);
                                
                                CommonProcessController.MuteEffectSound("Ingame", 20);
                            }
                            this.UpdateDashboard();
                            // flag = true;
                        }
                    }
                    
                    this.isIdling = false;

                    // 매칭 한 후
                    NormalMatch match = new NormalMatch(this.stage);
                    List<NormalMatchResult> results = match.AnalyseAll();

                    // 일반매칭결과를 처리한다.
                    if (null != results) {
                        foreach (NormalMatchResult result in results) {
                            Block specialBlock = this.AttackNormal(result);
                            if (null != specialBlock) {
                                // 생성 후 약간의 지연이 필요하다.
                                await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
                                this.dirtyCount++;
                            }
                        }
                        // 일반매칭 후 중력효과 발휘까지 대기 시간
                        await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
                    }

                    ////////// GRAVITING /////////

                    // 중력효과를 반영한다.
                    Dictionary<Block, List<Toss>> movements = Gravity.CalcMovements(this.stage);
                    if (null != movements) {
                        foreach (KeyValuePair<Block, List<Toss>> m in movements) {
                            Block block = m.Key;
                            List<Toss> tosses = m.Value;
                            BlockController blockController;
                            switch (tosses[0].type) {
                                case TossType.Normal:
                                case TossType.WrapIn:
                                    blockController = this.FindBlockController(block);
                                    if (null != blockController) {
                                        block.state = BlockState.Floating;
                                        blockController.coGravity = blockController.Gravity(tosses);
                                    }
                                    break;
                                case TossType.Genesis:
                                case TossType.WrapOut:
                                    blockController = BlockController.Create(block, tosses[0].toRow, tosses[0].toCol, true);
                                    if (null != blockController) {
                                        block.state = BlockState.Floating;
                                        blockController.coGravity = blockController.Gravity(tosses);
                                    }
                                    break;
                            }
                        }
                    } else {
                        // NOTE: 각 턴 효과가 발휘도고 난 후 마지막 위치가 된다.

                        // 잔디가 있는 스테이지 이고, 지금 스테이지에서 잔디를 제거 못했다면,
                        // 잔디 하나를 추가해준다.
                        // 최초턴에는 늘리지 않는다. 시작이니까.
                        if (this.stage.isWeeding && 0 == this.lastWeedsCount && 1 <= this.stage.turn) {
                            Cell cell = this.stage.GetNewWeedsCell();
                            if (null != cell) {
                                cell.bottomBlock = Block.FactoryWeeds();
                                BottomController.Create(cell.bottomBlock, cell.row, cell.col);
                                // 역으로 늘어난다.
                                if (this.stage.ClearCountdown(ClearType.Puddle, -1))
                                    this.UpdateDashboard();
                            }
                            this.lastWeedsCount = 0;
                        }

                        // NOTE: 턴 하나가 끝나는 지점
                        //Debug.Log("TURN OK");

                        if(0 == BlockController.gravityCount)
                            this.isIdling = true;

                        // 스테이지가 클리어 되었으면 완료 스프라이트를 올려준다.
                        if (this.isAutoplaying) {
                            switch (this.stage.mode) {
                                case Mode.TimeAttack:
                                    // 타임어텍 모드
                                    // if (this.stage.IsCleared)
                                    //     if (null != this.delegateTimeout)
                                    //         this.delegateTimeout(this);
                                    break;
                                default:
                                    // 일반 모드 or 일반+플레이타임 모드
                                    if (this.stage.IsCleared) {
                                        if (null != this.delegateClearStage)
                                            this.delegateClearStage(this);
                                    } else {
                                        if (null != this.delegateAutoplay)
                                            this.delegateAutoplay(this);
                                    }
                                    break;
                            }
                        } else {
                            switch (this.stage.mode) {
                                case Mode.TimeAttack:
                                    // 타임어텍 모드
                                    // if (this.stage.IsCleared)
                                    //     if (null != this.delegateTimeout)
                                    //         this.delegateTimeout(this);
                                    
                                    // 힌트 처리한다.
                                    // 힌트가 중력효과가 끝나고 나서부터 처리하도록 변경
                                    if(isIdling)
                                        this.ShowHintOrRefresh().Forget();
                                    break;
                                default:
                                    if (this.stage.IsCleared) {
                                        if (null != this.delegateClearStage)
                                            this.delegateClearStage(this);
                                    }
                                    else
                                    {
                                        if(isIdling)
                                            this.ShowHintOrRefresh().Forget();
                                    }
                                    break;
                            }
                        }
                            //생성이 안된것이 있다면 생성시켜준다.
                            foreach (KeyValuePair<string, Cell> item in srcCells) {
                                Cell cell = item.Value;
                                if (BlockAttr.Movable == cell.block.attr) {
                                    Debug.Log("SHERIFF: CREATE BLOCK. UNIQKEY = " + item.Key);
                                    BlockController.Create(cell.block, cell.row, cell.col);
                                }
                                // 움직여야 되는 속성이 있는 블럭이 고정되어 있다면 풀어준다.
                                if (null != cell.block && BlockAttr.Movable != cell.block.attr && cell.block.IsMovable) {
                                    Debug.Log("SHERIFF: MODITY ATTR.MOVABLE BLOCK. UNIQKEY = " + item.Key);
                                    cell.block.attr = BlockAttr.Movable;
                                }
                            }
                            srcCells = null;
                        }
                        // 턴 하나를 끝냈다.
                        throw new OperationCanceledException();
                    }

                } finally {
                    this.isGraviting = false;
                }

                // 중력효과가 완료될 때까지 대기
                await UniTask.WaitUntil(() => 0 == BlockController.gravityCount);

            } // for (;;)

        } finally {
            this.gravityStackCount--;
            if (0 == this.gravityStackCount)
                this.coMatchAndGravity = null;
            this.delegateCheckResultModalNeed?.Invoke();
        }
    }
  </code>
</pre>

---

> 블록이 채워지기 위해 각각의 블록이 이동경로를 만드는 과정

<pre>
  <code>
    public static Dictionary<Block, List<Toss>> CalcMovements(Stage s)
    {
        List<Snap> snaps = Gravity.CalcSnaps(s);
        if (null != snaps) {
            Dictionary&lt;Block, List&lt;Toss>> movements = new Dictionary&lt;Block, List&lt;Toss>>();
            foreach (Snap snap in snaps) {
                foreach (Toss toss in snap.tosses) {
                    if (null == toss.block)
                        Debug.Log("ERROR : toss가 널 입니다.");
                    else {
                        if (! movements.ContainsKey(toss.block))
                            movements.Add(toss.block, new List<Toss>());
                        movements[toss.block].Add(toss);
                    }
                }
            }
            return movements;
        } else
            return null;
    }
  </code>
</pre>
