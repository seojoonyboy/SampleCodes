STARWAY 3Match Puzzle Game
==========================
> 스노우볼스 3Match 퍼즐 클라이언트 개발   
> 개발 기간 : 2022.05 ~ 2024.01   
> 출시 여부 : Google PlayStore, Apple Store 정식 런칭 [24.05 서비스 종료 상태]   

개발 환경
==========================
엔진 : Unity 3d Engine 2021.3.23f1   
플랫폼 : Android, iOS   
버전 관리 : Git, Github   


프로젝트 소개
==========================
*스타웨이 특징*   
아이돌 포토 카드 수집 요소를 포함한 모바일 전용 3Match Puzzle Game. 카드마다 고유 카드 스킬을 갖고 있어,
3Match 퍼즐 내에서 활용할 수 있는 특징을 갖고 있다.

*이벤트 시스템과 아이돌 포토 카드 컨텐츠*   
일일 출석, 일일 이벤트 미션, 주간 이벤트 미션, 패스 시스템 등을 구축
글로벌 런칭 이후, 시즌마다 새로운 포토 카드와 매거진을 패치 시스템으로 추가

*다양한 플레이 모드*   
그 외에도 단순 순차적 스테이지 진행 방식뿐만 아니라
시간 제한 내에 점수 상위 랭킹에게 보상을 주는 랭킹 시스템과 타임 어택 모드
어려운 스테이지 위주의 스타웨이 모드
무작위 스테이지를 즐기며, 일정 횟수 클리어마다 보상을 받는 무한 모드등이 있다.

총 6개의 STARWAY 시리즈를 런칭. (김호중, 장민호, 강다니엘, 에이티즈, 권은비, 아이콘)


