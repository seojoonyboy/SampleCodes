using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Artistar.Puzzle.Core;
using Snowballs.Client.Etc;
using Snowballs.Client.View;
using Unity.VisualScripting;
using Cysharp.Threading.Tasks;

public class BlockController : BaseController
{
    public enum State
    {
        Init,

        Idle,
        Gravity,
        
        RightLookup,
        LeftLookup,
        TopLookup,
        BottomKookup,
        InJail,
    };

    private Block block = null;
    private GameObject blockObject = null;
    public GameObject BlockObject => this.blockObject;

    private GameObject normalBlockObject;
    public GameObject NormalBlockObject => this.normalBlockObject;

    [HideInInspector]
    public static int gravityCount = 0;
    private bool isGraviting = false;
    // public bool IsGraviting { get { return this.isGraviting; } }

    // 외부의 공격에 포인트변화를 알기 위한 값들
    private int prevNormalPoint = 0;
    private int prevNormalSidePoint = 0;
    private int prevSpecialPoint = 0;

    private State prevState = State.Init;
    private State state = State.Init;

    // 도시락 클리어 조건 검색에 사용된다.
    private bool prevIsCleared = false;

    [HideInInspector]
    public UniTask? coGravity = null;

    private static System.Random Random = new System.Random();

    public static BlockController Create(Block block, int row, int col, bool isGenesis = false, Action onFinishedCallback = null)
    {
        //TODO 에이티즈 분기 작업
        GameObject prefabObject = IngameBlockPoolController.SpawnBlock();
        prefabObject.name = "B-" + block.ToString();
        SBDebug.Log("SJW : " + prefabObject.name);
        
        // 블록콘트롤러 설정
        BlockController self = prefabObject.GetComponent<BlockController>();
        self.blockObject = prefabObject.transform.Find("Block").gameObject;
        block.state = BlockState.Fixed;
        self.block = block;
        
        prefabObject.transform.SetParent(stageController.transform);
        
        SpriteRenderer sr = self.blockObject.GetComponent<SpriteRenderer>();
        sr.sprite = Resources.Load("Blocks/110/" + GetSpriteName(block), typeof(Sprite)) as Sprite;
        sr.enabled = true;
        
        NormalBlockController normalSelf = null;
        
        switch (CommonProcessController.GetNameString())
        {
            case CommonProcessController.KWONEUNBIINFO:
            case CommonProcessController.IKONINFO:    
                if (block.IsNormal)
                {
                    // 스태틱이라 함수 처리 x
                    // 노멀블록 프리팹 경로
                    StringBuilder blockAddress = new StringBuilder();
                    blockAddress.Append("GUI/Fx/Prefabs/Block/Normal/");
                    blockAddress.Append(GetSpriteName(block));
                    blockAddress.Append("/Block_");
                    blockAddress.Append(GetSpriteName(block));
                    
                    // 기존 프리팹 Block 밑에 추가로 생성
                    GameObject blockObj = (GameObject)Resources.Load(blockAddress.ToString());
                    var animBlock = Instantiate(blockObj, prefabObject.transform);
                    
                    self.normalBlockObject = prefabObject.transform.GetChild(1).gameObject;
                    normalSelf = self.normalBlockObject.GetComponent<NormalBlockController>();

                    if (block.isFirstBlock)
                    {
                        normalSelf.WaitRandomTime();
                    }
                }

                // 이미지로 처리한 블록들 안보이게 처리
                if (block.IsSpecial || block.IsNormal)
                    sr.enabled = false;
                break;
            // 김강장
            default: 
                break;
        }
        
        if (block.type == BlockType.CardSkill)
        {
            GameObject skillBlock = Resources.Load("Blocks/CardSkill/SkillBlock") as GameObject;
            GameObject skillBlockObj = Instantiate(skillBlock, self.blockObject.transform);
            skillBlockObj.name = "SkillBlock";
            
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name.Contains("Test"))
            {
                Sprite skillBlockTexture = Resources.Load<Sprite>("Blocks/CardSkill/999");
                SpriteRenderer skillBlockBackSr = skillBlockObj.transform.Find("Back").GetComponent<SpriteRenderer>();
                skillBlockBackSr.sprite = skillBlockTexture;
            }
            else
            {
                var skillBlockTexture = block.skillBlockTexture;
                float width = skillBlockTexture.width;
                float height = skillBlockTexture.height;

                SpriteRenderer skillBlockBackSr = skillBlockObj.transform.Find("Animation_Controller/Back").GetComponent<SpriteRenderer>();
                skillBlockBackSr.sprite = Sprite.Create(skillBlockTexture, new Rect(0.0f, 0.0f, width, height), new Vector2(0.5f, 0.5f));
                skillBlockBackSr.transform.localScale = new Vector3(100.0f / width, 100.0f / height, 1.0f);
            }
            
            
        }
        
        //특수블록인 경우
        if ((int)block.type / 100 == 5) {
            sr.enabled = false;  
        }

        self.transform.localPosition = self.GetLocalPosition(row, col);

        // 제네시스 블럭에서 생성된 경우 처음 시작은 투명하게
        if (isGenesis) {
            switch (CommonProcessController.GetNameString())
            {
                case CommonProcessController.KWONEUNBIINFO:
                case CommonProcessController.IKONINFO:    
                    FadeNormalBlock(normalSelf, 0.0f, false);
                    break;
                default:
                    FadeNormalBlockAteez(sr);
                    break;
            }
        }
        // Order In Layer 값 변경
        if (block.IsNormal || block.IsMission)
            sr.sortingOrder = 10;
        else if (block.IsSpecial)
            sr.sortingOrder = 20;

        switch (block.type) {
            case BlockType.RainbowBalloonBox:
            case BlockType.RedBalloonBox:
            case BlockType.YellowBalloonBox:
            case BlockType.GreenBalloonBox:
            case BlockType.PurpleBalloonBox:
                self.prevNormalSidePoint = block.pieces.Count;
                break;
            default:
                self.prevNormalPoint = block.normalPoint;
                self.prevNormalSidePoint = block.normalSidePoint;
                self.prevSpecialPoint = block.specialPoint;
                break;
        }

