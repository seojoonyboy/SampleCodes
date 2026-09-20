using System;
using UnityEngine;
using BestHTTP;
using Snowballs.Network.Dto;
using Snowballs.Util;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace Snowballs.Network
{
	public static class SBHttp
	{
		private static readonly SBConfigs configs = SBConfigs.Instance;

		public static string CP = "0";
		private static bool isSetUpdateTime = false;

		private static Dictionary<InvokeKind, Action<InvokeDto>> invokeListener = new Dictionary<InvokeKind, Action<InvokeDto>>();

		[SerializeField]
		public class SSOAccessRequest
		{
			public String uuid;
			public Int32 type;
			public String lang;
		}

		[SerializeField]
		public class SSOAccessResponse
		{
			public Int32 code;
			public String accessCode;
		}

		[SerializeField]
		public class SSOResultRequest
		{
			public String uuid;
			public String code;
		}

		[SerializeField]
		public class SSOResultResponse
		{
			public Int32 code;
			public Boolean isNew;
			public Boolean isGuest;
			public String token;
		}

		/// <summary>
		/// 서버가 업데이트가 필요한 정보를 클라이언트로 보내줬을 때 처리하기 위해 리스너를 등록.
		/// 하나의 invoke에 하나의 리스너만 등록 가능 함.
		/// </summary>
		/// <param name="kind">리스너에 등록할 invoke 종류</param>
		/// <param name="action">invoke가 발생했을 때 처리할 callback</param>
		public static void AddInvokeListener(InvokeKind kind, Action<InvokeDto> invokeDto)
		{
			if (invokeListener.ContainsKey(kind))
			{
				SBDebug.Log("<color=yellow>" + kind + "의 이벤트 처리가 이미 정의되어 있습니다. 중복된 이벤트 등록을 시도하였습니다.</color>");

				return;
			}
			invokeListener.Add(kind, invokeDto);
		}

		/// <summary>
		/// 등록한 invoke 리스너를 삭제 함.
		/// </summary>
		/// <param name="kind">리스너를 삭제할 invoke 종류</param>
		public static void RemoveInvokeListener(InvokeKind kind)
		{
			invokeListener.Remove(kind);
		}

		/// <summary>
		/// 리스너에게 invoke 이벤트를 발생시킴.
		/// </summary>
		/// <param name="invokes">리스너를 삭제할 invoke 종류</param>
		private static void OnInvokeEvent(InvokeDto[] invokes)
		{
			for (int i = 0, len = invokes.Length; i < len; i++)
			{
				InvokeDto invoke = invokes[i];
				if (invoke.kind == InvokeKind.CP)
				{
					CP = invoke.value.ToString();
					continue;
				}
				if (!invokeListener.ContainsKey(invoke.kind))
				{
					SBDebug.Log("<color=yellow>" + invoke.kind + "의 이벤트 처리가 누락되었습니다. 해당 이벤트 처리가 필요합니다.</color>");

					continue;
				}
				invokeListener[invoke.kind](invoke);
			}
		}

		/// <summary>
		/// 대기열 체크
		/// </summary>
		/// <param name="cb">요청 후 결과를 callback으로 전달 받음</param>
		public static void RequestQueueCheck(Action<QueueDto> cb)
		{
			Uri url = new Uri(configs.QueueServerUrl);
			Request(HTTPMethods.Get, url, default, (code, text, req) =>
			{
				if (code != ResponseCode.OK)
				{
					cb(null);
					return;
				}
				QueueDto response = JsonUtility.FromJson<QueueDto>(text);
				cb(response);
			});
		}

		/// <summary>
		/// 서버에 연결하기 위한 정보를 요청 
		/// </summary>
		/// <param name="cb">요청 후 결과를 callback으로 전달 받음</param>
		public static void RequestAccess(Action<AccessDto> cb)
		{
			// 최소 15초 텀으로 v값을 붙여서 캐싱 안되도록 관리
			long time = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
			long v = time / 15;
			Uri url = new Uri(configs.AccessInfoUrl + "?v=" + v);
			Request(HTTPMethods.Get, url, default, (code, text, req) =>
			{
				if (code != ResponseCode.OK)
				{
					cb(null);
					return;
				}
				AccessDto response = JsonUtility.FromJson<AccessDto>(text);
				cb(response);
			});
		}

		/// <summary>
		/// 애셋 데이터 정보 요청
		/// </summary>
		/// <param name="cb">요청 후 결과를 callback으로 전달 받음</param>
		public static void RequestAssetDataInfo(Action<FileDto[]> cb)
		{
			Uri url = new Uri(configs.ResourcePath + configs.ResourceInfo);
			Request(HTTPMethods.Get, url, default, (code, text, req) =>
			{
				if (code != ResponseCode.OK)
				{
					cb(null);
					return;
				}
				FileListDto response = JsonUtility.FromJson<FileListDto>(text);
				cb(response.files);
			});
		}

		/// <summary>
		/// 서버로 데이터 요청
		/// </summary>
		/// <param name="method">메소드 타입 (Get, Post, Put, Delete...)</param>
		/// <param name="path">요청 경로</param>
		/// <param name="cb">요청 후 결과를 callback으로 전달 받음</param>
		public static void RequestAPI<T>(HTTPMethods method, string path, Action<ResponseDto<T>> cb, bool isRetry = true)
		{
			Uri url = new Uri(configs.GetAPIServerAddress() + path);
			Request(method, url, "", (code, text, req) =>
			{
				ResponseDto<T> response = new ResponseDto<T>((UInt16)code, SBTime.Instance.ISOServerTime, new InvokeDto[0])
				{
					error = new ErrorDto(true, false, true)
				};
				if (code != ResponseCode.OK)
				{
					RequestDto<string> empty = null;
					ApiExceptionController.Except(req, empty, response, isRetry);

					cb(response);
					return;
				}
				response = JsonUtility.FromJson<ResponseDto<T>>(text);

				/* if (ApiExceptionController.IsCheckExcept((ResponseCode)response.code))
				 {
					 ApiExceptionController.CheckExcept((ResponseCode)response.code);
					 return;
				 }*/

				if ((ResponseCode)response.code != ResponseCode.OK)
				{
					RequestDto<string> empty = null;
					ApiExceptionController.Except(req, empty, response, isRetry);
				}


				if (response.invokes != null && response.invokes.Length > 0)
				{
					OnInvokeEvent(response.invokes);
				}
				else
                {
					CommonProcessController.DeleteMailBoxCount();
				}
				cb(response);
			});
		}

		/// <summary>
		/// API 서버로 데이터 요청
		/// </summary>
		/// <param name="method">메소드 타입 (Get, Post, Put, Delete...)</param>
		/// <param name="path">요청 경로</param>
		/// <param name="sendData">서버로 보낼 데이터</param>
		/// <param name="cb">요청 후 결과를 callback으로 전달 받음</param>
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

		/// <summary>
		/// 서버로 데이터 요청
		/// </summary>
		/// <param name="method">메소드 타입 (Get, Post, Put, Delete...)</param>
		/// <param name="url">연결 주소</param>
		/// <param name="sendData">서버로 보낼 데이터</param>
		/// <param name="cb">요청 후 결과를 callback으로 전달 받음</param>
		public static void Request(HTTPMethods method, Uri url, String sendData, Action<ResponseCode, String, HTTPRequest> cb)
		{
			_Request(method, url, new Dictionary<string, string>(), sendData, false, cb);
		}

		/// <summary>
		/// 통합계정 관련 프로토콜
		/// </summary>
		/// <param name="cb">요청 후 결과를 callback으로 전달 받음</param>
		public static void RequestSSO<T>(string key, string path, String send, Action<ResponseCode, T> cb)
		{
			Uri url = new Uri(configs.LoginUrl + path);
			Dictionary<string, string> headers = new Dictionary<string, string>();
			headers.Add("x-sb-key", key);
			headers.Add("x-sb-appid", Application.identifier);
			headers.Add("x-sb-version", Application.version);
#if UNITY_EDITOR
			headers.Add("x-sb-device", "Unity");
#elif UNITY_ANDROID
			headers.Add("x-sb-device", "Android");
#elif UNITY_IOS
			headers.Add("x-sb-device", "iOS");
#else
			headers.Add("x-sb-device", "UNKNOWN");
#endif

			_Request(HTTPMethods.Post, url, headers, send, true, (code, text, req) =>
			{
				Debug.Log("code: " + code + ", text: " + text);
				if (code != ResponseCode.OK)
				{
					cb(code, (T)default);
					return;
				}
				Debug.Log("code: " + code + ", text: " + text);
				cb(code, JsonUtility.FromJson<T>(text));
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

		/// <summary>
		/// 파일 다운로드
		/// </summary>
		/// <param name="url">파일 주소</param>
		/// <param name="cb">요청 후 결과를 callback으로 전달 받음</param>
		public static void RequestFile(Uri url, Action<ResponseCode, Byte[]> cb)
		{
			HTTPRequest request = new HTTPRequest(url, (req, resp) => {

				if (resp != null)
					SBDebug.Log("<color=yellow>FILE RESPNOSE> " + url + "(" + resp.StatusCode + ")</color>");
				else
					SBDebug.Log("<color=yellow>FILE RESPNOSE> " + url + " CONNECTION_REFUSED</color>");

				if (resp == null)
				{
					cb(ResponseCode.ConnectionRefused, default);
					return;
				}

				ResponseCode resCode = ResponseCode.OK;
				int statusCode = resp.StatusCode;
				byte[] data = new byte[0];
				switch (req.State)
				{
					case HTTPRequestStates.Finished:
						if (resp.IsSuccess)
						{
							data = resp.Data;

							SBDebug.Log("<color=green>FILE SUCCESS> Size: " + data.Length + "</color>");

						}
						else
						{

							SBDebug.Log("<color=red>FILE FAILED> StatusCode: " + statusCode + "</color>");

							resCode = (ResponseCode)statusCode;
							if (!Enum.IsDefined(typeof(ResponseCode), resCode))
							{
								resCode = ResponseCode.StatusError;
							}
						}
						break;
					case HTTPRequestStates.Error:

						SBDebug.Log("<color=red>FILE ERROR> StatusCode: " + statusCode + "</color>");

						resCode = ResponseCode.StatusError;
						break;
					case HTTPRequestStates.Aborted:

						SBDebug.Log("<color=red>FILE ABORTED> StatusCode: " + statusCode + "</color>");

						resCode = ResponseCode.StatusAbort;
						break;
					case HTTPRequestStates.ConnectionTimedOut:

						SBDebug.Log("<color=red>FILE CONNECTION TIMEOUT> StatusCode: " + statusCode + "</color>");

						resCode = ResponseCode.RequestTimeout;
						break;
					case HTTPRequestStates.TimedOut:

						SBDebug.Log("<color=red>FILE TIMEOUT> StatusCode: " + statusCode + "</color>");

						resCode = ResponseCode.RequestTimeout;
						break;
					default:

						SBDebug.Log("<color=yellow>FILE UNKNOWN> StatusCode: " + statusCode + ", " + req.State.ToString() + "</color>");

						resCode = ResponseCode.StatusUnknown;
						break;
				}

				cb(resCode, data);
			});

			request.EnableTimoutForStreaming = true;
			request.Timeout = new TimeSpan(0, 0, 500);

			request.Send();
		}
	}
}
