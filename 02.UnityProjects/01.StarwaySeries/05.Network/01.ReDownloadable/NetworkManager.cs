using UnityEngine;

using System;
using System.Collections;
using System.Threading.Tasks;

using Snowballs.Network.API;
using Snowballs.Network.Dto;
using Snowballs.Sheets;
using Snowballs.Util;
using com.snowballs.SWHJ.client.model;
using com.snowballs.SWHJ.client.view;
using static Snowballs.Network.SBHttp;
using com.snowballs.SWHJ.presenter;
using System.Net;
using com.snowballs.SWHJ.type;

namespace Snowballs.Network
{
	public class NetworkManager : MonoBehaviour
	{
		[SerializeField] String AccessInfoURL;
		[SerializeField] String LoginKey;

		private static readonly SBConfigs configs = SBConfigs.Instance;
		private IEnumerator refreshTokenCoroutine;
		private bool isStarted = false;
		public bool IsStarted
		{
			get
			{
				return isStarted;
			}
		}

		public void SetAccessInfoURL(string url)
		{
			if (string.IsNullOrEmpty(url)) return;

			this.AccessInfoURL = url;
		}

		public string GetAccessInfoURL
		{
			get
			{
				return this.AccessInfoURL;
			}
		}

		private AccessDto accessData;
		private int assetLoadRetryCount = 0;

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
					// SBDebug.Log("TestAssetLoader 01 : " + file.filename);

					//내가 갖고있지 않은 파일인 경우
					if (!playerSheetStorage.IsFileExist(file.filename, file.createdAt))
					{
						SBDebug.Log("<color=green>Update file.filename : </color>" + file.filename);
						SBDebug.Log("<color=green>Update file.name : </color>" + file.name);

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

				/*Debug.Log(JsonUtility.ToJson(SBDataSheet.Instance.ItemProduction[1], true));
				foreach (var item in SBDataSheet.Instance.ItemProduction.Values)
				{
					Debug.Log("item(Code:" + item.Code + ") - " + JsonUtility.ToJson(item, true));*
				}*/

				callback?.Invoke(isDownloadOccured);
			}
			else
				callback?.Invoke(isDownloadOccured);
		}

		public void Open(Action initProcessCallback = null)
		{
			this.isStarted = true;
			configs.init(AccessInfoURL);
			StartRefreshTokenCoroutine();
			InitProcess(initProcessCallback);
		}

		public IEnumerator CheckVersionData(AccessDto access, VersionDto version, Action callback)
		{
			////////////////// 테스트용 코드 넣는곳.////////////

			/////////////////////////////////////////////////////

			if (version == null)
			{
				// 버전 값에 맞는 정보가 없을 경우
				Debug.Log("No access information was found for the current version.");
				yield break;
			}

			// 아이피 체크.
			yield return this.CheckGetIP(access);

			// 앱 강제업데이트 체크.
			yield return this.CheckAppUpdate(version);

			// 팝업메시지 체크.
			yield return this.CheckPopupMessage(version);

			// 팝업웹뷰 체크.
			yield return this.CheckPopupUrl(version);

			yield return this.CheckAllow(version);

			yield return this.CheckResourceServerUrl(version);

			if (version.loginKey != null)
			{
				LoginKey = version.loginKey;
			}
			if (version.resourcePath != null)
			{
				// 리소스 다운받을 경로
				configs.UpdateResourcePath(version.resourcePath);
			}
			if (version.resourceInfo != null)
			{
				// 해당 버전의 리소스 정보
				configs.UpdateResourceInfo(version.resourceInfo);
			}
			if (version.queueServer != null)
			{
				// 대기열 서버 주소
				configs.UpdateQueueServerUrl(version.queueServer);
			}
			if (version.loginUrl != null)
			{
				// 로그인 주소
				configs.UpdateLoginUrl(version.loginUrl);
			}

			if (version.bgImages != null)
			{
				//타이틀 이미지
				configs.UpdateBgImages(version.bgImages);
			}

			if (version.bgVideos != null)
			{
				//타이틀 영상
				configs.UpdateBgVideos(version.bgVideos);
			}

			// API 서버와 이벤트 서버 리스트 세팅
			string[] apiUrls = version.apiServers == null ? new string[0] : version.apiServers;
			string[] eventUrls = version.eventServers == null ? new string[0] : version.eventServers;
			configs.UpdateServerUrls(apiUrls, eventUrls);

			//이용약관 웹뷰를 띄운다.
			//처리가 끝나면 아래 callback을 호출한다.
			AuthDto auth = SBConfigs.Instance.GetAuthDtoByPlayerPrefs();
			if (auth == null)
			{
				Debug.Log("SJW #1 11111");
#if UNITY_EDITOR
				callback?.Invoke();
#elif (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
					string url = access.terms + "?lang=" + ((GameStorage.UserAccountLanguage == com.snowballs.SWHJ.type.Language.ko) ? "ko" : "en");
					Debug.Log("url = " + url);
					this.ShowTermsWebView(url, (isSucess) => {
						if (isSucess)
						{
							GameStorage.TermsVersion = access.termsVersion;
							GameStorage.TermsAgreeDate = SBTime.Instance.ServerTime;
							Debug.Log("Terms WebViewResult OK");
						}
						else
						{
							Debug.Log("Terms WebViewResult Failed");
						}
						callback?.Invoke();
					});
#endif
			}
			else
			{
#if UNITY_EDITOR
				callback?.Invoke();
#elif (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
				if ((GameStorage.TermsVersion < access.termsVersion) || (SBTime.Instance.ServerTime >= GameStorage.TermsAgreeDate.AddYears(2)))
                {
					string url = access.terms + "?lang=" + ((GameStorage.UserAccountLanguage == com.snowballs.SWHJ.type.Language.ko) ? "ko" : "en");
					Debug.Log("url = " + url);
					this.ShowTermsWebView(url, (isSucess) => 
					{
						if (isSucess)
						{
							GameStorage.TermsVersion = access.termsVersion;
							GameStorage.TermsAgreeDate = SBTime.Instance.ServerTime;

							Debug.Log("Terms WebViewResult OK");
						}
						else
						{
							Debug.Log("Terms WebViewResult Failed");
						}
						callback?.Invoke();
					});
                }
				else
                {
					callback?.Invoke();
                }
#endif
			}
		}