        // 생성 이팩트
        GameObject prefabCreation = null;
        switch (block.type) {
            case BlockType.Rocket:
                prefabCreation = IngameEffectPrefabLoader.Instance
                    .GetBlockCreatePrefab(block.type);
                switch (CommonProcessController.GetNameString())
                {
                    case CommonProcessController.KWONEUNBIINFO:
                    case CommonProcessController.IKONINFO:    
                        if (SceneController.currentSceneSceneId == SceneController.Scene.InGame 
                            && !PopupRoot.Instance.IsPopupExist())
                        {
                            CommonProcessController.PlayEffectSound("Ingame", 6);
                            CommonProcessController.MuteEffectSound("Ingame", 0);
                        }

                        CreateEffect(prefabCreation);
                        WaitForSec(0.5f, () =>
                        {
                            onFinishedCallback?.Invoke();
                        });
                        break;
                    default:
                        GameObject crossObj = CreateEffectAteez(prefabCreation);
                        SetSpecialAteez(block.type, crossObj, onFinishedCallback);
                        break;
                }
                break;
            case BlockType.PaperPlane:
                if (SceneController.currentSceneSceneId == SceneController.Scene.InGame 
                    && !PopupRoot.Instance.IsPopupExist())
                {
                    CommonProcessController.MuteEffectSound("Ingame", 0);
                    CommonProcessController.PlayEffectSound("Ingame", 8);
                }

                prefabCreation = IngameEffectPrefabLoader.Instance
                    .GetBlockCreatePrefab(block.type);
                GameObject airplaneObj = null;
                switch (CommonProcessController.GetNameString())
                {
                    case CommonProcessController.KWONEUNBIINFO:
                    case CommonProcessController.IKONINFO:
                        airplaneObj = CreateEffect(prefabCreation);
                        break;
                    default:
                        airplaneObj = CreateEffectAteez(prefabCreation);
                        break;
                }
                
                AirplaneBlockEffect effectView = airplaneObj.GetComponent<AirplaneBlockEffect>();
                effectView.OnCreate(() =>
                {
                    onFinishedCallback?.Invoke();
                });
                break;
            case BlockType.Mirrorball:
                if (SceneController.currentSceneSceneId == SceneController.Scene.InGame
                    && !PopupRoot.Instance.IsPopupExist())
                {
                    CommonProcessController.MuteEffectSound("Ingame", 0);
                    CommonProcessController.PlayEffectSound("Ingame", 12);
                }

                prefabCreation = IngameEffectPrefabLoader.Instance
                    .GetBlockCreatePrefab(block.type);
                switch (CommonProcessController.GetNameString())
                {
                    case CommonProcessController.KWONEUNBIINFO:
                    case CommonProcessController.IKONINFO:
                        CreateEffect(prefabCreation);
                        WaitForSec(0.5f, () =>
                        {
                            onFinishedCallback?.Invoke();
                        });
                        break;
                    default:
                        GameObject mirrorBallAteezObj = CreateEffectAteez(prefabCreation);
                        SetSpecialAteez(block.type,mirrorBallAteezObj, onFinishedCallback);
                        break;
                }
                break;
            case BlockType.Bomb:
                // Debug.Log("Create Fx_Create_Bomb_01");
                prefabCreation = IngameEffectPrefabLoader.Instance
                    .GetBlockCreatePrefab(block.type);

                switch (CommonProcessController.GetNameString())
                {
                    case CommonProcessController.KWONEUNBIINFO:
                    case CommonProcessController.IKONINFO:    
                        if (SceneController.currentSceneSceneId == SceneController.Scene.InGame && 
                            !PopupRoot.Instance.IsPopupExist())
                        {
                            CommonProcessController.MuteEffectSound("Ingame", 0);
                            CommonProcessController.PlayEffectSound("Ingame", 3);
                        }

                        CreateEffect(prefabCreation);
                        WaitForSec(0.5f, () =>
                        {
                            onFinishedCallback?.Invoke();
                        });
                        break;
                    default:
                        GameObject bombObj = CreateEffectAteez(prefabCreation);
                        SetSpecialAteez(block.type,bombObj, onFinishedCallback);
                        break;
                }
                break;
            case BlockType.ShootingStar:
                CommonProcessController.MuteEffectSound("Ingame", 0);
                CommonProcessController.PlayEffectSound("Ingame", 16);
                
                // Debug.Log("Create Fx_Create_StarRay_01");
                prefabCreation = IngameEffectPrefabLoader.Instance
                    .GetBlockCreatePrefab(block.type);
                switch (CommonProcessController.GetNameString())
                {
                    case CommonProcessController.KWONEUNBIINFO:
                    case CommonProcessController.IKONINFO:    
                        CreateEffect(prefabCreation);
                        WaitForSec(0.5f, () =>
                        {
                            onFinishedCallback?.Invoke();
                        });
                        break;
                    default:
                        GameObject finaleObj = CreateEffect(prefabCreation);
                        finaleObj = CreateEffectAteez(prefabCreation);
                        SetSpecialAteez(block.type,finaleObj, onFinishedCallback);
                        break;
                }
                break;
        }

        BlockLog.Instance.CreateBlock(block);
        return self;

