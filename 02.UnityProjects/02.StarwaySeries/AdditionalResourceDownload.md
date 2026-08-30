# 추가 리소스 다운로드 (Additional Resource Download)

모바일 환경에서는 와이파이에서 셀룰러로 전환되는 순간, 지하철 구간 진입, 일시적인 기지국 혼잡 등으로
다운로드가 언제든 끊길 수 있습니다. STARWAY는 게임 실행 시점(Title 화면)에 서버로부터 테이블 데이터와
UI/스테이지 리소스를 내려받아야 진행이 가능한 구조이기 때문에, 이 콜드 스타트 구간에서 네트워크가
불안정하면 유저는 게임을 아예 시작하지 못합니다. 이 문서는 `TitleScene.cs`와 `NetworkManager.cs`에서
그 다운로드 파이프라인이 실패를 어떻게 감지하고, 어디까지 되돌아가서 재시도하는지를 코드 레벨로
정리합니다.

AssetData의 경우 기획자가 작성한 Game에 필요한 테이블 정보 Binary 파일을 말한다.   
![image](https://github.com/user-attachments/assets/bf37b3eb-dd01-43ef-a17e-957c515b2e7e)   

여기서 ResourceData은 UI에 필요한 Image 리소스, 퍼즐 스테이지 정보 등을 말한다.   
![image](https://github.com/user-attachments/assets/bf2ca62a-9ae4-4b87-bdb2-f8b82cc13c79)   

**설계 포인트 — 두 리소스를 왜 다르게 다루는가**

같은 "다운로드"처럼 보이지만 두 데이터는 갱신 단위와 실패 시 감수해야 할 비용이 다릅니다. AssetData는
파일 단위로 잘게 쪼개져 있어 필요한 파일만 골라 받을 수 있는 반면(3단계), ResourceData는 이미지/스테이지
번들을 zip 하나로 묶어 받는 대신 압축 해제 이전까지는 "부분적으로 받은 상태"가 의미를 가지지 못합니다.
그래서 두 단계는 아래에서 보듯 서로 다른 재시도 전략(파일 단위 재개 vs zip 단위 재다운로드)을 갖습니다.

State 로 다운로드 단계를 관리하고 절차적으로 진행되도록 한다.   
> AssetDataDownload, AssetDataDownloadFinished, ResourceDataDownload, ResourceDataDownloadFinished

```csharp
public enum STATE
{
    None,
    ResizingCanvas,
    Logo,                               //CI 등장
    NetworkCheck,                       //네트워크 체크
    ServerCheck,                        //서버 점검 체크
    BuildVersionCheck,                  //빌드 버전 체크
    AssetDataDownload,                  //애셋 테이블 데이터 다운르도
    AssetDataDownloadFinished,          //애셋 테이블 데이터 다운로드 완료
    ResourceDataDownload,               //리소스 데이터 다운로드
    ResourceDataDownloadFinished        //리소스 데이터 다운로드 완료
}
```

`TitleScene`은 이 enum 값을 하나씩 순서대로만 세팅합니다(역행하지 않음). 각 상태 전환 시점마다
`titleView.UpdateLoadingText(msg)`로 로딩 문구를 갈아끼우기 때문에, 다운로드가 어느 단계에서 멈췄는지
로그 없이도 화면 문구만으로 파악할 수 있습니다. 별도의 부팅 게이트 체인(`NetworkManager.Open()` 쪽
버전 체크·강제 업데이트·공지 팝업 흐름)은 [TitleSequence 문서](05.Network/02.%20TitleSequence/readme.md)에서
다루며, 이 문서는 그 체인을 통과한 뒤에 이어지는 "리소스 실체 다운로드" 구간에 집중합니다.

## 1단계 — 받아야 할 목록 확인

다운로드 받아야 할 ResourceData 파일 목록을 Server로 부터 확인한다.

```csharp
this.SetState(STATE.ResourceDataDownload);
msg = "Checking Resource Data";
titleView.UpdateLoadingText(msg);
yield return WaitRequestResourceDataList();
```

```csharp
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
}
```

**설계 포인트**

- 로컬에 저장된 `AssetVersion`(PlayerPrefs)을 서버에 함께 보내 "지금 버전 대비 갱신분"만 요청합니다.
  매번 전체 목록을 받는 대신 diff 개념으로 접근한 것으로, 이후 3단계에서 파일 단위 diff와 같은 맥락으로
  이어집니다.
- 목록 조회 자체가 실패하면(`assetList == null`) 팝업 확인 콜백에서 `StartCoroutine(Initialize(true))`로
  **타이틀 초기화 코루틴 전체를 처음부터 다시 태웁니다**(`isSkipLogo = true`라 로고 연출만 건너뜁니다).
  개별 단계를 재시도하는 대신 상위 루틴으로 되돌리는 단순하고 보수적인 복구 전략입니다.

## 2단계 — ResourceData 다운로드

ResourceData 파일들을 다운로드 받는다. zip 형태로 다운로드 처리를 하고 이후에 압축을 푸는 형태

> 중간에 네트워크 연결이 끊어지는 경우 zip 파일을 다시 받는다.

```csharp
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
                        SBDebug.Log("SDJ ZZ : " + e.Message);

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
                        SBDebug.Log("SDJ 00");
                        fs.Dispose();

                        // 4.HTTP 요청 닫기, delegate 해제
                       // request.OnStreamingData -= OnData;
                        request.Dispose();
                    }
                    SBDebug.Log("SDJ AA");
                    if (!isError)
                    {
                        SBDebug.Log("SDJ BB");
                        // 비동기로 압축을 푼다.
                        await Task.Run(() => UnZipFiles(zipFile, AssetPathController.PATH_FOLDER_ASSETS.ToString(), this.zipPassword, true), tokenSource.Token);
                        SBDebug.Log("SDJ CC");
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

                SBDebug.Log("SDJ 11");
                if (!isError)
                {
                    SBDebug.Log("SDJ PPP");
                    PlayerPrefs.SetInt("AssetVersion", this.assetList.version);
                    jobFinished = true;
                }

            } catch (Exception e)
            {
                SBDebug.Log("SDJ XX : " + e.Message);
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

            SBDebug.Log("dataFragment : " + dataFragment.Length);
            SBDebug.Log("dataFragmentLength : " + dataFragmentLength);

            fs.Write(dataFragment, 0, dataFragmentLength);

            // 진행바를 그리고
            this.position += (uint)dataFragmentLength;

           // this.prePosition = this.position;

            float ratio = (float)this.position / (float)this.assetList.totalSize;
            titleView.downloadGaugeBar.UpdateLoadingRatioGauge(ratio);
        }
        else
        {
            Debug.Log("OnData Fail");
        }

        return true;
    }
    yield return new WaitUntil(() => jobFinished);
}
```

같은 파일 안에 있는 `CheckPosition` 코루틴이 위 다운로드 루프를 감시하는 워치독입니다.

```csharp
private IEnumerator CheckPosition(FileStream fs, HTTPRequest request, CancellationTokenSource tokenSource, CancellationTokenSource tokenSource2, Action callback)
{
    int waitCount = 0;
    uint checkPosision = 0;
    while (this != null && position < this.assetList.totalSize)
    {
        if (waitCount > 500)
        {
            if (checkPosision == position)
            {
                // 500초 가까이 진행량(position)이 그대로다 = 다운로드가 멈춘 것으로 간주
                if (!tokenSource2.IsCancellationRequested) tokenSource2.Cancel();
                tokenSource.Cancel();
                isError = true;
                fs.Dispose();
                request.OnStreamingData = null;
                request.Dispose();
                fs.Close();
                ViewController.OpenApiErrorPopup((int)ClientErrorType.CheckPositionError, (isOk) =>
                {
                    callback();
                });
                yield break;
            }
            checkPosision = position;
            waitCount = 0;
        }
        yield return new WaitForSeconds(1);
        waitCount++;
    }
}
```

**설계 포인트**

- `HTTPRequest.ConnectTimeout`을 `TimeSpan(0, 0, 15)`로 명시적으로 지정합니다. BestHTTP의 기본 타임아웃에
  기대지 않고 이 프로젝트가 겪는 실제 회선 환경(3G/LTE 전환 구간 포함)에 맞춰 숫자를 코드에 못박아 둔
  값입니다.
- `HTTPRequest.OnStreamingData`로 데이터가 스트리밍되는 족족 `fs.Write`로 파일에 흘려 쓰면서 동시에
  `position`을 누적합니다. `ConnectTimeout`은 "연결 자체가 안 되는" 상황만 잡아내므로, "연결은 됐지만
  응답이 뚝뚝 끊겨 진행이 멈춘" 상황을 잡기 위해 별도로 `CheckPosition` 워치독을 코루틴으로 병행 실행합니다
  — 1초마다 `position`을 스냅샷 떠서, 500번(약 500초) 동안 값이 그대로면 멈춘 것으로 간주하고 강제로
  취소·재시작시킵니다. 타임아웃 하나만으로는 잡을 수 없는 "느리게 죽어가는 연결"까지 감시 범위에 넣은
  것입니다.
- 다운로드는 파일 단위가 아니라 **zip 전체 단위**로 실패를 처리합니다. `catch` 블록에서 예외가 나면
  같은 `OnClickDownloadAssets(titleView)`를 그대로 다시 호출해 해당 zip을 처음부터 다시 받습니다 —
  압축 해제 전까지는 파일이 "반쯤 유효한" 상태를 가질 수 없기 때문에, 이어받기 대신 통짜 재다운로드를
  선택한 것입니다(3단계의 파일 단위 이어받기와 의도적으로 다른 전략).
- 압축 해제 후 `config.json`을 읽어 `deleteFiles` 목록에 있는 파일을 로컬에서 지워줍니다. 리소스
  갱신이 "추가"뿐 아니라 "삭제"까지 표현할 수 있어야 구버전 리소스가 기기에 계속 쌓이는 문제를 막을 수
  있습니다.

**실제 라이브 대응 과정에서**

이 구간의 재시도·타임아웃 값들은 처음부터 이 숫자로 정해진 것이 아니라, 라이브 운영 중 관측된 실패
패턴에 맞춰 반복적으로 튜닝된 흔적입니다. 리소스 다운로드 중 네트워크가 불안정해지는 케이스에 대응하는
재시도 로직이 `NetworkManager.cs`에 먼저 들어갔고, 실제 QA/라이브 테스트를 거친 뒤 얼마 지나지 않아
재시도 임계값이 한 차례 더 조정되었습니다. 같은 날 저녁과 다음 날에 걸쳐 HTTP 타임아웃 값도 추가로
관측 기반으로 두 차례 손질되었는데, 위 `ConnectTimeout = 15초`와 `CheckPosition`의 "500회 대기" 임계값이
바로 그 튜닝의 결과물입니다 — 도입 → 실측 → 재조정을 두 차례 거친, "감으로 정한 숫자가 아니라 실패
로그를 보고 좁혀 들어간 숫자"라는 점이 이 코드 구간의 특징입니다.

## 3단계 — AssetData 다운로드

> 네트워크 불가시 재연결 시도를 하고, 재연결시 다음 파일부터 이어서 다운로드 받는다.

호출부는 `TitleScene.WaitAssetDataCheck()`이며, 실제 다운로드 로직은 `NetworkManager.TestAssetLoader()`에
있습니다.

```csharp
networkManager.TestAssetLoader((isDownloadExist) =>
{
    // 리소스 다운로드가 발생한 경우 ScoreMode 썸네일을 초기화한다.
    if(isDownloadExist) TextureController.InitThumbNailImages();

    isFinished = true;
},
(currentIndex, totalFileCount) =>
{
    titleView.downloadGaugeBar.gameObject.SetActive(true);
    titleView.downloadGaugeBar.UpdateSubLoadingLeftText("Loading");

    float ratio = ((float)currentIndex / totalFileCount);
    titleView.downloadGaugeBar.UpdateSubLoadingRightText(currentIndex + "/" + totalFileCount);
    titleView.downloadGaugeBar.UpdateLoadingRatioGauge(ratio);
},
dto => { });
```

```csharp
public async void TestAssetLoader(Action<bool> callback, Action<int, int> progress, Action<FileDto> targetToDownload = null)
{
    bool isDownloadOccured = false;

    PlayerSheetStorage playerSheetStorage =
        GameStorage.Instance.GetStorage<PlayerSheetStorage>();

    string resourcePath = configs.ResourcePath;
    string resourceInfo = configs.ResourceInfo;
    if (resourcePath != null && resourceInfo != null)
    {
        Uri url = new Uri(resourcePath + resourceInfo);
        var listSrc = new TaskCompletionSource<FileDto[]>();

        SBHttp.RequestAssetDataInfo((files) =>
        {
            listSrc.TrySetResult(files);
        });
        FileDto[] files = await listSrc.Task;
        int fileCount = 1;
        foreach (FileDto file in files)
        {
            //내가 갖고있지 않은 파일인 경우
            if (!playerSheetStorage.IsFileExist(file.filename, file.createdAt))
            {
                targetToDownload?.Invoke(file);
                var reqSrc = new TaskCompletionSource<byte[]>();
                SBHttp.RequestFile(
                    new Uri(resourcePath + file.filename),
                    (code, data) => {
                        if (data == null)
                        {
                            if (this.assetLoadRetryCount >= 5)
                            {
                                ViewController.OpenRestartGamePopup((int)ClientErrorType.AssetLoaderErrorCountOver, (isOk) =>
                                {
                                    GameScene.Instance.OnRestart();
                                });
                            }
                            else
                            {
                                ViewController.OpenApiErrorPopup2((int)ClientErrorType.AssetLoaderError, (isOk) =>
                                {
                                    //파일 url 다운로드 실패시 재귀호출
                                    this.TestAssetLoader(callback, progress, targetToDownload);
                                    this.assetLoadRetryCount++;
                                });
                            }
                            return;
                        }
                        reqSrc.TrySetResult(data);
                        this.assetLoadRetryCount = 0;
                    });
                byte[] data = await reqSrc.Task;
                if (data != null)
                {
                    SBDataSheet.Instance.SetData(file.name, data);
                    playerSheetStorage.WriteFile(
                        file.name,
                        file.filename,
                        file.createdAt,
                        data,
                        file.size
                    );
                }
                else
                {
                    SBDebug.LogWarning(string.Format("{0} file not found!", file.name));
                }

                if (!isDownloadOccured) isDownloadOccured = true;
            }
            //내가 갖고 있는 파일인 경우
            else
            {
                byte[] binData = playerSheetStorage.GetFileData(file.name);

                if (binData != null)
                {
                    //바이너리 파일간의 비교
                    SBDataSheet.Instance.SetData(file.name, binData);
                }
            }
            fileCount++;
            progress?.Invoke(fileCount, files.Length);
        }

        playerSheetStorage.WritePlayerSheetInfo(files);

        callback?.Invoke(isDownloadOccured);
    }
    else
        callback?.Invoke(isDownloadOccured);
}
```

**설계 포인트**

- 2단계와 정반대의 전략입니다. 각 테이블 파일은 `playerSheetStorage.IsFileExist(file.filename,
  file.createdAt)`로 **파일 단위 diff**를 먼저 확인하고, 이미 가진(버전이 같은) 파일은 네트워크 호출
  없이 건너뜁니다. 실패도 파일 단위로 격리되기 때문에, 재시도가 처음부터 다시 시작해도 `IsFileExist`
  체크에 걸려 이미 받은 파일은 재요청하지 않습니다 — 주석의 "재연결시 다음 파일부터 이어서 다운로드"가
  실제로는 "전체를 재호출하되 받은 파일은 자연히 스킵되는" 방식으로 구현되어 있습니다.
- 실패 시 `this.TestAssetLoader(callback, progress, targetToDownload)`를 **같은 함수 안에서 재귀
  호출**합니다. 이 재귀에 브레이크가 없다면 응답이 계속 실패하는 한 무한히 반복될 수 있는데,
  `assetLoadRetryCount`가 5 이상이면 재귀를 멈추고 `OpenRestartGamePopup`으로 유저에게 재시작을
  요구합니다. 실패 원인이 파일 하나의 일시적 오류가 아니라 서버·회선 자체의 문제일 가능성이 높다고
  보고, "조용히 계속 재시도"에서 "유저 개입을 요구"로 전환하는 상한선입니다.
- 재시도 카운트는 파일 하나라도 성공하면(`this.assetLoadRetryCount = 0`) 즉시 초기화됩니다. 즉 이
  카운터는 "누적 실패 횟수"가 아니라 "연속 실패 횟수"에 가깝게 동작해, 간헐적으로만 실패하는 회선에서
  불필요하게 재시작 팝업이 뜨는 것을 막아줍니다.
- `progress` 콜백으로 `(currentIndex, totalFileCount)`를 매 파일마다 넘겨 게이지 바를 갱신합니다. 2단계는
  바이트 단위 진행률(`OnStreamingData`)을, 3단계는 파일 개수 단위 진행률을 쓰는데, 이는 각각 "zip 하나를
  스트리밍으로 받는다"와 "작은 파일 여러 개를 순회한다"는 전송 방식의 차이를 그대로 반영한 결과입니다.

이 통신 계층은 이후 STARWAY 라인업이 6개 아이돌 IP 타이틀로 늘어나면서 Git 서브모듈 구조로 공용화되어,
동일한 다운로드/재시도 로직을 6개 프로젝트가 함께 사용하게 됩니다. 10인 미만 스튜디오가 타이틀마다
네트워크 계층을 새로 짜지 않고 이 코드를 그대로 재사용할 수 있었던 것은, 여기서 보듯 실패 처리가
`ClientErrorType` 이넘과 팝업 콜백으로 일관되게 추상화되어 있었기 때문입니다.

---

**관련 코드**:
- [NetworkManager 부팅 게이트 시퀀스 분석 (TitleSequence)](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/05.Network/02.%20TitleSequence/readme.md) — 이 다운로드 단계 이전에 통과해야 하는 버전 체크/강제 업데이트/공지 팝업 게이트 체인
- [Git 브랜치 전략](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/06.GitBranchStrategy) — 이 통신 계층을 6개 타이틀이 공유하게 되면서 정리된 브랜치/서브모듈 운영 방식
