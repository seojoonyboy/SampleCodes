using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json.Linq;

using Artistar.Puzzle.Core;
using Snowballs.Client.Etc;
using Snowballs.Client.Model;
using Snowballs.Client.Ext.Event;
using Snowballs.Client.View.ingame;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Snowballs.Network.Dto;
using Snowballs.Sheets;
using UnityEngine.UI;
using MatchType = Artistar.Puzzle.Core.MatchType;
using TMPro;
using UnityEngine.SceneManagement;

#if UNITY_IOS
//using UnityEditor.iOS;
#endif

public class StageController : BaseController
{
    [SerializeField] private RawImage backgroundSpr, defaultBackgroundSpr;

    [SerializeField] private TMP_Text moveText;
    [SerializeField] private TMP_Text missionText;

    [SerializeField] private DOTweenAnimation adPlusAnim;
    [SerializeField] private TMP_Text adPlusText;

    public BackController backController;
    public HintController hintController;

    // 첫 터치 셀 임시 보관용
    private Cell fromCell = null;
    // 매치와 중력효과 코루틴
    private UniTask? coMatchAndGravity = null;

    // 데쉬보드 업데이트용 델리게이트
    public delegate void DelegateController(StageController self);
    public DelegateController delegateUpdateDashboard = null; // 데쉬보드 업데이트
    public DelegateController delegateClearStage = null; // 스테이지 클리어
    public DelegateController delegateAutoplay = null; // 오토플레이(한 턴이 끝나면 호출하여 다음 턴을 진행시킴)
    public DelegateController delegateUpdateSeconds = null;
    public DelegateController delegateTimeout = null;
    public delegate void DelegateCalcScore(StageController self, double basicScore);
    public DelegateCalcScore delegateCalcScore = null;
    
    [SerializeField] private TextMeshProUGUI normalModeStageLevelText;              //SWEB
    [SerializeField] private TextMeshProUGUI normalAndTimeModeLevelText;            //SWEB
    [SerializeField] private TextMeshProUGUI timeAttackModeLevelText;               //SWEB
    
    // 오토플래이 중인가?
    public bool isAutoplaying { get {
        var controller = this.GetComponent<AutoplayController>();
        return null != controller && controller.isAutoplaying;
    }}

    // 종료 모달 띄울지 여부 판단용 델리게이트
    public Action delegateCheckResultModalNeed;

    [SerializeField]
    private ResolutionControllerBase resolutionController;

    private bool lockHint = false;
    public bool lockInput = false;

    // 각 턴에서 잔디를 삭제했던 카운트, 잔디를 키울지 그대로 둘지 판단하는데 사용됨
    private int lastWeedsCount = 0;

    // 각종 문맥 비교용 매칭 카운트
    [HideInInspector]
    public uint dirtyCount = 0;
    private uint gravityStackCount = 0;
    public bool isGraviting = false;
    private int specialAttackingCount = 0;
    // 블록이 이동하고있는지 체크용
    [HideInInspector]
    public bool isIdling = true;
    // 스킬을 쓰고있는 상태 체크용
    [HideInInspector]
    public bool isSkillPlaying = false;

    // 타임어택용
    private readonly float tickPerSecond = 0.1f;
    private float spanTime = 0f;
    
    // 합쳐질때 위치 저장
    private List<Vector2> movedSavePos;
    
    // 토큰
    private CancellationTokenSource moveCts;
    
    // 슈팅스타상태관리용
    public enum ShootingState
    {
        Idle=0, // 평소상태 
        Play,   // 동작하고있는상태
    }

    private ShootingState shootingState = ShootingState.Idle;
    public ShootingState shootingStarState => shootingState;

    void Start()
    {
        moveCts = new CancellationTokenSource();
        movedSavePos = new List<Vector2>();

        if (null != GameStorage.Instance) {
            this.moveText.text = LocaleController.GetSystemLocale(321);
            // this.missionText.text = LocaleController.GetSystemLocale(320);
            if (null != this.resolutionController)
                this.resolutionController.Open(() => { this.SetBackground(); });
        }
    }

    private void SetBackground()
    {
        if (InGameController.isScoreModeSelected)
        {
            if(InGameController.selectedScoreStageDto == null) return;

            int level = InGameController.selectedScoreStageDto.stage;
            Int32 stageInfoBgIndex = SBDataSheet.Instance.ScoreModeStageSon[level].BGImage;
            string localeAddress = SBDataSheet.Instance.StageResource[stageInfoBgIndex].Address;
            var filePath = AssetPathController.PATH_FOLDER_ASSETS + localeAddress;
            WWWFile.DownloadPath downloadPath = new WWWFile.DownloadPath(WWWFile.TYPE.Bytes, String.Empty, filePath);
            TextureController.GetTexture(downloadPath, false, (boolRes, texture) => {
                if (boolRes) {
                    this.backgroundSpr.SetTexture(downloadPath);
                    this.ResizeBackgroundImage();
                } else {
                    defaultBackgroundSpr.gameObject.SetActive(true);
                }
            });
        }
        else
        {
            if(InGameController.selectedStageDto == null) return;
        
            var level = InGameController.selectedStageDto.level;
            if (InGameController.selectedStageDto.mode == StageMode.NORMAL) {
                Int32 stageInfoBgIndex = SBDataSheet.Instance.StageInfo[level].BGImage;
                string localeAddress = SBDataSheet.Instance.StageResource[stageInfoBgIndex].Address;
                var filePath = AssetPathController.PATH_FOLDER_ASSETS + localeAddress;
                WWWFile.DownloadPath downloadPath = new WWWFile.DownloadPath(WWWFile.TYPE.Bytes, String.Empty, filePath);
                TextureController.GetTexture(downloadPath, false, (boolRes, texture) => {
                    if (boolRes) {
                        this.backgroundSpr.SetTexture(downloadPath);
                        this.ResizeBackgroundImage();
                    } else {
                        defaultBackgroundSpr.gameObject.SetActive(true);
                    }
                });
                
                switch (CommonProcessController.GetNameString())
                {
                    case CommonProcessController.KWONEUNBIINFO:
                    case CommonProcessController.IKONINFO: 
                        string systemLocaleStr = LocaleController.GetSystemLocale(12051);
                        if (!string.IsNullOrEmpty(systemLocaleStr))
                        {
                            this.normalModeStageLevelText.text = LocaleController
                                .GetSystemLocale(12051)
                                .Replace("{0}", level.ToString("D2"));
                        }
                        //로케일 찾을 수 없는 경우
                        else
                        {
                            this.normalModeStageLevelText.gameObject.SetActive(false);
                        }
                        break;
                }
            } else {
                //스타웨이, 무한모드
                Int32 stageInfoBgIndex = SBDataSheet.Instance.ChallengeStage[level].BGImage;
                string localeAddress = SBDataSheet.Instance.StageResource[stageInfoBgIndex].Address;
                var filePath = AssetPathController.PATH_FOLDER_ASSETS + localeAddress;
                WWWFile.DownloadPath downloadPath = new WWWFile.DownloadPath(WWWFile.TYPE.Bytes, String.Empty, filePath);
                TextureController.GetTexture(downloadPath, false, (boolRes, texture) => {
                    if (boolRes) {
                        this.backgroundSpr.SetTexture(downloadPath);
                        this.ResizeBackgroundImage();
                    } else {
                        defaultBackgroundSpr.gameObject.SetActive(true);
                    }
                });
                
                switch (CommonProcessController.GetNameString())
                {
                    case CommonProcessController.KWONEUNBIINFO:
                    case CommonProcessController.IKONINFO: 

                        if (InGameController.selectedStageDto.mode == StageMode.CHALLENGE)
                        {
                            string systemLocaleStr = LocaleController.GetSystemLocale(12052);
                            if (!string.IsNullOrEmpty(systemLocaleStr))
                            {
                                this.normalModeStageLevelText.text =
                                    LocaleController
                                        .GetSystemLocale(12052)
                                        .Replace("{0}", level.ToString("D2"));
                            }
                            //로케일 찾을 수 없는 경우
                            else
                            {
                                this.normalModeStageLevelText.gameObject.SetActive(false);
                            }
                        }
                        else if (InGameController.selectedStageDto.mode == StageMode.INFINITE)
                        {
                            string systemLocaleStr = LocaleController.GetSystemLocale(12053);
                            if (!string.IsNullOrEmpty(systemLocaleStr))
                            {
                                this.normalModeStageLevelText.text = LocaleController
                                    .GetSystemLocale(12053)
                                    .Replace("{0}", level.ToString("D2"));
                            }
                            //로케일 찾을 수 없는 경우
                            else
                            {
                                this.normalModeStageLevelText.gameObject.SetActive(false);
                            }
                        }
                        break;
                }
            }
        }
    }

    private void ResizeBackgroundImage()
    {
        /*RectTransform backgroundRect = this.backgroundSpr.GetComponent<RectTransform>();
        float screenHeight = this.transform
            .GetComponentInParent<Canvas>()
            .GetComponent<RectTransform>()
            .rect.height;
        float textureRatio = 1920f / 1661f;

        float targetWidth = screenHeight / textureRatio;
        backgroundRect.sizeDelta = new Vector2(targetWidth, screenHeight);*/
    }

    void Update()
    {
        this.spanTime += Time.deltaTime;
        switch (this.stage.mode) {
            case Mode.TimeAttack:
                // 게임 모드에 따라 시간을 화면에 넣어준다.
                if (this.tickPerSecond <= this.spanTime && false == this.lockInput) {
                    // 시간 측정을 시작하였다.
                    if (DateTime.MinValue != this.stage.startDate) {
                        DateTime now = DateTime.Now;
                        TimeSpan diff = now - this.stage.startDate;
                        int totalSeconds = (int)diff.TotalSeconds;
                        if (this.stage.totalClearTime < totalSeconds)
                            this.lockInput = true;
                        if ((int)this.stage.lastSeconds != totalSeconds) {
                            this.stage.lastSeconds = totalSeconds;
                            if (null != this.delegateUpdateSeconds)
                                this.delegateUpdateSeconds(this);
                        }
                        // 타임아웃되었는가?
                        if (this.stage.IsCleared)
                            if (null != this.delegateTimeout)
                                this.delegateTimeout(this);
                    }
                    this.spanTime = 0f;
                }
                break;
            case Mode.NormalAndTime:
                // 게임 모드에 따라 시간을 화면에 넣어준다.
                if (this.tickPerSecond <= this.spanTime) {
                    // 시간 측정을 시작하였다.
                    if (DateTime.MinValue != this.stage.startDate && !this.stage.IsCleared) {
                        DateTime now = DateTime.Now;
                        TimeSpan diff = now - this.stage.startDate;
                        int totalSeconds = (int)diff.TotalSeconds;
                        if ((int)this.stage.lastSeconds != totalSeconds) {
                            this.stage.lastSeconds = totalSeconds;
                            if (null != this.delegateUpdateSeconds)
                                this.delegateUpdateSeconds(this);
                        }
                    }
                    this.spanTime = 0f;
                }
                break;
        }
        if (Input.GetMouseButtonDown(0) && !this.isSkillPlaying) {
            int row, col;
            bool ok = this.GetMaxtrixByInput(out row, out col);
            Cell cell = this.stage.GetCellSafely(row, col);
            // 셀과 블록이 있고 블록이 움직일 수 있는 것이라면
            if (!this.lockHint && !this.lockInput &&
                ok &&
                null != cell &&
                CellType.Alive == cell.type &&
                null != cell.block &&
                // 첫블록은 반드시 노말블록 또는 특수블록
                (cell.block.IsNormal || cell.block.IsSpecial) &&
                BlockAttr.Movable == cell.block.attr &&
                BlockState.Fixed == cell.block.state &&
                !this.stage.IsCleared
                )
            {
                movedSavePos.Clear();
                this.fromCell = cell;
                // 힌트 셀렉트를 없애주고
                this.hintController.Unselect();
                this.dirtyCount++;

                // 터치 입력 되어 노말 블록 등은 터치 연출이 필요
                if (cell.block.IsNormal) {
                    BlockController controller = this.FindBlockController(cell.block);
                    if (null != controller)
                        controller.Touch().Forget();
                }
            }
        } else if (null != this.fromCell && !this.isSkillPlaying) {
            int row, col;
            bool ok = this.GetMaxtrixByInput(out row, out col);
            Cell toCell = this.stage.GetCellSafely(row, col);
            // 같은 셀을 터치 했다.
            if (ok && this.fromCell == toCell) {
                if (Input.GetMouseButtonUp(0)) {
                    movedSavePos.Clear();
                    // Debug.Log("SELECT SAME CELLS");
                    // 특수블록이면(미러볼은 예외) 터트려야 한다.
                    if (null != this.fromCell.block && this.fromCell.block.IsSpecial && BlockType.Mirrorball != this.fromCell.block.type)
                        this.SwapBlocks(new Cell[1] { this.fromCell }).Forget();
                    this.fromCell = null;
                }
                // PASS
            }
            // 다른 셀이 선택되었다.
            else if (
                ok &&
                null != toCell &&
                CellType.Alive == this.fromCell.type &&
                CellType.Alive == toCell.type &&
                // 상하좌우 1칸으로 인접되어 있을 경우만 이동가능하다.
                1 == (Math.Abs(this.fromCell.row - toCell.row) + Math.Abs(this.fromCell.col - toCell.col)))
            {
                movedSavePos.Clear();
                if ((null == toCell || null == toCell.block || (BlockAttr.Fixable != toCell.block.attr && BlockState.Floating != toCell.block.state)) &&
                    !this.stage.IsDefancedByWall(this.fromCell, toCell))
                {
                    // 2개의 블록을 교환하고 매치&중력효과를 진행한다.
                    this.SwapBlocks(new Cell[2] { this.fromCell, toCell }).Forget();
                }
                this.fromCell = null;
            }
            // 선택해제
            else if (
                !ok ||
                null == toCell ||
                CellType.Alive != toCell.type ||
                (null != toCell.block && BlockAttr.Movable != toCell.block.attr))
            {
                movedSavePos.Clear();
                // Debug.Log("RELEASE CELLS");
                this.fromCell = null;
            }
        }
    }

    public void Clear()
    {
        for (int i = 0; i < this.transform.childCount; i++)
            Destroy(transform.GetChild(i).gameObject);
        this.fromCell = null;
        this.coMatchAndGravity = null;
        this.lockInput = false;
        this.lockHint = false;
        this.lastWeedsCount = 0;
        this.dirtyCount = 0;
        this.gravityStackCount = 0;
        this.isGraviting = false;
        this.specialAttackingCount = 0;
        this.isIdling = true;
        this.spanTime = 0f;
        this.stage.Clear();
        BlockController.gravityCount = 0;
        this.UpdateDashboard();
    }

    public void LoadStage(JObject obj)
    {
        this.Clear();
        this.stage.Clear();
        this.stage.FromJObject(obj);
        this.InitOffset();
        this.InitStage(obj);

        // 만약 시작할 때 매칭되는 것이 한개도 없다면 판을 새로 깔아준다.
        Hint hint = new Hint(this.stage);
        hint.Calc();
        // 매칭이 하나도 없는 경우 새로 정렬한다.
        if (0 == hint.hints.Count) {
            hint.Refresh();
            List<SwapBlock> swapCells = hint.swaps;
            if (null != swapCells)
                foreach (SwapBlock swap in swapCells) {
                    Cell cell = this.stage.cells[swap.cell.row, swap.cell.col];
                    Block beforeBlock = cell.block;
                    cell.block = swap.afterBlock;
                    this.FindBlockController(beforeBlock)?.SwapNewBlock(swap.afterBlock);
                }
        }

        // 중력효과 적용
        this.coMatchAndGravity = this.MatchAndGravity();
    }

