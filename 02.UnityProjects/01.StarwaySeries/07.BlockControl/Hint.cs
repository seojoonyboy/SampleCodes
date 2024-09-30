using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace Artistar.Puzzle.Core
{
    public enum HintType
    {
        None = 0,           // 매칭이 하나도 없다.
        TouchSpecial = 1,   // 특수블록 하나를 터치
        MoveNormal = 2,     // 노말블럭을 이동한다.
    }

    public class HintResult 
    {
        public Cell from = null;
        public Cell to = null;
        public HintType type = HintType.None;
        public NormalMatchResult matchResult;
        public HintResult(Cell from, Cell to, HintType type, NormalMatchResult matchResult) {
            this.from = from;
            this.to = to;
            this.type = type;
            this.matchResult = matchResult;
        }
    }

    public class SwapBlock 
    {
        public Cell cell = null;
        public Block beforeBlock = null;
        public Block afterBlock = null;
        public SwapBlock(Cell cell, Block beforeBlock, Block afterBlock)
        {
            this.cell = cell;
            this.beforeBlock = beforeBlock;
            this.afterBlock = afterBlock;
        }
    }

    public class Hint
    {
        public bool isCalcing = false;
        public bool isRefreshing = false;
        public List<HintResult> hints = new List<HintResult>();
        public List<SwapBlock> swaps = null;

        private Stage stage = null;
        private System.Random Random = new System.Random();

        public Hint(Stage stage)
        {
            // Stage 객체를 하나 복사해서 저장 후 계산해준다.
            JObject obj = stage.ToJObject();
            this.stage = Stage.Factory(obj);
        }

        public void CalcInThread()
        {
            // 쓰레드로 처리하도록 하였음
            Thread th = new Thread(_Calc);
            th.IsBackground = true;
            this.hints.Clear();
            this.isCalcing = true;
            th.Start();
        }

        public void Calc()
        {
            this.hints.Clear();
            this.isCalcing = true;
            this._Calc();
        }

        private void _Calc()
        {
            try {
                // 일반블록 매칭을 구한다.
                this.GetMatchNormalBlocks();
                // 특수블록이 있으면 해당 블록 터치를 얻고
                if (0 == this.hints.Count)
                    this.GetSpecialBlocks();
                // 가중치 정렬을 하고
                hints.Sort(delegate (HintResult a, HintResult b) {
                    int aw = (HintType.TouchSpecial == a.type) ? (int)(a.from.block.type) : (int)(a.matchResult.type);
                    int bw = (HintType.TouchSpecial == b.type) ? (int)(b.from.block.type) : (int)(b.matchResult.type);
                    return bw.CompareTo(aw); // 내림차순 정렬
                });
            } finally {
                isCalcing = false;
            }
        }

        private void GetSpecialBlocks(int count = 9999)
        {
            int n = 0;
            // 특수블록이 있으면 해당 블록 터치를 얻고
            for (int r = 0; r < this.stage.rowCount; r++)
                for (int c = 0; c < this.stage.colCount; c++) {
                    Cell cell = this.stage.cells[r, c];
                    if (CellType.Alive == cell.type && null != cell.block && cell.block.IsSpecial) {
                        // 특수블록이 미러볼인데 4방에 일반블럭이 없다면 스킵
                        if (BlockType.Mirrorball == cell.block.type) {
                            Block tb = this.GetBlock(r-1,c);
                            Block bb = this.GetBlock(r+1,c);
                            Block lb = this.GetBlock(r,c-1);
                            Block rb = this.GetBlock(r,c+1);
                            if ((null == tb || !tb.IsNormal) &&
                                (null == bb || !bb.IsNormal) &&
                                (null == lb || !lb.IsNormal) &&
                                (null == rb || !rb.IsNormal))
                                continue;
                        }
                        this.hints.Add(new HintResult(cell, null, HintType.TouchSpecial, null));
                        n++;
                        if (count <= n)
                            return;
                    }
                }
        }

        private Block GetBlock(int r, int c)
        {
            if (0 <= r && r < this.stage.rowCount && 0 <= c && c < this.stage.colCount)
                return this.stage.cells[r, c].block;
            else
                return null;
        }

        private void GetMatchNormalBlocks(int count = 9999)
        {
            NormalMatch match = new NormalMatch(this.stage);
            for (int r = 0; r < this.stage.rowCount; r++) {
                for (int c = 0; c < this.stage.colCount; c++) {
                    Cell from = this.stage.cells[r, c];
                    if (CellType.Alive == from.type && 
                        null != from.block && 
                        from.block.IsNormal &&
                        BlockAttr.Movable == from.block.attr &&
                        //Note. from.topBlock이 null이 아닌 경우 (예. 일반 블록 위에 울타리 존재) Swap 불가능한 영역으로 처리한다.
                        from.topBlock==null)
                    {
                        // 4방향 움직임으로 부터 매칭 결과를 얻는다.
                        Cell[] cells4 = new Cell[] {
                            this.stage.GetCellSafely(r-1, c),
                            this.stage.GetCellSafely(r, c+1),
                            this.stage.GetCellSafely(r+1, c),
                            this.stage.GetCellSafely(r, c-1),
                        };
                        foreach (Cell to in cells4) {
                            if (null != to) {
                                // 벽유무를 검사해서 교환가능한지 체크해야 한다.
                                if (this.stage.IsDefancedByWall(from, to))
                                    continue;
                                // Note. to block이 Movable 상태이지만 to.topBlock이 null인 경우(예. 블록이 비어있는 경우) skip 
                                if (null != to.block && BlockAttr.Movable != to.block.attr && null == to.topBlock)
                                    continue;
                                // 교환하고
                                if (to.topBlock == null && to.block!=null)
                                {
                                    this.stage.ChangeBlocks(from, to);
                                    List<NormalMatchResult> matchResults = match.Analyse(new Cell[] { to });
                                    // 원복한다.
                                    this.stage.ChangeBlocks(from, to);
                                    if (null != matchResults && 0 < matchResults.Count)
                                        foreach (NormalMatchResult matchResult in matchResults)
                                            this.hints.Add(new HintResult(from, to, HintType.MoveNormal, matchResult));
                                    match.Clear();
                                }
                            }
                        }
                    }
                }
            }
        }

        // 노말 블럭을 새로 장전한다.
        public void RefreshInThread()
        {
            Thread th = new Thread(_Refresh);
            th.IsBackground = true;
            this.isRefreshing = true;
            th.Start();
        }

        public void Refresh()
        {
            this.isRefreshing = true;
            this._Refresh();
        }

        private void _Refresh()
        {
            int limit = 200;
            int mr = 0;
            try {
                int n;
                List<NormalMatchResult> matchResults = null;
                // 맞춤 블럭이 하나 이상 나올 때까지 계속 놓는다.
                for (n = 0; n < limit; n++) {
                    this.swaps = this.RefreshNormalBlock();
                    this.Calc();
                    // Debug.Log("RECALC swaps.count = " + this.swaps.Count);
                    // 노말매칭이 있다면 다시 하도록 처리
                    NormalMatch match = new NormalMatch(this.stage);
                    matchResults = match.AnalyseAll();
                    mr = null != matchResults ? matchResults.Count : 0;
                    // Debug.Log("RECALC MR=" + mr);
                    if (limit <= n)
                        break;
                    if (0 == mr && 0 < this.hints.Count)
                        break;
                }
                // Debug.Log("REDO MR=" + mr + ", H=" + this.hints.Count + ", N=" + n);

            } catch (System.Exception e) {
                Debug.Log(e);
            } finally {
                isRefreshing = false;
            }
        }

        // 바로 붙어 있는 셀을 동일한 ALL 블록으로 붙여준다.

        // 노말블럭을 재설정한다.
        private List<SwapBlock> RefreshNormalBlock()
        {
            List<SwapBlock> swaps = new List<SwapBlock>();
            // 컴포넌트를 얻고
            List<Block> blocks = this.stage.components["ALL"];
            if (null == blocks)
                return null;

            // 연속하는 두 셀에 동일한 블록을 넣어주고
            List<Cell> normalCells = this.getCellWithNormalBlock();
            Cell[] twoCells = new Cell[3] { null, null, null };
            if (3 < normalCells.Count) {
                ShuffleHelper.Shuffle<Cell>(normalCells);
                // 처음 두 개를 동일한 블록으로 넣어주고
                Block firstBlock = blocks[this.Random.Next(0, blocks.Count)];
                for (int i = 0; i < 3; i++) {
                    Block beforeBlock = normalCells[i].block;
                    Block newBlock = Block.Factory(firstBlock);
                    normalCells[i].block = newBlock;
                    swaps.Add(new SwapBlock(normalCells[i], beforeBlock, newBlock));
                    twoCells[i] = normalCells[i];
                }
            }

            // 맞춤이 하나도 없는 상태로 깔아주고
            for (int r = 0; r < this.stage.rowCount; r++)
                for (int c = 0; c < this.stage.colCount; c++) {
                    Cell cell = this.stage.cells[r, c];
                    if (CellType.Alive == cell.type && 
                        CellAttr.NoRefresh != cell.attr &&
                        null != cell.block && 
                        cell.block.IsNormal)
                    {
                        if (cell != twoCells[0] && cell != twoCells[1] && cell != twoCells[2]) {
                            Block beforeBlock = cell.block;
                            Block afterBlock = Block.Factory(_Exclude(r, c));
                            cell.block = afterBlock;
                            swaps.Add(new SwapBlock(cell, beforeBlock, afterBlock));
                        }
                    }
                }

            return swaps;

            Block _Exclude(int row, int col) {
                Cell upCell = this.stage.GetCellSafely(row - 1, col);
                Cell leftCell = this.stage.GetCellSafely(row, col - 1);
                Block upBlock = 
                    null != upCell && CellType.Alive == upCell.type &&
                    null != upCell.block && upCell.block.IsNormal
                        ? upCell.block : null;
                Block leftBlock =
                    null != leftCell && CellType.Alive == leftCell.type &&
                    null != leftCell.block && leftCell.block.IsNormal
                        ? leftCell.block : null;
                Block block = null;
                for (;;) {
                    block = blocks[this.Random.Next(0, blocks.Count)];
                    if (null == upBlock && null == leftBlock)
                        break;
                    else if (null != upBlock && null == leftBlock && upBlock.type != block.type)
                        break;
                    else if (null == upBlock && null != leftBlock && leftBlock.type != block.type)
                        break;
                    else if (null != upBlock && null != leftBlock && upBlock.type != block.type && leftBlock.type != block.type)
                        break;
                }
                return block;
            }
        }

        private List<Cell> getCellWithNormalBlock() 
        {
            var result = new List<Cell>();
            for (int r = 0; r < this.stage.rowCount; r++)
                for (int c = 0; c < this.stage.colCount; c++) {
                    Cell cell = this.stage.cells[r, c];
                    if (CellType.Alive == cell.type && 
                        CellAttr.NoRefresh != cell.attr &&
                        null != cell.block && 
                        cell.block.IsNormal)
                    {
                        result.Add(cell);
                    }
                }
            return result;
        }
    }

    public static class ShuffleHelper 
    {
        private static System.Random Random = new System.Random();
        // 사용방법 : Shuffle<Cell>(tmp);
        public static void Shuffle<T>(this IList<T> list)  
        {  
            int n = list.Count;  
            while (n > 1) {  
                n--;  
                int k = Random.Next(n + 1);  
                T value = list[k];  
                list[k] = list[n];  
                list[n] = value;  
            }  
        }
    }

}