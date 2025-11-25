using System;
using System.Collections;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using UnityEngine;

using Newtonsoft.Json.Linq;
using ICSharpCode.SharpZipLib.Zip;
using BestHTTP;

using Artistar.Puzzle.Network;
using com.snowballs.SWHJ.client.view;
using com.snowballs.SWHJ.client.model;
using com.snowballs.SWHJ.type;

namespace com.snowballs.SWHJ.presenter
{
    public class TitleScene : Scene
    {
        [SerializeField] private TitleSceneResolutionController resolutionController;
        [SerializeField] private Camera sceneCam;

        private readonly string zipPassword = "tmdkshansk";

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

        private void Awake()
        {
            this.SetState(STATE.None);
            TitleView titleView = (TitleView)this.mainSceneView;
            titleView.SetCopyright(LocaleController.GetBuiltInLocale(5));
            titleView.SetVersion(Application.version);
#if ENABLE_SELECT_SERVER
            titleView.SetServer(GameScene.Instance.NetworkManager.GetAccessInfoURL);
#endif
            this.ResizingCanvas();
        }

        private void ResizingCanvas()
        {
            this.SetState(STATE.ResizingCanvas);

            resolutionController.Open(() =>
            {
                if (GameStorage.UID != 0)
                {
#if APP_GUARD
                     AppGuardUnityManager.Instance.setUserId(GameStorage.UID.ToString());
#endif
                }
                StartCoroutine(this.Initialize());
            });
        }

        private IEnumerator Initialize(bool isSkipLogo = false)
        {
            this.SetState(STATE.Logo);
            TitleView titleView = (TitleView)this.mainSceneView;
            string msg = "";
            titleView.UpdateLoadingText(msg);

            //CI & BI 재생
            if(!isSkipLogo) yield return titleView.ShowLogo();

            GameScene.Instance.ResolutionController.SetBackgroundDark();

            //네트워크 체크...
            this.SetState(STATE.NetworkCheck);
            msg = "Checking Network";
            titleView.UpdateLoadingText(msg);
            yield return WaitNetworkAvailableState();

            sceneCam.clearFlags = CameraClearFlags.Depth;

            // yield return new WaitForSeconds(0.5f);

            //server 체크
            this.SetState(STATE.ServerCheck);
            msg = "Checking Server";
            titleView.UpdateLoadingText(msg);

            //빌드 버전 체크
            this.SetState(STATE.BuildVersionCheck);
            msg = "Checking BuildVersion";
            titleView.UpdateLoadingText(msg);

            //NetworkManager Open
            yield return NetworkManagerOpen();

            //title 배경 설정
            yield return DownloadTitleImagesAndVideos();
            titleView.ActiveBILogo(GameStorage.UserAccountLanguage);

            //Asset으로부터 Lobby, Title BGM 을 세팅한다.
            yield return GameScene.Instance.AudioController.InitTitleBGM();
            yield return this.PlayVoiceAndBgm();
            
            // 문제가 생긴다면 삭제해야함. 일단 빠르게 구현하기 위해... (1)
            PopupRoot.Instance.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            this.SetState(STATE.ResourceDataDownload);
            msg = "Checking Resource Data";
            titleView.UpdateLoadingText(msg);
            yield return WaitRequestResourceDataList();

#if !BUILTIN_RESOURCE
            msg = "";
            titleView.UpdateLoadingText(msg);
#endif
            yield return WaitResourceDataDownload(titleView);

            SBDebug.Log("SDJ 555");

            if (!isError)
            {
                SBDebug.Log("SDJ 666");
                this.SetState(STATE.ResourceDataDownloadFinished);

                this.SetState(STATE.AssetDataDownload);

                //SBSheet 서버 요청 대기
                msg = "Checking AssetData";
                titleView.UpdateLoadingText(msg);
                GameStorage.Instance.OpenSubStorage(new PlayerSheetStorage());
                GameStorage.Instance.OpenSubStorage(new ThumbnailStorage());

                yield return WaitAssetDataCheck();

                GameStorage.Instance.GetStorage<ThumbnailStorage>().UpdateThumbnailDictionary();

                msg = "Opening Storage";
                titleView.UpdateLoadingText(msg);
#if !BUILTIN_RESOURCE
                GameStorage.Instance.OpenSubStorage(new CardStorage());
#endif
                GameStorage.Instance.OpenSubStorage(new StageStorage());
                GameStorage.Instance.OpenSubStorage(new MailBoxStorage());

                GameStorage.Instance.OpenSubStorage(new IngameEventStackStorage());
                // GameStorage.Instance.OpenSubStorage(new ScoreModeStorage());
                GameStorage.Instance.OpenSubStorage(new SeasonBuffStorage());
                GameStorage.Instance.OpenSubStorage(new ArtistStorage());
                GameStorage.Instance.OpenSubStorage(new ThemeStorage());
                //GameStorage.Instance.OpenSubStorage(new MyCardStorage(null));

                StartCoroutine(GameScene.Instance.AudioController.InitLobbyBGM());
                GameStorage.TitleBGMFilePath = CommonProcessController.GetTitleBGMUri;

                //튜토리얼 매니저 설정
                yield return this.TutorialManagerOpen();
                yield return new WaitForSeconds(0.5f);

                // 문제가 생긴다면 삭제<<<<해야함. 일단 빠르게 구현하기 위해... (2)
                PopupRoot.Instance.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceCamera;

                yield return this.LoadLoginScene();
            }
        }

   