        async UniTask WaitForSec(float waitTime, Action cb)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(waitTime));
            cb?.Invoke();
        }

        GameObject CreateEffect(GameObject prefabOrigin)
        {
            GameObject prefabObjectCreation = Instantiate(prefabOrigin, self.blockObject.transform);
            Canvas canvas = prefabObjectCreation.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            // Default로 통일
            canvas.sortingLayerName = "Default";

            return prefabObjectCreation;
        }
        
        GameObject CreateEffectAteez(GameObject prefabOrigin)
        {
            GameObject prefabObjectCreation = Instantiate(prefabOrigin, self.blockObject.transform);
            Canvas canvas = prefabObjectCreation.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingLayerName = "ParticleEffect";
            prefabObjectCreation.transform.localPosition = Vector3.zero;

            return prefabObjectCreation;
        }
    }

    public Vector3 GetLocalPosition(int row, int col)
    {
        // 세탁기는 55픽셀 우하단으로
        if (BlockType.Stand == block.type ||
            BlockType.Pizza == block.type ||
            BlockType.Fridge == block.type ||
            BlockType.Fishbowl == block.type ||
            BlockType.RainbowBalloonBox == block.type ||
            BlockType.RedBalloonBox == block.type ||
            BlockType.YellowBalloonBox == block.type ||
            BlockType.GreenBalloonBox == block.type ||
            BlockType.PurpleBalloonBox == block.type ||
            BlockType.Vault == block.type)
            return GetPositionByMatrix(row, col, 55f, 55f);
        else
            return GetPositionByMatrix(row, col);
    }

    public static void AddCombineEffect(BlockType blockType1, BlockType blockType2, Transform parent)
    {
        GameObject combineEffectPrefab = IngameEffectPrefabLoader.Instance
            .GetBlockCombinePrefab();
        GameObject combineObj = Instantiate(combineEffectPrefab, parent);
        combineObj.transform.localPosition = Vector3.zero;
        SelfDestroy selfDestroy = combineObj.AddComponent<SelfDestroy>();
        selfDestroy.interval = 3.0f;
        
        var targetSprite1 = Resources.Load("Blocks/110/" + (int)blockType1, typeof(Sprite)) as Sprite;
        var targetSprite2 = Resources.Load("Blocks/110/" + (int)blockType2, typeof(Sprite)) as Sprite;
        
        switch (CommonProcessController.GetNameString())
        {
            case CommonProcessController.KWONEUNBIINFO:
            case CommonProcessController.IKONINFO:    
                combineObj.transform.Find("Block_01").GetComponent<SpriteRenderer>().sprite = targetSprite1;
                combineObj.transform.Find("Block_02").GetComponent<SpriteRenderer>().sprite = targetSprite2;
                break;
            // 에이티즈
            default:
                combineObj.transform.Find("Block_01").GetComponent<Image>().sprite = targetSprite1;
                combineObj.transform.Find("Block_02").GetComponent<Image>().sprite = targetSprite2;
                break;
        }
    }

    private void Start()
    {
        // 아이들 모션 시작
        if (null != this.block && 
            (this.block.IsNormal || this.block.IsSpecial) && 
            BlockAttr.Movable == this.block.attr)
        {
            this.state = State.Idle;
        }
    }

    private void Update()
    {
        // Vector2 v = this.transform.localPosition;
        // int row, col;
        // this.GetMatrixByPosition(v, out row, out col);
        // Cell c = this.stage.cells[row, col];
        // UnityEngine.UI.Text txt = this.transform.Find("DEBUG").Find("LOG").gameObject.GetComponent<UnityEngine.UI.Text>();
        // if (null != c.block)
        //     txt.text = ((int)(this.block.type)).ToString() + " " + ((int)(c.block.type)).ToString();
        // else 
        //     txt.text = ((int)(this.block.type)).ToString();

        // TODO: 미션블럭 등은 HP의 값에 따라 다른 스프라이트를 가지게 되므로 HP값에 따른 스프라이트를 Update에서 교체처리하도록 한다.
        if (null != block) {
            switch (block.type) {
                case BlockType.Red:
                case BlockType.Blue:
                case BlockType.Green:
                case BlockType.Yellow:
                case BlockType.Purple:
                case BlockType.Pink:
                case BlockType.Brown:
                case BlockType.PaperPlane:
                case BlockType.Rocket:
                case BlockType.Bomb:
                case BlockType.Mirrorball:
                case BlockType.ShootingStar:
                case BlockType.CardSkill:
                    // 감옥에서 빠져나온 노말 블럭을 아이들 상태로 둔다.
                    // 이동가능한 상태가 되면 아이들 모션으로
                    if (State.Idle != this.state && BlockAttr.Movable == this.block.attr)
                        this.state = State.Idle;
                    // 상태에 따라 모션을 변경해준다.
                    if (this.prevState != this.state) {
                        this.prevState = this.state;
                        if (State.Idle == this.state)
                            this.Idle().Forget();
                    }
                    break;
                case BlockType.Luckyball:
                case BlockType.Woodbox:
                case BlockType.Stand:
                // V2
                case BlockType.TeaCup:
                case BlockType.Vase:
                case BlockType.Pumpkin:
                case BlockType.Lantern:
                case BlockType.RedWoodbox:
                case BlockType.YellowWoodbox:
                case BlockType.GreenWoodbox:
                case BlockType.PurpleWoodbox:
                case BlockType.Pizza:
                case BlockType.Fridge:
                case BlockType.Fishbowl:
                case BlockType.IceCube:
                case BlockType.TopiarySpring:
                case BlockType.TopiaryWinter:
                case BlockType.FloorLamp:
                    if (this.prevNormalSidePoint != block.normalSidePoint) {
                        SpriteRenderer sr = this.blockObject.GetComponent<SpriteRenderer>();
                        sr.sprite = Resources.Load("Blocks/110/" + GetSpriteName(this.block), typeof(Sprite)) as Sprite;
                        this.prevNormalSidePoint = block.normalSidePoint;
                        Play(
                            IngameEffectPrefabLoader.Instance.GetBlockExplosionPrefab(block.type),
                            this.transform.localPosition);
                    }
                    break;
                case BlockType.RainbowBalloonBox:
                case BlockType.RedBalloonBox:
                case BlockType.YellowBalloonBox:
                case BlockType.GreenBalloonBox:
                case BlockType.PurpleBalloonBox:
                    if (this.prevNormalSidePoint != block.pieces.Count) {
                        SpriteRenderer sr = this.blockObject.GetComponent<SpriteRenderer>();
                        sr.sprite = Resources.Load("Blocks/110/" + GetSpriteName(this.block), typeof(Sprite)) as Sprite;
                        this.prevNormalSidePoint = block.pieces.Count;
                        Play(
                            IngameEffectPrefabLoader.Instance.GetBlockExplosionPrefab(block.type),
                            this.transform.localPosition);
                    }
                    break;
                case BlockType.Gemstone:
                case BlockType.Coconut:
                case BlockType.CannedSoda:
                case BlockType.CannedFood:
                case BlockType.JewelStash:
                case BlockType.Vault:
                case BlockType.IronBox:
                    if (this.prevSpecialPoint != block.specialPoint) {
                        SpriteRenderer sr = this.blockObject.GetComponent<SpriteRenderer>();
                        sr.sprite = Resources.Load("Blocks/110/" + GetSpriteName(this.block), typeof(Sprite)) as Sprite;
                        this.prevSpecialPoint = block.specialPoint;
                    }
                    break;
                case BlockType.TicketBox:
                    if (null != this.block.clear && this.prevIsCleared != this.block.clear.IsCleared) {
                        SpriteRenderer sr = this.blockObject.GetComponent<SpriteRenderer>();
                        sr.sprite = Resources.Load("Blocks/110/" + GetSpriteName(this.block), typeof(Sprite)) as Sprite;
                        this.prevIsCleared = this.block.clear.IsCleared;
                    }
                    break;
            }
        } else {
            // 제거되지 않은 블럭 스프라이트를 없애준다.
            Destroy(this);
            Debug.Log("Forcelly Destroyed !!!" + block.type);
        }
    }

    public static string GetSpriteName(Block block)
    {
        switch (block.type) {
            case BlockType.Luckyball:
            case BlockType.Woodbox:
            case BlockType.Stand:
            // V2
            case BlockType.TeaCup:
            case BlockType.Vase:
            case BlockType.Pumpkin:
            case BlockType.Lantern:
            case BlockType.RedWoodbox:
            case BlockType.YellowWoodbox:
            case BlockType.GreenWoodbox:
            case BlockType.PurpleWoodbox:
            case BlockType.Pizza:
            case BlockType.Fridge:
            case BlockType.Fishbowl:
            case BlockType.IceCube:
            case BlockType.TopiarySpring:
            case BlockType.TopiaryWinter:
            case BlockType.FloorLamp:
                return ((int)block.type).ToString() + "-" + Math.Max(1, block.normalSidePoint).ToString();
            case BlockType.RainbowBalloonBox: {
                    int r = 0, y = 0, g = 0, p = 0;
                    foreach (Piece pi in block.pieces) {
                        switch (pi.blockType) {
                            case BlockType.Red: r = 1; break;
                            case BlockType.Yellow: y = 1; break;
                            case BlockType.Green: g = 1; break;
                            case BlockType.Purple: p = 1; break;
                        }
                    }
                    return ((int)block.type).ToString() + "-" + 
                        r.ToString() + 
                        y.ToString() +
                        g.ToString() +
                        p.ToString();
                }
            case BlockType.RedBalloonBox:
            case BlockType.YellowBalloonBox:
            case BlockType.GreenBalloonBox:
            case BlockType.PurpleBalloonBox:
                    return ((int)block.type).ToString() + "-" + Math.Max(1, block.pieces.Count).ToString();
            case BlockType.Coconut:
            case BlockType.CannedSoda:
            case BlockType.CannedFood:
            case BlockType.JewelStash:
            case BlockType.Vault:
            case BlockType.IronBox:
            case BlockType.Gemstone:
                return ((int)block.type).ToString() + "-" + Math.Max(1, block.specialPoint).ToString();
            case BlockType.TicketBox:
                if (null != block.clear && block.clear.IsCleared)
                    return ((int)block.type).ToString() + "-1";
                else
                    return ((int)block.type).ToString() + "-2";
            default: 
                return ((int)block.type).ToString();
        }
    }

    private async UniTask Idle()
    {
        // 랜덤으로 기다린다.
        await UniTask.Delay(TimeSpan.FromSeconds(BlockController.Random.Next(0, 200) / 100f));
        //yield return new WaitForSeconds((float)BlockController.Random.Next(0, 200) / 100f);
        if (this.IsDestroyed() || State.Idle != this.state)
            throw new OperationCanceledException();
            //yield break;
        Transform t = this.blockObject.transform;
        float tick = 0;
        for (;;) {
            if (! this.isGraviting) {
                tick += Time.deltaTime;
                int quotient = (int)Math.Truncate(tick/2f);
                float remainder = (tick/2f) % 1f;
                float upfloating = 0 == (quotient % 2) ? this.Linear(0f, 0.1f, remainder) : this.Linear(0.1f, 0f, remainder);
                float scale = 0 == (quotient % 2) ? this.Linear(1f, 1.04f, remainder) : this.Linear(1.04f, 1f, remainder);
                t.localPosition = new Vector3(0, upfloating, 0);
                t.localScale = new Vector3(scale, scale, 1);
                if (this.IsDestroyed() || State.Idle != this.state)
                    throw new OperationCanceledException();
                    //yield break;
            }

            await UniTask.NextFrame();
            //yield return null;
            if (this.IsDestroyed() || State.Idle != this.state)
                throw new OperationCanceledException();
                //yield break;
        }
    }

    public async UniTask Gravity(List<Toss> tosses)
    {
        this.isGraviting = true;
        BlockController.gravityCount++;
        // this.block.state = BlockState.Floating; <- Gravity를 호출하기 전에 해당하는 블록을 일괄 처리했다.
        try {
            const float durationToss = 0.135f;
            if (0 < tosses.Count)
                await UniTask.Delay(TimeSpan.FromSeconds(tosses[0].no * durationToss));
                //yield return new WaitForSeconds(tosses[0].no*durationToss);
            if (this.IsDestroyed())
                throw new OperationCanceledException();
                //yield break;
            SpriteRenderer sr = null;
            NormalBlockController nc = null;
            Toss toss = null;
            Vector3 from = new Vector3(0,0,0);
            Vector3 to = new Vector3(0,0,0);
            float tick = 0.0f;
            float endTick = tosses.Count * durationToss;

            float startX = null == tosses[0].from ?
                GetPositionByMatrix(tosses[0].toRow, tosses[0].toCol).x
                :
                GetPositionByMatrix(tosses[0].fromRow, tosses[0].fromCol).x;
            float endX = GetPositionByMatrix(tosses[tosses.Count-1].toRow, tosses[tosses.Count-1].toCol).x;
            
            while (tick < endTick) {

                // 이징 곡선을 적용하여 현재 위치를 얻는다.
                float offset = this.EaseOutBack(0, tosses.Count, tick/endTick);
                // 나머지 값을 얻어 오프셋을 설정한다.
                int n = Math.Min((int)offset, tosses.Count-1);
                float offsetD = offset - n;
                // 이동 스탭이 변경되었을 때 toss를 얻도록 처리
                if (toss != tosses[n]) {
                    toss = tosses[n];
                    // 워프인이라면 삭제 한다.
                    if (TossType.WrapIn == toss.type) {
                        this.FadeoutTo(toss.toRow, toss.toCol, durationToss).Forget();
                        await UniTask.NextFrame();
                        //yield return null;
                    }
                    if (null != toss.from) {
                        from = GetPositionByMatrix(toss.fromRow, toss.fromCol);
                        // sr = null;
                    } else {
                        from = GetPositionByMatrix(toss.toRow - 1, toss.toCol);
                        sr = this.blockObject.GetComponent<SpriteRenderer>();
                        if (this.normalBlockObject != null)
                        {
                            nc = this.normalBlockObject.GetComponent<NormalBlockController>();
                        }
                    }
                    to = GetPositionByMatrix(toss.toRow, toss.toCol);
                }
                // 이동
                this.transform.localPosition = new Vector3(this.Linear(from.x, to.x, offsetD), this.Linear(from.y, to.y, offsetD), 0f);

                // 경사 이동할 때 회전
                if (startX != endX) {
                    const float gap = 50f;
                    const float angle = -25f;
                    float x = this.transform.localPosition.x;
                    // 우측으로 이동
                    if (startX < endX) {
                        if (x <= startX + gap)
                            this.transform.rotation = Quaternion.Euler(0f, 0f, this.Linear(0f, -angle, (x-startX)/gap));
                        else if (startX + gap < x && x <= endX - gap)
                            this.transform.rotation = Quaternion.Euler(0f, 0f, -angle);
                        else 
                            this.transform.rotation = Quaternion.Euler(0f, 0f, this.Linear(0f, -angle, (endX-x)/gap));
                    } else {
                        if (startX - gap <= x)
                            this.transform.rotation = Quaternion.Euler(0f, 0f, this.Linear(0f, angle, (startX-x)/gap));
                        else if (endX + gap <= x && x < startX - gap)
                            this.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                        else 
                            this.transform.rotation = Quaternion.Euler(0f, 0f, this.Linear(0f, angle, (x-endX)/gap));
                    }
                }
                // 투명도
                if (null != sr) {
                    switch (CommonProcessController.GetNameString())
                    {
                        case CommonProcessController.KWONEUNBIINFO:
                        case CommonProcessController.IKONINFO: 
                            FadeNormalBlock(nc, EasingFunction.Linear(0f, 1f, tick/durationToss), true);
                            break;
                        default:
                            FadeNormalBlockAteez(sr);
                            break;
                    }
                }
                tick += Time.deltaTime;
                await UniTask.NextFrame();
            }
            // 최종값을 한 번 더 넣어서 스키핑에 의한 부정확을 맞춘다.
            // 최종위치값으로 넣어준다.
            toss = tosses[tosses.Count-1];
            to = GetPositionByMatrix(toss.toRow, toss.toCol);
            this.transform.localPosition = to;
            if (sr) {
                switch (CommonProcessController.GetNameString())
                {
                    case CommonProcessController.KWONEUNBIINFO:
                    case CommonProcessController.IKONINFO:
                        FadeNormalBlock(nc, 1f, true);
                        break;
                    default:
                        FadeNormalBlockAteez(sr);
                        break;
                }
            }
            // 미세 기울어짐 보정
            this.transform.rotation = Quaternion.Euler(0,0,0);
        } finally {
            block.state = BlockState.Fixed;
            BlockController.gravityCount--;
            this.isGraviting = false;
            this.coGravity = null;
        }
    }

    private static void FadeNormalBlockAteez(SpriteRenderer sr)
    {
        Color color = sr.color;
        color.a = 1f;
        sr.color = color;
    }
    private static void FadeNormalBlock(NormalBlockController nc ,float alpha ,bool check)
    {
        if (nc != null)
        {
            // 기존 a값 변경
            foreach (var t in nc.Obj)
            {
                Color normalColor = t.color;
                normalColor.a = alpha;
                t.color = normalColor;
            }
            // 기존 노멀블록은 sprite 한개만 관리하지만
            if (nc.Animators != null)
            {
                if (alpha.Equals(1.0f))
                {
                    nc.Animators.enabled = check;
                }
            }

            foreach (var effect in nc.Effects)
            {
                if(alpha.Equals(1.0f))
                    effect.SetActive(check);
            }
        }
    }

    // 블럭을 특정 위치로 이동 후 제거
    // 일반매칭으로 제거되는 블럭 효과
    public async UniTask RemoveTo(int toRow, int toCol, float duration)
    {
        BlockState prevState = this.block.state;
        this.block.state = BlockState.Floating;
        try {
            // 이름을 미리 바꿔서 삭제 대기중으로 한다 -> 검색이 안되도록 하는 이유
            this.gameObject.name = "Removing_" + this.gameObject.name;
            float tick = 0;
            Vector3 from = new Vector3(this.transform.localPosition.x, this.transform.localPosition.y, this.transform.localPosition.z);
            Vector3 to = GetPositionByMatrix(toRow, toCol);
            while (tick <= duration) {
                this.transform.localPosition = new Vector3(
                        EasingFunction.EaseInCirc(from.x, to.x, tick/duration),
                        EasingFunction.EaseInCirc(from.y, to.y, tick/duration),
                        from.z
                    );
                tick += Time.deltaTime;
                await UniTask.NextFrame();
            }
            this.transform.localPosition = to;
            await UniTask.NextFrame();

        } finally {
            this.block.state = BlockState.Fixed;
            IngameBlockPoolController.CheckFirstBlock(this.block);
            IngameBlockPoolController.ReleaseNormalBlock(this.gameObject);
        }
    }

    // 블럭을 특정 위치로 이동 후 제거
    // 일반매칭으로 제거되는 블럭 효과
    public async UniTask FadeoutTo(int toRow, int toCol, float duration)
    {
        BlockState prevState = this.block.state;
        this.block.state = BlockState.Floating;
        try {
            // 이름을 미리 바꿔서 삭제 대기중으로 한다 -> 검색이 안되도록 하는 이유
            this.gameObject.name = "Removing_" + this.gameObject.name;
            float tick = 0;
            Vector3 from = new Vector3(this.transform.localPosition.x, this.transform.localPosition.y, this.transform.localPosition.z);
            Vector3 to = GetPositionByMatrix(toRow, toCol);
            SpriteRenderer sr = this.blockObject.GetComponent<SpriteRenderer>();
            while (tick <= duration) {
                this.transform.localPosition = new Vector3(
                        EasingFunction.EaseInCirc(from.x, to.x, tick/duration),
                        EasingFunction.EaseInCirc(from.y, to.y, tick/duration),
                        from.z
                    );
                // 투명도
                if (null != sr) {
                    Color color = sr.color;
                    color.a = EasingFunction.EaseOutCirc(1f, 0f, tick/duration);
                    sr.color = color;
                }
                tick += Time.deltaTime;
                await UniTask.NextFrame();
            }
            this.transform.localPosition = to;
            await UniTask.NextFrame();
            //yield return null;

        } finally {
            switch (CommonProcessController.GetNameString())
            {
                case CommonProcessController.KWONEUNBIINFO:
                case CommonProcessController.IKONINFO: 
                    
                    break;
                default:
                    break;
            }
            this.block.state = BlockState.None;
            IngameBlockPoolController.CheckFirstBlock(this.block);
            IngameBlockPoolController.ReleaseNormalBlock(this.gameObject);
        }
    }

    public async UniTask Explode(float duration = 0f)
    {
        BlockState prevState = this.block.state;
        this.block.state = BlockState.Floating;
        try
        {
            if (this == null) throw new OperationCanceledException();
            
            // 이름을 미리 바꿔서 삭제 대기중으로 한다 -> 검색이 안되도록 하는 이유
            this.gameObject.name = "Removing_" + this.gameObject.name;
            
            switch (this.block.type) {
                case BlockType.Red:
                case BlockType.Blue:
                case BlockType.Green:
                case BlockType.Yellow:
                case BlockType.Purple:
                case BlockType.Pink:
                case BlockType.Brown:
                    CommonProcessController.PlayEffectSound("Ingame", 0, 0.7f);
                    
                    var normalPrefab = IngameEffectPrefabLoader.Instance.GetBlockExplosionPrefab(block.type);
                    Play(
                        normalPrefab,
                        this.transform.localPosition
                    );
                    
                    break;
                
                case BlockType.Bomb:
                case BlockType.Rocket:
                case BlockType.PaperPlane:
                case BlockType.Mirrorball:
                case BlockType.ShootingStar:
                case BlockType.CardSkill:
                    if (stageController.shootingStarState == StageController.ShootingState.Play)
                    {
                        ShootingStarFromSpecialBlockEffect();
                    }
                    break;
                    
                case BlockType.BombBomb:
                    CommonProcessController.MuteEffectSound("Ingame", 0);
                    CommonProcessController.PlayEffectSound("Ingame", 5);
                    
                    var bombBombObj = Play(
                        IngameEffectPrefabLoader.Instance.GetBlockExplosionPrefab(block.type),
                        this.transform.localPosition,
                        false);
                    break;
                case BlockType.Woodbox:
                    GameObject woodPrefab = IngameEffectPrefabLoader.Instance
                        .GetBlockExplosionPrefab(block.type);
                    GameObject instantiatedWoodPrefab = Instantiate(woodPrefab);
                    instantiatedWoodPrefab.transform.position = transform.position;
                    instantiatedWoodPrefab.transform.localScale = Vector3.one;
                    break;
                case BlockType.IceCube:
                case BlockType.TopiarySpring:
                case BlockType.TopiaryWinter:
                case BlockType.FloorLamp:
                case BlockType.RedWoodbox:
                case BlockType.YellowWoodbox:
                case BlockType.GreenWoodbox:
                case BlockType.PurpleWoodbox:
                case BlockType.Fishbowl:
                case BlockType.Stand:
                    //터지는 효과
                    GameObject explodePrefab = IngameEffectPrefabLoader.Instance
                        .GetBlockExplosionPrefab(block.type);
                    GameObject explodeObj = Instantiate(explodePrefab);
                    explodeObj.transform.position = transform.position;
                    break;
                case BlockType.Pizza:
                case BlockType.Fridge:
                    break;
            }

            await UniTask.Delay(TimeSpan.FromSeconds(duration));
        } finally {
            this.block.state = prevState;
            IngameBlockPoolController.CheckFirstBlock(this.block);
            
            if(this.gameObject != null) IngameBlockPoolController.ReleaseNormalBlock(this.gameObject);
        }
    }

    public static async UniTask WaitForSec(float waitTime, Action cb)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(waitTime));
        cb?.Invoke();
    }

    // 십자폭탄 효과를 넣어주고
    public static void CrossEffect(Cell cell, BlockController mirrorRocket=null) {
        var pos = GetPositionByMatrix(cell.row, cell.col);
        if (mirrorRocket != null)
        {
            pos = mirrorRocket.transform.position;
        }

        var crossObj = BlockController.Play(
            IngameEffectPrefabLoader.Instance.GetBlockExplosionPrefab(BlockType.Rocket), 
            pos,
            false);
        float crossObjClipLength = crossObj.transform.Find("Fx_Holder").GetComponent<Animation>().clip.length;
        WaitForSec(crossObjClipLength, () => { Destroy(crossObj); }).Forget();
    }
    
    public static void BombEffect(Cell cell, BlockController mirrorBomb=null)
    {
        CommonProcessController.MuteEffectSound("Ingame", 0);
        CommonProcessController.PlayEffectSound("Ingame", 4);
        
        var pos = GetPositionByMatrix(cell.row, cell.col);
        if (mirrorBomb != null)
        {
            pos = mirrorBomb.transform.position;
        }
        
        var bombObj = Play(
            IngameEffectPrefabLoader.Instance.GetBlockExplosionPrefab(BlockType.Bomb), 
            pos,
            false);
        WaitForSec(1.0f, () => { Destroy(bombObj); }).Forget();
    }

    public static GameObject MirrorBallIdleEffect(Cell cell)
    {
        CommonProcessController.MuteEffectSound("Ingame", 0);
        CommonProcessController.PlayEffectSound("Ingame", 13);
        
        var idleObj =
            Play(
                IngameEffectPrefabLoader.Instance.GetMirrorBallIdlePrefab(),
                GetPositionByMatrix(cell.row, cell.col),
                false
            );
        
        return idleObj;
    }
    
    public static GameObject MirrorBallSoulEffect(Cell cell)
    {
        var soulObj=
            Play(
                IngameEffectPrefabLoader.Instance.GetMirrorBallAbsorptionPrefab(),
            GetPositionByMatrix(cell.row, cell.col),
                false
        );
        
        return soulObj;
    }

    public static void ShootingStarEffect(Cell cell)
    {
        var shootingStarObj = 
            Play(
            IngameEffectPrefabLoader.Instance.GetBlockExplosionPrefab(BlockType.ShootingStar), 
            GetPositionByMatrix(cell.row, cell.col),
            false
            );
        shootingStarObj.transform.localScale = new Vector3(1f, 1f, 1f);
    }
    
    public void ShootingStarFromSpecialBlockEffect()
    {
        CommonProcessController.PlayEffectSound("Ingame", 0, 0.7f);
        
        Play(
            IngameEffectPrefabLoader.Instance.GetSpecialBlockFromShootingStarPrefab(),
            this.transform.localPosition
        );
    }
    
    public static GameObject Play(GameObject prefab, Vector3 pos, bool selfDestroy = true)
    {
        GameObject root = GameObject.Find("Stage");
        GameObject prefabObject = Instantiate(prefab, root.transform);
        if (selfDestroy) prefabObject.AddComponent<SelfDestroy>();
        prefabObject.transform.localPosition = pos;
        return prefabObject;
    }

    
    public async UniTask Touch() 
    {
        if (null != BaseController.stageObject.transform.Find("B" + this.block.ToJObject() + "_touch"))
            throw new OperationCanceledException();
            //yield break;
        float tick = 0;
        GameObject prefab = null;
        switch (this.block.type) {
            case BlockType.Red: prefab = IngameEffectPrefabLoader.Instance.GetBlockTouchPrefab(BlockType.Red); break;
            case BlockType.Yellow : prefab = IngameEffectPrefabLoader.Instance.GetBlockTouchPrefab(BlockType.Yellow); break;
            case BlockType.Blue : prefab = IngameEffectPrefabLoader.Instance.GetBlockTouchPrefab(BlockType.Blue); break;
            case BlockType.Green : prefab = IngameEffectPrefabLoader.Instance.GetBlockTouchPrefab(BlockType.Green); break;
            case BlockType.Purple : prefab =IngameEffectPrefabLoader.Instance.GetBlockTouchPrefab(BlockType.Purple); break;
            case BlockType.Pink : prefab = IngameEffectPrefabLoader.Instance.GetBlockTouchPrefab(BlockType.Pink); break;
            case BlockType.Brown : prefab = IngameEffectPrefabLoader.Instance.GetBlockTouchPrefab(BlockType.Brown); break;
        }
        if (null != prefab) {
            GameObject root = BaseController.stageObject;
            GameObject prefabObject = Instantiate(prefab, root.transform);
            prefabObject.name = "B" + this.block.ToJObject() + "_touch";
            try {
                while (tick < 2.0f) {
                    if (!stageController.isIdling || stageController.isGraviting) { return; }
                    
                    Vector3 worldPoint = this.blockObject.transform.TransformPoint(this.blockObject.transform.localPosition);
                    Vector3 localPoint = root.transform.InverseTransformPoint(worldPoint);
                    prefabObject.transform.localPosition = localPoint;
                    tick += Time.deltaTime;
                    await UniTask.NextFrame();
                }
            } finally {
                Destroy(prefabObject);
            }
        }
    }

    public async UniTask MoveTo(int row, int col, float duration,CancellationTokenSource cts)
    {
        CommonProcessController.PlayEffectSound("Ingame", 2);

        this.block.state = BlockState.Floating;
        try {
            float tick = 0;
            Vector3 from = new Vector3(this.transform.localPosition.x, this.transform.localPosition.y, this.transform.localPosition.z);
            Vector3 to = GetPositionByMatrix(row, col);
            while (tick <= duration) {
                this.transform.localPosition = new Vector3(
                        EasingFunction.EaseInOutCubic(from.x, to.x, tick/duration),
                        EasingFunction.EaseInOutCubic(from.y, to.y, tick/duration),
                        from.z
                    );
                tick += Time.deltaTime;
                await UniTask.NextFrame();
            }
            this.transform.localPosition = to;
            await UniTask.NextFrame();
        } finally
        {
            this.block.state = BlockState.Fixed;
            await UniTask.Delay(1, cancellationToken: cts.Token);
            if (cts.IsCancellationRequested) throw new OperationCanceledException();
        }
    }

    public async UniTask MoveToReturnly(int row, int col, float duration)
    {
        this.block.state = BlockState.Floating;
        try {
            float tick = 0;
            Vector3 from = new Vector3(this.transform.localPosition.x, this.transform.localPosition.y, this.transform.localPosition.z);
            Vector3 to = GetPositionByMatrix(row, col);
            while (tick <= duration) {
                this.transform.localPosition = new Vector3(
                        EasingFunction.EaseInOutCubic(from.x, to.x, tick/duration),
                        EasingFunction.EaseInOutCubic(from.y, to.y, tick/duration),
                        from.z
                    );
                tick += Time.deltaTime;
                await UniTask.NextFrame();
            }
            this.transform.localPosition = to;
            await UniTask.NextFrame();
            tick = 0;
            while (tick <= duration) {
                this.transform.localPosition = new Vector3(
                        EasingFunction.EaseInOutCubic(to.x, from.x, tick/duration),
                        EasingFunction.EaseInOutCubic(to.y, from.y, tick/duration),
                        to.z
                    );
                tick += Time.deltaTime;
                await UniTask.NextFrame();
            }
            this.transform.localPosition = from;
            await UniTask.NextFrame();
        } finally {
            this.block.state = BlockState.Fixed;
        }
    }

    public async UniTask ThreatedBySideAttack(int row, int col, float duration)
    {
        float tick = 0;
        Transform t = this.blockObject.transform;

        while (tick <= duration) {
            float scale = EasingFunction.EaseOutQuint(1f, 1.2f, tick/duration);
            t.localScale = new Vector3(scale, scale, 1);
            tick += Time.deltaTime;
            await UniTask.Yield();
        }
        t.localScale = new Vector3(1f, 1.2f, 1f);
        await UniTask.Yield();

        while (tick <= duration) {
            float scale = EasingFunction.EaseInQuint(1.2f, 1f, tick/duration);
            t.localScale = new Vector3(scale, scale, 1);
            tick += Time.deltaTime;
            await UniTask.Yield();
        }
        t.localScale = new Vector3(1f, 1f, 1f);
    }

    // 매칭할 블럭이 없어서 블럭이 교체되었을 경우의 애니메이션
    public void SwapNewBlock(Block newBlock)
    {
        // 블록콘트롤러 설정
        this.block = newBlock;
        this.blockObject.GetComponent<SpriteRenderer>().sprite = Resources.Load("Blocks/110/" + (int)block.type, typeof(Sprite)) as Sprite;
        this.name = "B-" + newBlock.ToString();
        var prefabObject = this.blockObject.transform.parent;

        switch (CommonProcessController.GetNameString())
        {
            case CommonProcessController.KWONEUNBIINFO:
            case CommonProcessController.IKONINFO:    
                // 매칭되고 나서 다시 해당하는 프리팹 생성
                CreateNormalBlockPrefab(prefabObject);
        
                IngameBlockPoolController.CheckFirstBlock(this.block);
                // 그전에 만들어진 프리팹 반환
                IngameBlockPoolController.ReleaseNormalBlock(prefabObject.transform.GetChild(1).gameObject);
                break;
            default:
                break;
        }
    }

    private void CreateNormalBlockPrefab(Transform createPos)
    {
        // 노멀블록 프리팹 경로
        StringBuilder blockAddress = new StringBuilder();
        blockAddress.Append("GUI/Fx/Prefabs/Block/Normal/");
        blockAddress.Append(GetSpriteName(block));
        blockAddress.Append("/Block_");
        blockAddress.Append(GetSpriteName(block));
                    
        // 기존 프리팹 Block 밑에 추가로 생성
        GameObject blockObj = (GameObject)Resources.Load(blockAddress.ToString());
        var animBlock = Instantiate(blockObj, createPos);
    }


    #region ATEEZ

    private static void SetSpecialAteez(BlockType type, GameObject specialObj,Action onFinishedCallback)
    {
        switch (type)
        {
            case BlockType.Rocket:
                specialObj.transform.localScale = new Vector3(0.01f, 0.01f, 1);
                float crossObjClipLength = specialObj.GetComponent<Animation>().clip.length;
                
                WaitForSec(crossObjClipLength, () =>
                {
                    onFinishedCallback?.Invoke();
                });
                break;
            case BlockType.Mirrorball:
                float mirrorBallClipLength = specialObj.GetComponent<Animation>().clip.length;
                WaitForSec(mirrorBallClipLength, () =>
                {
                    onFinishedCallback?.Invoke();
                });
                break;
            case BlockType.Bomb:
                float bombObjClipLength = specialObj.GetComponent<Animation>().clip.length;
                WaitForSec(bombObjClipLength, () =>
                {
                    onFinishedCallback?.Invoke();
                });
                break;
            case BlockType.ShootingStar:
                float finaleClipLength = specialObj.GetComponent<Animation>().clip.length;
                WaitForSec(finaleClipLength, () =>
                {
                    onFinishedCallback?.Invoke();
                });
                break;
        }
    }

    private static void DestroyAirplaneAteez(GameObject airplaneObj)
    {
        if(airplaneObj != null) airplaneObj.AddComponent<SelfDestroy>();
    }
    

    #endregion
    

    ////////////////// COORDINATION FUNCITONS //////////////////

    private float Linear(float start, float end, float value)
    {
        return start + (end - start) * value;
    }

    private float EaseOutBack(float start, float end, float value)
    {
        float s = 0.7f; //1.70158f;
        end -= start;
        value = (value) - 1;
        return end * ((value) * value * ((s + 1f) * value + s) + 1f) + start;
    }

}