		bool isSkipAllow = false;

		private IEnumerator CheckGetIP(AccessDto access)
		{
			bool isFinished = false;

			if (access.ipCheckUrl != null)
			{
				try
				{
					string externalIP = new WebClient().DownloadString(access.ipCheckUrl);

					isSkipAllow = Array.Exists(access.allowIPs, x => x.Equals(externalIP));

					isFinished = true;
				}
				catch (Exception e)
				{
					isSkipAllow = false;
					isFinished = true;
				}
			}
			else
			{
				isFinished = true;
			}

			yield return new WaitUntil(() => isFinished);
		}




		private IEnumerator CheckPopupMessage(VersionDto version)
		{
			bool isFinished = false;

			if (version.popupMessage != null && version.popupMessage.Length > 0)
			{
				// 팝업 메시지 로케일 구분 했습니다.
				string lang = ((GameStorage.UserAccountLanguage == com.snowballs.SWHJ.type.Language.ko) ? "ko" : "en");
				string message = "";
				foreach (var popupMessage in version.popupMessage)
				{
					// 디폴트 영문 세팅
					if (String.IsNullOrEmpty(message) && popupMessage.region == "en")
					{
						message = popupMessage.text;
					}
					// 설정된 언어 값에 해당하는 메시지가 있다면 세팅
					if (popupMessage.region == lang)
					{
						message = popupMessage.text;
					}
				}
				ViewController.OpenConfirmPopup(LocaleController.GetBuiltInLocale(6), message, (isOk) =>
				{
					if (version.allow)
					{
						isFinished = true;
					}
					else
					{
						if (isSkipAllow)
						{
							isFinished = true;
						}
						else
						{
							GameScene.Instance.OnRestart();
						}
					}
				});
			}
			else
			{
				isFinished = true;
			}

			yield return new WaitUntil(() => isFinished);
		}

		private IEnumerator CheckPopupUrl(VersionDto version)
		{
			bool isFinished = false;

			if (version.popupUrl != null)
			{
#if UNITY_EDITOR
				ViewController.OpenConfirmPopup(LocaleController.GetBuiltInLocale(6), "에디터라서 팝업으로 대체", (isOk) =>
				{
					if (version.allow)
					{
						isFinished = true;
					}
					else
					{
						if (isSkipAllow)
						{
							isFinished = true;
						}
						else
						{
							UnityEditor.EditorApplication.isPlaying = false;
						}
					}
				});
#else
				string url = version.popupUrl + "?lang=" + ((GameStorage.UserAccountLanguage == com.snowballs.SWHJ.type.Language.ko) ? "ko" : "en");
				ViewController.OpenWebView(url, () =>
				{
					if (version.allow)
					{
						isFinished = true;
					}
					else
                    {	
					    if (isSkipAllow)
						{
							isFinished = true;
						}
						else
						{
							Application.Quit();
						}
					}
				});
#endif
			}
			else
			{
				isFinished = true;
			}

			yield return new WaitUntil(() => isFinished);
		}

		private string _assetServerName;
		public string AssetServerName => this._assetServerName;

		private IEnumerator CheckResourceServerUrl(VersionDto versionDto)
		{
			this._assetServerName = versionDto.asset;
			yield return null;
		}

		private IEnumerator CheckAppUpdate(VersionDto version)
		{
			bool isFinished = false;

			if (version.needToBeUpdated == true)
			{
				ViewController.OpenApplicationUpdatePopup((isOk) =>
				{
					string url = CommonProcessController.GetStoreURL();
					Application.OpenURL(url);

#if UNITY_EDITOR
					UnityEditor.EditorApplication.isPlaying = false;
#else
					Application.Quit();
#endif
				});

				yield return null;
			}
			else
			{
				isFinished = true;
			}

			yield return new WaitUntil(() => isFinished);
		}