    public void InitStage(JObject obj)
    {
        // 랜덤용
        System.Random random = new System.Random();

        CancellationTokenSource gravityCts = new CancellationTokenSource();
        
        // 중간값 초기화
        this.fromCell = null;
        if (null != this.coMatchAndGravity)
            gravityCts.Cancel();
            //StopCoroutine(this.coMatchAndGravity);
        this.hintController.Unselect();

        // BlockController.gravityCount = 0;
        // 화면에 블록을 넣어준다.
        for (int r = 0; r < this.stage.rowCount; r++)
            for (int c = 0; c < this.stage.colCount; c++) {
                Cell cell = this.stage.cells[r, c];
                // 메인블록을 넣어주고
                Block block = cell.block;
                if (null != block) {
                    // 최초 블럭을 깔아줄 때 랜덤 요소가 있으면 컴포넌트로 부터 새로 생성해서 넣어준다.
                    if (BlockType.Random == block.type) {
                        if (this.stage.components.ContainsKey(block.componentName)) {
                            List<Block> blocks = this.stage.components[block.componentName];
                            Block newBlock = blocks[random.Next(0, blocks.Count)];
                            if (BlockType.None != newBlock.type && BlockType.Invalid != newBlock.type) {
                                block = Block.Factory(newBlock);
                                cell.block = block;
                                BlockController.Create(block, r, c);
                            } else {
                                // 사용할 수 없는 블록이므로 없는것으로 하기위해서 null으로 바꿔서 넣어준다.
                                block = null;
                                cell.block = null;
                            }
                        } else {
                            this.Clear();
                            throw new Exception("랜덤 블록 생성의 컴포넌트가 없습니다(" + block.componentName + ").");
                        }
                    } else if (BlockType.Stand == block.type ||
                        BlockType.Pizza == block.type ||
                        BlockType.Fridge == block.type ||
                        BlockType.Fishbowl == block.type ||
                        BlockType.RainbowBalloonBox == block.type ||
                        BlockType.RedBalloonBox == block.type ||
                        BlockType.YellowBalloonBox == block.type ||
                        BlockType.GreenBalloonBox == block.type ||
                        BlockType.PurpleBalloonBox == block.type ||
                        BlockType.Vault == block.type)
                    {
                        if (BlockRank.Master == block.rank)
                            BlockController.Create(block, r, c);
                        // CAUTION: Slave는 화면에 넣지 않는다.
                    }
                    else
                    {
                        block.isFirstBlock = true;
                        BlockController.Create(block, r, c);
                    }
                }
                // 측면/하단 벽을 넣어주고
                if (null != cell.rightBlock)
                    WallController.Create(cell.rightBlock, r, c, WallControllerType.Vertical);
                if (null != cell.belowBlock)
                    WallController.Create(cell.belowBlock, r, c, WallControllerType.Horizontal);
                // 뽁뽁이, 잔디, 물감
                if (null != cell.bottomBlock)
                    BottomController.Create(cell.bottomBlock, r, c);
                // 감옥창살
                if (null != cell.topBlock)
                    TopController.Create(cell.topBlock, r, c);
                // 트로피 출구
                if (CellAttr.FlowerpotTerminal == cell.attr) {
                    GameObject prefab = Resources.Load<GameObject>("Prefabs/outPrefab");
                    GameObject prefabObject = Instantiate(prefab, BaseController.stageObject.transform);
                    prefabObject.name = "BOUT" + cell.row + "x" + cell.col;
                    prefabObject.transform.localPosition = GetPositionByMatrix(cell.row, cell.col, 0f, 75f);
                } else if (CellAttr.WarpIn == cell.attr) {
                    GameObject prefab = Resources.Load<GameObject>("Prefabs/wrapInPrefab");
                    GameObject prefabObject = Instantiate(prefab, BaseController.stageObject.transform);
                    prefabObject.name = "WRAPIN" + cell.row + "x" + cell.col;
                    prefabObject.transform.localPosition = GetPositionByMatrix(cell.row, cell.col, 0f, 75f);
                } else if (CellAttr.WarpOut == cell.attr) {
                    GameObject prefab = Resources.Load<GameObject>("Prefabs/wrapOutPrefab");
                    GameObject prefabObject = Instantiate(prefab, BaseController.stageObject.transform);
                    prefabObject.name = "WRAPOUT" + cell.row + "x" + cell.col;
                    prefabObject.transform.localPosition = GetPositionByMatrix(cell.row, cell.col, 0f, -75f);
                }
            }
        // 백판 깔아주고
        this.backController.LoadStage();
        // 데쉬보드 업데이트 하고
        this.UpdateDashboard();
    }

    ////////////////// MATCHING COROUTINE //////////////////

    #region MATCHING COROUTINE

    // 일반블록 매칭결과로 특수블록을 생성하고 리턴한다.
    private Block AttackNormal(NormalMatchResult match)
    {
        // 매칭 스타일에 따라 특수블록을 하나 만들고
        Block specialblock = Block.FactoryBySpecialMatch(match.type);

        // 노말사이드어텍
        this.AttackNormalSide(match);

        // 블록을 터트리고
        Cell me = match.cells[0];
        foreach (Cell cell in match.cells) {
            // 탑(감옥창살)블록
            if (null != cell.topBlock) {
                Block block = cell.topBlock;
                block.normalPoint = Math.Max(0, block.normalPoint - 1);
                if (block.IsDead) {
                    if (this.RemoveTopBlock(cell))
                        this.UpdateDashboard();
                } else {
                    if (block.type == BlockType.Barricade) {
                        TopController controller = this.FindTopController(block);
                        if (null != controller)
                            controller.showBarricadeChangeEffect();
                    }
                }
            } else {
                // 일반블록
                if (null != cell.block) {
                    Block block = cell.block;
                    block.normalPoint = Math.Max(0, block.normalPoint - 1);
                    if (block.IsDead) {
                        if (null != specialblock) {
                            BlockController controller = this.FindBlockController(block);
                            if (null != controller) {
                                // 합쳐질때 일반블록 사라지는 연출도 위치 조정
                                Cell meCell = GetExactPos(match);
                                controller.RemoveTo(meCell.row, meCell.col, 0.1f).Forget();
                            }
                        } else {
                            BlockController controller = this.FindBlockController(block);
                            if (null != controller)
                            {
                                controller.Explode().Forget();
                            }
                        }
                        this.stage.RemoveBlock(cell);
                        // 스코어를 반영한다.
                        if (null != this.delegateCalcScore)
                            this.delegateCalcScore(this, Stage.SCORE_NORMAL);
                        if (this.stage.ClearCountdownForNormalBlock(block))
                            this.UpdateDashboard();
                    }
                }
                // 보텀(뽁뽁이,잔디)블록
                if (null != cell.bottomBlock) {
                    Block block = cell.bottomBlock;
                    block.normalPoint = Math.Max(0, block.normalPoint - 1);

                    if (block.IsDead)
                    {
                        if (this.RemoveBottomBlock(cell))
                            this.UpdateDashboard();
                    }
                    else
                    {
                        BottomController controller = this.FindBottomController(block);
                        controller.BottomRemoveEffect(block.type);
                    }
                }
            }
        }

        // 특수블록을 넣어준다.
        if (null != specialblock) {
            // 넣어줄 위치 설정
            Cell cell = GetExactPos(match);
            BlockController.Create(specialblock, cell.row, cell.col,false, () =>
            {
                movedSavePos.Clear();
            });
            this.stage.AddBlock(cell, specialblock);
        }

        // 스킬피버 노말매칭 카운트다운
        if (null != specialblock)
            this.stage.skillFever += Stage.SKILLFEVER_NORMAL_CREATE_SPECIAL;
        else
            this.stage.skillFever += Stage.SKILLFEVER_NORMAL;

        // 스코어를 반영한다.
        // if (null != this.delegateCalcScore)
        //     this.delegateCalcScore(this, match.cells.Length * Stage.SCORE_NORMAL);

        // 화면 데쉬보드 업뎃
        this.UpdateDashboard();

        return specialblock;
    }
    private Cell GetExactPos(NormalMatchResult match)
    {
        // 저장된 위치 
        for (var i = 0; i < match.cells.Count(); i++)
        {
            for (int j = 0; j < movedSavePos.Count; j++)
            {
                if (Math.Abs(match.cells[i].row - movedSavePos[j].x) == 0 &&
                    Math.Abs(match.cells[i].col - movedSavePos[j].y) == 0)
                {
                    var cell = match.cells[i];
                    return cell;
                }
            }
        }
        return match.cells[0];
    }

