### 퍼즐 초기화 과정 설명

3-매치 퍼즐은 스테이지에 입장할 때마다 "판을 새로 깐다"는 요구사항이 반복적으로 발생한다. 최초 입장뿐 아니라 재시작(리트라이), 아이템으로 인한 재배치, 언두/리두 등 여러 진입점에서 동일한 초기화 로직을 안전하게 재사용해야 하고, 그 결과물이 항상 "적어도 한 번은 매칭 가능한 판"이어야 한다는 제약도 있다. 아래는 JSON으로 저장된 스테이지 데이터를 실제 화면의 블록/벽/배경 오브젝트로 전개하는 `StageController`의 초기화 흐름이다.

1. 해당 퍼즐 스테이지 정보를 Json 파일로 부터 가져온다.   

> 스테이지 Json 파일 내용   
![image](https://github.com/user-attachments/assets/1c6580ad-d61b-4f50-a9c2-0fc3a68c15aa)


> 인게임 진입 시, IngameController.cs -> Start 호출
```csharp
string json = String.Empty;
if (File.Exists(stageFilePath))
{
    json = System.IO.File.ReadAllText(stageFilePath);
}
else
{
    json = Resources.Load<TextAsset>(stageFilePath).text;
}

JObject obj = JObject.Parse(json);
BaseController.stageController.LoadStage(obj);
```

> 스테이지 파일이 로컬(다운로드된 파일)에 있으면 그것을 우선 사용하고, 없으면 Resources 번들에 포함된 기본 데이터를 사용한다. 이렇게 얻은 JObject를 `StageController.LoadStage`에 그대로 넘긴다.

---
2. `StageController.LoadStage(obj)` — 스테이지 전체를 초기화하고 첫 매칭 루프를 시작한다.

```csharp
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
```

각 호출이 담당하는 역할은 다음과 같다.

- `this.Clear()` / `this.stage.Clear()` — 이전 씬 오브젝트와 내부 데이터를 완전히 비워, 재시작·재입장 시 이전 판의 잔재가 남지 않게 한다.
- `this.stage.FromJObject(obj)` — JSON을 파싱해 `Stage`의 2차원 셀 배열(`cells[,]`)과 랜덤 블록 컴포넌트 목록을 채운다.
- `this.InitOffset()` — 보드를 화면 중앙에 배치하기 위한 좌표 오프셋을 계산한다(호출부만 이 파일에 있으며 구현은 상위 컨트롤러에 있다).
- `this.InitStage(obj)` — 셀 배열을 순회하며 실제 블록/벽/배경 GameObject를 생성한다(아래 3번 항목).
- Hint 계산 및 리프레시 — 판을 깐 직후 매칭이 0개면 `hint.Refresh()`로 얻은 스왑 목록을 적용해 즉시 재정렬한다. 매 스테이지 수작업 QA가 해야 했을 검증을 코드가 대신 보장한다.
- `this.coMatchAndGravity = this.MatchAndGravity();` — 이후 매칭/중력 처리를 이어서 진행한다. `coMatchAndGravity`는 `UniTask?` 타입 필드로, 코루틴이 아니라 async/await 기반으로 실행된다.

3. 블록을 초기화 한다.(Stage 정보에 맞춰 채워준다) — `InitStage(obj)` 내부

> 2차원 클래스 배열 형태로 관리 -> cells [,]
![image](https://github.com/user-attachments/assets/d2610bda-8fe0-42b7-aed4-0d53123eb7fc)

> 블록을 초기화 하는 코드
> 일반 블록 생성하는 경우 Block.Factory 함수를 호출하여 Block 객체를 생성하고 cells 에 전달한다.

> block = Block.Factory(newBlock);
> cell.block = block;

```csharp
for (int r = 0; r < this.stage.rowCount; r++)
    for (int c = 0; c < this.stage.colCount; c++) {
        Cell cell = this.stage.cells[r, c];
        // 메인블록을 넣어주고
        Block block = cell.block;
        if (null != block) {
            if (BlockType.Random == block.type) {
                if (this.stage.components.ContainsKey(block.componentName)) {
                    List<Block> blocks = this.stage.components[block.componentName];
                    Block newBlock = blocks[random.Next(0, blocks.Count)];
                    if (BlockType.None != newBlock.type && BlockType.Invalid != newBlock.type) {
                        block = Block.Factory(newBlock);
                        cell.block = block;
                        BlockController.Create(block, r, c);
                    } else {
                        block = null;
                        cell.block = null;
                    }
                } else {
                    this.Clear();
                    throw new Exception("랜덤 블록 생성의 컴포넌트가 없습니다(" + block.componentName + ").");
                }
            }

          // ... 중략(고정 랭크 블록/일반 블록 생성, 벽·바닥·상단 오브젝트 배치) ...
    }
// 백판 깔아주고
this.backController.LoadStage();
// 데쉬보드 업데이트 하고
this.UpdateDashboard();
```

> `BlockType.Random`으로 저장된 셀은 실행 시점에 `this.stage.components`의 후보 목록에서 뽑아 `Block.Factory`로 실체화한다. 같은 루프에서 벽·바닥(뽁뽁이·잔디)·상단(감옥창살)·트로피 출구 오브젝트도 함께 배치하고, 끝으로 배경(`backController.LoadStage()`)과 UI 데쉬보드 갱신까지 처리한다.

4. 스테이지 배경을 설정해준다.
> this.backController.LoadStage();

```csharp
public void LoadStage()
{
    for (int r = -1; r < stage.rowCount + 1; r++)
    {
        for (int c = -1; c < stage.colCount + 1; c++) {
            string filename = IsAlivedCell(this.stage, r, c)
                ? "111-111-111"
                : GetBackFilename(this.stage, r, c);
            if (null != filename) {
                GameObject prefabObject = Instantiate(this.backPrefab, this.transform);
                prefabObject.name = "T" + r + "x" + c;
                prefabObject.GetComponent<SpriteRenderer>().sprite = Resources.Load("Tiles/Backgrounds/110/" + filename, typeof(Sprite)) as Sprite;
                prefabObject.transform.localPosition = GetPositionByMatrix(r, c);
            }
        }
    }
}
```

> 실제 보드보다 한 칸씩 넓은 범위(`-1 ~ rowCount/colCount + 1`)를 순회해 테두리까지 배경 타일로 채운다. 살아있는 셀은 공통 타일(`111-111-111`)을, 그 외에는 주변 셀 상태로 계산한 파일명(`GetBackFilename`)의 타일을 붙여 외곽선을 마감한다.

### 설계 포인트

- **데이터와 표현의 분리** — 스테이지 상태는 `Stage`의 2차원 `cells[,]` 배열로만 관리하고, `BlockController` 등 화면 오브젝트는 그 데이터를 읽어 생성되는 산출물일 뿐이다. JSON 포맷이 바뀌어도 초기화 로직 골격은 그대로 재사용된다.
- **런타임 랜덤 치환** — `BlockType.Random` + `components` 딕셔너리 조합으로, 콘텐츠 제작 시점에는 "랜덤"이라고만 적어두고 실제 후보 블록 풀은 별도 관리한다. 밸런스 조정 시 JSON을 다시 만들 필요가 없다.
- **시작 판 품질 보장** — `LoadStage` 안에서 `Hint` 계산을 즉시 수행해, 매칭이 하나도 없는 상태로 게임이 시작되는 경우를 코드 레벨에서 차단한다.
- **코루틴이 아닌 async 기반 진행 루프** — 초기화 직후 매칭/중력 처리를 이어받는 `coMatchAndGravity`는 `UniTask` 타입 필드로, 코루틴이 아닌 async/await로 관리된다.

관련 코드: [BlockMatchLogic.md](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/BlockMatchLogic.md) · [07.BlockControl 폴더](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/07.BlockControl)