		private IEnumerator CheckAllow(VersionDto version)
		{
			bool isFinished = false;
			if (!version.allow)
			{
				if (isSkipAllow)
				{
					isFinished = true;
				}
				else
				{
					// 서버에서 접속을 차단하고 있음
					Debug.Log("Server access is being blocked.");

					ViewController.OpenConfirmPopup(LocaleController.GetBuiltInLocale(6), LocaleController.GetBuiltInLocale(11), (isOk) =>
					{
						GameScene.Instance.OnRestart();
					});
				}
			}
			else
			{
				isFinished = true;
			}

			yield return new WaitUntil(() => isFinished);
		}


		public void InitProcess(Action callback = null)
		{
			SBHttp.RequestAccess((access) =>
			{
				//accessUrl 에 통신 실패.
				if (access == null)
				{
					ViewController.OpenNetworkUnAvailablePopup();
				}
				else
				{
					VersionDto version = null;
					string curVersion = Application.version;
					foreach (VersionDto item in access.versions)
					{
						// 접속 정보에서 현재 버전에 해당 되는 정보를 찾음
						int minDiff = item.min == null ? 1 : SBString.versionDiff(curVersion, item.min);
						int maxDiff = item.max == null ? 1 : SBString.versionDiff(item.max, curVersion);
						if (minDiff + maxDiff > 0)
						{
							version = item;
							break;
						}
					}

					accessData = access;

					StartCoroutine(CheckVersionData(access, version, callback));
				}
			});
		}

		//저장된 인증 정보가 있는지 확인
		public void CheckAuth(Action<bool> callback)
		{
			AuthDto auth = SBConfigs.Instance.GetAuthDtoByPlayerPrefs();
			callback(auth != null);     //토큰이 만료되거나, 존재하지 않으면 null임
		}