    // 포인트가 차감되었다면 true를 리턴한다.
    public void AttackMission(Cell toCell, int normalSidePoint, int specialPoint, BlockType fromBlockType = BlockType.None, bool isSpecialAttack = false)
    {
        BlockLog.Instance.RemoveBlock(toCell.block);
        switch (toCell.block.type) {
            // case BlockType.Fireworkbox:
            //     break;
            case BlockType.Luckyball:
            case BlockType.TeaCup:
            case BlockType.Vase:
            case BlockType.Pumpkin:
            case BlockType.Lantern:
                if (0 < normalSidePoint) {
                    toCell.block.normalSidePoint = Math.Max(0, toCell.block.normalSidePoint - normalSidePoint);
                    // Debug.Log("LOCKYBALL CELL " + cell.block.type + " " + cell.row + "x" + cell.col);
                    if (toCell.block.IsDead) {
                        BlockController self = this.FindBlockController(toCell.block);
                        if (null != self)
                        {
                            self.Explode().Forget();
                        }
                        //터지는 효과
                        GameObject prefab = IngameEffectPrefabLoader.Instance
                            .GetBlockExplosionPrefab(toCell.block.type);
                        GameObject pangObj = Instantiate(prefab, self.gameObject.transform.root);
                        pangObj.transform.position = self.gameObject.transform.position;
                        // pangObj.transform.localScale = Vector3.one;
                        pangObj.AddComponent<SelfDestroy>();
                        ClearType ct = ClearType.None;
                        switch (toCell.block.type) {
                            case BlockType.Luckyball: ct = ClearType.Luckyball; break;
                            case BlockType.TeaCup: ct = ClearType.TeaCup; break;
                            case BlockType.Vase:  ct = ClearType.Vase; break;
                            case BlockType.Pumpkin: ct = ClearType.Pumpkin; break;
                            case BlockType.Lantern: ct = ClearType.Lantern; break;
                        }
                        this.stage.RemoveBlock(toCell);
                        if (this.stage.ClearCountdown(ct))
                            this.UpdateDashboard();
                    }
                    else
                    {
                        BlockController self = this.FindBlockController(toCell.block);

                        GameObject prefab = IngameEffectPrefabLoader.Instance
                            .GetBlockExplosionPrefab(toCell.block.type);
                        GameObject pangObj = Instantiate(prefab, self.gameObject.transform);
                        pangObj.AddComponent<SelfDestroy>();
                    }
                }
                break;
            // case BlockType.Bubblewrap:
            //     break;
            case BlockType.TicketBox:
                // 런치박스는 통합 포인트 개념임
                if (0 < normalSidePoint) {
                    BlockController self = this.FindBlockController(toCell.block);
                    if (null != self && null != toCell.block.clear && !toCell.block.clear.IsCleared) {
                        toCell.block.normalSidePoint = Math.Max(0, toCell.block.normalSidePoint - normalSidePoint);
                        // Debug.Log("LUNCHBOX CELL " + cell.block.type + " " + cell.row + "x" + cell.col);
                        this.stage.ClearCountdown(ClearType.TicketBox);
                        // if (this.stage.ClearCountdown(ClearType.TicketBox))
                        //     this.UpdateDashboard();
                        //Tail Effect
                        this.DoMissionTailEffect(
                            ((int)ClearType.TicketBox).ToString(),
                            self,
                            () => { this.UpdateDashboard(); }
                        );
                    }
                }
                break;
            // case BlockType.Jail:
            //     break;
            case BlockType.Gemstone:
            case BlockType.Coconut:
            case BlockType.CannedSoda:
            case BlockType.CannedFood:
            case BlockType.JewelStash:
            case BlockType.IronBox:
                if (0 < specialPoint) {
                    toCell.block.specialPoint = Math.Max(0, toCell.block.specialPoint - specialPoint);
                    // Debug.Log("CAN CELL " + cell.block.type + " " + cell.row + "x" + cell.col);
                    if (toCell.block.IsDead) {
                        BlockController controller = this.FindBlockController(toCell.block);
                        if (null != controller)
                        {
                            controller.Explode().Forget();
                        }

                        //터지는 효과
                        GameObject prefab = IngameEffectPrefabLoader.Instance
                            .GetBlockExplosionPrefab(toCell.block.type);
                        GameObject pangObj = Instantiate(prefab, controller.gameObject.transform.root);
                        pangObj.transform.position = controller.transform.position;
                        // pangObj.transform.localScale = Vector3.one;
                        
                        ClearType ct = ClearType.None;
                        switch (toCell.block.type) {
                            case BlockType.Gemstone: ct = ClearType.Gemstone; break;
                            case BlockType.Coconut: ct = ClearType.Coconut; break;
                            case BlockType.CannedSoda: ct = ClearType.CannedSoda; break;
                            case BlockType.CannedFood: ct = ClearType.CannedFood; break;
                            case BlockType.JewelStash: ct = ClearType.JewelStash; break;
                            case BlockType.IronBox: ct = ClearType.IronBox; break;
                        }
                        this.stage.RemoveBlock(toCell);
                        if (this.stage.ClearCountdown(ct))
                            this.UpdateDashboard();
                    }
                    else
                    {
                        BlockController self = this.FindBlockController(toCell.block);

                        GameObject prefab = IngameEffectPrefabLoader.Instance
                            .GetBlockExplosionPrefab(toCell.block.type);
                        GameObject pangObj = Instantiate(prefab, self.gameObject.transform);
                    }
                }
                break;
            // case BlockType.Popcornbox:
            //     break;
            // case BlockType.Paints:
            //    break;
            case BlockType.Woodbox:
            case BlockType.IceCube:
            case BlockType.TopiarySpring:
            case BlockType.TopiaryWinter:
            case BlockType.FloorLamp:
                if (0 < normalSidePoint) {
                    toCell.block.normalSidePoint = Math.Max(0, toCell.block.normalSidePoint - normalSidePoint);
                    // Debug.Log("PAPERBOX CELL " + cell.block.type + " " + cell.row + "x" + cell.col);
                    if (toCell.block.IsDead) {
                        BlockController controller = this.FindBlockController(toCell.block);
                        if (null != controller)
                        {
                            controller.Explode().Forget();
                        }

                        ClearType ct = ClearType.None;
                        switch (toCell.block.type) {
                            case BlockType.Woodbox: ct = ClearType.Woodbox; break;
                            case BlockType.IceCube: ct = ClearType.IceCube; break;
                            case BlockType.TopiarySpring: ct = ClearType.TopiarySpring; break;
                            case BlockType.TopiaryWinter: ct = ClearType.TopiaryWinter; break;
                            case BlockType.FloorLamp: ct = ClearType.FloorLamp; break;
                        }
                        this.stage.RemoveBlock(toCell);
                        if (this.stage.ClearCountdown(ct))
                            this.UpdateDashboard();
                    }
                    else
                    {
                        BlockController self = this.FindBlockController(toCell.block);

                        GameObject prefab = IngameEffectPrefabLoader.Instance
                            .GetBlockExplosionPrefab(toCell.block.type);
                        GameObject pangObj = Instantiate(prefab, self.gameObject.transform);
                        pangObj.AddComponent<SelfDestroy>();
                    }
                }
                break;
            // case BlockType.Weeds:
            //     break;
            case BlockType.Stand:
            case BlockType.Pizza:
            case BlockType.Fridge:
            case BlockType.Fishbowl:
                if (0 < normalSidePoint && null != toCell.block.masterCell) {
                    Cell masterCell = toCell.block.masterCell;
                    BlockController self = this.FindBlockController(toCell.block);
                    ClearType ct = ClearType.None;
                    switch (masterCell.block.type) {
                        case BlockType.Stand: ct = ClearType.Stand; break;
                        case BlockType.Pizza: ct = ClearType.Pizza; break;
                        case BlockType.Fridge: ct = ClearType.Fridge; break;
                        case BlockType.Fishbowl: ct = ClearType.Fishbowl; break;
                    }
                    // 터지는 효과
                    GameObject prefab = IngameEffectPrefabLoader.Instance
                        .GetBlockExplosionPrefab(toCell.block.type);
                    BlockController masterCellBlock = this.FindBlockController(masterCell.block);
                    GameObject pangObj = Instantiate(prefab, masterCellBlock.gameObject.transform);
                    SelfDestroy selfDestroy = pangObj.AddComponent<SelfDestroy>();
                    selfDestroy.interval = 2f;

                    if (10 == masterCell.block.normalSidePoint) {
                        // 10 -> 9 포인트로 내려 갈때는 데쉬보드 차감이 없다.
                        masterCell.block.normalSidePoint = Math.Max(0, masterCell.block.normalSidePoint - normalSidePoint);
                    } else {
                        masterCell.block.normalSidePoint = Math.Max(0, masterCell.block.normalSidePoint - normalSidePoint);
                        this.stage.ClearCountdown(ct);

                        BlockController controller = this.FindBlockController(masterCell.block);
                        // this.UpdateDashboard();
                        if (masterCell.block.IsDead) {
                            if (null != controller)
                            {
                                controller.Explode().Forget();
                            }

                            this.stage.RemoveBlock(masterCell);
                            // 슬레이브 셀도 삭제해야 한다.
                            int r = masterCell.row;
                            int c = masterCell.col;
                            this.stage.RemoveBlock(this.stage.cells[r,c+1]);
                            this.stage.RemoveBlock(this.stage.cells[r+1,c]);
                            this.stage.RemoveBlock(this.stage.cells[r+1,c+1]);
                        }
                        else
                        {
                            int targetCode = 0;
                            if (masterCell.block.type == BlockType.Stand) { targetCode = 212; }
                            else if (masterCell.block.type == BlockType.Pizza) { targetCode = 414; }
                            else if (masterCell.block.type == BlockType.Fridge) { targetCode = 415; }
                            else if(masterCell.block.type == BlockType.Fishbowl) { targetCode = 416; }
                            this.DoMissionTailEffect(
                                targetCode.ToString(),
                                controller, () => { }
                            );
                        }
                    }
                }
                break;

            case BlockType.RainbowBalloonBox:
                if (0 < normalSidePoint && null != toCell.block.masterCell) {
                    Cell masterCell = toCell.block.masterCell;
                    if (-1 < masterCell.block.matchNumbers.IndexOf(this.stage.matchCount))
                        break;
                    BlockType tb = BlockType.None;
                    ClearType ct = ClearType.None;
                    int idx = 0;
                    // 일반공격의 경우에는 4개중 어느블록인지 체크한다.
                    if (BlockType.None != fromBlockType) {
                        foreach (var p in masterCell.block.pieces) {
                            if (p.blockType == fromBlockType) {
                                tb = fromBlockType;
                                switch (tb) {
                                    case BlockType.Red: ct = ClearType.RedBalloonBox; break;
                                    case BlockType.Yellow: ct = ClearType.YellowBalloonBox; break;
                                    case BlockType.Green: ct = ClearType.GreenBalloonBox; break;
                                    case BlockType.Purple: ct = ClearType.PurpleBalloonBox; break;
                                }
                                break;
                            }
                            idx++;
                        }
                    }
                    // 발견되었다. 또는 스페셜어텍
                    if (ClearType.None != ct || isSpecialAttack) {
                        BlockController self = this.FindBlockController(toCell.block);
                        // 터지는 효과
                        GameObject prefab = IngameEffectPrefabLoader.Instance
                            .GetBlockExplosionPrefab(toCell.block.type);
                        BlockController masterCellBlock = this.FindBlockController(masterCell.block);
                        GameObject pangObj = Instantiate(prefab, masterCellBlock.gameObject.transform);
                        SelfDestroy selfDestroy = pangObj.AddComponent<SelfDestroy>();
                        selfDestroy.interval = 2f;
                        masterCell.block.pieces.RemoveAt(idx); // 스페셜어텍의 경우 앞에서 삭제한다. 즉 idx가 0이 된다.
                        // 중복방지 넘버
                        masterCell.block.matchNumbers.Add(this.stage.matchCount);
                        bool isUpdated0 = this.stage.ClearCountdown(ct);
                        bool isUpdated1 = this.stage.ClearCountdown(ClearType.BalloonBox);
                        if (isUpdated0 || isUpdated1)
                            this.UpdateDashboard();
                        if (masterCell.block.IsDead) {
                            BlockController controller = this.FindBlockController(masterCell.block);
                            if (null != controller)
                            {
                                controller.Explode().Forget();
                            }
                            this.stage.RemoveBlock(masterCell);
                            // 슬레이브 셀도 삭제해야 한다.
                            int r = masterCell.row;
                            int c = masterCell.col;
                            this.stage.RemoveBlock(this.stage.cells[r,c+1]);
                            this.stage.RemoveBlock(this.stage.cells[r+1,c]);
                            this.stage.RemoveBlock(this.stage.cells[r+1,c+1]);
                        }
                        this.DoMissionTailEffect(
                            ((int)BlockType.Stand).ToString(),
                            masterCellBlock, () => { this.UpdateDashboard(); }
                        );
                    }
                }
                break;
            case BlockType.RedBalloonBox:
            case BlockType.YellowBalloonBox:
            case BlockType.GreenBalloonBox:
            case BlockType.PurpleBalloonBox:
                // Debug.Log("BOLLOONBOX ENTER "+ toCell.block.type);
                if (0 < normalSidePoint && null != toCell.block.masterCell) {
                    Cell masterCell = toCell.block.masterCell;
                    if (-1 < masterCell.block.matchNumbers.IndexOf(this.stage.matchCount))
                        break;
                    ClearType ct = ClearType.None;
                    BlockType tb = BlockType.None;
                    // Debug.Log("BOLLOONBOX PASS 0 "+ toCell.block.type);
                    switch (toCell.block.type) {
                        case BlockType.RedBalloonBox: ct = ClearType.RedBalloonBox; tb = BlockType.Red; break;
                        case BlockType.YellowBalloonBox: ct = ClearType.YellowBalloonBox; tb = BlockType.Yellow; break;
                        case BlockType.GreenBalloonBox: ct = ClearType.GreenBalloonBox; tb = BlockType.Green; break;
                        case BlockType.PurpleBalloonBox: ct = ClearType.PurpleBalloonBox; tb = BlockType.Purple; break;
                    }
                    // Debug.Log("BOLLOONBOX PASS 1 "+ toCell.block.type + ", tb=" + tb.ToString() + ", fromBlockType=" + fromBlockType.ToString());
                    if (fromBlockType == tb || isSpecialAttack) {
                        BlockController self = this.FindBlockController(toCell.block);
                        if (0 < masterCell.block.pieces.Count) {
                            // 터지는 효과
                            GameObject prefab = IngameEffectPrefabLoader.Instance
                                .GetBlockExplosionPrefab(toCell.block.type);
                            BlockController masterCellBlock = this.FindBlockController(masterCell.block);
                            GameObject pangObj = Instantiate(prefab, masterCellBlock.gameObject.transform);
                            SelfDestroy selfDestroy = pangObj.AddComponent<SelfDestroy>();
                            selfDestroy.interval = 2f;
                            // NOTE: 반드시 normalSidePoint가 1이므로 하나씩 삭제하고 있다.
                            masterCell.block.pieces.RemoveAt(0);
                            // 중복방지 넘버
                            masterCell.block.matchNumbers.Add(this.stage.matchCount);
                            bool isUpdated0 = this.stage.ClearCountdown(ct);
                            bool isUpdated1 = this.stage.ClearCountdown(ClearType.BalloonBox);
                            if (isUpdated0 || isUpdated1)
                                this.UpdateDashboard();
                            if (masterCell.block.IsDead) {
                                BlockController controller = this.FindBlockController(masterCell.block);
                                if (null != controller)
                                {
                                    controller.Explode().Forget();
                                }
                                this.stage.RemoveBlock(masterCell);
                                // 슬레이브 셀도 삭제해야 한다.
                                int r = masterCell.row;
                                int c = masterCell.col;
                                this.stage.RemoveBlock(this.stage.cells[r,c+1]);
                                this.stage.RemoveBlock(this.stage.cells[r+1,c]);
                                this.stage.RemoveBlock(this.stage.cells[r+1,c+1]);
                            }
                            this.DoMissionTailEffect(
                                ((int)BlockType.Stand).ToString(),
                                masterCellBlock, () => { this.UpdateDashboard(); }
                            );
                        }
                    }
                }
                break;

            case BlockType.RedWoodbox:
            case BlockType.YellowWoodbox:
            case BlockType.GreenWoodbox:
            case BlockType.PurpleWoodbox:
                if (0 < normalSidePoint) {
                    BlockType targetBlockType = BlockType.None;
                    ClearType targetClearType = ClearType.None;
                    switch (toCell.block.type) {
                        case BlockType.RedWoodbox: targetBlockType = BlockType.Red; targetClearType = ClearType.RedWoodbox; break;
                        case BlockType.YellowWoodbox: targetBlockType = BlockType.Yellow; targetClearType = ClearType.YellowWoodbox; break;
                        case BlockType.GreenWoodbox: targetBlockType = BlockType.Green; targetClearType = ClearType.GreenWoodbox; break;
                        case BlockType.PurpleWoodbox: targetBlockType = BlockType.Purple; targetClearType = ClearType.PurpleWoodbox; break;
                    }
                    // 공격해오는 블럭이 노말레드이고 공격치가 0 이상이면...
                    if (targetBlockType == fromBlockType || isSpecialAttack) {
                        toCell.block.normalSidePoint = Math.Max(0, toCell.block.normalSidePoint - normalSidePoint);
                        if (toCell.block.IsDead) {
                            BlockController self = this.FindBlockController(toCell.block);
                            if (null != self)
                            {
                                self.Explode().Forget();
                            }
                            this.stage.RemoveBlock(toCell);
                            bool isUpdated0 = this.stage.ClearCountdown(ClearType.ColoredWoodbox); // 통합으로 카운트 하기 때문에 이 값을 차감함.
                            bool isUpdated1 = this.stage.ClearCountdown(targetClearType);
                            if (isUpdated0 || isUpdated1)
                                this.UpdateDashboard();
                        }
                        else
                        {
                            BlockController self = this.FindBlockController(toCell.block);
                            GameObject prefab = IngameEffectPrefabLoader.Instance
                                .GetBlockExplosionPrefab(toCell.block.type);
                            GameObject pangObj = Instantiate(prefab, self.gameObject.transform);
                            pangObj.AddComponent<SelfDestroy>();
                        }
                    }
                }
                break;

            case BlockType.Vault:
                if (0 < specialPoint && null != toCell.block.masterCell) {
                    Cell masterCell = toCell.block.masterCell;
                    if (-1 < masterCell.block.matchNumbers.IndexOf(this.stage.matchCount))
                        break;
                    masterCell.block.specialPoint = Math.Max(0, masterCell.block.specialPoint - specialPoint);
                    // 중복방지 넘버
                    masterCell.block.matchNumbers.Add(this.stage.matchCount);
                    // Debug.Log("CAN CELL " + cell.block.type + " " + cell.row + "x" + cell.col);
                    if (masterCell.block.IsDead) {
                        BlockController controller = this.FindBlockController(masterCell.block);
                        if (null != controller)
                        {
                            GameObject prefab =
                                IngameEffectPrefabLoader.Instance.GetBlockExplosionPrefab(masterCell.block.type);
                            Instantiate(prefab, controller.transform, true);
                            
                            controller.Explode().Forget();
                        }
                            
                        this.stage.RemoveBlock(masterCell);
                        // 슬레이브 셀도 삭제해야 한다.
                        int r = masterCell.row;
                        int c = masterCell.col;
                        this.stage.RemoveBlock(this.stage.cells[r,c+1]);
                        this.stage.RemoveBlock(this.stage.cells[r+1,c]);
                        this.stage.RemoveBlock(this.stage.cells[r+1,c+1]);
                        if (this.stage.ClearCountdown(ClearType.Vault))
                            this.UpdateDashboard();
                    }
                    else
                    {
                        BlockController controller = this.FindBlockController(masterCell.block);

                        GameObject prefab = IngameEffectPrefabLoader.Instance
                            .GetBlockExplosionPrefab(toCell.block.type);
                        GameObject pangObj = Instantiate(prefab, controller.gameObject.transform);
                        pangObj.AddComponent<SelfDestroy>();
                    }
                }
                break;
        }
    }

    private void DoMissionTailEffect(string targetMissionImageName, BlockController self, Action cb)
    {
        InGameController inGameController = GetComponent<InGameController>();
        if (inGameController) {
            foreach (IngameMissionUIView uiView in inGameController.SelectedUpperUIView.GetMissionViews()) {
                if (!uiView.gameObject.activeSelf) continue;
                if (uiView.IconImage.sprite == null) continue;
                if (uiView.IconType.Contains(targetMissionImageName)) {
                    GameObject tailPrefab = IngameEffectPrefabLoader.Instance.GetMissionTailPrefab();
                    GameObject tailObj = Instantiate(tailPrefab, self.transform);
                    Mission.OnTweenEffect(tailObj, self.transform, uiView.gameObject, uiView.IconImage, () => { cb?.Invoke(); });
                } else {
                    cb?.Invoke();
                }
            }
        }
    }

    private void AttackNormalSide(NormalMatchResult matchResult)
    {
        NormalMatch match = new NormalMatch(this.stage);
        List<Cell> sideCells;

        bool isNormalBlockExploded = false;
        
        if (match.AnalyseSide(matchResult, out sideCells)) {
            Cell me = matchResult.cells[0];
            foreach (Cell cell in sideCells) {
                if (null != cell.block) {
                    // 노말블럭에게 위협당하는 애니메이션을 넣어주고
                    if (cell.block.IsNormal) {
                        BlockController controller = this.FindBlockController(cell.block);
                        if (null != controller)
                            controller.ThreatedBySideAttack(me.row, me.col, 0.15f);
                        //StartCoroutine(controller.ThreatedBySideAttack(me.row, me.col, 0.15f));
                    }
                    if (null != cell.block && BlockState.Floating != cell.block.state && cell.block.IsMission) {
                        // 바리케이트가 있으면 공격을 안한다.
                        if (null != cell.topBlock && BlockType.Barricade == cell.topBlock.type)
                            continue;
                        this.AttackMission(cell, 1, 0, matchResult.blockType);

                        isNormalBlockExploded = true;
                    }
                }
            }

            if (isNormalBlockExploded)
            {
                CommonProcessController.MuteEffectSound("Ingame", 0);
                CommonProcessController.PlayEffectSound("Ingame", 18, 1.0f);
            }
        }
    }
    
