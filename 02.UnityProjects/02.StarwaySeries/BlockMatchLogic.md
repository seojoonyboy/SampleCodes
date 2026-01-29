다음은 미션 블록 처리에 대한 로직 설명입니다.

* 동일 기능 파생 블록(4자리 타입)을 베이스 타입과 동일한 처리 파이프라인에 편입시켜, 콘텐츠 스킨 추가 시 코어 로직 수정 없이 확장 가능하도록 설계
* 미션 블록 HP(=normalSidePoint) 변화는 diff 기반 감지로 스프라이트/이펙트 갱신을 최소화하여 퍼포먼스와 시각 피드백을 동시에 확보
* 파괴 연출은 Explode 코루틴에서 타입 그룹핑으로 관리해 룰/연출의 일관성과 유지보수성을 강화

> IceCube를 예시로 활용

# IceCube(BlockType.IceCube = 7101) 라이프사이클 요약 (InitStage → AttackNormal → Explode/Destroy)

> 대상 코드
- `Assets/Scripts/Artistar/Puzzle/Core/Type.cs`
- `Assets/Scripts/Controller/StageController.cs`
- `Assets/Scripts/Controller/Blocks/BlockController.cs`

---

## 0) IceCube는 “Woodbox(710)와 동일 기능” 파생 타입

`Type.cs`에서 IceCube(7101)는 **Woodbox(710)와 동일 기능(룰/피격 방식 공유)** 으로 정의되어 있음.

```csharp
// 동일기능, 나무상자타입 추가 = Woodbox(710)
IceCube = 7101,
TopiarySpring = 7102,
TopiaryWinter = 7103,
FloorLamp = 7104,
```

1) Stage 로드 시점: InitStage에서 블록 생성/배치
1-1. StageController.LoadStage → InitStage 호출

```csharp
public void LoadStage(JObject obj) {
  this.stage.FromJObject(obj);
  this.InitOffset();
  this.InitStage(obj);
  StartCoroutine(this.coMatchAndGravity = this.MatchAndGravity());
}
```

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

2) “피격 → HP 감소” 단계: AttackNormal이 호출되는 흐름
2-1. 일반 매칭 발생 시 AttackNormal이 실행됨

```csharp
private Block AttackNormal(NormalMatchResult match) {
  Block specialblock = Block.FactoryBySpecialMatch(match.type);

  // 노말사이드어택(주변 미션블록 등에 영향)
  this.AttackNormalSide(match);

  // 매치된 셀들 블록을 실제로 터뜨림
  foreach (Cell cell in match.cells) {
    if (null != cell.block) {
      Block block = cell.block;
      block.normalPoint = Math.Max(0, block.normalPoint - 1);

      if (block.IsDead) {
        BlockController controller = this.FindBlockController(block);
        if (null != controller) StartCoroutine(controller.Explode());
        this.stage.RemoveBlock(cell);
      }
    }
  }
  ...
}

```

3) IceCube의 HP(=normalSidePoint) 감소가 화면에 반영되는 방식
IceCube/Woodbox 계열은 BlockController.Update()에서 normalSidePoint 변화 감지 → 스프라이트 갱신 + 피격 이펙트를 처리함.

```csharp
case BlockType.Woodbox:
case BlockType.IceCube:
case BlockType.TopiarySpring:
case BlockType.TopiaryWinter:
case BlockType.FloorLamp:
  if (this.prevNormalSidePoint != block.normalSidePoint) {
    SpriteRenderer sr = this.blockObject.GetComponent<SpriteRenderer>();
    sr.sprite = Resources.Load("Blocks/110/" + this.GetSpriteName(), typeof(Sprite)) as Sprite;

    this.prevNormalSidePoint = block.normalSidePoint;
    Play(IngameFxResource.Instance.GetPrefab(IngameFxResource.PrefabType.NormalExplosion),
         this.transform.localPosition);
  }
  break;

```

또한 해당 계열의 스프라이트는 {type}-{normalSidePoint} 형태로 결정됨:
```chsharp
return ((int)block.type).ToString() + "-" + Math.Max(1, block.normalSidePoint).ToString();
```

4) 최종 파괴 단계: Explode → Destroy(gameObject)
IceCube는 Explode에서 Woodbox와 동일한 파괴 FX(BlockPang_WoodBox) 를 사용하고,
마지막에 Destroy(this.gameObject)로 오브젝트가 제거됨.

```csharp
public IEnumerator Explode(float duration = 0f) {
  try {
    switch (this.block.type) {
      case BlockType.Woodbox:
      case BlockType.IceCube:
      case BlockType.TopiarySpring:
      case BlockType.TopiaryWinter:
      case BlockType.FloorLamp:
        GameObject woodPrefab = IngameFxResource.Instance.GetPrefab(
          IngameFxResource.PrefabType.BlockPang_WoodBox);
        GameObject woodObj = Instantiate(woodPrefab);
        woodObj.transform.position = transform.position;
        break;
    }
    yield return new WaitForSeconds(duration);
  }
  finally {
    if (!this.IsDestroyed()) Destroy(this.gameObject);
  }
}

```

5) 전체 라이프사이클(요약 시퀀스)
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
  -->|Yes| [BlockController.Explode: Woodbox]
          [Destroy(gameObject)]
```