        //Voice 이후에 BGM을 재생해야 해서 따로 처리
        IEnumerator PlayVoiceAndBgm()
        {
            bool isFinished = false;
            CommonProcessController.PlayEffectSound(AudioController.SoundEffect.GameTitle, () =>
            {
                GameScene.Instance.RefreshAudio(SceneController.Scene.Title);
                isFinished = true;
            });

            yield return new WaitUntil(() => isFinished);
        }

        private IEnumerator WaitNetworkAvailableState()
        {
            bool isNetworkAvailable = NetworkStatusController.IsNetworkAvailable();
            Popup.Params p = new Popup.Params();

            p.dummyHeaderText = LocaleController.GetBuiltInLocale(6);

            string locale = LocaleController.GetBuiltInLocale(12);
            p.dummyContext = (locale.Contains("{0}")) ? string.Format(locale, (int)ClientErrorType.NotNetworkConnection) : locale;

            p.dummyYesBtnContext = LocaleController.GetBuiltInLocale(7);

            if (!isNetworkAvailable)
                LoadNetworkUnAvailablePopup(p);

            void LoadNetworkUnAvailablePopup(Popup.Params p)
            {
                Popup.Load("Network/NetworkUnAvailablePopup", p, ((popup, result) =>
                {
                    GameScene.Instance.OnRestart();
                    /**SceneController.OpenScene(SceneController.Scene.EntryScene, () =>
                    {
                        Destroy(GameScene.Instance.gameObject);
                    });*/
                }));
            }
            yield return new WaitUntil(() => isNetworkAvailable);
        }

        private IEnumerator NetworkManagerOpen()
        {
            bool isFinished = false;
            var networkManager = GameScene.Instance.NetworkManager;
            networkManager.Open(() =>
            {
                isFinished = true;
            });

            yield return new WaitUntil(() => isFinished);
        }

        private IEnumerator TutorialManagerOpen()
        {
            bool isFinished = false;
            GameScene.Instance.TutorialManager.Open(() =>
            {
                isFinished = true;
            });

            yield return new WaitUntil(() => isFinished);
        }

        private IEnumerator DownloadTitleImagesAndVideos()
        {
            yield return GameScene.Instance.DownloadTitleImage();
        }

        private Coroutine dummyLoading = null;
        private IEnumerator WaitAssetDataCheck()
        {
            bool isFinished = false;

            TitleView titleView = mainSceneView as TitleView;
            var networkManager = GameScene.Instance.NetworkManager;
            
            titleView.downloadGaugeBar.UpdateSubLoadingCenterText(GaugeBar.PROGRESSBARLEVEL.TWO);

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
                titleView.UpdateLoadingText(
                    string.Format("")
                );
                
                titleView.downloadGaugeBar.UpdateSubLoadingRightText(currentIndex + "/" + totalFileCount);
                titleView.downloadGaugeBar.UpdateLoadingRatioGauge(ratio);
            },
            dto => { });

