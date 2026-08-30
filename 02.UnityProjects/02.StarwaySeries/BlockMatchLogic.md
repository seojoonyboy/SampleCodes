## 다음은 미션 블록 처리에 대한 로직 설명입니다.

* 동일 기능 파생 블록(4자리 타입)을 베이스 타입과 동일한 처리 파이프라인에 편입시켜, 콘텐츠 스킨 추가 시 코어 로직 수정 없이 확장 가능하도록 설계
* 미션 블록 HP(=normalSidePoint) 변화는 diff 기반 감지로 스프라이트/이펙트 갱신을 최소화하여 퍼포먼스와 시각 피드백을 동시에 확보
* 파괴 연출은 Explode(async UniTask)에서 타입 그룹핑으로 관리해 룰/연출의 일관성과 유지보수성을 강화

> IceCube를 예시로 활용
<img width="110" height="110" alt="7101-3" src="https://github.com/user-attachments/assets/3f360909-dc8d-484c-85ae-81d0692a5d59" />

## IceCube(BlockType.IceCube = 7101) 
* 라이프사이클 요약 (InitStage → AttackNormal → Explode/Pool 반납)

> 대상 코드
- `Assets/Scripts/Artistar/Puzzle/Core/Type.cs`
- `Assets/Scripts/Controller/StageController.cs`
- `Assets/Scripts/Controller/Blocks/BlockController.cs`

## IceCube는 “Woodbox(710)와 동일 기능” 파생 타입

`Type.cs`에서 IceCube(7101)는 **Woodbox(710)와 동일 기능(룰/피격 방식 공유)** 으로 정의되어 있음.

```csharp
// 동일기능, 나무상자타입 추가 = Woodbox(710)
IceCube = 7101,
TopiarySpring = 7102,
TopiaryWinter = 7103,
FloorLamp = 7104,
```

(1) Stage 로드 시점: InitStage에서 블록 생성/배치

1-1. StageController.LoadStage → InitStage 호출

```csharp
public void LoadStage(JObject obj)
{
    this.Clear();
    this.stage.Clear();
    this.stage.FromJObject(obj);
    this.InitOffset();
    this.InitStage(obj);

    // ... 중략(시작 판에 매칭이 없으면 Hint로 재정렬) ...

    // 중력효과 적용
    this.coMatchAndGravity = this.MatchAndGravity();
}
```

> `coMatchAndGravity`는 `UniTask?` 필드이며, `MatchAndGravity()`는 `async UniTask` 메서드다. 즉 코루틴이 아니라 async/await 기반으로 실행된다.

1-2. InitStage에서 cell.block 기반으로 BlockController.Create 수행

```csharp
for (int r = 0; r < this.stage.rowCount; r++){
  for (int c = 0; c < this.stage.colCount; c++) {
    Cell cell = this.stage.cells[r, c];
    Block block = cell.block;
  
    if (null != block) {
      // ... Random 처리 생략 ...
  
      // 일반 케이스는 화면에 생성
      BlockController.Create(block, r, c);
    }
  }
}
```

(2) “피격 → HP 감소” 단계: AttackNormal이 호출되는 흐름
2-1. 일반 매칭 발생 시 AttackNormal이 실행됨

```csharp
private Block AttackNormal(NormalMatchResult match)
{
  Block specialblock = Block.FactoryBySpecialMatch(match.type);

  // 노말사이드어텍
  this.AttackNormalSide(match);

  // 블록을 터트리고
  foreach (Cell cell in match.cells) {
    // ... 중략(topBlock/bottomBlock 처리) ...
    if (null != cell.block) {
      Block block = cell.block;
      block.normalPoint = Math.Max(0, block.normalPoint - 1);
      if (block.IsDead) {
        BlockController controller = this.FindBlockController(block);
        if (null != controller) {
          controller.Explode().Forget();
        }
        this.stage.RemoveBlock(cell);
      }
    }
  }
  // ...
}

```

> `Explode()`는 `async UniTask` 메서드이므로 `StartCoroutine`이 아니라 `.Forget()`으로 fire-and-forget 호출된다.

(3) IceCube의 HP(=normalSidePoint) 감소가 화면에 반영되는 방식
IceCube/Woodbox 계열은 BlockController.Update()에서 normalSidePoint 변화 감지 → 스프라이트 갱신 + 피격 이펙트를 처리함.

```csharp
// ... 중략(다른 케이스: Luckyball/Woodbox/Stand/TeaCup 등) ...
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
```

또한 해당 계열의 스프라이트는 {type}-{normalSidePoint} 형태로 결정됨:
```csharp
return ((int)block.type).ToString() + "-" + Math.Max(1, block.normalSidePoint).ToString();
```

(4) 최종 파괴 단계: Explode → 오브젝트 풀 반납
IceCube는 Woodbox와 같은 그룹(파생 타입)이지만 Explode의 case는 서로 분리되어 있고,
각자 `IngameEffectPrefabLoader.Instance.GetBlockExplosionPrefab(block.type)`로 자기 타입에 맞는 파괴 FX를 받아온다.
마지막 정리도 `Destroy`가 아니라 `IngameBlockPoolController.ReleaseNormalBlock`으로 블록 오브젝트를 풀에 반납하는 방식이다.

```csharp
public async UniTask Explode(float duration = 0f)
{
  BlockState prevState = this.block.state;
  this.block.state = BlockState.Floating;
  try {
    if (this == null) throw new OperationCanceledException();
    this.gameObject.name = "Removing_" + this.gameObject.name;

    switch (this.block.type) {
      // ... 중략(일반 색상 블록/Bomb·Rocket 등 특수 블록 케이스) ...
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
        GameObject explodePrefab = IngameEffectPrefabLoader.Instance
            .GetBlockExplosionPrefab(block.type);
        GameObject explodeObj = Instantiate(explodePrefab);
        explodeObj.transform.position = transform.position;
        break;
    }

    await UniTask.Delay(TimeSpan.FromSeconds(duration));
  } finally {
    this.block.state = prevState;
    IngameBlockPoolController.CheckFirstBlock(this.block);
    if (this.gameObject != null) IngameBlockPoolController.ReleaseNormalBlock(this.gameObject);
  }
}
```

(5) 전체 라이프사이클(요약 시퀀스)
```text
flowchart TD
  1. [LoadStage(JObject)] --> B[InitStage: 셀 순회]
  2. [BlockController.Create(IceCube)]
  3. [게임 진행 중 매치 발생]
  4. [AttackNormal 호출]
  5. [AttackNormalSide로 주변 미션블록 피격]
  6. [IceCube.normalSidePoint 감소]
  7. [BlockController.Update: HP 변화 감지]
  8. [Sprite 갱신: 7101-{HP}, 피격 FX]
  9. {HP <= 0 ?}
  -->|Yes| [BlockController.Explode: IceCube]
          [IngameBlockPoolController.ReleaseNormalBlock로 풀 반납]
```

관련 코드: [PuzzleInit.md](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/PuzzleInit.md) · [07.BlockControl 폴더](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/07.BlockControl)