		public async void LoginProcess(Action<ResponseDto<SonPlayerDto>, ResponseDto<SonPlayerSubDto>> cb, Action hideCallback)
		{
			bool isNewAccount = false;
			AuthDto auth = SBConfigs.Instance.GetAuthDtoByPlayerPrefs();
			if (auth == null)
			{
				// 저장된 인증 정보가 없을 경우 로그인 팝업을 띄워서 로그인 진행
				var webViewSrc = new TaskCompletionSource<SSOResultResponse>();
				//isNewAccount = webViewSrc.isNew;
#if UNITY_EDITOR
				isNewAccount = true;
				auth = new AuthDto(AuthKind.GUEST, null, true);
#elif UNITY_ANDROID || UNITY_IPHONE || UNITY_IOS
				ShowLoginWebView((code, res) =>
				{
					res.code = (int)code;
					webViewSrc.TrySetResult(res);
				});
				SSOResultResponse result = await webViewSrc.Task;		//웹뷰 로그인 처리하는 동안 대기
				if ((ResponseCode)result.code != ResponseCode.OK)
					return;
				isNewAccount = result.isNew;
				Debug.Log("res.isNew = " + result.isNew);
				auth = new AuthDto(result.isGuest ? AuthKind.GUEST : AuthKind.CODE, result.token, isNewAccount);
#endif
			}

			hideCallback();

			// 대기열 체크
			var queueSrc = new TaskCompletionSource<QueueDto>();
			SBHttp.RequestQueueCheck((response) =>
			{
				queueSrc.TrySetResult(response);
			});
			QueueDto queue = await queueSrc.Task;
			if (queue.remainedNo > 0)
			{
				// 반복 시켜야 하지만 테스트 코드이니 패스
			}

			// 서버 정보(타임존, 오프셋 등) 동기화
			var infoSrc = new TaskCompletionSource<bool>();
			Entry.Info((response) =>
			{
				if ((ResponseCode)response.code != ResponseCode.OK)
				{
					infoSrc.TrySetResult(false);
				}
				else
				{
					SBTime.Instance.SetTimezone(response.data.timezone);
					SBTime.Instance.SetTimeOffset(response.data.timeOffset);
					GameStorage.ADStorage.IsADEnable = response.data.adEnabled;
					infoSrc.TrySetResult(true);
				}
			});
			bool infoRes = await infoSrc.Task;
			if (!infoRes)
				return;

			// API 서버로 로그인
			var tokenSrc = new TaskCompletionSource<bool>();
			RequestDto<AuthDto> authReq = new RequestDto<AuthDto>(0, auth, Guid.NewGuid().ToString(), SBTime.Instance.ISOServerTime);
			string pushToken = FirebaseManager.FcmToken; // 푸시 전송을 위한 토큰 값
			DeviceDto device = new DeviceDto(
#if UNITY_EDITOR
				Dto.DeviceType.UNITY,
#elif UNITY_ANDROID
				Dto.DeviceType.ANDROID,
#elif UNITY_IOS
				Dto.DeviceType.IOS,
#else
				Dto.DeviceType.UNKNOWN,
#endif
				pushToken
			);
			device.push = GameStorage.IsPushNotification;  // 푸시 알림 수신 여부 (기본 TRUE);
			device.locale = (Application.systemLanguage == SystemLanguage.Korean) ? "ko" : "en"; // 디바이스 언어 값

			if (device.push)
			{
				FirebaseManager.SubscribeTopicMessage(() => { });
			}
			else
			{
				FirebaseManager.UnSubscribeTopicMessage(() => { });
			}

			SBHttp.CP = "0";

			//	GameStorage.UserAccountLanguage = (Application.systemLanguage == SystemLanguage.Korean) ? com.snowballs.SWHJ.type.Language.ko : com.snowballs.SWHJ.type.Language.en;

			authReq.data.device = device;
			Auth.GetToken(authReq, (response) =>
			{
				if ((ResponseCode)response.code != ResponseCode.OK)
				{
					tokenSrc.TrySetResult(false);
					return;
				}
				configs.SetToken(auth, response.data);
				tokenSrc.TrySetResult(true);
			});
			bool tokenRes = await tokenSrc.Task;

			if (!tokenRes)
			{
				return;
			}

			if (isNewAccount)
			{
				LoadingIndicator.Hide();
				OpenNicknamePopup();
			}
			else
			{
				Debug.Log("SJW #4 Not New Account");
				GetPlayerInfo((response) =>
				{
					if ((ResponseCode)response.code == ResponseCode.AuthNeedInit)
					{
						LoadingIndicator.Hide();
						OpenNicknamePopup();
					}
					else
					{
						GetPlayerSubInfo((response2) =>
						{
							cb(response, response2);
						});
					}
				});
			}

			void OpenNicknamePopup()
			{
				TextInpupPopup.Params p = new TextInpupPopup.Params();

				p.Code = 90;

				var data = SBDataSheet.Instance.PopupInfo[p.Code].GetPopUpLocale();

				p.context1LocaleCode = data[0].DesLocale;
				p.context2LocaleCode = data[1].DesLocale;
				p.context3LocaleCode = data[2].DesLocale;

				p.placeholderLocaleCode = data[1].DesLocale;
				p.confirmBtnTextLocaleCode = data[3].DesLocale;


				GameScene.Instance.LockBackButton();
				Popup.Load("NickName/TextInputPopup", p, (popup, result) =>
				{
					//로딩 show
					LoadingIndicator.Show();

					// 닉네임 설정
					TextInpupPopup textInpupPopup = (TextInpupPopup)popup;
					var inputStr = textInpupPopup.InputString();

					string nickname = inputStr;
					RequestDto<String> initReq = new RequestDto<String>(0, nickname, Guid.NewGuid().ToString(), SBTime.Instance.ISOServerTime);
					GamePlayer.Init(initReq, (response) =>
					{
						GameScene.Instance.UnLockBackButton();
						if (response.code == (int)ResponseCode.OK)
						{
							Debug.Log("SJW #3 GamePlayer.Init response code : " + response.code);
							GetPlayerInfo((response) => {

								GetPlayerSubInfo((response2) =>
								{
									cb(response, response2);
								});

								//로딩 hide
								LoadingIndicator.Hide();
							});
						}
						else
						{
							LoadingIndicator.Hide();

							if ((ResponseCode)response.code == ResponseCode.GamePlayerNicknameNotAvailableWord)
							{
								ViewController.OpenConfirmPopup(10, (isOk) =>
								{
									OpenNicknamePopup();
								});
							}
							else if((ResponseCode)response.code == ResponseCode.GamePlayerNicknameIsTooShortOrTooLong)
							{
								ViewController.OpenConfirmPopup(10, (isOk) =>
								{
									OpenNicknamePopup();
								});
							}
							else if ((ResponseCode)response.code == ResponseCode.GamePlayerDuplicateNickname)
							{
								ViewController.OpenConfirmPopup(11, (isOk) =>
								{
									OpenNicknamePopup();
								});
							}
						}
					});
				});
			}

			// 플레이어 정보 가져오기
			/*void GetPlayerInfo(Action<ResponseDto<PlayerDto>> callback)
			{ 
				GamePlayer.GetPlayerInfo((response) =>
				{
					callback(response);
				});
			}*/

			// 플레이어 정보 가져오기
			void GetPlayerInfo(Action<ResponseDto<SonPlayerDto>> callback)
			{
				GamePlayer.GetSonPlayerInfo((response) =>
				{
					callback(response);
				});
			}

			void GetPlayerSubInfo(Action<ResponseDto<SonPlayerSubDto>> callback)
			{
				GamePlayer.GetSonPlayerSubInfo((response) =>
				{
					callback(response);
				});
			}
		}


		/*public void GetMagazineList(Action<ResponseDto<MagazineListDto>> callback)
		{
			GameMagazine.GetList((response) =>
			{
				callback(response);
			});
		}*/


