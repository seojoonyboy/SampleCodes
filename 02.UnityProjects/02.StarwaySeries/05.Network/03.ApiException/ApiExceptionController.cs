using BestHTTP;
using Snowballs.Client.View;
using Snowballs.Client.Scene;
using Snowballs.Network.Dto;
using Snowballs.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class ApiExceptionController
{
    public static void Except<T, T2>(HTTPRequest request, RequestDto<T> reqData, ResponseDto<T2> resData, bool isRetry)
    {
        // 일단 리스요청은 정상적으로 Finished.
        if (request.State == HTTPRequestStates.Finished)
        {
            SBDebug.Log("HTTPRequestStates.Finished");
            // Http 리스폰스가 정상적으로 받았을 때.
            if (request.Response.StatusCode == (int)ResponseCode.OK)
            {
                SBDebug.Log("request.Response.StatusCode = " + request.Response.StatusCode);
                // 우리게임 서버에서 api 200이 아닐때.
                if (resData.code != (int)ResponseCode.OK)
                {
                    if (resData.code == (int)ResponseCode.NeedUpdateSheet)
                    {
                        if (LoadingIndicator.IsShow)
                        {
                            LoadingIndicator.Hide();
                        }
                        ViewController.OpenConfirmPopup(95, (isOk) =>
                        {
                            GameScene.Instance.OnRestart();
                        });

                        return;
                    }
                    else if (resData.code == (int)ResponseCode.AccessDenied)
                    {
                        if (LoadingIndicator.IsShow)
                        {
                            LoadingIndicator.Hide();
                        }
                        ViewController.OpenConfirmPopup(134, (isOk) =>
                        {
                            GameScene.Instance.OnRestart();
                        });

                        return;
                    }
                    else if (resData.code == (int)ResponseCode.NeedUpdateStore)
                    {
                        if (LoadingIndicator.IsShow)
                        {
                            LoadingIndicator.Hide();
                        }
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

                        return;
                    }
                    else if ((ResponseCode)resData.code == ResponseCode.AuthNeedInit)
                    {
                        return;
                    }
                    else if ((ResponseCode)resData.code == ResponseCode.AuthSuspendAccount ||
                             (ResponseCode)resData.code == ResponseCode.AuthBlockAccount || (ResponseCode)resData.code == ResponseCode.AuthDropAccount)
                    {
                        if (LoadingIndicator.IsShow)
                        {
                            LoadingIndicator.Hide();
                        }
                        ViewController.OpenConfirmPopup(94, (isOk) =>
                        {
                            CommonProcessController.DeleteUserPlayerPrefs();
                            GameScene.Instance.OnRestart();
                        });

                        return;
                    }
                    else if ((ResponseCode)resData.code == ResponseCode.AuthInvalidToken || (ResponseCode)resData.code == ResponseCode.AuthExpiredToken || (ResponseCode)resData.code == ResponseCode.AuthSSOVerifyFail)
                    {
                        if (LoadingIndicator.IsShow)
                        {
                            LoadingIndicator.Hide();
                        }
                        ViewController.OpenConfirmPopup(94, (isOk) =>
                        {
                            if (string.IsNullOrEmpty(PlayerPrefs.GetString("GUEST")))
                            {
                                CommonProcessController.DeleteUserPlayerPrefs();
                            }

                            GameScene.Instance.OnRestart();

                        });
                    }
                    else if ((ResponseCode)resData.code == ResponseCode.GameWaitingResponseADReward)
                    {
                        if (reqData.no > 3)
                        {
                            if (LoadingIndicator.IsShow)
                            {
                                LoadingIndicator.Hide();
                            }
                            ViewController.OpenConfirmPopup(128, (isOk) =>
                            {
                                
                            });
                            return;
                        }
                    }

                    // 재시도 가능한 api.
                    if (resData.error.canRetry)
                    {
                        ApiExceptionController.OpenApiErrorRetryPopup(resData.code, request, reqData);
                    }
                    // 재시도 가능하지 않은 api라 
                    else
                    {
                        // 재실행 시키는 팝업 띄운다..
                        if (resData.error.needRestart)
                        {
                            ViewController.OpenRestartGamePopup(resData.code, (isOk) =>
                            {
                                GameScene.Instance.OnRestart();
                            });
                        }
                    }
                }
            }
            // Http 리스폰스가 정상적으로 받지 않았음.
            else
            {
                SBDebug.Log("request.Response.StatusCode = " + request.Response.StatusCode);
                
                if(isRetry)
                    ApiExceptionController.OpenApiErrorRetryPopup(request.Response.StatusCode, request, reqData);
                else
                    ViewController.OpenApiErrorPopup(
                        request.Response.StatusCode, 
                        0, 
                        0, 
                        0, 
                        (isOk) => { }
                    );
            }
        }
        else if (request.State == HTTPRequestStates.ConnectionTimedOut)
        {
            SBDebug.Log("HTTPRequestStates.ConnectionTimedOut");

            ApiExceptionController.OpenApiErrorRetryPopup((int)request.State, request, reqData);
        }
        else if (request.State == HTTPRequestStates.TimedOut)
        {
            SBDebug.Log("HTTPRequestStates.TimedOut");

            ApiExceptionController.OpenApiErrorRetryPopup((int)request.State, request, reqData);
        }
        else if (request.State == HTTPRequestStates.Error)
        {
            SBDebug.Log("HTTPRequestStates.Error");

            ApiExceptionController.OpenApiErrorRetryPopup((int)request.State, request, reqData);
        }
    }

    public static void OpenApiErrorRetryPopup<T>(int errorCode, HTTPRequest request, RequestDto<T> reqData)
    {
        if (LoadingIndicator.IsShow)
        {
            LoadingIndicator.Hide();
        }
        ViewController.OpenApiErrorPopup(errorCode, (isOk) =>
        {
            ApiExceptionController.RetryRequest(request, reqData);
        });
    }

    public static bool IsCheckExcept(ResponseCode code)
    {
        if ((ResponseCode)code == ResponseCode.NeedUpdateStore || (ResponseCode)code == ResponseCode.NeedUpdateSheet)
        {
            return true;
        }

        return false;
    }

    public static void CheckExcept(ResponseCode code)
    {
        if ((ResponseCode)code == ResponseCode.NeedUpdateStore)
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
            return;
        }
        else if ((ResponseCode)code == ResponseCode.NeedUpdateSheet)
        {
            ViewController.OpenConfirmPopup(95, (isOk) =>
            {
                Snowballs.Client.Scene.GameScene.Instance.OnRestart();
            });

            return;
        }
    }


    public static void RetryRequest<T>(HTTPRequest request, RequestDto<T> reqData)
    {
        HTTPRequest newReqeust = null;

        newReqeust = request;

        request.Dispose();
        newReqeust.SetRequestTime();

        if (reqData != null)
        {
            reqData.no += 1;
            reqData.time = SBTime.Instance.ISOServerTime;

            var rawData = SBCrypto.Encrypt(JsonUtility.ToJson(reqData));

            newReqeust.RawData = System.Text.Encoding.UTF8.GetBytes(rawData);
          
            SBDebug.Log("<color=cyan>[Retry Uri : " + newReqeust.Uri.AbsoluteUri.ToString() + "]</color> " + JsonUtility.ToJson(reqData));
        }

        LoadingIndicator.Show();
        newReqeust.Send();
    }
}