    public async UniTask AttackSpecial(Cell me, BlockType targetBlockType = BlockType.None, List<Block> excludedBlocks = null)
    {
        void _AttackBlock(Cell cell, ref List<Cell> specialCells)
        {
            if (null != cell.topBlock) {
                // 탑블록을 처리하고
                Block block = cell.topBlock;
                if (0 < block.specialPoint)
                    block.specialPoint = Math.Max(0, block.specialPoint - 1);
                else if (0 < block.normalPoint)
                    block.normalPoint = Math.Max(0, block.normalPoint - 1);
                if (block.IsDead) {
                    if (this.RemoveTopBlock(cell))
                        this.UpdateDashboard();
                } else {
                    TopController controller = this.FindTopController(block);
                    if (null != controller)
                        controller.showBarricadeChangeEffect();
                }
            } else {
                // 미들블록(기본블록)을 처리한다.
                Block block = cell.block;
                if (null != block) { // && BlockState.Fixed == block.state) {
                    // if (me != cell) { <- 필요충분조건이다.

                    if (block.IsSpecial && shootingState==ShootingState.Play)
                    {
                        BlockController controller = this.FindBlockController(cell.block);
                        if (null != controller)
                        {
                            controller.Explode().Forget();
                        }
                        this.stage.RemoveBlock(cell);
                        // 스코어를 반영한다.
                        if (null != this.delegateCalcScore)
                            this.delegateCalcScore(this, Stage.SCORE_NORMAL);
                        if (this.stage.ClearCountdownForNormalBlock(block))
                            this.UpdateDashboard();
                    }
                    else if (block.IsSpecial && shootingState==ShootingState.Idle)
                        specialCells.Add(cell);
                    else if (block.IsMission)
                        this.AttackMission(cell, 1, 1, BlockType.None, true);
                    else {
                        if (0 < block.specialPoint)
                            block.specialPoint = Math.Max(0, block.specialPoint - 1);
                        else if (0 < block.normalSidePoint)
                            block.normalSidePoint = Math.Max(0, block.normalSidePoint - 1);
                        else if (0 < block.normalPoint)
                            block.normalPoint = Math.Max(0, block.normalPoint - 1);
                        if (block.IsDead) {
                            BlockController controller = this.FindBlockController(cell.block);
                            if (null != controller)
                            {
                                controller.Explode().Forget();
                            }
                            this.stage.RemoveBlock(cell);
                            // 스코어를 반영한다.
                            if (null != this.delegateCalcScore)
                                this.delegateCalcScore(this, Stage.SCORE_NORMAL);
                            if (this.stage.ClearCountdownForNormalBlock(block))
                                this.UpdateDashboard();
                        }
                    }
                    if (block.IsNormal)
                        _AttackBottomBlock(cell);
                } else
                    _AttackBottomBlock(cell);
            }
        }

        void _AttackBottomBlock(Cell cell) {
            // 보텀(뽁뽁이,잔디)블록
            Block block = cell.bottomBlock;
            if (null != block) {
                block.normalPoint = Math.Max(0, block.normalPoint - 1);

                if (block.IsDead)
                {
                    if (this.RemoveBottomBlock(cell))
                        this.UpdateDashboard();
                }
                else
                {
                    BottomController controller = this.FindBottomController(block);
                    controller.BottomRemoveEffect(block.type);
                }
            }
        }

        // 연쇄폭발을 리턴하게 된다.
        void _AttackBlocks(List<Cell> cells,ref List<Cell> specialCells) {
            if (null != cells)
            {
                bool isMissionBlockExploded = cells.Exists(x => x.block != null && x.block.IsMission);
                if (isMissionBlockExploded)
                {
                    CommonProcessController.MuteEffectSound("Ingame", 0);
                    CommonProcessController.PlayEffectSound("Ingame", 18, 1.0f);
                }
                
                foreach (Cell cell in cells)
                    _AttackBlock(cell, ref specialCells);
            }
                
        }

        // 벽공격
        void _AttackWalls(List<WallResult> walls) {
            if (null != walls)
                foreach (WallResult wr in walls) {
                    string name = null;
                    if (WallResultType.Vertical == wr.type)
                        name = "WV" + wr.cell.rightBlock.ToString();
                    else if (WallResultType.Horizontal == wr.type)
                        name = "WH" + wr.cell.belowBlock.ToString();
                    if (null != name) {
                        GameObject wallObject = this.transform.Find(name)?.gameObject;
                        if (WallResultType.Vertical == wr.type)
                            wr.cell.rightBlock = null;
                        else
                            wr.cell.belowBlock = null;
                        Destroy(wallObject);
                    }
                }
        }

        List<TemporatedCell> temps = new List<TemporatedCell>();
        // 셀내 블록을 락 상태로
        void _beginLock() {
            this.specialAttackingCount++;
        }
        void _AddLock(List<Cell> cells) {

            if (null != cells)
                foreach (Cell c in cells)
                    if (null != c.block) {
                        var tc = new TemporatedCell(c);
                        tc.Enter();
                        temps.Add(tc);
                    }
        }
        // 언락한다.
        void _UnlockAll() {
            this.specialAttackingCount--;
            foreach (var tc in temps)
                tc.Leave();
        }

        this.dirtyCount++;

        // 진입조건이 안되면 리턴
        if (null == me.block || !me.block.IsSpecial)
            throw new OperationCanceledException();

        // CAUTION: 터트려야 할 때는 고정상태로 하여 중력효과를 받지 못하도록 설정한다.
        //          모든 특수블럭 터트림은 일단 플로팅 상태로 셋팅..
        me.block.attr = BlockAttr.Fixable;
        me.block.state = BlockState.Floating;

        // CAUTION: 중력효과가 진행중이라면 대기한다. 조정이 필요하다.
        // while (0 < BlockController.gravityCount)
        //     yield return null;

        SpecialMatch match;
        SpecialMatchResult result;
        // 미러볼 잔재 리스트
        List<GameObject> soulObj = new List<GameObject>();
        List<Cell> specialCells = new List<Cell>();
        switch (me.block.type) {
            case BlockType.PaperPlane:
                match = new SpecialMatch(this.stage, excludedBlocks);
                result = match.Analyse(me);
                _AttackBottomBlock(me);
                // cells1 : 표적 셀 1개
                // cells2 : 제자리 4방 셀
                _AddLock(result.cells1);
                _AddLock(result.cells2);
                _beginLock();
                try {
                    BlockController controllerPaperPlane = this.FindBlockController(me.block);
                    if (null != controllerPaperPlane)
                    {
                        await controllerPaperPlane.Explode();
                    }

                    //await (controllerPaperPlane.Explode());
                    this.stage.RemoveBlock(me);
                    // cells2 : 제자리 4방 셀
                    _AttackBlocks(result.cells2, ref specialCells);
                    if (null != result.cells1 && 0 < result.cells1.Count)
                        await this.FlyPaperAirplane(me, result.cells1[0]);
                        //await (this.FlyPaperAirplane(me, result.cells1[0]));
                    // cells1 : 표적 셀 1개
                    _AttackBlocks(result.cells1, ref specialCells);
                    _AttackWalls(result.walls);
                    // _RemoveBottomBlocks(result.bottoms);
                    this.stage.skillFever += Stage.SKILLFEVER_USE_SEPCIAL;
                    this.stage.ClearCountdown(ClearType.PaperPlane);
                } finally {
                    _UnlockAll();
                }
                break;
            case BlockType.Rocket:
                BlockController controllerRocket = this.FindBlockController(me.block);
                if (null != controllerRocket)
                {
                    BlockController.CrossEffect(me);
                    
                    CommonProcessController.MuteEffectSound("Ingame", 0);
                    CommonProcessController.PlayEffectSound("Ingame", 7);
                    
                    await controllerRocket.Explode();
                }
                
                match = new SpecialMatch(this.stage, excludedBlocks);
                result = match.Analyse(me);
                _AttackBottomBlock(me);
                _AddLock(result.cells1);
                _beginLock();
                try {
                    this.stage.RemoveBlock(me);
                    _AttackBlocks(result.cells1, ref specialCells);
                    _AttackWalls(result.walls);
                    // _RemoveBottomBlocks(result.bottoms);
                    this.stage.skillFever += Stage.SKILLFEVER_USE_SEPCIAL;
                    this.stage.ClearCountdown(ClearType.Rocket);
                } finally {
                    _UnlockAll();
                }
                break;
            case BlockType.Bomb:
                BlockController controllerBomb = this.FindBlockController(me.block);

                if (null != controllerBomb)
                {
                    BlockController.BombEffect(me);
                    await controllerBomb.Explode(0.2f);
                }

                match = new SpecialMatch(this.stage, excludedBlocks);
                result = match.Analyse(me);
                // 바탕에 깔린 블럭 삭제하고
                _AttackBottomBlock(me);
                _AddLock(result.cells1);
                _beginLock();
                try {
                    this.stage.RemoveBlock(me);
                    _AttackBlocks(result.cells1, ref specialCells);
                    _AttackWalls(result.walls);
                    // _RemoveBottomBlocks(result.bottoms);
                    this.stage.skillFever += Stage.SKILLFEVER_USE_SEPCIAL;
                    this.stage.ClearCountdown(ClearType.Bomb);
                } finally {
                    _UnlockAll();
                }
                break;
            case BlockType.Mirrorball:
                switch (CommonProcessController.GetNameString())
                {
                    case CommonProcessController.KWONEUNBIINFO:
                    case CommonProcessController.IKONINFO: 
                        var prefabObject = BlockController.MirrorBallIdleEffect(me);
                        
                        // 매칭 진행하고
                        match = new SpecialMatch(this.stage, excludedBlocks);
                        result = match.Analyse(me, 0 , targetBlockType);
                            
                        _AddLock(result.cells1);
                        _beginLock();
                                
                        try {
                            if (null != result.cells1)
                            {
                                UniTask[] coroutines = new UniTask[result.cells1.Count];
                                for (var index = 0; index < result.cells1.Count; index++)
                                {
                                    var c = result.cells1[index];
                                    _AttackBlock(c, ref specialCells);

                                    // 미러볼 잔재 이펙트 생성
                                    var soul = BlockController.MirrorBallSoulEffect(me);
                                    soulObj.Add(soul);

                                    await UniTask.Delay(TimeSpan.FromSeconds(0.1f));
                                    coroutines[index] = this.VanishBlackHole(me, c, soul);
                                }
                                foreach (var routine in coroutines)
                                {
                                    await routine;
                                }
                            }
                            
                            // 볼을 터트리고
                            await UniTask.Delay(TimeSpan.FromSeconds(0.2f));

                            BlockController controller5 = this.FindBlockController(me.block);
                            if (null != controller5)
                                await controller5.Explode();
                            this.stage.RemoveBlock(me);
                            _AttackBottomBlock(me);
                            _AttackWalls(result.walls);
                            // _RemoveBottomBlocks(result.bottoms);
                            // 미러볼만 사이드어텍을 넣어준다.
                            if (null != result.sides)
                                foreach (var side in result.sides) {
                                    foreach (var cell in side.toCells) {
                                        if (null != cell.block && (null == cell.topBlock || BlockType.Barricade != cell.topBlock.type)) {
                                            // Debug.Log(cell.block.type);
                                            this.AttackMission(cell, 1, 0, side.fromBlockType);
                                        }
                                    }
                                }
                            this.stage.skillFever += Stage.SKILLFEVER_USE_SEPCIAL;
                            this.stage.ClearCountdown(ClearType.Mirrorball);
                        } finally {
                            Destroy(prefabObject);
                            _UnlockAll();
                        };
                        break;
                    default:
                        await ExplodeMirrorBallAteez(me, targetBlockType, excludedBlocks);
                        break;
                }
                break;
            case BlockType.ShootingStar:
                shootingState = ShootingState.Play;
                var loopCount = (int)Math.Max(
                    Math.Max(stage.rowCount - me.row, stage.colCount - me.col),
                    Math.Max(me.row, me.col)
                );
                
                BlockController controllerShootingStar = this.FindBlockController(me.block);

                if (null != controllerShootingStar)
                {
                    BlockController.ShootingStarEffect(me);
                    
                    // 자기 자신 폭팔할때 잠깐 제외
                    shootingState = ShootingState.Idle;
                    await controllerShootingStar.Explode(0.2f);
                    shootingState = ShootingState.Play;
                }
                // 기모으기 대기시간
                CommonProcessController.MuteEffectSound("Ingame", 0);
                CommonProcessController.PlayEffectSound("Ingame", 15);
                await UniTask.Delay(TimeSpan.FromSeconds(1.0f));
                
                CommonProcessController.MuteEffectSound("Ingame", 0);
                CommonProcessController.PlayEffectSound("Ingame", 17);

                for (var i=0; i<=loopCount; i++ )
                {
                    match = new SpecialMatch(this.stage, excludedBlocks);
                    result = match.Analyse(me,i);
                    
                    _AttackBottomBlock(me);
                    _AddLock(result.cells1);
                    _beginLock();
                    try {
                        CommonProcessController.PlayEffectSound("Ingame", 14);
                        
                        // 마지막일때 사라지게 수정
                        if (i == loopCount)
                        {
                            this.stage.RemoveBlock(me);
                        }
                        _AttackBlocks(result.cells1, ref specialCells);
                        _AttackWalls(result.walls);
                        // 연쇄폭발 대기시간
                        await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
                        // _RemoveBottomBlocks(result.bottoms);
                        this.stage.skillFever += Stage.SKILLFEVER_USE_SEPCIAL;
                        this.stage.ClearCountdown(ClearType.ShootingStar);
                    } finally
                    {
                        _UnlockAll();
                    }
                }
                shootingState = ShootingState.Idle;
                break;

            // 3줄짜리 크로스가 생긴다.
            case BlockType.RocketRocket:
                BlockController.AddCombineEffect(
                    BlockType.Rocket,
                    BlockType.Rocket,
                    FindBlockController(me.block).transform);
                // 0.5초간 애니메이션이 있고 난 후
                BlockController controllerRocketRocket = this.FindBlockController(me.block);
                await controllerRocketRocket.Explode();
                match = new SpecialMatch(this.stage, excludedBlocks);
                result = match.Analyse(me);
                _AttackBottomBlock(me);
                _AddLock(result.cells1);
                _beginLock();
                try {
                    for (int c = me.col-1; c<=me.col+1; c++)
                        for (int r = me.row-1; r<=me.row+1; r++)
                            if (0<=c && c<this.stage.colCount && 0<=r && r<this.stage.rowCount)
                                BlockController.CrossEffect(this.stage.cells[r,c]);
                    
                    CommonProcessController.MuteEffectSound("Ingame", 0);
                    CommonProcessController.PlayEffectSound("Ingame", 7);
                    
                    this.stage.RemoveBlock(me);
                    _AttackBlocks(result.cells1, ref specialCells);
                    _AttackWalls(result.walls);
                    // _RemoveBottomBlocks(result.bottoms);
                    this.stage.skillFever += Stage.SKILLFEVER_USE_COMBINED_SPECIAL;
                } finally {
                    _UnlockAll();
                }
                break;
            // 타겟이 날라가서 그 위치에 크로스가 발생
            case BlockType.RocketPaperPlane:
                BlockController.AddCombineEffect(
                    BlockType.Rocket,
                    BlockType.PaperPlane,
                    FindBlockController(me.block).transform);
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
                //yield return new WaitForSeconds(0.5f);

                match = new SpecialMatch(this.stage, excludedBlocks);
                result = match.Analyse(me);
                _AttackBottomBlock(me);
                _AddLock(result.cells1);
                _AddLock(result.cells2);
                _AddLock(result.cells3);
                _beginLock();
                try {
                    BlockController controller6 = this.FindBlockController(me.block);
                    if (null != controller6)
                    {
                        await controller6.Explode();
                    }

                    this.stage.RemoveBlock(me);
                    // cells1 : 날라가는 표적 셀
                    // cells2 : 제자리 4방 셀
                    // cells3 : 십자폭탄이 터지는 셀
                    _AttackBlocks(result.cells2, ref specialCells);
                    if (null != result.cells1 && 0 < result.cells1.Count) {
                        await (this.FlyPaperAirplane(me, result.cells1[0]));
                        // 십자폭탄 효과를 넣어주고
                        BlockController.CrossEffect(result.cells1[0]);
                        
                        CommonProcessController.MuteEffectSound("Ingame", 0);
                        CommonProcessController.PlayEffectSound("Ingame", 7);
                    }
                    _AttackBlocks(result.cells1, ref specialCells);
                    _AttackBlocks(result.cells3, ref specialCells);
                    _AttackWalls(result.walls);
                    // _RemoveBottomBlocks(result.bottoms);
                    this.stage.skillFever += Stage.SKILLFEVER_USE_COMBINED_SPECIAL;
                } finally {
                    _UnlockAll();
                }
                break;
            // 5줄짜리 크로스가 생긴다.
            case BlockType.RocketBomb:
                BlockController.AddCombineEffect(
                    BlockType.Rocket,
                    BlockType.Bomb,
                    FindBlockController(me.block).transform);
                // 0.5초간 애니메이션이 있고 난 후
                BlockController controller3 = this.FindBlockController(me.block);
                if (null != controller3)
                {
                    await controller3.Explode();
                }

                match = new SpecialMatch(this.stage, excludedBlocks);
                result = match.Analyse(me);
                _AttackBottomBlock(me);
                _AddLock(result.cells1);
                _beginLock();
                try {
                    for (int c = me.col-2; c<=me.col+2; c++)
                        for (int r = me.row-2; r<=me.row+2; r++)
                            if (0<=c && c<this.stage.colCount && 0<=r && r<this.stage.rowCount)
                                BlockController.CrossEffect(this.stage.cells[r,c]);
                    
                    CommonProcessController.MuteEffectSound("Ingame", 0);
                    CommonProcessController.PlayEffectSound("Ingame", 7);
                    
                    this.stage.RemoveBlock(me);
                    _AttackBlocks(result.cells1, ref specialCells);
                    _AttackWalls(result.walls);
                    // _RemoveBottomBlocks(result.bottoms);
                    this.stage.skillFever += Stage.SKILLFEVER_USE_COMBINED_SPECIAL;
                } finally {
                    _UnlockAll();
                }
                break;
            // 가장 많은 개수의 일반 블록 1종을 전부 십자블록으로 바꾸고 한 번에 폭파
            case BlockType.RocketMirrorball:
                BlockController.AddCombineEffect(
                    BlockType.Rocket,
                    BlockType.Mirrorball,
                    FindBlockController(me.block)?.transform);
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
                //yield return new WaitForSeconds(0.5f);

                // 미러볼 발사 이팩트
                var rocketMirrorballIdle = BlockController.MirrorBallIdleEffect(me);
                
                // 매칭 진행하고
                match = new SpecialMatch(this.stage, excludedBlocks);
                result = match.Analyse(me);
                // cells1 : 가장 많은 일반 블록들
                // cells2 : 십자폭탄으로 터지는 블록들
                _AddLock(result.cells1);
                _AddLock(result.cells2);
                _beginLock();
                try {
                    var controllers = new List<BlockController>();
                    try {
                        if (null != result.cells1)
                            foreach (Cell c in result.cells1) {
                                await (this.ShootMissile(me, c));
                                // yield return new WaitForSeconds(0.2f);
                                _AttackBlock(c, ref specialCells);
                                // CAUTION: 여기에 십자 폭탄을 FAKE로 넣어준다.
                                Block crossBlock = Block.FactorySpecial(BlockType.Rocket);
                                controllers.Add(BlockController.Create(crossBlock, c.row, c.col));
                                // this.stage.AddBlock(c, crossBlock); <- 실제로 Stage에 넣어주고 있지는 않는다.
                            }
                        // 볼을 터트리고
                        await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
                        //yield return new WaitForSeconds(0.2f);
                        BlockController controller7 = this.FindBlockController(me.block);
                        if (null != controller7)
                        {
                            await controller7.Explode();
                        }

                        // Destroy(this.FindBlockController(me.block)?.gameObject);
                        this.stage.RemoveBlock(me);
                        _AttackBottomBlock(me);
                        // 십자폭탄으로 터지는 블록
                        _AttackBlocks(result.cells2, ref specialCells);
                    } finally {
                        // Fake로 넣어줬던 Cross를 폭파시켜 삭제해준다.
                        foreach (var ctr in controllers)
                        {
                            if (null != ctr)
                            {
                                BlockController.CrossEffect(me,ctr);
                                
                                CommonProcessController.MuteEffectSound("Ingame", 0);
                                CommonProcessController.PlayEffectSound("Ingame", 7);
                                
                                ctr.Explode().Forget();
                            }
                        }
                    }
                    // 기타등등
                    _AttackWalls(result.walls);
                    // _RemoveBottomBlocks(result.bottoms);
                    this.stage.skillFever += Stage.SKILLFEVER_USE_SEPCIAL;;
                } finally {
                    Destroy(rocketMirrorballIdle);
                    _UnlockAll();
                }
                break;
            // 3개의 타겟이 날라간다.
            case BlockType.PaperPlanePaperPlane:
                BlockController.AddCombineEffect(
                    BlockType.PaperPlane,
                    BlockType.PaperPlane,
                    FindBlockController(me.block)?.transform);
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
                //yield return new WaitForSeconds(0.5f);

                match = new SpecialMatch(this.stage, excludedBlocks);
                result = match.Analyse(me);
                _AddLock(result.cells1);
                _AddLock(result.cells2);
                _beginLock();
                try {
                    BlockController controller8 = this.FindBlockController(me.block);
                    if (null != controller8)
                    {
                        await controller8.Explode();
                    }

                    _AttackBottomBlock(me);
                    this.stage.RemoveBlock(me);
                    // cells2 : 제자리 4방 셀
                    _AttackBlocks(result.cells2, ref specialCells);
                    // cells1 : 표적 셀 3개
                    var cors = new UniTask?[3] {
                        0<result.cells1.Count && null != result.cells1[0] ? this.FlyPaperAirplane(me, result.cells1[0]) : null,
                        1<result.cells1.Count && null != result.cells1[1] ? this.FlyPaperAirplane(me, result.cells1[1]) : null,
                        2<result.cells1.Count && null != result.cells1[2] ? this.FlyPaperAirplane(me, result.cells1[2]) : null
                    };
                    foreach (UniTask cor in cors)
                        if (null != cors)
                            await cor;
                    _AttackBlocks(result.cells1, ref specialCells);
                    _AttackWalls(result.walls);
                    // _RemoveBottomBlocks(result.bottoms);
                    this.stage.skillFever += Stage.SKILLFEVER_USE_COMBINED_SPECIAL;
                } finally {
                    _UnlockAll();
                }
                break;
            // 타겟이 날라가서 폭탄을 터트린다.
            case BlockType.PaperPlaneBomb:
                BlockController.AddCombineEffect(
                    BlockType.PaperPlane,
                    BlockType.Bomb,
                    FindBlockController(me.block)?.transform);
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
                //yield return new WaitForSeconds(0.5f);

                match = new SpecialMatch(this.stage, excludedBlocks);
                result = match.Analyse(me);
                _AttackBottomBlock(me);
                // cells1 : 표적 셀 1개
                // cells2 : 제자리 4방 셀
                // cells3 : 폭탄이 터진 셀
                _AddLock(result.cells1);
                _AddLock(result.cells2);
                _AddLock(result.cells3);
                _beginLock();
                try {
                    BlockController controller9 = this.FindBlockController(me.block);
                    if (null != controller9)
                    {
                        await controller9.Explode();
                    }

                    this.stage.RemoveBlock(me);
                    _AttackBlocks(result.cells2, ref specialCells);
                    if (null != result.cells1 && 0 < result.cells1.Count) {
                        await (this.FlyPaperAirplane(me, result.cells1[0]));
                        BlockController.BombEffect(result.cells1[0]);
                        //await UniTask.Delay(TimeSpan.FromSeconds(0.8f));
                        //yield return new WaitForSeconds(0.8f);
                    }
                    _AttackBlocks(result.cells1, ref specialCells);
                    _AttackBlocks(result.cells3, ref specialCells);
                    _AttackWalls(result.walls);
                    // _RemoveBottomBlocks(result.bottoms);
                    this.stage.skillFever += Stage.SKILLFEVER_USE_COMBINED_SPECIAL;
                } finally {
                    _UnlockAll();
                }
                break;
            // 가장 많은 개수의 일반 블록 1종을 전부 티겟블록으로 바꾸고 한 번에 폭파
            case BlockType.PaperPlaneMirrorball:
                BlockController.AddCombineEffect(
                    BlockType.PaperPlane,
                    BlockType.Mirrorball,
                    FindBlockController(me.block)?.transform);
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
                //yield return new WaitForSeconds(0.5f);

                match = new SpecialMatch(this.stage, excludedBlocks);
                result = match.Analyse(me);
                _AttackBottomBlock(me);
                // cells1 : 가장 많은 수량의 일반셀들 -> 1차표적셀
                // cells2 : 제자리 4방 셀
                // cells3 : cells1에서 날라간서 맞출 타겟 셀들 -> 2차표적셀
                // cells4 : cells1위치에서 4방향 블럭이 있는 셀들 <- 사용않함
                // cells5 : cells3위치에서 4방향 블럭이 있는 셀들
                _AddLock(result.cells1);
                _AddLock(result.cells2);
                _AddLock(result.cells3);
                //_AddLock(result.cells4);
                _AddLock(result.cells5);
                _beginLock();
                try {
                    var paperPlaneMirrorballIdle = BlockController.MirrorBallIdleEffect(me);
                    
                    // 임시 블럭 컨트롤러 리스트
                    var controllers = new List<BlockController>();
                    try {
                        if (null != result.cells1)
                            foreach (Cell c in result.cells1) {
                                await (this.ShootMissile(me, c));
                                _AttackBlock(c, ref specialCells);
                                // CAUTION: 여기에 FAKE로 넣어준다.
                                Block crossTarget = Block.FactorySpecial(BlockType.PaperPlane);
                                controllers.Add(BlockController.Create(crossTarget, c.row, c.col));
                                // this.stage.AddBlock(c, crossBlock); <- 실제로 Stage에 넣어주고 있지는 않는다.
                            }
                        // 날리고
                        await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
                        //yield return new WaitForSeconds(0.2f);
                        BlockController controller10 = this.FindBlockController(me.block);
                        if (null != controller10)
                        {
                            await controller10.Explode();
                        }

                        // Destroy(this.FindBlockController(me.block)?.gameObject);
                        this.stage.RemoveBlock(me);
                        _AttackBlocks(result.cells2, ref specialCells);
                    } finally {
                        // Fake로 넣어줬던 블록을 삭제해준다.
                        foreach (var ctr in controllers)
                        {
                            ctr.Explode().Forget();
                        }

                        Destroy(paperPlaneMirrorballIdle);
                    }
                    // 종이비행기 날리기~~
                    var cors = new List<UniTask>();
                    for (int i = 0; i < result.cells1.Count; i++)
                        if (null != result.cells3 && i < result.cells3.Count) {
                            Cell from = result.cells1[i];
                            Cell to = result.cells3[i];
                            cors.Add(this.FlyPaperAirplane(from, to));
                        }
                    foreach (UniTask cor in cors)
                        await cor;
                    // 비행기 자리 폭파
                    _AttackBlocks(result.cells3, ref specialCells);
                    // cells5 : cells3위치에서 4방향 블럭이 있는 셀들
                    _AttackBlocks(result.cells5, ref specialCells);
                    // 벽처리
                    _AttackWalls(result.walls);
                    // _RemoveBottomBlocks(result.bottoms);
                    this.stage.skillFever += Stage.SKILLFEVER_USE_COMBINED_SPECIAL;
                } finally {
                    _UnlockAll();
                }
                break;
            // 9x9 영역을 폭파~
            case BlockType.BombBomb:
                BlockController.AddCombineEffect(
                    BlockType.Bomb,
                    BlockType.Bomb,
                    FindBlockController(me.block)?.transform);
                BlockController controller11 = this.FindBlockController(me.block);
                if (null != controller11)
                {
                    await controller11.Explode();
                }

                match = new SpecialMatch(this.stage, excludedBlocks);
                result = match.Analyse(me);
                _AttackBottomBlock(me);
                _AddLock(result.cells1);
                _beginLock();
                try {
                    this.stage.RemoveBlock(me);
                    _AttackBlocks(result.cells1, ref specialCells);
                    _AttackWalls(result.walls);
                    // _RemoveBottomBlocks(result.bottoms);
                    this.stage.skillFever += Stage.SKILLFEVER_USE_COMBINED_SPECIAL;
                } finally {
                    _UnlockAll();
                }
                break;
            // 가장 많은 개수의 일반 블록 1종을 전부 폭탄블록으로 바꾸고 한 번에 폭파
            case BlockType.BombMirrorball:
                BlockController.AddCombineEffect(
                    BlockType.Bomb,
                    BlockType.Mirrorball,
                    FindBlockController(me.block)?.transform);
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
                //yield return new WaitForSeconds(0.5f);

                // 미러볼 발사 이팩트
                var bombMirrorballIdle = BlockController.MirrorBallIdleEffect(me);
                
                // 매칭 진행하고
                match = new SpecialMatch(this.stage, excludedBlocks);
                result = match.Analyse(me);
                // cells1 : 가장 많은 수량의 일반셀들 -> 1차표적셀
                // cells2 : 폭탄으로 제거된 셀들
                _AddLock(result.cells1);
                _AddLock(result.cells2);
                _beginLock();
                try {
                    var controllers = new List<BlockController>();
                    try {
                        if (null != result.cells1)
                            foreach (Cell c in result.cells1) {
                                await (this.ShootMissile(me, c));
                                _AttackBlock(c, ref specialCells);
                                // CAUTION: 여기에 십자 폭탄을 FAKE로 넣어준다.
                                Block crossBlock = Block.FactorySpecial(BlockType.Bomb);
                                controllers.Add(BlockController.Create(crossBlock, c.row, c.col));
                                // this.stage.AddBlock(c, crossBlock); <- 실제로 Stage에 넣어주고 있지는 않는다.
                            }
                        // 볼을 터트리고
                        await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
                        //yield return new WaitForSeconds(0.2f);
                        BlockController controller12 = this.FindBlockController(me.block);
                        if (null != controller12)
                        {
                            await controller12.Explode();
                        }

                        // Destroy(this.FindBlockController(me.block)?.gameObject);
                        this.stage.RemoveBlock(me);
                        _AttackBottomBlock(me);
                        _AttackBlocks(result.cells2, ref specialCells);
                    } finally {
                        // Fake로 넣어줬던 Bomb를 폭파시켜 삭제해준다.
                        foreach (var ctr in controllers)
                        {
                            if (null != ctr)
                            {
                                BlockController.BombEffect(me,ctr);
                                ctr.Explode().Forget();
                            }
                        }
                    }
                    _AttackWalls(result.walls);
                    // _RemoveBottomBlocks(result.bottoms);
                    this.stage.skillFever += Stage.SKILLFEVER_USE_COMBINED_SPECIAL;
                } finally {
                    Destroy(bombMirrorballIdle);
                    _UnlockAll();
                }
                break;
            // 피날레와 동일하다.
            case BlockType.MirrorballMirrorball:
                BlockController.AddCombineEffect(
                    BlockType.Mirrorball,
                    BlockType.Mirrorball,
                    FindBlockController(me.block)?.transform);
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
                // 블랙홀 폭발 이펙트는 없지만 Combine이미지 삭제용
                BlockController controller14 = this.FindBlockController(me.block);
                if (null != controller14)
                {
                    await controller14.Explode();
                }
                match = new SpecialMatch(this.stage, excludedBlocks);
                result = match.Analyse(me);
                _AttackBottomBlock(me);
                _AddLock(result.cells1);
                _beginLock();
                try {
                    this.stage.RemoveBlock(me);
                    _AttackBlocks(result.cells1, ref specialCells);
                    _AttackWalls(result.walls);
                    // _RemoveBottomBlocks(result.bottoms);
                    this.stage.skillFever += Stage.SKILLFEVER_USE_SEPCIAL;
                } finally {
                    _UnlockAll();
                }
                break;

            // 카드스킬블록
            case BlockType.CardSkill: {
                CardSkillMatch match2 = new CardSkillMatch(this.stage, excludedBlocks);
                CardSkillMatchResult result2 = match2.Analyse3(me);
                _AttackBottomBlock(me);
                _AddLock(result2.cells);
                _beginLock();
                try {
                    // 제거 직전 깜빡이 효과 보여주고
                    var removeEffectTexture = me.block.skillBlockBeforeRemoveEffectTexture;
                    foreach (Cell cell in result2.cells)
                    {
                        BlockController blockController = this.FindBlockController(cell.block);
                        if (blockController == null)
                            continue;

                        GameObject effect = Resources.Load("Blocks/CardSkill/BeforeRemoveSkillBlock") as GameObject;
                        GameObject effectObj = Instantiate(effect, blockController.transform);
                        effectObj.name = "BeforeRemoveSkillBlock";

                        Scene scene = SceneManager.GetActiveScene();
                        if (scene.name.Contains("Test")) {
                            SpriteRenderer effectSr = effectObj.transform.Find("Front").GetComponent<SpriteRenderer>();
                            Sprite dummyImage = Resources.Load<Sprite>("Blocks/CardSkill/block_tile_emerald");
                            effectSr.sprite = dummyImage;
                            effectSr.transform.localScale = new Vector3( 1, 1, 1.0f);
                        } else {
                            SpriteRenderer effectSr = effectObj.transform.Find("Front").GetComponent<SpriteRenderer>();
                            effectSr.sprite = Sprite.Create(
                                removeEffectTexture,
                                new Rect(0.0f, 0.0f, removeEffectTexture.width, removeEffectTexture.height),
                                new Vector2(0.5f, 0.5f)
                            );
                            effectSr.transform.localScale = new Vector3(1, 1, 1.0f);
                        }
                        SelfDestroy selfDestroy = effectObj.AddComponent<SelfDestroy>();
                        selfDestroy.interval = 1.5f;
                    }

                    await UniTask.Delay(TimeSpan.FromSeconds(0.8f));

                    if (me.block.skillCardIndex.HasValue)
                    {
                        BroadcastTunnel<string, int>.Notify(
                            "com.snowballs.UseSkillCard", 
                            me.block.skillCardIndex.Value
                        );
                    }
                    
                    foreach (Cell cell in result2.cells)
                    {
                        if (cell.block == null)
                        {
                            // SBDebug.Log(
                            //     string.Format("row : {0}, col : {1}에 대한 블록 정보를 찾을 수 없음", cell.row, cell.col)
                            // );
                            continue;
                        }
                        GameObject skillBlockExplodePrefab = IngameEffectPrefabLoader.Instance.GetSkillBlockExplosionPrefab();
                        GameObject skillBlockExplodeObj = Instantiate(skillBlockExplodePrefab);

                        if (cell.block != null)
                        {
                            if (this.FindBlockController(cell.block) != null)
                            {
                                skillBlockExplodeObj.transform.position = this.FindBlockController(cell.block).transform.position;
                        
                                SelfDestroy selfDestroy = skillBlockExplodeObj.AddComponent<SelfDestroy>();
                                selfDestroy.interval = 1.4f;
                            }
                        }
                    }
                    
                    // 카드스킬블록을 제거하고
                    BlockController controller13 = this.FindBlockController(me.block);
                    if (null != controller13)
                    {
                        await controller13.Explode();
                    }

                    this.stage.RemoveBlock(me);
                    // if (AudioController.IsSoundEffect)
                    //     this.audioRemoveBlock.Play();
                    _AttackBlocks(result2.cells, ref specialCells);

                    foreach (Cell cell in specialCells)
                    {
                        BlockController bc = this.FindBlockController(cell.block);
                        
                        GameObject skillBlockExplodePrefab = IngameEffectPrefabLoader.Instance.GetSkillBlockExplosionPrefab();
                        GameObject skillBlockExplodeObj = Instantiate(skillBlockExplodePrefab);
                        
                        skillBlockExplodeObj.transform.position = bc.transform.position;
                        
                        SelfDestroy selfDestroy = skillBlockExplodeObj.AddComponent<SelfDestroy>();
                        selfDestroy.interval = 1.4f;
                    }
                    
                    this.stage.skillFever += Stage.SKILLFEVER_USE_SEPCIAL;
                } finally {
                    _UnlockAll();
                }
            }
            break;

            default:
                Debug.Log("INVALIDATE BLOCK TYPE " + me.block.type);
                break;
        }

        // 특수폭탄터지고 대기시간
        await UniTask.Delay(TimeSpan.FromSeconds(0.25f));

        // 클리어 조건 카운트다운
        this.stage.ClearCountdown(ClearType.Special);
        this.UpdateDashboard(); // 피버카운트 반영이 필요하므로 반드시 업데이트가 되는 조건이다.

        // 특수블록을 연쇄폭발시킨다.
        if (0 < specialCells.Count) {
            SBDebug.Log("SJW Async Start");
            //yield return new WaitForSeconds(0.2f);
            // 특수폭탄에 의해 터지는 특수폭탄을 미리 안터지는 목록에 넣은 후
            foreach (Cell cell in specialCells) {
                // 순차적으로 특수블록을 터트린다.
                // 터트리는 블록 자체는 비포함에 넣고
                if (null == excludedBlocks)
                    excludedBlocks = new List<Block>();
                excludedBlocks.Add(cell.block);
            }
            // 순차적으로 모두 터트린다.
            List<UniTask> cors = new();
            foreach (Cell cell in specialCells)
            {
                cors.Add(this.AttackSpecial(cell, BlockType.None, excludedBlocks));
            }
            
            // 모두 마무리 될 때까지 기다린다.
            foreach (UniTask cor in cors)
            {
                await cor.TimeoutWithoutException(new TimeSpan(0, 0, 0, 10));
            }
        }
    }
    
