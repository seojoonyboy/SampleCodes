using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Artistar.Puzzle.Core;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    public Coroutine coGravity = null;

    private static System.Random Random = new System.Random();

    public static BlockController Create(Block block, int row, int col, bool isGenesis = false, Action onFinishedCallback = null)
    {
        GameObject prefab =
            block.type == BlockType.CardSkill ?
            Resources.Load<GameObject>("Prefabs/cardSkillPrefab")
            :
            Resources.Load<GameObject>("Prefabs/blockPrefab");

        // 프리팹으로 부터 생성하고
        GameObject prefabObject = Instantiate(prefab, BaseController.stageObject.transform);
        prefabObject.name = "B-" + block.ToString();
        // 블록콘트롤러 설정
        BlockController self = prefabObject.GetComponent<BlockController>();
        self.blockObject = prefabObject.transform.Find("Block").gameObject;
        self.block = block;
        SpriteRenderer sr = self.blockObject.GetComponent<SpriteRenderer>();
        sr.sprite = Resources.Load("Blocks/110/" + GetSpriteName(block), typeof(Sprite)) as Sprite;

        if (block.type == BlockType.CardSkill)
        {
            GameObject skillBlock = Resources.Load("Blocks/CardSkill/SkillBlock") as GameObject;
            GameObject skillBlockObj = Instantiate(skillBlock, self.blockObject.transform);

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

                SpriteRenderer skillBlockBackSr = skillBlockObj.transform.Find("Back").GetComponent<SpriteRenderer>();
                skillBlockBackSr.sprite = Sprite.Create(skillBlockTexture, new Rect(0.0f, 0.0f, width, height), new Vector2(0.5f, 0.5f));
                skillBlockBackSr.transform.localScale = new Vector3(100.0f / width, 100.0f / height, 1.0f);
            }

        }

        if ((int)block.type / 100 == 5) {
            sr.enabled = false;
        }

        self.transform.localPosition = self.GetLocalPosition(row, col);

        // 제네시스 블럭에서 생성된 경우 처음 시작은 투명하게
        if (isGenesis) {
            Color color = sr.color;
            color.a = 0f;
            sr.color = color;
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
                // Debug.Log("Create Fx_Create_Missile_01");
                prefabCreation = IngameFxResource.Instance
                    .GetPrefab(IngameFxResource.PrefabType.CreateMissile);
                GameObject crossObj = CreateEffect(prefabCreation);
                crossObj.transform.localScale = new Vector3(0.01f, 0.01f, 1);
                float crossObjClipLength = crossObj.GetComponent<Animation>().clip.length;
                CoroutineTaskManager.AddTask(WaitForSec(crossObjClipLength, () =>
                {
                    onFinishedCallback?.Invoke();
                    //crossObj.AddComponent<SelfDestroy>();
                }));
                break;
            case BlockType.PaperPlane:
                // Debug.Log("Create Fx_Create_Airplane_01");
                prefabCreation = IngameFxResource.Instance
                    .GetPrefab(IngameFxResource.PrefabType.CreateAirplane);
                GameObject airplaneObj = CreateEffect(prefabCreation);
                AirplaneBlockEffect effectView = airplaneObj.GetComponent<AirplaneBlockEffect>();
                effectView.OnCreate(() =>
                {
                    onFinishedCallback?.Invoke();
                    if(airplaneObj != null) airplaneObj.AddComponent<SelfDestroy>();
                });
                break;
            case BlockType.Mirrorball:
                // Debug.Log("Create Fx_Create_MirrorBall_01");
                prefabCreation = IngameFxResource.Instance
                    .GetPrefab(IngameFxResource.PrefabType.CreateMirrorBall);
                GameObject mirrorBallObj = CreateEffect(prefabCreation);
                float mirrorBallClipLength = mirrorBallObj.GetComponent<Animation>().clip.length;
                CoroutineTaskManager.AddTask(WaitForSec(mirrorBallClipLength, () =>
                {
                    onFinishedCallback?.Invoke();
                    if(mirrorBallObj != null) mirrorBallObj.AddComponent<SelfDestroy>();
                }));
                break;
            case BlockType.Bomb:
                // Debug.Log("Create Fx_Create_Bomb_01");
                prefabCreation = IngameFxResource.Instance
                    .GetPrefab(IngameFxResource.PrefabType.CreateBomb);
                GameObject bombObj = CreateEffect(prefabCreation);
                float bombObjClipLength = bombObj.GetComponent<Animation>().clip.length;
                CoroutineTaskManager.AddTask(WaitForSec(bombObjClipLength, () =>
                {
                    onFinishedCallback?.Invoke();
                    // bombObj.AddComponent<SelfDestroy>();
                }));
                break;
            case BlockType.ShootingStar:
                // Debug.Log("Create Fx_Create_StarRay_01");
                prefabCreation = IngameFxResource.Instance
                    .GetPrefab(IngameFxResource.PrefabType.CreateStarRays);
                GameObject finaleObj = CreateEffect(prefabCreation);
                float finaleClipLength = finaleObj.GetComponent<Animation>().clip.length;
                CoroutineTaskManager.AddTask(WaitForSec(finaleClipLength, () =>
                {
                    onFinishedCallback?.Invoke();
                    if(finaleObj != null) finaleObj.AddComponent<SelfDestroy>();
                }));
                break;
        }
        return self;

        IEnumerator WaitForSec(float waitTime, Action cb)
        {
            yield return new WaitForSeconds(waitTime);
            cb?.Invoke();
        }

        GameObject CreateEffect(GameObject prefabOrigin)
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
        GameObject combineEffectPrefab = IngameFxResource.Instance
            .GetPrefab(IngameFxResource.PrefabType.BlockCombine);
        GameObject combineObj = Instantiate(combineEffectPrefab, parent);
        combineObj.transform.localPosition = Vector3.zero;
        SelfDestroy selfDestroy = combineObj.AddComponent<SelfDestroy>();
        selfDestroy.interval = 3.0f;

        var targetSprite1 = Resources.Load("Blocks/110/" + (int)blockType1, typeof(Sprite)) as Sprite;
        var targetSprite2 = Resources.Load("Blocks/110/" + (int)blockType2, typeof(Sprite)) as Sprite;
        combineObj.transform.Find("Block_01").GetComponent<Image>().sprite = targetSprite1;
        combineObj.transform.Find("Block_02").GetComponent<Image>().sprite = targetSprite2;
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
                            StartCoroutine(this.Idle());
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
                            IngameFxResource.Instance.GetPrefab(IngameFxResource.PrefabType.NormalExplosion),
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
                            IngameFxResource.Instance.GetPrefab(IngameFxResource.PrefabType.NormalExplosion),
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

    private IEnumerator Idle()
    {
        // 랜덤으로 기다린다.
        yield return new WaitForSeconds((float)BlockController.Random.Next(0, 200) / 100f);
        if (this.IsDestroyed() || State.Idle != this.state)
            yield break;
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
                    yield break;
            }
            yield return null;
            if (this.IsDestroyed() || State.Idle != this.state)
                yield break;
        }
    }

    public IEnumerator Gravity(List<Toss> tosses)
    {
        this.isGraviting = true;
        BlockController.gravityCount++;
        // this.block.state = BlockState.Floating; <- Gravity를 호출하기 전에 해당하는 블록을 일괄 처리했다.
        try {
            const float durationToss = 0.135f;
            if (0 < tosses.Count)
                yield return new WaitForSeconds(tosses[0].no*durationToss);
            if (this.IsDestroyed())
                yield break;
            SpriteRenderer sr = null;
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
                        StartCoroutine(this.FadeoutTo(toss.toRow, toss.toCol, durationToss));
                        yield return null;
                    }
                    if (null != toss.from) {
                        from = GetPositionByMatrix(toss.fromRow, toss.fromCol);
                        // sr = null;
                    } else {
                        from = GetPositionByMatrix(toss.toRow - 1, toss.toCol);
                        sr = this.blockObject.GetComponent<SpriteRenderer>();
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
                    Color color = sr.color;
                    color.a = EasingFunction.Linear(0f, 1f, tick/durationToss);
                    sr.color = color;
                }
                tick += Time.deltaTime;
                yield return null;
                if (this.IsDestroyed())
                    yield break;
            }
            // 최종값을 한 번 더 넣어서 스키핑에 의한 부정확을 맞춘다.
            // 최종위치값으로 넣어준다.
            toss = tosses[tosses.Count-1];
            to = GetPositionByMatrix(toss.toRow, toss.toCol);
            this.transform.localPosition = to;
            if (sr) {
                Color color = sr.color;
                color.a = 1f;
                sr.color = color;
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

    // 블럭을 특정 위치로 이동 후 제거
    // 일반매칭으로 제거되는 블럭 효과
    public IEnumerator RemoveTo(int toRow, int toCol, float duration)
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
                yield return null;
                if (this.IsDestroyed())
                    yield break;
            }
            this.transform.localPosition = to;
            yield return null;

        } finally {
            this.block.state = prevState;
            // this.stage.RemoveBlock(me);
            if (!this.IsDestroyed())
                Destroy(this.gameObject);
        }
    }

    // 블럭을 특정 위치로 이동 후 제거
    // 일반매칭으로 제거되는 블럭 효과
    public IEnumerator FadeoutTo(int toRow, int toCol, float duration)
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
                yield return null;
                if (this.IsDestroyed())
                    yield break;
            }
            this.transform.localPosition = to;
            yield return null;

        } finally {
            this.block.state = prevState;
            // this.stage.RemoveBlock(me);
            if (!this.IsDestroyed())
                Destroy(this.gameObject);
        }
    }
    
    public IEnumerator Explode(float duration = 0f)
    {
        BlockState prevState = this.block.state;
        this.block.state = BlockState.Floating;
        try {
            if(this == null) yield break;

            // 이름을 미리 바꿔서 삭제 대기중으로 한다 -> 검색이 안되도록 하는 이유
            this.gameObject.name = "Removing_" + this.gameObject.name;
            switch (this.block.type) {
                case BlockType.Red:
                    Play(IngameFxResource.Instance.GetPrefab(
                        IngameFxResource.PrefabType.Explosion_Noraml_Block_Red),
                        this.transform.localPosition);
                    break;
                case BlockType.Blue:
                    Play(IngameFxResource.Instance.GetPrefab(
                        IngameFxResource.PrefabType.Explosion_Normal_Block_Blue),
                        this.transform.localPosition);
                    break;
                case BlockType.Green:
                    Play(IngameFxResource.Instance.GetPrefab(
                        IngameFxResource.PrefabType.Explosion_Normal_Block_Green),
                        this.transform.localPosition);
                    break;
                case BlockType.Yellow:
                    Play(IngameFxResource.Instance.GetPrefab(
                        IngameFxResource.PrefabType.Explosion_Normal_Block_Yellow),
                        this.transform.localPosition);
                    break;
                case BlockType.Purple:
                    Play(IngameFxResource.Instance.GetPrefab(
                        IngameFxResource.PrefabType.Explosion_Normal_Block_Purple),
                        this.transform.localPosition);
                    break;
                case BlockType.Pink:
                    Play(IngameFxResource.Instance.GetPrefab(
                        IngameFxResource.PrefabType.Explosion_Normal_Block_Pink),
                        this.transform.localPosition);
                    break;
                case BlockType.Brown:
                    Play(IngameFxResource.Instance.GetPrefab(
                        IngameFxResource.PrefabType.Explosion_Normal_Block_Brown),
                        this.transform.localPosition);
                    break;
                case BlockType.Rocket:
                    var crossObj = Play(IngameFxResource.Instance.GetPrefab(
                        IngameFxResource.PrefabType.RocketExplosion),
                        this.transform.localPosition,
                        false);
                    float crossObjClipLength = crossObj.transform.Find("Fx_Holder").GetComponent<Animation>().clip.length;
                    CoroutineTaskManager.AddTask(WaitForSec(crossObjClipLength, () => { Destroy(crossObj); }));
                    break;
                case BlockType.PaperPlane:
                    break;
                case BlockType.Mirrorball:
                    break;
                case BlockType.BombBomb:
                    var bombBombObj = Play(
                        IngameFxResource.Instance.GetPrefab(IngameFxResource.PrefabType.BombLargeExplosion),
                        this.transform.localPosition,
                        false);
                    break;
                case BlockType.Woodbox:
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
                    GameObject explodePrefab =
                        IngameFxResource.Instance.GetPrefab(this.block.type);
                    GameObject explodeObj = Instantiate(explodePrefab);
                    explodeObj.transform.position = transform.position;
                    break;
                case BlockType.Pizza:
                case BlockType.Fridge:
                    break;
            }
            yield return new WaitForSeconds(duration);
        } finally {
            this.block.state = prevState;
            if (!this.IsDestroyed())
                Destroy(this.gameObject);
        }
    }

    public static IEnumerator WaitForSec(float waitTime, Action cb)
    {
        yield return new WaitForSeconds(waitTime);
        cb?.Invoke();
    }

    // 십자폭탄 효과를 넣어주고
    public static void CrossEffect(Cell cell) {
        var crossObj = BlockController.Play(
            IngameFxResource.Instance.GetPrefab(IngameFxResource.PrefabType.RocketExplosion),
            GetPositionByMatrix(cell.row, cell.col),
            false);
        float crossObjClipLength = crossObj.transform.Find("Fx_Holder").GetComponent<Animation>().clip.length;
        CoroutineTaskManager.AddTask(BlockController.WaitForSec(crossObjClipLength, () => { Destroy(crossObj); }));
    }

    public static void BombEffect(Cell cell)
    {
        var bombObj = Play(
            IngameFxResource.Instance.GetPrefab(IngameFxResource.PrefabType.BombSmallExplosion),
            GetPositionByMatrix(cell.row, cell.col),
            false);
        CoroutineTaskManager.AddTask(BlockController.WaitForSec(1.0f, () => { Destroy(bombObj); }));
    }

    public static GameObject Play(GameObject prefab, Vector3 pos, bool selfDestroy = true)
    {
        GameObject root = GameObject.Find("Stage");
        GameObject prefabObject = Instantiate(prefab, root.transform);
        if (selfDestroy)
            prefabObject.AddComponent<SelfDestroy>();
        prefabObject.transform.localPosition = pos;
        return prefabObject;
    }

    public IEnumerator Touch()
    {
        if (null != BaseController.stageObject.transform.Find("B" + this.block.ToJObject() + "_touch"))
            yield break;
        float tick = 0;
        GameObject prefab = null;
        switch (this.block.type) {
            case BlockType.Red: prefab = IngameFxResource.Instance.GetPrefab(IngameFxResource.PrefabType.TouchRed); break;
            case BlockType.Yellow : prefab = IngameFxResource.Instance.GetPrefab(IngameFxResource.PrefabType.TouchYellow); break;
            case BlockType.Blue : prefab = IngameFxResource.Instance.GetPrefab(IngameFxResource.PrefabType.TouchBlue); break;
            case BlockType.Green : prefab = IngameFxResource.Instance.GetPrefab(IngameFxResource.PrefabType.TouchGreen); break;
            case BlockType.Purple : prefab = IngameFxResource.Instance.GetPrefab(IngameFxResource.PrefabType.TouchPurple); break;
            case BlockType.Pink : prefab = IngameFxResource.Instance.GetPrefab(IngameFxResource.PrefabType.TouchPink); break;
            case BlockType.Brown : prefab = IngameFxResource.Instance.GetPrefab(IngameFxResource.PrefabType.TouchBrown); break;
        }
        if (null != prefab) {
            GameObject root = BaseController.stageObject;
            GameObject prefabObject = Instantiate(prefab, root.transform);
            prefabObject.name = "B" + this.block.ToJObject() + "_touch";
            try {
                while (tick < 2.0f) {
                    Vector3 worldPoint = this.blockObject.transform.TransformPoint(this.blockObject.transform.localPosition);
                    Vector3 localPoint = root.transform.InverseTransformPoint(worldPoint);
                    prefabObject.transform.localPosition = localPoint;
                    tick += Time.deltaTime;
                    yield return null;
                    if (this.IsDestroyed())
                        yield break;
                }
            } finally {
                Destroy(prefabObject);
            }
        }
    }

    public IEnumerator MoveTo(int row, int col, float duration)
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
                yield return null;
                if (this.IsDestroyed())
                    yield break;
            }
            this.transform.localPosition = to;
            yield return null;
            if (this.IsDestroyed())
                yield break;
        } finally {
            this.block.state = BlockState.Fixed;
        }
    }

    public IEnumerator MoveToReturnly(int row, int col, float duration)
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
                yield return null;
                if (this.IsDestroyed())
                    yield break;
            }
            this.transform.localPosition = to;
            yield return null;
            if (this.IsDestroyed())
                yield break;
            tick = 0;
            while (tick <= duration) {
                this.transform.localPosition = new Vector3(
                        EasingFunction.EaseInOutCubic(to.x, from.x, tick/duration),
                        EasingFunction.EaseInOutCubic(to.y, from.y, tick/duration),
                        to.z
                    );
                tick += Time.deltaTime;
                yield return null;
                if (this.IsDestroyed())
                    yield break;
            }
            this.transform.localPosition = from;
            yield return null;
        } finally {
            this.block.state = BlockState.Fixed;
        }
    }

    public IEnumerator ThreatedBySideAttack(int row, int col, float duration)
    {
        float tick = 0;
        Transform t = this.blockObject.transform;

        while (tick <= duration) {
            float scale = EasingFunction.EaseOutQuint(1f, 1.2f, tick/duration);
            t.localScale = new Vector3(scale, scale, 1);
            tick += Time.deltaTime;
            yield return null;
            if (this.IsDestroyed())
                yield break;
        }
        t.localScale = new Vector3(1f, 1.2f, 1f);
        yield return null;
        if (this.IsDestroyed())
            yield break;

        while (tick <= duration) {
            float scale = EasingFunction.EaseInQuint(1.2f, 1f, tick/duration);
            t.localScale = new Vector3(scale, scale, 1);
            tick += Time.deltaTime;
            yield return null;
            if (this.IsDestroyed())
                yield break;
        }
        t.localScale = new Vector3(1f, 1f, 1f);
        // yield return null;
    }

    // 매칭할 블럭이 없어서 블럭이 교체되었을 경우의 애니메이션
    public void SwapNewBlock(Block newBlock)
    {
        // 블록콘트롤러 설정
        this.block = newBlock;
        this.blockObject.GetComponent<SpriteRenderer>().sprite = Resources.Load("Blocks/110/" + (int)block.type, typeof(Sprite)) as Sprite;
        this.name = "B-" + newBlock.ToString();
    }

    public void showGemstoneChangeEffect()
    {
        GameObject prefab = IngameFxResource.Instance.GetPrefab(IngameFxResource.PrefabType.BlockPang_GemStone);
        GameObject obj = Instantiate(prefab, this.transform);
        obj.transform.position = this.transform.position;
    }

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
