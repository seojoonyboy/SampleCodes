AssetData의 경우 기획자가 작성한 Game에 필요한 테이블 정보 Binary 파일을 말한다.   
![image](https://github.com/user-attachments/assets/bf37b3eb-dd01-43ef-a17e-957c515b2e7e)   

여기서 ResourceData은 UI에 필요한 Image 리소스, 퍼즐 스테이지 정보 등을 말한다.   
![image](https://github.com/user-attachments/assets/bf2ca62a-9ae4-4b87-bdb2-f8b82cc13c79)   

State 로 다운로드 단계를 관리하고 절차적으로 진행되도록 한다.   
> AssetDataDownload, AssetDataDownloadFinished, ResourceDataDownload, ResourceDataDownloadFinished

<pre>
  <code>
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
  </code>
</pre>

1 단계 : 다운로드 받아야 할 ResourceData 파일 목록을 Server로 부터 확인한다.   
<pre>
  <code>
      this.SetState(STATE.ResourceDataDownload);
      msg = "Checking Resource Data";
      titleView.UpdateLoadingText(msg);
      yield return WaitRequestResourceDataList();
  </code>
</pre>

<pre>
  <code>
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
  </code>
</pre>

2단계 : ResourceData 파일들을 다운로드 받는다. zip 형태로 다운로드 처리를 하고 이후에 압축을 푸는 형태   
> 중간에 네트워크 연결이 끊어지는 경우 zip 파일을 다시 받는다.   
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

3단계 : AssetData를 다운로드 받는다.
> 네트워크 불가시 재연결 시도를 하고, 재연결시 다음 파일부터 이어서 다운로드 받는다.

<pre>
  <code>
    networkManager.AdditionalAssetDownload((isScoreModeFileUpdated) =>
            {
                // 리소스 다운로드가 발생한 경우 ScoreMode 썸네일을 초기화한다.
                if (isScoreModeFileUpdated)
                {
                    SBDebug.Log("ScoreModeStageSon file 갱신 감지됨");
                    TextureController.InitThumbNailImages();
                }

                isFinished = true;
            },
  </code>
</pre>

<pre>
  <code>
    public async void AdditionalAssetDownload(Action<bool> callback, Action<int, int> progress, Action<FileDto> targetToDownload = null)
		{
			bool isScoreModeFileUpdated = false;

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
				int fileCount = 0;

				int newFileCount = 0;

				assetFiles.Clear();

				await Task.Run(() =>
				{
					foreach (FileDto file in files)
					{
						if (!playerSheetStorage.IsFileExist(file.filename, file.createdAt))
						{
							assetFiles.Add(file, false);
							newFileCount ++;
						}
						else
                        {
							assetFiles.Add(file, true);
						}
					}
				});

				foreach (FileDto file in files)
				{
					// SBDebug.Log("TestAssetLoader 01 : " + file.filename);

					//내가 갖고있지 않은 파일인 경우
					if (!assetFiles[file])
					{
						SBDebug.Log("<color=green>Update file.filename : </color>" + file.filename);
						SBDebug.Log("<color=green>Update file.name : </color>" + file.name);

						if (file.name.Equals("ScoreModeStageSon"))
						{
							isScoreModeFileUpdated = true;
						}
						
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
											this.AdditionalAssetDownload(callback, progress, targetToDownload);
											this.assetLoadRetryCount++;
										});
									}
									return;
								}
								reqSrc.TrySetResult(data);
								this.assetLoadRetryCount = 0;
							});
						byte[] data = await reqSrc.Task;

						switch (CommonProcessController.GetNameString())
						{
							case CommonProcessController.KWONEUNBIINFO:
							case CommonProcessController.IKONINFO:
								await Task.Run(() =>
								{
									data = SBCrypto.DecryptData(data);
									data = data.Reverse().ToArray();

									if (data != null)
									{
										SBDataSheet.Instance.SetData(file.name, data, false);

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
								});

								break;

							default:
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
								break;
						}

						// 데이터 업데이트때도 잘 되는지 확인 필요. 		
						fileCount++;
						progress?.Invoke(fileCount, newFileCount);

					}
					//내가 갖고 있는 파일인 경우
					else
					{
						switch (CommonProcessController.GetNameString())
						{
							case CommonProcessController.KWONEUNBIINFO:
							case CommonProcessController.IKONINFO:	
								await Task.Run(() =>
								{
									byte[] binData = playerSheetStorage.GetFileData(file.name);

									if (binData != null)
									{
										//바이너리 파일간의 비교
										SBDataSheet.Instance.SetData(file.name, binData, false);
									}

									if (file.name.Equals("ScoreModeStageSon"))
									{
										isScoreModeFileUpdated = true;
									}
								});
								break;

							default:
								byte[] binData = playerSheetStorage.GetFileData(file.name);

								if (binData != null)
								{
									//바이너리 파일간의 비교
									SBDataSheet.Instance.SetData(file.name, binData);
								}

								if (file.name.Equals("ScoreModeStageSon"))
								{
									isScoreModeFileUpdated = true;
								}
								break;
						}		
					}	
				}


				switch (CommonProcessController.GetNameString())
				{
					case CommonProcessController.KWONEUNBIINFO:
					case CommonProcessController.IKONINFO: 
						await Task.Run(() =>
						{
							playerSheetStorage.WritePlayerSheetInfo(files);
						});
						break;

					default:
							playerSheetStorage.WritePlayerSheetInfo(files);
						break;

				}
						
				/*Debug.Log(JsonUtility.ToJson(SBDataSheet.Instance.ItemProduction[1], true));
				foreach (var item in SBDataSheet.Instance.ItemProduction.Values)
				{
					Debug.Log("item(Code:" + item.Code + ") - " + JsonUtility.ToJson(item, true));*
				}*/

				callback?.Invoke(isScoreModeFileUpdated);
			}
			else
				callback?.Invoke(isScoreModeFileUpdated);
		}
  </code>
</pre>