		public void SetStageMagazineMessage(RequestDto<MagazineDto> requestDto, Action<ResponseDto<String>> callback)
		{
			GameMagazine.SetStageMessage(requestDto, (response) =>
			{
				callback(response);
			});
		}

		public void SetSpecialMagazineMessage(RequestDto<MagazineDto> requestDto, Action<ResponseDto<String>> callback)
		{
			GameMagazine.SetSpecialMessage(requestDto, (response) =>
			{
				callback(response);
			});
		}



		/*public void ChangeDelegatePhoto(RequestDto<MagazineCodeDto> requestDto, Action<ResponseDto<String>> callback)
		{
			GamePlayer.ChangeDelegatePhoto(requestDto, (response) =>
			{
				callback(response);
			});
		}*/


		/*public void GetCardList(Action<ResponseDto<CardListDto>> callback)
		{	
			GameCard.GetCardList((response) =>
			{
				callback(response);
			});
		}*/


		public void GetConnectUrl(Action<ResponseDto<String>> callback)
		{
			Auth.GetSSOConnectUrl((response) =>
			{
				callback(response);
			});
		}


		public void GetMyUrl(Action<ResponseDto<String>> callback)
		{
			Auth.GetSSOMyUrl((response) =>
			{
				callback(response);
			});
		}


		public void GetLeaveUrl(Action<ResponseDto<String>> callback)
		{
			Auth.GetSSOLeaveUrl((response) =>
			{
				callback(response);
			});
		}


		/*public void SetDelegate(RequestDto<CardDelegateListDto> requestDto, Action<ResponseDto<String>> callback)
		{
			GameCard.SetSonDelegate(requestDto, (response) =>
			{
				callback(response);
			});
		}*/



		public void Equip(RequestDto<SonPlayerEquipDto> requestDto, Action<ResponseDto<String>> callback)
		{
			GamePlayer.Equip(requestDto, (response) =>
			{
				callback(response);
			});
		}



		public void SetMessage(RequestDto<CardMessageDto> requestDto, Action<ResponseDto<String>> callback)
		{
			GameCard.SetSonMessage(requestDto, (response) =>
			{
				callback(response);
			});
		}


		public void Upgrade(RequestDto<CardUpgradeDto> requestDto, Action<ResponseDto<CardDto>> callback)
		{
			GameCard.Upgrade(requestDto, (response) =>
			{
				callback(response);
			});
		}


		public void UpgradeReward(RequestDto<CardUpgradeDto> requestDto, Action<ResponseDto<AcquiredItemDto>> callback)
		{
			GameCard.UpgradeReward(requestDto, (response) =>
			{
				callback(response);
			});
		}

		public void RankUp(RequestDto<CodeValueDto> requestDto, Action<ResponseDto<AcquiredItemDto>> callback)
		{
			GameCard.SonRankUp(requestDto, (response) =>
			{
				callback(response);
			});
		}
		public void RankUpReward(RequestDto<CodeValueDto> requestDto, Action<ResponseDto<AcquiredItemDto>> callback)
		{
			GameCard.SonRankUpReward(requestDto, (response) =>
			{
				callback(response);
			});
		}

		public void LevelUp(RequestDto<CardLevelUpDto> requestDto, Action<ResponseDto<AcquiredItemDto>> callback)
		{
			GameCard.SonLevelUp(requestDto, (response) =>
			{
				callback(response);
			});
		}


		public void ThemeReward(RequestDto<CodeDto> requestDto, Action<ResponseDto<AcquiredItemDto>> callback)
		{
			GameCard.ThemeReward(requestDto, (response) =>
			{
				callback(response);
			});
		}

		public void CardGacha(RequestDto<GachaCardBuyDto> requestDto, Action<ResponseDto<AcquiredItemDto>> callback)
		{
			GameGacha.SonCardGacha(requestDto, (response) =>
			{
				callback(response);
			});
		}

		public void PartsGacha(RequestDto<GachaPartsBuyDto> requestDto, Action<ResponseDto<SonGachaPartsResponseDto>> callback)
		{
			GameGacha.SonPartsGacha(requestDto, (response) =>
			{
				callback(response);
			});
		}

		public void Ceiling(RequestDto<CeilingDto> requestDto, Action<ResponseDto<AcquiredItemDto>> callback)
		{
			GameGacha.SonCeiling(requestDto, (response) =>
			{
				callback(response);
			});
		}


		public void BuyDiamond(RequestDto<StoreBuyDto> requestDto, Action<ResponseDto<AcquiredItemDto>> callback)
		{
			GameStore.BuyDiamond(requestDto, (response) =>
			{
				callback(response);
			});
		}

		public void BuyGold(RequestDto<StoreBuyDto> requestDto, Action<ResponseDto<AcquiredItemDto>> callback)
		{
			GameStore.BuyGold(requestDto, (response) =>
			{
				callback(response);
			});
		}

		public void BuyFromRecommend(RequestDto<StoreBuyDto> requestDto, Action<ResponseDto<AcquiredItemDto>> callback)
		{
			GameStore.BuyFromRecommend(requestDto, (response) =>
			{
				callback(response);
			});
		}


