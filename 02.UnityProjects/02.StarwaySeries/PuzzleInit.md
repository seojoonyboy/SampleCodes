### 퍼즐 초기화 과정 설명

1. 해당 퍼즐 스테이지 정보를 Json 파일로 부터 가져온다.   

> 스테이지 Json 파일 내용   
![image](https://github.com/user-attachments/assets/1c6580ad-d61b-4f50-a9c2-0fc3a68c15aa)


> 인게임 진입 시, IngameController.cs -> Start 호출
<pre>
  <script>
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
  </script>
</pre>

> StageController.cs 의 LoadStage 함수 호출된다.
> this.InitStage(obj); 에서 스테이지 정보를 실제로 초기화한다.

--- 
2. 블록을 초기화 한다.(Stage 정보에 맞춰 채워준다)   
> this.InitStage(obj);

<pre>
  <script>
    this.Clear();
    this.stage.Clear();
    this.stage.FromJObject(obj);
    this.InitOffset();
    this.InitStage(obj);
  </script>
</pre>

> 2차원 클래스 배열 형태로 관리 -> cells [,]
![image](https://github.com/user-attachments/assets/d2610bda-8fe0-42b7-aed4-0d53123eb7fc)

> 블록을 초기화 하는 코드
> 일반 블록 생성하는 경우 Block.Factory 함수를 호출하여 Block 객체를 생성하고 cells 에 전달한다.

> block = Block.Factory(newBlock);
> cell.block = block;

<pre>
  <script>
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

                  ... 중략 ...
            }
        // 백판 깔아주고
        this.backController.LoadStage();
        // 데쉬보드 업데이트 하고
        this.UpdateDashboard();
  </script>
</pre>

3. 스테이지 배경을 설정해준다.
> this.backController.LoadStage();

<pre>
  <script>
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
  </script>
</pre>
