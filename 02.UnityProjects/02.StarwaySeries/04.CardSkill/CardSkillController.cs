using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Artistar.Puzzle.Core;
using com.snowballs.SWHJ.client.view;
using Snowballs.Sheets.Data;

public class CardSkillController : BaseController
{
    [SerializeField] private Camera UICamera;
    private System.Random Random = new System.Random();

    public bool isCardUsed = false;
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


    private CardSkillMatchResult Analyse(int type, int method = 2)
    {
        CardSkillType cktype = (CardSkillType)type;
        CardSkillMatch match = new CardSkillMatch(this.stage);
        CardSkillMatchResult result = 2 == method ? match.Analyse2(cktype) : match.Analyse(cktype);
        return result;
    }

    public class AttackCardSkillArgs
    {
        public int cardSkillCode;
        public CardSkillSet cardSkillSet;

        public Texture2D skillBlockTexture;
        public Texture2D skillBlockBeforeRemoveEffectTexture;
        
        public Transform tf;
        public CardSkillMatchResult match;
        public GameObject tailPrefab;
        public GameObject glowPrefab;
    }
    
    private IEnumerator AttackCardSkill(AttackCardSkillArgs args)
    {
        yield return SetCardSkillBlock(BlockType.CardSkill, args);
    }

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


    public void OnClickBoosterItem(int index)
    {
        BlockType type = BlockType.None;
        switch (index) {
            case 1: type = BlockType.Rocket; break;
            case 2: type = BlockType.Bomb; break;
            case 3: type = BlockType.Mirrorball; break;
        }

        // TODO: 사용한 아이템 차감 루틴을 넣어주세요.
        StartCoroutine(this.SetBoosterItem(type));
    }

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

    private IEnumerator SetBoosterItem(BlockType type)
    {
        List<Cell> cells = this.getNormalCells();
        StageController sc = stageController;
        if (0 < cells.Count && BlockType.None != type) {
            // 랜덤으로 셀을 하나 얻고
            Cell cell = cells[Random.Next(0, cells.Count)];
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
	}

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
                if (0 < normalCells.Count) {
                    tmpIndex = Random.Next(0, normalCells.Count);
                    types[0] = BlockType.PaperPlane;
                    cells[0] = normalCells[tmpIndex];
                    normalCells.RemoveAt(tmpIndex);
                    
                    tmpIndex = Random.Next(0, normalCells.Count);
                    types[1] = BlockType.Bomb;
                    cells[1] = normalCells[tmpIndex];
                    normalCells.RemoveAt(tmpIndex);
                    
                    tmpIndex = Random.Next(0, normalCells.Count);
                    types[2] = BlockType.Mirrorball;
                    cells[2] = normalCells[Random.Next(0, normalCells.Count)];
                    normalCells.RemoveAt(tmpIndex);
                }
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

    // 카드스킬 테스트 버튼

    public void OnClickTEST()
    {
        BlockType type = BlockType.CardSkill;
        StartCoroutine(this.SetCardSkillBlock(type, null));
    }

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
}