            yield return new WaitUntil(() => isFinished);
            titleView.downloadGaugeBar.gameObject.SetActive(false);
        }

        private AssetsResponse assetList = null;

        /// <summary>
        /// 사용자의 빌드 버전과 서버의 최신 버전 대조하고, 다운받아야 할 AssetList 받는다.
        /// </summary>
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

        private uint position = 0;

        private IEnumerator CheckPosition(FileStream fs, HTTPRequest request, CancellationTokenSource tokenSource, CancellationTokenSource tokenSource2, Action callback)
        {
            int waitCount = 0;

            uint checkPosision = 0;
            while (this != null && position < this.assetList.totalSize)
            {
                SBDebug.Log("CheckPosition");

                if(waitCount > 500)
                {
                    SBDebug.Log("waitCount : " + waitCount);

                    if (checkPosision == position)
                    {
                        if (!tokenSource2.IsCancellationRequested)
                        {
                            tokenSource2.Cancel();
                        }
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
        IEnumerator coroutine;

        /// <summary>
        /// Resource Data 다운로드
        /// </summary>
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

        bool isError = false;


        private IEnumerator DummyWaitNetworkAvailableState()
        {
            bool isNetworkAvailbale = false;
            Popup.Params p = new Popup.Params();
            int cnt = 0;

            LoadNetworkUnAvailablePopup(p);

            void LoadNetworkUnAvailablePopup(Popup.Params p)
            {
                Popup.Load("Network/NetworkUnAvailablePopup", p, ((popup, result) =>
                {
                    isNetworkAvailbale = cnt >= 5;
                    if(!isNetworkAvailbale) LoadNetworkUnAvailablePopup(p);
                    cnt++;
                }));
                Debug.Log("Popup ! cnt (" + cnt + ")");
            }
            yield return new WaitUntil(() => isNetworkAvailbale);
        }

        public bool UnZipFiles(string zipFilePath, string unZipTargetFolderPath, string password, bool isDeleteZipFile)
        {
            // zip 파일을 받는 경우, 썸네일 이미지를 초기화 해준다. [임시 최적화]

            bool retVal = false;
            // ZIP 파일이 있는 경우만 수행.
            if (System.IO.File.Exists(zipFilePath)) {
                // ZIP 스트림 생성.
                ZipInputStream zipInputStream = new ZipInputStream(System.IO.File.OpenRead(zipFilePath));
                // 패스워드가 있는 경우 패스워드 지정.
                if (password != null && password != String.Empty)
                    zipInputStream.Password = password;
                try {
                    ZipEntry theEntry;
                    // 반복하며 파일을 가져옴.
                    while ((theEntry = zipInputStream.GetNextEntry()) != null) {
                        // 폴더
                        string directoryName = System.IO.Path.GetDirectoryName(theEntry.Name);
                        string fileName = System.IO.Path.GetFileName(theEntry.Name); // 파일
                        // 폴더 생성
                        System.IO.Directory.CreateDirectory(unZipTargetFolderPath + directoryName);
                        // 파일 이름이 있는 경우
                        if (fileName != String.Empty) {
                            // 파일 스트림 생성.(파일생성)
                            System.IO.FileStream streamWriter = System.IO.File.Create((unZipTargetFolderPath + theEntry.Name));
                            int size = 2048;
                            byte[] data = new byte[2048];
                            // 파일 복사
                            while (true) {
                                size = zipInputStream.Read(data, 0, data.Length);
                                if (size > 0)
                                    streamWriter.Write(data, 0, size);
                                else
                                   break;
                            }
                            // 파일스트림 종료
                            streamWriter.Close();
                        }
                    }
                    retVal = true;
                } catch {
                    retVal = false;
                } finally {
                    // ZIP 파일 스트림 종료
                    zipInputStream.Close();
                }
                // ZIP파일 삭제를 원할 경우 파일 삭제.
                if (isDeleteZipFile) {
                    try {
                        System.IO.File.Delete(zipFilePath);
                    } catch {}
                }
            }
            return retVal;
        }

        private IEnumerator LoadLoginScene()
        {
            LoadingIndicator.Show();
            yield return new WaitForSeconds(1.0f);
            SceneController.OpenScene(SceneController.Scene.Login, () => { });
        }
    }
}