		public void BuyByMileage(RequestDto<MileageStoreBuyDto> requestDto, Action<ResponseDto<AcquiredItemDto>> callback)
		{
			GameStore.BuyByMileage(requestDto, (response) =>
			{
				callback(response);
			});
		}
		public void BuyChance(RequestDto<StageChanceDto> requestDto, Action<ResponseDto<AcquiredItemDto>> callback)
		{
			GameStage.BuyChance(requestDto, (response) =>
			{
				callback(response);
			});
		}

		public void AllClearReward(RequestDto<CodeDto> requestDto, Action<ResponseDto<AcquiredItemDto>> callback)
		{
			GameStage.AllClearReward(requestDto, (response) =>
			{
				callback(response);
			});
		}

		public void SeasonReward(RequestDto<CodeDto> requestDto, Action<ResponseDto<AcquiredItemDto>> callback)
		{
			GameStage.SeasonReward(requestDto, (response) =>
			{
				callback(response);
			});
		}





		public void GetSubscription(Action<ResponseDto<SubscriptionDto>> callback)
		{
			GameSubscription.GetSubscription((response) =>
			{
				callback(response);
			});
		}


		public void Restore(RequestDto<String> requestDto, Action<ResponseDto<String>> callback)
		{
			GameSubscription.Restore(requestDto, (response) =>
			{
				callback(response);
			});
		}

		public void GetRenew(Action<ResponseDto<AcquiredItemDto>> callback)
		{
			GameSubscription.GetRenew((response) =>
			{
				callback(response);
			});
		}

		public void ADCheck(RequestDto<CodeDto> requestDto, Action<ResponseDto<String>> callback)
		{
			GameAD.Check(requestDto, (response) =>
			{
				callback(response);
			});
		}

		public void ADReward(RequestDto<ADDto> requestDto, Action<ResponseDto<AcquiredItemDto>> callback)
		{
			GameAD.Reward(requestDto, (response) =>
			{
				callback(response);
			});
		}


		public void SeasonRank(RequestDto<CodeDto> requestDto, Action<ResponseDto<SeasonScoreRankDto>> callback)
		{
			GameRank.SeasonRank(requestDto, (response) =>
			{
				callback(response);
			});
		}

		public void GetScoreRank100(RequestDto<CodeDto> requestDto, Action<ResponseDto<ScoreRankDto>> callback)
		{
			GameRank.GetScoreRank100(requestDto, (response) =>
			{
				callback(response);
			});
		}


		public void Ack(RequestDto<HistoryDto> requestDto, Action<ResponseDto<String>> callback)
		{
			GameHistory.Ack(requestDto, (response) =>
			{
				if (response != null && (ResponseCode)response.code == ResponseCode.OK)
				{
					switch (requestDto.data.resource)
					{
						case "StageRewardInfo":
							if (GameScene.unreceivedStageReward != null)
							{
								var targetItem = GameScene.unreceivedStageReward.Find(x => x.value == requestDto.data.no);
								if (targetItem != null)
									GameScene.unreceivedStageReward.Remove(targetItem);
							}
							break;

						case "DailyBonusInfo":
							if (GameScene.unreceivedDailyBonusReward != null)
							{
								var targetItem = GameScene.unreceivedDailyBonusReward.Find(x => x.value == requestDto.data.no);
								if (targetItem != null)
									GameScene.unreceivedDailyBonusReward.Remove(targetItem);
							}
							break;

						case "PassInfo":
							if (GameScene.unreceivedPassReward != null)
							{
								var targetItem = GameScene.unreceivedPassReward.Find(x => x.value == requestDto.data.no);
								if (targetItem != null)
									GameScene.unreceivedPassReward.Remove(targetItem);
							}
							break;

						case "Advertisement":
							if (GameScene.unreceivedADReward != null)
							{
								var targetItem = GameScene.unreceivedADReward.Find(x => x.value == requestDto.data.no);
								if (targetItem != null)
									GameScene.unreceivedADReward.Remove(targetItem);
							}
							break;

						case "CardGachaSon":
							if (GameScene.unreceivedCardGachaReward != null)
							{
								var targetItem = GameScene.unreceivedCardGachaReward.Find(x => x.value == requestDto.data.no);
								if (targetItem != null)
									GameScene.unreceivedCardGachaReward.Remove(targetItem);
							}
							break;

							// 사용가능 한 부분이지만 QA가 필요한 부분이라 일단 주석.
							/*
							default:
								if (GameScene.unreceivedEtcReward != null)
								{
									var targetItem = GameScene.unreceivedEtcReward.Find(x => x.resource == requestDto.data.resource && x.value == requestDto.data.no);
									if (targetItem != null)
										GameScene.unreceivedEtcReward.Remove(targetItem);
								}
								break;
							*/
					}
				}

				callback(response);
			});
		}