    public async UniTask MatchAndGravity()
    {
        this.dirtyCount++;
        this.gravityStackCount++;
        try {

            //Debug.Log("Gravity Stack Count = " + this.gravityStackCount);
            // if (2 < this.gravityStackCount)
            //     yield break;

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

                        // 보안관을 심어준다.
                        if (1 == this.gravityStackCount && 0 < this.stage.turn) {
                            var srcCells = new Dictionary<string, Cell>();
                            for (int r = 0; r < this.stage.rowCount; r++)
                                for (int c = 0; c < this.stage.colCount; c++) {
                                    Block block = this.stage.cells[r, c].block;
                                    if (null != block) { // && BlockAttr.Movable == block.attr)
                                        srcCells.Add(block.ToString(), this.stage.cells[r, c]);
                                        // SBDebug.Log("SJW srcCells add : " + block.ToString());
                                        // NOTE: 강제로 floating 상태를 벗어나게 해준다.
                                        block.state = BlockState.Fixed;
                                    }
                                }
                            int i, ie = this.transform.childCount;
                            for (i = 0; i < ie; i++) {
                                Transform child = this.transform.GetChild(i);
                                if (child.gameObject.name.StartsWith("B-")) {
                                    BlockController controller = child.gameObject.GetComponent<BlockController>();
                                    if (null != controller) {
                                        string uniqString = child.gameObject.name.Substring(2);
                                        Cell cell = null;
                                        if (srcCells.TryGetValue(uniqString, out cell)) {
                                            if (BlockAttr.Movable == cell.block.attr) {
                                                // 정상적 위치에 있는지 확인해서 넣어준다.
                                                Vector3 v = controller.GetLocalPosition(cell.row, cell.col);
                                                if (! v.Equals(controller.transform.localPosition)) {
                                                    // controller.transform.localPosition = v;
                                                    Debug.Log("SJW SHERIFF: RELOCATION BLOCK . UNIQKEY = " + uniqString);

                                                    if (moveCts is { IsCancellationRequested: true })
                                                    {
                                                        moveCts = new CancellationTokenSource();
                                                    }
                                                    controller.MoveTo(cell.row, cell.col, 0.15f,moveCts).Forget();
                                                }
                                                srcCells.Remove(uniqString);
                                            }
                                        } else {
                                            // 좀비 블럭이므로 삭제 시켜준다.
                                            Debug.Log("SJW SHERIFF: REMOVE ZOMBIE BLOCK. UNIQKEY = " + uniqString);
                                            if(cell != null) IngameBlockPoolController.CheckFirstBlock(cell.block);
                                            
                                            IngameBlockPoolController.ReleaseNormalBlock(controller.gameObject);
                                        }
                                    }
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

    private async UniTask ShowHintOrRefresh()
    {
        // 인풋락이 있다면 리턴
        if (true == this.lockHint)
            throw new OperationCanceledException();
            //yield break;

        // 문맥용 카운트
        uint myDirtyCount = this.dirtyCount;

        Hint hint = new Hint(this.stage);

        // 매칭이 있는지 분석하고
        bool oldLockHint = this.lockHint;
        this.lockHint = true;
        try {
            // 힌트안의 stage는 다른 문맥이므로 참조 비교해서는 안된다.
            hint.CalcInThread();
            // 계산 끝 날 때까지 항복하고
            while (hint.isCalcing)
                await UniTask.NextFrame();

            // 맞출 것이 하나도 없는 경우,
            if (0 == hint.hints.Count) {
                // 쓰레드모드로 계산하고
                hint.RefreshInThread();
                // 계산 끝 날 때까지 항복하고
                while (hint.isRefreshing)
                    await UniTask.NextFrame();

                // 맞출 것이 없다는 연출을 보여주고...
                InGameController inGameController = GetComponent<InGameController>();
                if (inGameController != null) {
                    NoBlockToSwapEffectView.Params effectParam = new NoBlockToSwapEffectView.Params();
                    PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();
                    if (playerStorage.Locale == "ko")
                        effectParam.ImageIndex = 0;
                    else
                        effectParam.ImageIndex = 1;

                    this.lockInput = true;
                    inGameController.NoBlockToSwapEffectView.Play(effectParam, () =>
                    {
                        this.lockInput = false;
                    });
                }

                // 새로운 블록으로 넣어준다.
                List<SwapBlock> swapCells = hint.swaps;
                if (null != swapCells)
                    foreach (SwapBlock swap in swapCells) {
                        // 문맥이 다르므로 row, col로 부터 블럭을 얻어 교체해야 한다.
                        Cell cell = this.stage.cells[swap.cell.row, swap.cell.col];
                        Block beforeBlock = cell.block;
                        cell.block = swap.afterBlock;
                        this.FindBlockController(beforeBlock)?.SwapNewBlock(swap.afterBlock);
                    }
            }

        } finally {
            this.lockHint = oldLockHint;
        }

        // 3초 뒤에 힌트를 발동 한다.
        await UniTask.Delay(TimeSpan.FromSeconds(3f));
        //yield return new WaitForSeconds(3f);
        if (this.dirtyCount != myDirtyCount || this.stage.IsCleared)
            throw new OperationCanceledException();

        HintResult hr = hint.hints[0];

        // 한 개의 블럭을 터치하라는 힌트의 경우
        if (HintType.TouchSpecial == hr.type) {
            // Debug.Log("TOUCH " + hr.from.row + "x" + hr.from.col);
            // CAUTION: hint내 스테이지 정보는 문맥이 다르므로 this.stage에서 찾아서 넣어준다.
            Cell cell = this.stage.cells[hr.from.row, hr.from.col];
            this.hintController.Select(cell);
        }

        // 노말 블럭을 이동하라는 힌트의 경우
        else if (HintType.MoveNormal == hr.type) {
            // Debug.Log("MOVE FROM From=" + hr.from.row + "x" + hr.from.col + " -> " + hr.to.row + "x" + hr.to.col);
            // CAUTION: hint내 스테이지 정보는 문맥이 다르므로 this.stage에서 찾아서 넣어준다.
            Cell from = this.stage.cells[hr.from.row, hr.from.col];
            Cell to = this.stage.cells[hr.to.row, hr.to.col];
            MatchType matchType = hr.matchResult.type;
            List<Cell> cells = new List<Cell>();
            foreach (Cell cell in hr.matchResult.cells)
                cells.Add(this.stage.cells[cell.row, cell.col]);
            if(this.hintController!=null)
                this.hintController.Select(from, to, matchType, cells);
        }
    }
    
    public async UniTask SwapBlocks(Cell[] cells)
    {
        const float duration = 0.15f;

        // 셀 하나를 터치 했을 경우, 특수블록의 경우임.
        if (1 == cells.Length) {
            if ((cells[0].block != null) && (cells[0].block.IsSpecial))
                if (!_IsAvailable(cells[0].block.type))
                    throw new OperationCanceledException();
            
            this.lastWeedsCount = 0;
            // for UndoManager
            this.Snap();

            // 시작 시간 스타트
            _CheckStartDate();
            
            this.isIdling = false;

            // 마지막턴이고 특수블럭이면 인풋락을 걸어준다.
            // 단, 스킬 블록은 턴 수를 소모하지 않게 변경되므로 예외처리 한다.
            if ((1 == this.stage.remainTurn) &&
                (null != cells[0].block) &&
                (cells[0].block.IsSpecial) &&
                (cells[0].block.type != BlockType.CardSkill))
            {
                this.lockInput = true;
            }

            if (!InGameController.isScoreModeSelected)
            {
                if((cells[0].block != null) && (!cells[0].block.IsSkill))
                    this.stage.TurnCountDown();
            }
            else
                this.stage.TurnCountDown();
            
            await this.AttackSpecial(cells[0]);

            this.coMatchAndGravity = this.MatchAndGravity();
            
            this.UpdateDashboard();
        }

        // 특수블록 + 특수블록
        else if (null != cells[0].block &&
            null != cells[1].block &&
            cells[0].block.IsSpecial &&
            cells[1].block.IsSpecial && 
            cells[0].topBlock == null &&
            cells[1].topBlock == null)
        {
            if ((cells[1].block != null) && (cells[1].block.IsSpecial))
                if(!_IsAvailable(cells[1].block.type)) throw new OperationCanceledException();
            
            // 합성하고 터트린다.
            BlockType combine = this.stage.GetCombineTypeOfSpecialBlocks(cells[0].block, cells[1].block);

            if (BlockType.None != combine) {
                this.lastWeedsCount = 0;
                // for UndoManager
                this.Snap();

                this.isIdling = false;
                // 마지막턴이면 인풋락을 걸어준다.
                if (1 == this.stage.remainTurn)
                    this.lockInput = true;

                // 시작 시간 스타트
                _CheckStartDate();

                // 제거전에 블럭타입목록을 얻어두고
                BlockType[] blockTypes = new BlockType[] { cells[0].block.type, cells[1].block.type };

                BlockController controller0 = this.FindBlockController(cells[0].block);
                if (null != controller0)
                {
                    if (moveCts is { IsCancellationRequested: true })
                    {
                        moveCts = new CancellationTokenSource();
                    }
                    await controller0.MoveTo(cells[1].row, cells[1].col, duration,moveCts);
                }

                BlockController controller1 = this.FindBlockController(cells[1].block);

                // 두 셀의 블럭을 제거하고
                // StartCoroutine(this.FindBlockController(cells[0].block)?.Explode());
                if (null != controller0 && null != controller0.gameObject)
                {
                    IngameBlockPoolController.CheckFirstBlock(cells[0].block,cells[1].block);
                    IngameBlockPoolController.ReleaseNormalBlock(controller0.gameObject);
                }
                
                this.stage.RemoveBlock(cells[0]);
                // StartCoroutine(this.FindBlockController(cells[1].block)?.Explode());
                if (null != controller1 && null != controller1.gameObject)
                {
                    IngameBlockPoolController.CheckFirstBlock(cells[0].block,cells[1].block);
                    IngameBlockPoolController.ReleaseNormalBlock(controller1.gameObject);
                }
                
                this.stage.RemoveBlock(cells[1]);
                // 교환효과음
                //this.audioChangeBlock.Play();

                // TODO: 특수블록 2가지가 합성되는 이팩트를 넣어줘야 한다.

                // 합성된 특수블럭을 넣어준다.
                // 1번에(2번째 이동 터치 셀) 넣어준다.
                Block block = Block.FactorySpecial(combine);
                BlockController.Create(block, cells[1].row, cells[1].col);
                this.stage.AddBlock(cells[1], block);
                // CAUTION: 중력효과를 받지 않도록 고정해준다.
                block.attr = BlockAttr.Fixable;

                // 생성 후 약간의 지연이 필요하다.
                // yield return new WaitForSeconds(0.5f);

                // 턴 감소 & 특수블럭 클리어 1 차감(합성고려)
                // 특수블록 미션카운트를 차감해준다.
                foreach (BlockType blockType in blockTypes) {
                    switch (blockType) {
                        case BlockType.PaperPlane: this.stage.ClearCountdown(ClearType.PaperPlane); break;
                        case BlockType.Rocket: this.stage.ClearCountdown(ClearType.Rocket); break;
                        case BlockType.Bomb: this.stage.ClearCountdown(ClearType.Bomb); break;
                        case BlockType.Mirrorball: this.stage.ClearCountdown(ClearType.Mirrorball); break;
                        case BlockType.ShootingStar: this.stage.ClearCountdown(ClearType.ShootingStar); break;
                    }
                }

                this.stage.ClearCountdown(ClearType.Special);
                this.stage.TurnCountDown();
                this.UpdateDashboard();
                
                // 1번블럭을 터트린다.
                await this.AttackSpecial(cells[1]);
                
                this.coMatchAndGravity = this.MatchAndGravity();
                
                BlockLog.Instance.AddSwapBlock(cells[0].block, cells[1].block);

            } else {

                // 시작 시간 스타트
                _CheckStartDate();

                // 복구 애니메이션을 넣어준다.
                if (null != cells[0].block) {
                    BlockController controller = this.FindBlockController(cells[0].block);
                    if (null != controller)
                        controller.MoveToReturnly(cells[1].row, cells[1].col, duration).Forget();
                }
                if (null != cells[1].block) {
                    BlockController controller = this.FindBlockController(cells[1].block);
                    if (null != controller)
                        this.FindBlockController(cells[1].block)?.MoveToReturnly(cells[0].row, cells[0].col, duration).Forget();
                }
                // yield return new WaitForSeconds(duration + 0.1f);
            }
        }

        // 특수블록 + 일반블록
        else if (
            null != cells[0].block &&
            null != cells[1].block &&
            ((cells[0].block.IsSpecial && cells[1].block.IsNormal) ||
             (cells[0].block.IsNormal && cells[1].block.IsSpecial)) &&
            cells[0].topBlock == null &&
            cells[1].topBlock == null)
        {
            if (!_IsAvailable(cells[0].block.type)) throw new OperationCanceledException();
            
            this.lastWeedsCount = 0;
            // for UndoManager
            this.Snap();

            // 마지막턴이면 인풋락을 걸어준다.
            if (1 == this.stage.remainTurn && !cells[0].block.IsSkill && !cells[1].block.IsSkill )
            {
                this.lockInput = true;
            }

            // 일단 교체한 후 특수블록을 터트려야 한다.
            this.stage.ChangeBlocks(cells[0], cells[1]);

            // CAUTION: 셀내 블록을 고정상태로 두고, 애니처리 후 해제한다.
            cells[0].block.attr = BlockAttr.Fixable;
            cells[1].block.attr = BlockAttr.Fixable;

            this.isIdling = false;

            // 두 블럭 교환 에니메이션 넣어주고
            if (null != cells[0].block) {
                BlockController controller = this.FindBlockController(cells[0].block);
                if (null != controller)
                {
                    if (moveCts is { IsCancellationRequested: true })
                        moveCts = new CancellationTokenSource();
                    controller.MoveTo(cells[0].row, cells[0].col, duration, moveCts).Forget();
                }
            }
            if (null != cells[1].block) {
                BlockController controller = this.FindBlockController(cells[1].block);
                if (null != controller)
                {
                    if (moveCts is { IsCancellationRequested: true })
                        moveCts = new CancellationTokenSource();
                    await controller.MoveTo(cells[1].row, cells[1].col, duration,moveCts);
                }
            }

            // CAUTION: 0.1f를 더해주는 매직넘버가 사용되었다. 이 문제 때문에 매우느린 폰의 경우 일반블록이 사라지지 않을 수 있다.
            // yield return new WaitForSeconds(duration + 0.1f);

            Cell meSpecial, youNormal;
            if ((cells[0]!= null) && (cells[0].block.IsSpecial)) {
                meSpecial = cells[0];
                youNormal = cells[1];
            } else {
                meSpecial = cells[1];
                youNormal = cells[0];
            }

            var checkBlock = !((cells[0].block.IsSkill && cells[1].block.IsNormal) || (cells[0].block.IsNormal && cells[1].block.IsSkill));
            
            if (!InGameController.isScoreModeSelected)
            {
                if (checkBlock && cells[0].block!=null && cells[1].block!=null )
                {
                    this.stage.TurnCountDown();
                }
            }
            else
            {
                this.stage.TurnCountDown();
            }

            this.UpdateDashboard();

            // 시작 시간 스타트
            _CheckStartDate();

            // 일반매칭 진행하고
            NormalMatch match = new NormalMatch(this.stage);
            NormalMatchResult result = match.Analyse(youNormal);
            BlockType targetBlockType = youNormal.block.type;
            youNormal.block.attr = BlockAttr.Movable;
            List<Block> excludedBlocks = null;
            
            GetFindClickPos(cells[0], cells[1],null, result);
            
            if (null != result) {
                // 노말블럭을 삭제해주고
                Block newSpecialBlock = this.AttackNormal(result);
                excludedBlocks = new List<Block>() { newSpecialBlock };
                await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
            }
            // NOTE: 일반매칭에 의해서 특수블록이 생성되었다면 이 블록은 터지지 않아야 한다. 특수블록을 터트리고 중력효과까지 진행시킨다.
            if (null != meSpecial.block) {
                meSpecial.block.attr = BlockAttr.Movable;
                await this.AttackSpecial(meSpecial, targetBlockType, excludedBlocks);
            }
            this.coMatchAndGravity = this.MatchAndGravity();
            
            BlockLog.Instance.AddSwapBlock(cells[0].block, cells[1].block);
        }

        // 특수블록 + 미션블록
        else if (
            null != cells[0].block &&
            null != cells[1].block &&
            (cells[0].block.IsSpecial && cells[1].block.IsMission) &&
            cells[0].topBlock == null && 
            cells[1].topBlock == null)
        {
            this.lastWeedsCount = 0;
            // for UndoManager
            this.Snap();

            // 마지막턴이면 인풋락을 걸어준다.
            if (1 == this.stage.remainTurn)
                this.lockInput = true;

            // 일단 교체한 후 특수블록을 터트려야 한다.
            this.stage.ChangeBlocks(cells[0], cells[1]);

            // 턴 감소
            if ((cells[1].block != null) && (!cells[1].block.IsSkill))
            {
                this.stage.TurnCountDown();
            }

            this.UpdateDashboard();

            this.isIdling = false;
            
            // 시작 시간 스타트
            _CheckStartDate();

            // 두 블럭 교환 에니메이션 넣어주고
            if (null != cells[0].block) {
                BlockController controller = this.FindBlockController(cells[0].block);
                if (null != controller)
                {
                    if (moveCts is { IsCancellationRequested: true })
                        moveCts = new CancellationTokenSource();
                    controller.MoveTo(cells[0].row, cells[0].col, duration,moveCts).Forget();
                }
            }
            if (null != cells[1].block) {
                BlockController controller = this.FindBlockController(cells[1].block);
                if (null != controller)
                {
                    if (moveCts is { IsCancellationRequested: true })
                        moveCts = new CancellationTokenSource();
                    await controller.MoveTo(cells[1].row, cells[1].col, duration, moveCts);
                }
            }

            // CAUTION: 0.1f를 더해주는 매직넘버가 사용되었다. 이 문제 때문에 매우느린 폰의 경우 일반블록이 사라지지 않을 수 있다.
            // yield return new WaitForSeconds(duration + 0.1f);
            
            // 특수블록을 터트리고 중력효과까지 진행시킨다.
            await this.AttackSpecial(cells[1]);

            this.coMatchAndGravity = this.MatchAndGravity();
            
            BlockLog.Instance.AddSwapBlock(cells[0].block, cells[1].block);
        }

        // 일반블록 + 일반블록
        else
        {
            if (
                cells[0] != null &&
                cells[0].block != null &&
                cells[1] != null &&
                cells[1].block != null &&
                cells[0].block.state != BlockState.Floating &&
                cells[1].block.state != BlockState.Floating && 
                cells[0].topBlock == null &&
                cells[1].topBlock == null)
            {
                this.stage.ChangeBlocks(cells[0], cells[1]);
                
                NormalMatch match = new NormalMatch(this.stage);
                List<NormalMatchResult> results = match.Analyse(cells);
                
                GetFindClickPos(cells[0], cells[1],results,null);
                if (null != results)
                {
                    this.lastWeedsCount = 0;

                    // 마지막턴이면 인풋락을 걸어준다.
                    if (1 == this.stage.remainTurn)
                        this.lockInput = true;

                    // 턴 감소
                    this.stage.TurnCountDown();
                    this.UpdateDashboard();

                    this.isIdling = false;

                    // 시작 시간 스타트
                    _CheckStartDate();

                    // 교환 애니매이션
                    if (null != cells[0].block) {
                        BlockController controller = this.FindBlockController(cells[0].block);
                        if (null != controller)
                        {
                            if (moveCts is { IsCancellationRequested: true })
                                moveCts = new CancellationTokenSource();
                            this.FindBlockController(cells[0].block)?.MoveTo(cells[0].row, cells[0].col, duration,moveCts).Forget();
                        }
                    }
                    if (null != cells[1].block) {
                        BlockController controller = this.FindBlockController(cells[1].block);
                        if (null != controller)
                        {
                            if (moveCts is { IsCancellationRequested: true })
                                moveCts = new CancellationTokenSource();
                            await controller.MoveTo(cells[1].row, cells[1].col, duration,moveCts);
                        }
                    }
                    // yield return new WaitForSeconds(duration + 0.1f);

                    // 매칭을 진행한다.
                    this.coMatchAndGravity = this.MatchAndGravity();

                    BlockLog.Instance.AddSwapBlock(cells[0].block, cells[1].block);
                    
                } else {

                    // 시작 시간 스타트
                    _CheckStartDate();
                    
                    this.stage.ChangeBlocks(cells[0], cells[1]);

                    BlockController controller0 = this.FindBlockController(cells[0].block);
                    BlockController controller1 = this.FindBlockController(cells[1].block);

                    // 원복 애니메이션을 넣어준다.
                    if (null != cells[0].block) {
                        if (null != controller0)
                            controller0.MoveToReturnly(cells[1].row, cells[1].col, 0.1f).Forget();
                    }
                    if (null != cells[1].block) {
                        if (null != controller1)
                            controller1.MoveToReturnly(cells[0].row, cells[0].col, 0.1f).Forget();
                    }

                    CommonProcessController.PlayEffectSound("Ingame", 1);
                    await UniTask.Delay(TimeSpan.FromSeconds(duration + 0.1f));
                }
            }
        }
        
        this.coMatchAndGravity = this.MatchAndGravity();
        
        void _CheckStartDate() {
            if (DateTime.MinValue == this.stage.startDate)
                this.stage.startDate = DateTime.Now;
        }

        bool _IsAvailable(BlockType blockType)
        {
            if (!this.isIdling)
            {
                if (blockType == BlockType.PaperPlane) return false;
                if (blockType == BlockType.Mirrorball) return false;

                if (blockType == BlockType.RocketPaperPlane) return false;
                if (blockType == BlockType.PaperPlaneBomb) return false;
                if (blockType == BlockType.PaperPlanePaperPlane) return false;
                if (blockType == BlockType.PaperPlaneMirrorball) return false;

                if (blockType == BlockType.RocketMirrorball) return false;
                if (blockType == BlockType.BombMirrorball) return false;
                if (blockType == BlockType.MirrorballMirrorball) return false;
            }
            return true;
        }
    }

    private async UniTask ShootMissile(Cell fromCell, Cell toCell)
    {
        GameObject prefab = IngameEffectPrefabLoader.Instance.GetMirrorBallShootingRayPrefab();
        GameObject prefabObject = Instantiate(prefab, BaseController.stageObject.transform);
        prefabObject.transform.localPosition = GetPositionByMatrix(fromCell.row, fromCell.col);
        Vector2 from = GetPositionByMatrix(fromCell.row, fromCell.col);
        Vector2 to = GetPositionByMatrix(toCell.row, toCell.col);
        float duration = 0.08f;
        float tick = 0f;
        
        while (tick <= duration) {
            prefabObject.transform.localPosition = new Vector3(
                EasingFunction.EaseInSine(from.x, to.x, tick/duration),
                EasingFunction.EaseInSine(from.y, to.y, tick/duration),
                0
            );
            tick += Time.deltaTime;
            await UniTask.NextFrame();
        }
        Destroy(prefabObject);
    }
    
    private async UniTask VanishBlackHole(Cell fromCell,Cell toCell,GameObject prefabObject)
    {
        Vector2 from = GetPositionByMatrix(fromCell.row, fromCell.col);
        Vector2 middle = GetPositionByMatrix((int)this.stage.rowCount/2, (int)this.stage.colCount/2);
        Vector2 to = GetPositionByMatrix(toCell.row, toCell.col);
        Vector2 v2 = to - from;
        
        float duration = 0.75f;
        float angle = Mathf.Atan2(v2.y, v2.x) * Mathf.Rad2Deg - 90f;
        prefabObject.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        float tick = 0f;
        Vector2 prevV = from;
        while (tick <= duration) {
            Vector2 to2 = this.CalcBezier(to, middle, from, tick/duration);
            prefabObject.transform.localPosition = to2;
            if (0 < tick) {
                v2 = to2 - prevV;
                angle = Mathf.Atan2(v2.y, v2.x) * Mathf.Rad2Deg - 90f;
                prefabObject.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
            prevV = to2;
            tick += Time.deltaTime;
            await UniTask.NextFrame();
        }
        Destroy(prefabObject);
    }

    private async UniTask FlyPaperAirplane(Cell fromCell, Cell toCell)
    {
        GameObject prefabObject = null;
        switch (CommonProcessController.GetNameString())
        {
            case CommonProcessController.KWONEUNBIINFO:
            case CommonProcessController.IKONINFO: 
                GameObject paperPlanePrefab = IngameEffectPrefabLoader.Instance.GetBlockCreatePrefab(BlockType.PaperPlane);
                prefabObject = Instantiate(paperPlanePrefab, BaseController.stageObject.transform);
                break;
            default:
                GameObject paperPlanePrefabAteez = IngameEffectPrefabLoader.Instance.GetBlockCreatePrefab(BlockType.PaperPlane);
                prefabObject = Instantiate(paperPlanePrefabAteez, BaseController.stageObject.transform);
                break;
        }

        // z-index 정리
        // Add => Get으로 변경

        switch (CommonProcessController.GetNameString())
        {
            case CommonProcessController.KWONEUNBIINFO:
            case CommonProcessController.IKONINFO: 
                Canvas canvas = prefabObject.GetComponent<Canvas>();
                canvas.overrideSorting = true;
                // Default로 통일
                canvas.sortingLayerName = "Default";
                break;
            default:
                SetCanvasAteez(prefabObject);
                break;
        }
        
        // 꼬리 이팩트
        AirplaneBlockEffect airplaneBlockEffect = prefabObject.GetComponent<AirplaneBlockEffect>();
        // 이미지 변경
        switch (CommonProcessController.GetNameString())
        {
            case CommonProcessController.KWONEUNBIINFO:
            case CommonProcessController.IKONINFO: 
                airplaneBlockEffect.ChangeImage();
                break;
            default:
                airplaneBlockEffect.MainImage.enabled = true;
                break;
        }
        airplaneBlockEffect.OffAll();
        prefabObject.transform.localScale = new Vector3(1f, 1f, 1f);
        GameObject tailPrefab = IngameEffectPrefabLoader.Instance.GetPaperPlaneMovePrefab();
        GameObject tailObject = Instantiate(tailPrefab, prefabObject.transform);
        tailObject.transform.localPosition = Vector3.zero;
        prefabObject.transform.localPosition = GetPositionByMatrix(fromCell.row, fromCell.col);
        Vector2 from = GetPositionByMatrix(fromCell.row, fromCell.col);

        //4방향 효과
        GameObject pangAlarmPrefab = IngameEffectPrefabLoader.Instance.GetPaperPlaneAlarmPrefab();
        GameObject pangAlarmObj = Instantiate(pangAlarmPrefab, BaseController.stageObject.transform);
        pangAlarmObj.transform.localPosition = from;
        
        CommonProcessController.MuteEffectSound("Ingame", 0);
        CommonProcessController.PlayEffectSound("Ingame", 9);

        Vector2 middle = GetPositionByMatrix((int)this.stage.rowCount/2, (int)this.stage.colCount/2);
        Vector2 to = GetPositionByMatrix(toCell.row, toCell.col);
        Vector2 v2 = to - from;
        float duration = 0.75f;
        float angle = Mathf.Atan2(v2.y, v2.x) * Mathf.Rad2Deg - 90f;
        prefabObject.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        float tick = 0f;
        Vector2 prevV = from;
        
        CommonProcessController.MuteEffectSound("Ingame", 0);
        CommonProcessController.PlayEffectSound("Ingame", 10);
        
        while (tick <= duration) {
            Vector2 to2 = this.CalcBezier(from, middle, to, tick/duration);
            prefabObject.transform.localPosition = to2;
            if (0 < tick) {
                v2 = to2 - prevV;
                angle = Mathf.Atan2(v2.y, v2.x) * Mathf.Rad2Deg - 90f;
                prefabObject.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
            prevV = to2;
            tick += Time.deltaTime;
            await UniTask.NextFrame();
        }

        prefabObject.GetComponent<AirplaneBlockEffect>().OnExplosion(() =>
        {
            switch (CommonProcessController.GetNameString())
            {
                case CommonProcessController.KWONEUNBIINFO:
                case CommonProcessController.IKONINFO: 
                    Animator animator = prefabObject.transform.Find("Ani_Conrtoller_Block_302").GetComponent<Animator>();
                    animator.gameObject.SetActive(true);
                    animator.Rebind();
                    animator.Update(0f);

                    var blockEffect = prefabObject.GetComponent<AirplaneBlockEffect>();
                    blockEffect.ResetImage();
                    IngameBlockPoolController.ReleaseExplodeSpecialBlock("301_move", prefabObject);
                    break;
                default:
                    Destroy(prefabObject);
                    break;
            }
        });
    }
    
    private void GetFindClickPos(Cell from, Cell to, List<NormalMatchResult> results = null, NormalMatchResult specialResult = null)
    {
        if (results != null)
        {
            foreach (var result in results)
            {
                foreach (var cell in result.cells)
                {
                    if ((cell.col == from.col && cell.row == from.row) ||
                        (cell.col == to.col && cell.row == to.row))
                    {
                        movedSavePos.Add(new Vector2(cell.row, cell.col));
                    }
                }
            }
        }
        else 
        {
            if (specialResult != null)
            {
                foreach (var cell in specialResult.cells)
                {
                    if ((cell.col == from.col && cell.row == from.row) ||
                        (cell.col == to.col && cell.row == to.row))
                    {
                        movedSavePos.Add(new Vector2(cell.row, cell.col));
                    }
                }
            }
        }
    }

    #endregion
    
    #region ATEEZ

    private void SetCanvasAteez(GameObject prefabObject)
    {
        Canvas canvas = prefabObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingLayerName = "ParticleEffect";
    }
    
    private async UniTask ExplodeMirrorBallAteez(Cell me, BlockType targetBlockType = BlockType.None, List<Block> excludedBlocks = null)
    {
        void _AttackBlock(Cell cell, ref List<Cell> specialCells)
        {
            if (null != cell.topBlock) {
                // 탑블록을 처리하고
                Block block = cell.topBlock;
                if (0 < block.specialPoint)
                    block.specialPoint = Math.Max(0, block.specialPoint - 1);
                else if (0 < block.normalPoint)
                    block.normalPoint = Math.Max(0, block.normalPoint - 1);
                if (block.IsDead) {
                    if (this.RemoveTopBlock(cell))
                        this.UpdateDashboard();
                } else {
                    TopController controller = this.FindTopController(block);
                    if (null != controller)
                        controller.showBarricadeChangeEffect();
                }
            } else {
                // 미들블록(기본블록)을 처리한다.
                Block block = cell.block;
                if (null != block) { // && BlockState.Fixed == block.state) {
                    // if (me != cell) { <- 필요충분조건이다.
                    if (block.IsSpecial)
                        specialCells.Add(cell);
                    else if (block.IsMission)
                        this.AttackMission(cell, 1, 1, BlockType.None, true);
                    else {
                        if (0 < block.specialPoint)
                            block.specialPoint = Math.Max(0, block.specialPoint - 1);
                        else if (0 < block.normalSidePoint)
                            block.normalSidePoint = Math.Max(0, block.normalSidePoint - 1);
                        else if (0 < block.normalPoint)
                            block.normalPoint = Math.Max(0, block.normalPoint - 1);
                        if (block.IsDead) {
                            BlockController controller = this.FindBlockController(cell.block);
                            if (null != controller)
                                controller.Explode().Forget();
                            this.stage.RemoveBlock(cell);
                            // 스코어를 반영한다.
                            if (null != this.delegateCalcScore)
                                this.delegateCalcScore(this, Stage.SCORE_NORMAL);
                            if (this.stage.ClearCountdownForNormalBlock(block))
                                this.UpdateDashboard();
                        }
                    }
                    if (block.IsNormal)
                        _AttackBottomBlock(cell);
                } else
                    _AttackBottomBlock(cell);
            }
        }

        void _AttackBottomBlock(Cell cell) {
            // 보텀(뽁뽁이,잔디)블록
            Block block = cell.bottomBlock;
            if (null != block) {
                block.normalPoint = Math.Max(0, block.normalPoint - 1);

                if (block.IsDead)
                {
                    if (this.RemoveBottomBlock(cell))
                        this.UpdateDashboard();
                }
                else
                {
                    BottomController controller = this.FindBottomController(block);
                    controller.BottomRemoveEffect(block.type);
                }
            }
        }

        // 연쇄폭발을 리턴하게 된다.
        void _AttackBlocks(List<Cell> cells, ref List<Cell> specialCells) {
            if (null != cells)
                foreach (Cell cell in cells)
                    _AttackBlock(cell, ref specialCells);
        }

        // 벽공격
        void _AttackWalls(List<WallResult> walls) {
            if (null != walls)
                foreach (WallResult wr in walls) {
                    string name = null;
                    if (WallResultType.Vertical == wr.type)
                        name = "WV" + wr.cell.rightBlock.ToString();
                    else if (WallResultType.Horizontal == wr.type)
                        name = "WH" + wr.cell.belowBlock.ToString();
                    if (null != name) {
                        GameObject wallObject = this.transform.Find(name)?.gameObject;
                        if (WallResultType.Vertical == wr.type)
                            wr.cell.rightBlock = null;
                        else
                            wr.cell.belowBlock = null;
                        Destroy(wallObject);
                    }
                }
        }

        List<TemporatedCell> temps = new List<TemporatedCell>();
        // 셀내 블록을 락 상태로
        void _beginLock() {
            this.specialAttackingCount++;
        }
        void _AddLock(List<Cell> cells) {

            if (null != cells)
                foreach (Cell c in cells)
                    if (null != c.block) {
                        var tc = new TemporatedCell(c);
                        tc.Enter();
                        temps.Add(tc);
                    }
        }
        // 언락한다.
        void _UnlockAll() {
            this.specialAttackingCount--;
            foreach (var tc in temps)
                tc.Leave();
        }
        
        SpecialMatch match;
        SpecialMatchResult result;
        GameObject prefabObject;
        
        List<Cell> specialCells = new List<Cell>();
        
        // 미러볼 발사 이팩트
        prefabObject = Instantiate(
            IngameEffectPrefabLoader.Instance.GetMirrorBallIdlePrefab(), 
            BaseController.stageObject.transform
        );
        prefabObject.transform.localPosition = GetPositionByMatrix(me.row, me.col);
        // 매칭 진행하고
        match = new SpecialMatch(this.stage, excludedBlocks);
        result = match.Analyse(me, 0 , targetBlockType);
        _AddLock(result.cells1);
        _beginLock();
        try {
            if (null != result.cells1)
                foreach (Cell c in result.cells1)
                {
                    await this.ShootMissile(me, c);
                    _AttackBlock(c, ref specialCells);
                }
            // 볼을 터트리고
            await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
            BlockController controller5 = this.FindBlockController(me.block);
            if (null != controller5)
                await controller5.Explode();
            this.stage.RemoveBlock(me);
            _AttackBottomBlock(me);
            _AttackWalls(result.walls);
            // _RemoveBottomBlocks(result.bottoms);
            // 미러볼만 사이드어텍을 넣어준다.
            if (null != result.sides)
                foreach (var side in result.sides) {
                    foreach (var cell in side.toCells) {
                        if (null != cell.block && (null == cell.topBlock || BlockType.Barricade != cell.topBlock.type)) {
                            // Debug.Log(cell.block.type);
                            this.AttackMission(cell, 1, 0, side.fromBlockType);
                        }
                    }
                }
            this.stage.skillFever += Stage.SKILLFEVER_USE_SEPCIAL;
            this.stage.ClearCountdown(ClearType.Mirrorball);
        } finally {
            Destroy(prefabObject);
            _UnlockAll();
        }
    }
    
    #endregion

    public void ShowADPlus(int count)
    {
        this.adPlusText.text = string.Format("+{0}", count);
        this.adPlusAnim.DORewind();
        this.adPlusAnim.DOPlay();
    }

    public void ADPlusEnd()
    {
        StartCoroutine(PlusEnd());
    }

    IEnumerator PlusEnd()
    {
        yield return new WaitForSeconds(2f);
        this.adPlusAnim.GetComponent<CanvasGroup>().alpha = 0;
    }

    public void UpdateDashboard()
    {
        if (null != this.delegateUpdateDashboard)
            this.delegateUpdateDashboard(this);
    }

    public BlockController FindBlockController(Block block)
    {
        return
            null != block
            ?
            this.transform.Find("B-" + block.ToString())?.gameObject.GetComponent<BlockController>()
            :
            null;
    }

    public BottomController FindBottomController(Block block)
    {
        return
            null != block
            ?
            this.transform.Find("BB-" + block.ToString())?.gameObject.GetComponent<BottomController>()
            :
            null;
    }

    public TopController FindTopController(Block block)
    {
        return
            null != block
            ?
            this.transform.Find("TB-" + block.ToString())?.gameObject.GetComponent<TopController>()
            :
            null;
    }

    // 탑블록을 제거
    public bool RemoveTopBlock(Cell cell)
    {
        Block block = cell.topBlock;
        if (null != block) {
            TopController controller = this.FindTopController(block);
            Vector3 targetPos = controller.transform.position;
            if (null != controller)
                controller.Remove(0.1f).Forget();
            bool regen = false;
            if (BlockType.Barricade == block.type) {
                // 감옥 클리어 카운트
                regen = this.stage.ClearCountdown(ClearType.Barricade);
                // 창살안에 있던 블록을 움직일 수 있게 설정해준다.
                if (null != cell.block)
                    cell.block.attr = Block.GetInitAttrOf(cell.block.type);
                
                GameObject prefab = IngameEffectPrefabLoader.Instance
                    .GetBlockExplosionPrefab(BlockType.Barricade);
                GameObject obj = Instantiate(prefab, this.transform);
                obj.transform.position = targetPos;
            }
            this.stage.RemoveTopBlock(cell);
            return regen;
        } else
            return false;
    }

    ////////////////// PRIVATE FUNCITONS //////////////////

    private Vector2 CalcBezier(Vector2 A, Vector2 B, Vector2 C, float ratio)
    {
        float u = 1f - ratio;
        float t2 = ratio * ratio;
        float u2 = u * u;
        return A*u2 + B*(ratio*u*2f) + C*t2;
    }

    // 보텀블록을 제거
    public bool RemoveBottomBlock(Cell cell)
    {
        Block block = cell.bottomBlock;
        if (null != block) {
            // TODO: 잔디&뽁뽁이 삭제 애니매이션을 넣어야 한다.
            bool regen = false;
            BottomController controller = this.FindBottomController(block);
            if (controller) {
                switch (block.type) {
                    case BlockType.Grass:
                        regen = this.stage.ClearCountdown(ClearType.Grass);
                        controller.RemoveAfterInterval(block.type, 0.3f).Forget();
                        break;
                    case BlockType.Snow:
                        regen = this.stage.ClearCountdown(ClearType.Snow);
                        controller.RemoveAfterInterval(block.type, 0.3f).Forget();
                        break;
                    case BlockType.Puddle:
                        regen = this.stage.ClearCountdown(ClearType.Puddle);
                        Destroy(controller.gameObject);
                        break;
                }
                this.stage.RemoveBottomBlock(cell);
                this.lastWeedsCount++;
            }
            return regen;
        } else
            return false;
    }

    ////////////////// COORDINATION FUNCITONS //////////////////

    private float EaseOutBack(float start, float end, float value)
    {
        float s = 0.7f; //1.70158f;
        end -= start;
        value = (value) - 1;
        return end * ((value) * value * ((s + 1f) * value + s) + 1f) + start;
    }

    ////////////////// UNDOMANAGER //////////////////

#if UNDO_MANAGER

    public void Snap()
    {
        UndoManager.singleton.Snap();
    }

    public void Undo()
    {
        if (0 == BlockController.gravityCount) {
            JObject obj = UndoManager.singleton.Undo();
            if (null != obj) {
                this.LoadFromStage(obj);
                StartCoroutine(this.MatchAndGravity());
            }
        }
    }

    public void Redo()
    {
        if (0 == BlockController.gravityCount) {
            JObject obj = UndoManager.singleton.Redo();
            if (null != obj) {
                this.LoadFromStage(obj);
                StartCoroutine(this.MatchAndGravity());
            }
        }
    }

    private void LoadFromStage(JObject obj)
    {
        // 모든 셀과 블럭을 삭제하고 다시 넣어준다.
        for (int i = 0; i < this.transform.childCount; i++)
            Destroy(transform.GetChild(i).gameObject);
        this.InitStage(obj);
    }

#else

    public void Snap() {}

#endif

}