프로젝트 관리
===========================
첫 런칭 게임인 STARWAY-김호중을 Main Branch로 삼음. 기준 브랜치   
STARWAY 신규 앱이 생길 때마다, Main Branch에서 새로운 Branch를 따는 방식   
앱 단위로 feature/develop/release 브랜치로 관리   
> 세부적으로 버전 정보까지 브랜치 이름에 담아 관리
> 예시) SWAT/develop/1.1.201 [STARWAY ATEEZ develop 브랜치 버전 1.1.201   

![git_graph](https://user-images.githubusercontent.com/110382516/182572212-a39c47f8-a690-4514-9c4e-d98dc8c8238c.PNG)   

Github Issue Tracker, Microsoft Planner를 이용하여 Issue를 관리   
![Alt text](/99.images/Branch_종류.PNG)   


***

Sample Code
============================

1. UniTask를 활용한 블록 제거와 채우는 과정에 대한 로직   
-----------------------------
IEnumerator로 작성하는 경우, 블록 제거 처리 과정에서 발생할 수 있는 Exception을 전달받기 어려운 점을   
UniTask를 활용하여 안정성을 향상시켰음.   
또한, Memory 최적화를 위해 UniTask를 활용   

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

***

2. 추가 리소스 다운로드와 네트워크 불안정에 대응한 이어서 다운로드 처리
-----------------------------

다운로드 해야할 파일 목록을 HTTPS 프로토콜 요청으로 가져온다.   

<pre>
  <code>
    yield return WaitRequestResourceDataList();
    yield return WaitResourceDataDownload(titleView);
  </code>
</pre>

<pre>
  <code>
    async void RequestAssetsList(Action<bool> isSuccess)
    {
        //설치되어 있는 에셋 정보를 얻는다.
        int currentVersion = 0;
        if (PlayerPrefs.HasKey("AssetVersion")) {
            currentVersion = PlayerPrefs.GetInt("AssetVersion");
        }

        //test code
        // currentVersion = 2;

        // 업데이트 해야할 에셋이 있는지 확인하고
        string assetServerName = GameScene.Instance.NetworkManager.AssetServerName;
        if (string.IsNullOrEmpty(assetServerName)) assetServerName = "LIVE";

        Debug.Log("Select Asset Server Name : " + assetServerName);

        string appName = CommonProcessController.GetResourceAppName();

        this.assetList = await GetAssetsList.Request(appName, assetServerName, currentVersion);
        if (null == this.assetList) {
            Debug.Log("에셋 다운로드 정보를 얻을 수 없습니다.");
            ViewController.OpenApiErrorPopup((int)ClientErrorType.AssetListNull, (isOK) => {
                StartCoroutine(Initialize(true));

            });
            //isSuccess(false);
        }
        else
        {
            isSuccess(true);
        }
    }

    private IEnumerator WaitRequestResourceDataList()
    {
        bool responseReceived = false;
        RequestAssetsList((isSuccess) =>
        {
            if (isSuccess)
            {
                Debug.Log("Request Asset List Success.");
            }
            else
            {
                Debug.Log("Request Asset List Failed.");
            }

            responseReceived = true;
        });

        yield return new WaitUntil(() => responseReceived);

        async void RequestAssetsList(Action<bool> isSuccess)
        {
            //설치되어 있는 에셋 정보를 얻는다.
            int currentVersion = 0;
            if (PlayerPrefs.HasKey("AssetVersion")) {
                currentVersion = PlayerPrefs.GetInt("AssetVersion");
            }

            // 업데이트 해야할 에셋이 있는지 확인하고
            string assetServerName = GameScene.Instance.NetworkManager.AssetServerName;
            if (string.IsNullOrEmpty(assetServerName)) assetServerName = "LIVE";

            Debug.Log("Select Asset Server Name : " + assetServerName);

            string appName = CommonProcessController.GetResourceAppName();

            this.assetList = await GetAssetsList.Request(appName, assetServerName, currentVersion);
            if (null == this.assetList) {
                Debug.Log("에셋 다운로드 정보를 얻을 수 없습니다.");
                ViewController.OpenApiErrorPopup((int)ClientErrorType.AssetListNull, (isOK) => {
                    StartCoroutine(Initialize(true));

                });
                //isSuccess(false);
            }
            else
            {
                isSuccess(true);
            }
        }
    }
  </code>
</pre>

다운로드 받을 목록에 대해 다운로드를 진행한다.   

<pre>
  <code>
    private IEnumerator WaitResourceDataDownload(TitleView titleView)
    {
        if (this.assetList == null || this.assetList.totalSize == 0)
        {
            yield break;
        }

#if BUILTIN_RESOURCE
        bool jobFinished = true;
        yield break;
#else
        bool jobFinished = false;
#endif

        Popup.Params p = new Popup.Params();

        p.dummyHeaderText = LocaleController.GetBuiltInLocale(1);
        p.dummyYesBtnContext = LocaleController.GetBuiltInLocale(7);
        var popup = Popup.Load("DownloadConfirmPopup", p,  (popup, result) =>
        {
            if (result.isOnOk)
            {
                OnClickDownloadAssets(titleView);
            }
        });

        DownloadConfirmPopup dcp = (DownloadConfirmPopup)popup;
        dcp.SetContext(
            this.assetList.totalSize,
            PlayerPrefs.GetInt("AssetVersion", 0) == 0
        );

        async void OnClickDownloadAssets(TitleView titleView)
        {
            // 있으면 다운로드 한다.
            if (null != this.assetList && 0 < this.assetList.totalSize) {
                titleView.downloadGaugeBar.gameObject.SetActive(true);
                
                titleView.downloadGaugeBar.UpdateSubLoadingLeftText("Loading");
                titleView.downloadGaugeBar.UpdateSubLoadingRightText("0/1");
                titleView.downloadGaugeBar.UpdateSubLoadingCenterText(GaugeBar.PROGRESSBARLEVEL.ONE);
                
                this.position = 0;

                isError = false;
                // 다운 진행바를 위한 값 설정
                try {
        
                    foreach (Artistar.Puzzle.Network.File f in this.assetList.files) {
   
                        HTTPRequest request = new HTTPRequest(new Uri(f.url));
                        request.ConnectTimeout = new TimeSpan(0, 0, 15);
                        
                        request.OnStreamingData += OnData;
                        string zipFile = AssetPathController.PATH_FOLDER_TMP + f.name;

                        // 1.저장할 파일 핸들을 만들고
                        var fs = new System.IO.FileStream(zipFile, System.IO.FileMode.Create);
                        
                        CancellationTokenSource tokenSource = new CancellationTokenSource();
                        CancellationTokenSource tokenSource2 = new CancellationTokenSource();
                        try {
                            request.Tag = fs;
                            // 2.다운 요청하고

                            coroutine = CheckPosition(fs, request, tokenSource, tokenSource2,() =>
                            {
                                StopAllCoroutines();
                                PlayerPrefs.DeleteKey("AssetVersion");
                                GameScene.Instance.OnRestart();
                            });

                            StartCoroutine(coroutine);

                            await request.GetAsStringAsync(tokenSource2.Token);
                        }

                        catch(Exception e)
                        {
                            isError = true;
                            //SBDebug.Log("SDJ ZZ : " + e.Message);

                            ViewController.OpenApiErrorPopup((int)ClientErrorType.ResourceDataDownloadException, (isOK) =>
                            {
                                if (coroutine != null)
                                {
                                    StopCoroutine(coroutine);
                                }
                                OnClickDownloadAssets(titleView);
                                return;
                            }); 
                        }
                        finally {
                            // 3.파일 핸들을 닫는다.
                            //SBDebug.Log("SDJ 00");
                            fs.Dispose();

                            // 4.HTTP 요청 닫기, delegate 해제
                           // request.OnStreamingData -= OnData;
                            request.Dispose();
                        }
                        //SBDebug.Log("SDJ AA");
                        if (!isError)
                        {
                            //SBDebug.Log("SDJ BB");
                            // 비동기로 압축을 푼다.

                            titleView.downloadGaugeBar.UpdateSubLoadingRightText("1/1");

                            await Task.Run(() => UnZipFiles(zipFile, AssetPathController.PATH_FOLDER_ASSETS.ToString(), this.zipPassword, true), tokenSource.Token);
                            //SBDebug.Log("SDJ CC");
                            // config.json 파일을 읽어서 삭제해야할 파일의 목록을 얻어 삭제해준다.
                            string configFile = AssetPathController.PATH_FOLDER_ASSETS.ToString() + "config.json";
                            FileInfo info = new FileInfo(configFile);
                            if (info.Exists)
                            {
                                StreamReader reader = new StreamReader(configFile);
                                string json = reader.ReadToEnd();
                                reader.Close();
                                // Debug.Log(json);
                                JObject obj = JObject.Parse(json);
                                var config = new Artistar.Puzzle.Core.AssetConfig();
                                config.FromJObject(obj);
                                // 파일을 삭제한다.
                                foreach (string file in config.deleteFiles)
                                {
                                    System.IO.File.Delete(AssetPathController.PATH_FOLDER_ASSETS.ToString() + file);
                                    // Debug.Log(file);
                                }
                            }
                        }
                    }
                    // 모두 다운로드 하였다.


                    //SBDebug.Log("SDJ 11");
                    if (!isError)
                    {
                        //SBDebug.Log("SDJ PPP");
                        PlayerPrefs.SetInt("AssetVersion", this.assetList.version);
                        jobFinished = true;
                    }

                } catch (Exception e)
                {
                    //SBDebug.Log("SDJ XX : " + e.Message);
                    /*isError = true;
                    ViewController.OpenApiErrorPopup((isOK) =>
                    {
                        OnClickDownloadAssets(titleView);
                        return;
                    });*/
                    // Debug.LogException(e);
                } finally {
                    //   if (!isError)
                    //  {
                    
                    //  }
                }

            } else {
                Debug.Log("에셋 파일 목록 요청을 먼저 해주세요.");
            }
        }

        bool OnData(HTTPRequest req, HTTPResponse res, byte[] dataFragment, int dataFragmentLength)
        {
            if(res == null)
            {
                return false;
            }

            if (res.IsSuccess) {
                // 파일에 저장하고
                var fs = req.Tag as System.IO.FileStream;

                //SBDebug.Log("dataFragment : " + dataFragment.Length);
                //SBDebug.Log("dataFragmentLength : " + dataFragmentLength);

                fs.Write(dataFragment, 0, dataFragmentLength);

                // 진행바를 그리고
                this.position += (uint)dataFragmentLength;

               // this.prePosition = this.position;

                float ratio = (float)this.position / (float)this.assetList.totalSize;

                titleView.downloadGaugeBar.UpdateLoadingRatioGauge(ratio);
            }
            else
            {
                SBDebug.Log("OnData Fail");
            }

            return true;
        }
        yield return new WaitUntil(() => jobFinished);
    }
  </code>
</pre>

***

3. 서버 시간과 클라이언트 시간을 동기화한 하트, 클로버 시간에 따른 충전 로직
-----------------------------

<pre>
  <code>
    public void BeginHeartAutoChargeTimer(DateTime serverBeginTime)
    {
        //이미 동작중인 경우 기존꺼를 멈추고 다시 시작한다.
        this.StopTimer();

        CancellableSignal signal = new CancellableSignal(() => { return this == null; });
        if (!gameObject.activeInHierarchy) return;


        this.timerCoroutine1 = StartCoroutine(HeartAutoChargeRemainingTimeTask(signal, serverBeginTime));

        PlayerStorage playerStorage = GameStorage.Instance.GetStorage<PlayerStorage>();

        if (playerStorage.Heart > 99)
        {
            this.heartAmountText.text = "99+";
            this.heartTimerText.text = "MAX";
        }
        else
        {
            this.heartAmountText.text = playerStorage.Heart.ToString();
        }

        this.heartIcon.sprite = this.normalHeartImage;
        Debug.Log("<color=yellow>HEART 자동충전 동작시킵니다.</color>");
    }
  </code>
</pre>

<pre>
  <code>
    public void OnClickReward(AttendanceItem attendanceItem)
    {
        //이미 다른 보상을 요청중이면 차단
        if(this.isRewardTask) return;
        if(!attendanceItem.CanClick()) return;
        
        this.isRewardTask = true;

        this.LockItemView();
        EventStorage eventStorage = GameStorage.Instance.GetStorage<EventStorage>();
        var eventData = eventStorage.GetDailyEventData(this.eventCode);
        
        attendanceItem.OnClick(
            this, 
            dailyDto, 
            dailyCumRewards, 
            eventData, 
            (slotIndex, acquiredItemDto) =>
        {
            eventData.dailyDto.todayReceivedPos = slotIndex;

            var targetReward = this.dailyRewards.Find(x => 
                (x.Bundle == eventData.dailyBonusInfo.DailyReward) && 
                (x.Item == acquiredItemDto.acquiredItems[0].code) && 
                ((x.Value == acquiredItemDto.acquiredItems[0].count)));
            
            if (targetReward != null)
            {
                //this.todayReceivedRewards = eventData.dailyDto.todayReceivedItems = targetReward.Code;
            }

            if (acquiredItemDto != null)
            {
                //Deep Copy to copiedAcquiredItemDto
                VariationItemDto[] copiedAcquiredItems = acquiredItemDto.acquiredItems != null ? 
                    new VariationItemDto[acquiredItemDto.acquiredItems.Length] : new VariationItemDto[0];
                VariationItemDto[] copiedNotReceivedItems = acquiredItemDto.notReceivedItems != null ?
                    new VariationItemDto[acquiredItemDto.notReceivedItems.Length] : new VariationItemDto[0];
                
                for (int i = 0; i < copiedAcquiredItems.Length; i++)
                {
                    copiedAcquiredItems[i] = acquiredItemDto.acquiredItems[i];
                }
                
                for (int i = 0; i < copiedNotReceivedItems.Length; i++)
                {
                    copiedNotReceivedItems[i] = acquiredItemDto.notReceivedItems[i];
                }
                
                AcquiredItemDto copiedAcquiredItemDto = new AcquiredItemDto(
                    copiedAcquiredItems, 
                    copiedNotReceivedItems
                );

                if (acquiredItemDto.cards == null)
                {
                    VariationItemDto[] tmp1 = new VariationItemDto[]{ acquiredItemDto.acquiredItems[0] };
                    copiedAcquiredItemDto.acquiredItems = tmp1;
                }
                //end Deep Copy

                this.rewardPopupDimmedObj.SetActive(true);
                
                CommonProcessController.PlayEffectSound("Common", 2);

                //1단계...보상 획득 팝업 처리
                this.Hide();
                ViewController.OpenRewardPopup(copiedAcquiredItemDto, () =>
                {
                    Int32[] tmp;
                    if (eventData.dailyDto.receivedCumRewards.Length == 0) { tmp = new Int32[0]; }
                    else { tmp = eventData.dailyDto.receivedCumRewards; }
                    var tmp2 = new Int32[tmp.Length + 1];
                    for (int i = 0; i < tmp.Length; i++) { tmp2[i] = tmp[i]; }
                    tmp2[tmp.Length] = copiedAcquiredItemDto.acquiredItems[0].code;

                    eventData.dailyDto.receivedCumRewards = tmp2;
                    eventData.dailyDto.count += 1;
                    
                    BroadcastTunnel<string, int>.Notify("Snowballs.Client.RefreshRedDot", 0);
            
                    //도달된 누적보상이 있다면, 해당 누적보상 index값을 찾는다.
                    if (this.dailyCumRewards != null)
                    {
                        bool isCloseBlack = true;
                        for (int i = 0; i < this.dailyCumRewards.Count; i++)
                        {
                            DailyCumReward dailyCumReward = this.dailyCumRewards[i];
                            bool isBoxItem = SBDataSheet.Instance.ItemProduction[dailyCumReward.Item].ItemType == 3;

                            //박스 보상인 경우
                            if (isBoxItem)
                            {
                                var boxItems = SBDataSheet.Instance.ItemProduction[dailyCumReward.Item].GetBoxBundle();
                                
                                //누적보상에 도달한 경우
                                if (dailyCumReward.DailyCumCount == eventData.dailyDto.count)
                                {
                                    VariationItemDto[] acquiredItems = new VariationItemDto[boxItems.Count];
                                    for (int j = 0; j < boxItems.Count; j++)
                                    {
                                        acquiredItems[j] = new VariationItemDto(boxItems[j].ItemProduction, boxItems[j].ItemQuantity);
                                    }
                                   
                                    VariationItemDto[] notReceivedItems = new VariationItemDto[0];

                                    AcquiredItemDto itemDto = new AcquiredItemDto(acquiredItems, notReceivedItems);
                                    
                                    //2단계...누적포상 팝업
                                    var i1 = i;
                                    
                                    copiedAcquiredItemDto.cards = acquiredItemDto.cards;
                                    itemDto.cards = copiedAcquiredItemDto.cards;

                                    isCloseBlack = false;
                                    ViewController.OpenRewardPopup(itemDto, () =>
                                    {
                                        this.Show();
                                        this.accumulateRewardItemViews[i1].SetStateOpen();
                                        this.rewardPopupDimmedObj.SetActive(false);
                                        
                                        ItemStorage itemStorage = GameStorage.Instance.GetStorage<ItemStorage>();
                                        itemStorage.GetReward(acquiredItemDto);
                                    });
                                }
                                else
                                {
                                    
                                }
                            }
                            //박스 보상이 아닌 경우
                            else
                            {
                                if (dailyCumReward.DailyCumCount == eventData.dailyDto.count)
                                {
                                    VariationItemDto[] acquiredItems = new VariationItemDto[1];
                                    acquiredItems[0] = new VariationItemDto(
                                        this.dailyCumRewards[i].Item, 
                                        this.dailyCumRewards[i].Value
                                    );
                                    VariationItemDto[] notReceivedItems = new VariationItemDto[0];
                                    AcquiredItemDto itemDto = new AcquiredItemDto(acquiredItems, notReceivedItems);
                                
                                    //2단계...누적포상 팝업
                                    var i1 = i;
                                    
                                    var itemProduction = dailyCumReward.GetItemProductionByItem();
                                    bool isCardItem = (ItemDataItemType)itemProduction.ItemType == ItemDataItemType.Card;
                                    if (isCardItem)
                                    {
                                        copiedAcquiredItemDto.cards = acquiredItemDto.cards;
                                        itemDto.cards = copiedAcquiredItemDto.cards;
                                    }

                                    isCloseBlack = false;
                                    ViewController.OpenRewardPopup(itemDto, () =>
                                    {
                                        this.Show();
                                        this.accumulateRewardItemViews[i1].SetStateOpen();
                                        this.rewardPopupDimmedObj.SetActive(false);
                                        
                                        ItemStorage itemStorage = GameStorage.Instance.GetStorage<ItemStorage>();
                                        itemStorage.GetReward(acquiredItemDto);
                                    });
                                }
                            }
                        }

                        if(isCloseBlack)
                        {
                            this.Show();
                            this.rewardPopupDimmedObj.SetActive(false);

                            ItemStorage itemStorage = GameStorage.Instance.GetStorage<ItemStorage>();
                            itemStorage.GetReward(acquiredItemDto);
                        }
                    }
                    else
                    {
                        this.Show();
                        this.rewardPopupDimmedObj.SetActive(false);
                        
                        ItemStorage itemStorage = GameStorage.Instance.GetStorage<ItemStorage>();
                        itemStorage.GetReward(acquiredItemDto);
                    }
                    
                    this.SetLocales();
                
                    this.isRewardTask = false;
                });
            }
        });
    }
  </code>
</pre>

<pre>
  <code>
    public void OnClick(
        AttendanceCheckPopup popup, 
        DailyDto dailyDto, 
        List<DailyCumReward> dailyCumRewards, 
        EventStorage.DailyEventData eventData,
        Action<int, AcquiredItemDto> cb)
    {
        this.isOpened = true;
        this.canOpen = false;
        
        popup.BlockCloseButton();
        
        CoroutineTaskManager.AddTask(Tasks(cb));

        IEnumerator Tasks(Action<int, AcquiredItemDto> cb)
        {
            bool protocolFinished = false;

            AcquiredItemDto acquiredItemDto = null;
            AcquiredItemDto responseDtoData = null;
            popup.RequestReward(this.itemIndex, this, responseDto =>
            {
                protocolFinished = true;
                acquiredItemDto = responseDto.data;
                responseDtoData = responseDto.data;
            });

            yield return new WaitUntil(() => protocolFinished && acquiredItemDto != null);

            LoadingIndicator.Hide();

            this.openEffect.SetActive(true);
      
            yield return new WaitForSeconds(1.0f);
            
            mainImage.sprite = Sprite.Create(
                this.frontCardImageTexture, 
                new Rect(0.0f, 0.0f, this.frontCardImageTexture.width, this.frontCardImageTexture.height), 
                new Vector2(0.5f, 0.5f), 100f
            );
            
            yield return new WaitForSeconds(0.8f);

            var targetDailyCumReward = dailyCumRewards.Find(x => x.DailyCumCount == (dailyDto.count + 1));

            AcquiredItemDto copiedAcquiredItemDto = 
                new AcquiredItemDto(acquiredItemDto.acquiredItems, null);
            List<VariationItemDto> copiedAcquiredItems = new List<VariationItemDto>();
            copiedAcquiredItems.AddRange(copiedAcquiredItemDto.acquiredItems.ToList());

            if (targetDailyCumReward != null)
            {
                var targetItem = copiedAcquiredItems.ToList()
                    .Find(x => x.code == targetDailyCumReward.Item);

                if (targetItem != null)
                {
                    var targetAccumulateItem =
                        copiedAcquiredItems.Find(x => x.code == targetItem.code);

                    if (targetAccumulateItem != null)
                    {
                        copiedAcquiredItems.Remove(targetItem);
                        copiedAcquiredItems.RemoveAll(
                            x => (x.parent != 0) && (x.parent == targetItem.code)
                        );
                        
                        copiedAcquiredItemDto.acquiredItems = copiedAcquiredItems.ToArray();
                    }
                }
            }
            
            if (responseDtoData != null)
            {
                this.UpdateRewardedIconWithAcquiredItems(copiedAcquiredItemDto);
            }

            Color tempColor = mainImage.color;
           
            popup.UnBlockCloseButton();           
            cb?.Invoke(this.itemIndex, acquiredItemDto);
            yield return new WaitForSeconds(0.8f);

            while (mainImage.color.a > 0)
            {
                tempColor.a -= Time.deltaTime / 0.5f;
                mainImage.color = tempColor;

                if (tempColor.a <= 0f) tempColor.a = 0f;
                yield return null;
            }
        }
    }

    public void RequestReward(int slotIndex, AttendanceItem attendanceItem, Action<ResponseDto<AcquiredItemDto>> cb)
    {
        UInt16 requestCount = 0;
        //code값은 DailyInfo의 Code값을 전달한다!
        DailyReceiveDto receiveDto = new DailyReceiveDto(this.eventCode, slotIndex);

        RequestDto<DailyReceiveDto> requestDto = new RequestDto<DailyReceiveDto>(
            requestCount,
            receiveDto,
            Guid.NewGuid().ToString(),
            SBTime.Instance.ISOServerTime
        );

        GameDaily.Receive(requestDto, (responseDto) =>
        {
            if (responseDto.code == (int)ResponseCode.OK)
            {
                HistoryDto dto = new HistoryDto("DailyBonusInfo", this.eventCode);
                RequestDto<HistoryDto> historyRequestDto = new RequestDto<HistoryDto>(0, dto,
                    Guid.NewGuid().ToString(), SBTime.Instance.ISOServerTime);

                var networkManager = GameScene.Instance.NetworkManager;
                networkManager.Ack(historyRequestDto, (ackResponse) =>
                {
                    cb?.Invoke(responseDto);
                });

                GameStorage.DailyDtoUpdatedTime = SBTime.Instance.ServerTime;
            }
            else
            {
                SBDebug.Log("Reward Receive Request Error In AttendanceCheckPopup");
                cb?.Invoke(responseDto);
            }
        });
    }

    public static void Receive(RequestDto<DailyReceiveDto> data, Action<ResponseDto<AcquiredItemDto>> cb)
		{
#if UNITY_EDITOR
			Debug.Log("<color=green>[SBHttp.RequestAPI(Post):/api/daily/receive]</color> " + JsonUtility.ToJson(data));
#endif
			SBHttp.RequestAPI<DailyReceiveDto, AcquiredItemDto>(BestHTTP.HTTPMethods.Post, "/api/daily/receive", data, (response) =>
			{
				cb(response);
			});
		}
  </code>
</pre>

4. 일일출석, 패스 등에서 HTTPS 통신과 ACK 를 통한 구매중 통신 안정성을 보장한 IAP 결제 처리
-----------------------------

<pre>
  <code>
    private void OnClickAvailableRewardItem(DailyReward dailyReward)
    {
        var targetItemProduction = SBDataSheet.Instance.ItemProduction[dailyReward.Item];
        bool isBoxItem = targetItemProduction.ItemType == 3 || targetItemProduction.ItemType == 4;
        if(!isBoxItem) return;
     
        AttendanceRewardDescriptionModal.Params modalParam = new AttendanceRewardDescriptionModal.Params();
        modalParam.rewardItems = new List<AttendanceRewardDescriptionModal.Item>();
        
        CoroutineTaskManager.AddTask(_DownloadTextures(() =>
        {
            this.descriptionModal.OnOpen(modalParam);
        }, targetItemProduction.ItemType));

        IEnumerator _DownloadTextures(Action cb, int itemType)
        {
            //랜덤 박스
            if (itemType == 4)
            {
                List<ItemRandomBox> randomBoxItems = targetItemProduction.GetRandomBoxBundle();
                
                foreach (ItemRandomBox randomBox in randomBoxItems)
                {
                    bool isFinished = false;
                    
                    var iconImage = randomBox.GetItemProductionByItemProduction().GetItemResourceByIconImage();
                    var downloadPath = TextureController.GetTexturePath(iconImage);
                    TextureController.GetTexture(downloadPath, false, (isSuccess, texture) =>
                    {
                        modalParam.rewardItems.Add(
                            new AttendanceRewardDescriptionModal.Item(texture, dailyReward.Value)
                        );
                        
                        isFinished = true;
                    });
                    
                    yield return new WaitUntil(() => isFinished);

                    modalParam.isRandomBox = true;
                }

                cb?.Invoke();
            }
            //일반 박스
            else
            {
                var boxItems = SBDataSheet.Instance.ItemProduction[dailyReward.Item]
                    .GetBoxBundle();
                foreach (ItemBox boxItem in boxItems)
                {
                    bool isFinished = false;
                
                    ItemResource iconImage = SBDataSheet.Instance.ItemProduction[boxItem.ItemProduction].GetItemResourceByIconImage();
                    var downloadPath = TextureController.GetTexturePath(iconImage);
                    TextureController.GetTexture(downloadPath, false, (isSuccess, texture) =>
                    {
                        modalParam.rewardItems.Add(
                            new AttendanceRewardDescriptionModal.Item(texture, boxItem.ItemQuantity)
                        );

                        isFinished = true;
                    });
                
                    yield return new WaitUntil(() => isFinished);
                }
            
                cb?.Invoke();
            }
        }
    }

    public static void RequestAPI<T1, T2>(HTTPMethods method, string path, RequestDto<T1> sendData, Action<ResponseDto<T2>> cb, bool isRetry = true)
		{
			if (path.StartsWith("/api/auth/token"))
			{
				isSetUpdateTime = true;
			}

			Uri url = new Uri(configs.GetAPIServerAddress() + path);
			String data = "";
			if (sendData != null)
			{
				data = SBCrypto.Encrypt(JsonUtility.ToJson(sendData));
			}

			Request(method, url, data, (code, text, req) =>
			{
				ResponseDto<T2> response = new ResponseDto<T2>((UInt16)code, SBTime.Instance.ISOServerTime, new InvokeDto[0]);
				response.error = new ErrorDto(true, false, true);
				if (code != ResponseCode.OK)
				{
					ApiExceptionController.Except(req, sendData, response, isRetry);
					cb(response);
					return;
				}
				response = JsonUtility.FromJson<ResponseDto<T2>>(text);
				if (response.invokes != null)
				{
					OnInvokeEvent(response.invokes);
				}
				else
				{
					CommonProcessController.DeleteMailBoxCount();
				}

				if (response.code != 200)
				{
					ApiExceptionController.Except(req, sendData, response, isRetry);
				}

				cb(response);
			});
		}

    private static void _Request(HTTPMethods method, Uri url, Dictionary<string, string> headers, String sendData, bool isJson, Action<ResponseCode, String, HTTPRequest> cb)
		{
			HTTPRequest request = new HTTPRequest(url, method, (req, resp) => {

				if (resp != null)
					SBDebug.Log("<color=yellow>NET RESPNOSE> [" + method.ToString() + "]" + url + "(" + resp.StatusCode + ")</color>");
				else
					SBDebug.Log("<color=yellow>NET RESPNOSE> [" + method.ToString() + "]" + url + " CONNECTION_REFUSED</color>");

				if (resp == null)
				{
					cb(ResponseCode.ConnectionRefused, "", req);
					return;
				}

				ResponseCode resCode = ResponseCode.OK;
				int statusCode = resp.StatusCode;
				string text = "{}";
				switch (req.State)
				{
					case HTTPRequestStates.Finished:
						if (resp.IsSuccess)
						{
							if (resp.DataAsText[0] != '{' && resp.DataAsText[0] != '[')
							{
								text = SBCrypto.Decrypt(resp.DataAsText);
							}
							else
							{
								text = resp.DataAsText;
							}

							SBDebug.Log("<color=green>SUCCESS> " + text + "</color>");

						}
						else
						{

							SBDebug.Log("<color=red>FAILED> StatusCode: " + statusCode + "</color>");

							resCode = (ResponseCode)statusCode;
							if (!Enum.IsDefined(typeof(ResponseCode), resCode))
							{
								resCode = ResponseCode.StatusError;
							}
						}
						break;
					case HTTPRequestStates.Error:

						SBDebug.Log("<color=red>ERROR> StatusCode: " + statusCode + "</color>");

						resCode = ResponseCode.StatusError;
						break;
					case HTTPRequestStates.Aborted:

						SBDebug.Log("<color=red>ABORTED> StatusCode: " + statusCode + "</color>");

						resCode = ResponseCode.StatusAbort;
						break;
					case HTTPRequestStates.ConnectionTimedOut:

						SBDebug.Log("<color=red>CONNECTION TIMEOUT> StatusCode: " + statusCode + "</color>");

						resCode = ResponseCode.RequestTimeout;
						break;
					case HTTPRequestStates.TimedOut:

						SBDebug.Log("<color=red>TIMEOUT> StatusCode: " + statusCode + "</color>");

						resCode = ResponseCode.RequestTimeout;
						break;
					default:

						SBDebug.Log("<color=yellow>UNKNOWN> StatusCode: " + statusCode + ", " + req.State.ToString() + "</color>");

						resCode = ResponseCode.StatusUnknown;
						break;
				}

				cb(resCode, text, req);
			});

			if (isJson)
			{
				request.SetHeader("Content-Type", "application/json; charset=UTF-8");
			}
			else
			{
				request.SetHeader("Content-Type", "text/plain; charset=UTF-8");
			}
			request.SetHeader("User-Agent",
							"AppID=" + Application.identifier
							+ "; Version=" + Application.version
							+ "; CP=" + CP
							+ "; Sheet=" + SBConfigs.Instance.ResourceInfo
#if UNITY_EDITOR
							+ "; Device=Unity");
#elif UNITY_ANDROID
							+ "; Device=Android");
#elif UNITY_IOS
							+ "; Device=iOS");
#else
							+ "; Device=UNKNOWN");
#endif
			foreach (string key in headers.Keys)
			{
				request.SetHeader(key, headers[key]);
			}

			if (configs.IsExistToken())
			{
				string accessToken = configs.GetAccessToken();
				request.SetHeader("Authorization", "Bearer " + accessToken);
			}

			if (sendData != null && sendData != "")
			{
				request.RawData = System.Text.Encoding.UTF8.GetBytes(sendData);
			}

			request.Timeout = new TimeSpan(0, 0, 30);
			request.Send();
		}
  </code>
</pre>