		void OnApplicationPause(bool isPaused)
		{
			// 활성/비활성화할 경우 토큰 갱신 코루틴 재설정
			if (isStarted)
			{
				if (isPaused)
				{
					StartRefreshTokenCoroutine();
				}
				else
				{
					StopCoroutine(refreshTokenCoroutine);
				}
			}
		}

		void StartRefreshTokenCoroutine()
		{
			refreshTokenCoroutine = RefreshToken();
			StartCoroutine(refreshTokenCoroutine);
		}

		IEnumerator RefreshToken()
		{
			// 5초에 한번씩 토큰이 존재하는지 체크하고 만료까지 지정된 시간이 남았는지 체크 후 갱신을 시도
			do
			{
				if (configs.IsExistToken() && configs.CanTokenRefresh())
				{
					AuthDto auth = new AuthDto(AuthKind.REFRESH_TOKEN, configs.GetRefreshToken(), false);
					RequestDto<AuthDto> authReq = new RequestDto<AuthDto>(0, auth, Guid.NewGuid().ToString(), SBTime.Instance.ISOServerTime);
					Auth.GetToken(authReq, (response) =>
					{
						if ((ResponseCode)response.code != ResponseCode.OK)
						{
							return;
						}

						configs.SetToken(null, response.data);
					});
				}
				yield return new WaitForSeconds(60f);
			} while (true);
		}


		public void ShowTermsWebView(string termsUrl, Action<bool> cb)
        {
			var webViewGameObject = new GameObject("UniWebView");
			var webView = webViewGameObject.AddComponent<UniWebView>();
			webView.SetShowSpinnerWhileLoading(true);
			webView.AddUrlScheme("sbauth");
			webView.Load(termsUrl);
			webView.Frame = new Rect(0, 0, Screen.width, Screen.height);
			webView.Show(false);
			webView.OnPageErrorReceived += (qwe, asd, d) =>
			{
				SBDebug.Log("OnPageErrorReceived !!");
				webView.Hide();

				ViewController.OpenApiErrorPopup2((int)ClientErrorType.TermsWebViewError, (isOk) =>
				{
					if (webView != null)
					{
						Destroy(webView);
						webView = null;
					}

					ShowTermsWebView(termsUrl, cb);
				});
			};
			webView.OnMessageReceived += (view, message) =>
			{
				if (message.Scheme == "sbauth" && message.Path == "success")
				{
					bool push = message.Args["push"].ToLower() == "true";
					bool nightPush = message.Args["nightpush"].ToLower() == "true";

					GameStorage.IsPushNotification = push;

					Destroy(webView);
					webView = null;
					cb(true);
				}
			};

			webView.OnShouldClose += (view) =>
			{
				if (webView != null)
				{
					Destroy(webView);
					webView = null;
				}
				return true;
			};
		}

		public void ShowLoginWebView(Action<ResponseCode, SSOResultResponse> cb)
		{
			SSOAccessRequest accessReq = new SSOAccessRequest()
			{
				uuid = Guid.NewGuid().ToString(),
				type = 0,
				lang = ((GameStorage.UserAccountLanguage == com.snowballs.SWHJ.type.Language.ko)
						? "ko" : "en"),
			};
			string data = JsonUtility.ToJson(accessReq);
			SBHttp.RequestSSO<SSOAccessResponse>(LoginKey, "/access", data, (code, accessRes) =>
			{
				if (code != ResponseCode.OK)
                {
					cb(code, default);
					return;
                }
				if (accessRes.code != 200)
				{
					cb((ResponseCode)accessRes.code, default);
					return;
				}

				string accessCode = accessRes.accessCode;
				string query = "?uuid=" + accessReq.uuid + "&code=" + accessCode;
				var safeBrowsing = UniWebViewSafeBrowsing.Create(configs.LoginUrl + query);

				Debug.Log("url = " + configs.LoginUrl + query);

				safeBrowsing.Show();
					
				safeBrowsing.OnSafeBrowsingFinished += (browsing) => {
					Destroy(safeBrowsing);
					safeBrowsing = null;

					SSOResultRequest resultReq = new SSOResultRequest()
					{
						uuid = accessReq.uuid,
						code = accessCode,
					};
					string data = JsonUtility.ToJson(resultReq);
					SBHttp.RequestSSO<SSOResultResponse>(LoginKey, "/result", data, (code, resultRes) =>
					{
						if (code != ResponseCode.OK)
						{
							cb(code, default);
							return;
						}
						cb((ResponseCode)resultRes.code, resultRes);
					});
				};
			});
		}

		public void ShowConnectWebView(string url, Action<ResponseCode, SSOResultResponse> cb)
		{
			SSOAccessRequest accessReq = new SSOAccessRequest()
			{
				uuid = Guid.NewGuid().ToString(),
				type = 1,
				lang = ((GameStorage.UserAccountLanguage == com.snowballs.SWHJ.type.Language.ko)
						? "ko" : "en"),
			};
			string data = JsonUtility.ToJson(accessReq);
			SBHttp.RequestSSO<SSOAccessResponse>(LoginKey, "/access", data, (code, accessRes) =>
			{
				if (code != ResponseCode.OK)
				{
					cb(code, default);
					return;
				}
				if (accessRes.code != 200)
				{
					cb((ResponseCode)accessRes.code, default);
					return;
				}

				string accessCode = accessRes.accessCode;
				string query = "&uuid=" + accessReq.uuid + "&code=" + accessCode;
				var safeBrowsing = UniWebViewSafeBrowsing.Create(url + query);

				Debug.Log("url = " + url + query);

				safeBrowsing.Show();

				safeBrowsing.OnSafeBrowsingFinished += (browsing) => {
					Destroy(safeBrowsing);
					safeBrowsing = null;

					SSOResultRequest resultReq = new SSOResultRequest()
					{
						uuid = accessReq.uuid,
						code = accessCode,
					};
					string data = JsonUtility.ToJson(resultReq);
					SBHttp.RequestSSO<SSOResultResponse>(LoginKey, "/result", data, (code, result) =>
					{
						if (code != ResponseCode.OK)
						{
							cb(code, default);
							return;
						}
						cb((ResponseCode)result.code, result);
					});
				};
			});
		}


		public void ShowMyWebView(string url, Action<bool> cb)
		{
			SSOAccessRequest accessReq = new SSOAccessRequest()
			{
				uuid = Guid.NewGuid().ToString(),
				type = 2,
				lang = ((GameStorage.UserAccountLanguage == com.snowballs.SWHJ.type.Language.ko)
						? "ko" : "en"),
			};
			string data = JsonUtility.ToJson(accessReq);
			SBHttp.RequestSSO<SSOAccessResponse>(LoginKey, "/access", data, (code, accessRes) =>
			{
				if (code != ResponseCode.OK || accessRes.code != 200)
				{
					// SSO 정보를 받아올 수 없음
					cb(false);
					return;
				}

				string accessCode = accessRes.accessCode;
				string query = "&uuid=" + accessReq.uuid + "&code=" + accessCode;
				var safeBrowsing = UniWebViewSafeBrowsing.Create(url + query);

				Debug.Log("url = " + url + query);

				safeBrowsing.Show();

				safeBrowsing.OnSafeBrowsingFinished += (browsing) => {
					Destroy(safeBrowsing);
					safeBrowsing = null;

					SSOResultRequest resultReq = new SSOResultRequest()
					{
						uuid = accessReq.uuid,
						code = accessCode,
					};
					string data = JsonUtility.ToJson(resultReq);
					SBHttp.RequestSSO<SSOResultResponse>(LoginKey, "/result", data, (code, result) =>
					{
						// SSO 내정보 확인 창이 닫힘
						cb(true);
					});
				};
			});
		}


		public void ShowLeaveWebView(string url, Action<bool> cb)
		{
			var webViewGameObject = new GameObject("UniWebView");
			var webView = webViewGameObject.AddComponent<UniWebView>();
			webView.SetShowToolbar(true, false, true, true);
			webView.SetShowToolbarNavigationButtons(true);
			webView.AddUrlScheme("sbauth");
			webView.SetHeaderField("x-sb-key", LoginKey);
			webView.SetHeaderField("x-sb-appid", Application.identifier);
			webView.SetHeaderField("x-sb-version", Application.version);
#if UNITY_EDITOR
			webView.SetHeaderField("x-sb-device", "Unity");
#elif UNITY_ANDROID
			webView.SetHeaderField("x-sb-device", "Android");
#elif UNITY_IOS
			webView.SetHeaderField("x-sb-device", "iOS");
#else
			webView.SetHeaderField("x-sb-device", "UNKNOWN");
#endif

			url = url + "&lang=" + GameStorage.PlayerStorage.Locale;

			webView.Load(url);
			webView.Frame = new Rect(0, 0, Screen.width, Screen.height);
			webView.Show(true, UniWebViewTransitionEdge.Bottom, 0.35f);

			webView.OnMessageReceived += (view, message) =>
			{

				if (message.Scheme == "sbauth")
				{

					if (message.Path == "logout")
					{
						cb(true);
						// 탈퇴 후 로그아웃처리
					}
					else if (message.Path == "cancel")
					{
						cb(false);
						// 탈퇴 취소
					}
					else if (message.Path == "fail")
                    {
						string code = message.Args["code"];

						// 에러 코드 처리

						cb(false);
					}
					else
                    {
						cb(false);
					}

					Destroy(webView);
					webView = null;
				}
			};

			webView.OnShouldClose += (view) =>
			{
				if (webView != null)
				{
					Destroy(webView);
					webView = null;
				}
				return true;
			};
		}

		public string GetNoticeUrl
        {
			get
			{
				if (accessData != null)
				{
					return accessData.notice;
				}

				return string.Empty;
			}
        }

		public string GetCSUrl
		{
			get
			{
				if (accessData != null)
				{
					return accessData.cs;
				}

				return string.Empty;
			}
		}
	}
}
