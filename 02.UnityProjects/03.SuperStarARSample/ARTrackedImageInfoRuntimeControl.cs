using System.Collections;
using System.Collections.Generic;

using UnityEngine;
#if ENABLE_AR_CONTENT
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif

using Ext.Unity3D.Cdn;

namespace com.dalcomsoft.project.app.control.contents
{
    using Control = ARTrackedImageInfoRuntimeControl;

    public class ARTrackedImageInfoRuntimeControl
    {
#if ENABLE_AR_CONTENT
        public static bool IsOpened { private set; get; }

        public static bool isInit = false;

        public delegate void InitResult(bool isSuccess, MutableRuntimeReferenceImageLibrary dynamicLib);
        private static InitResult callback;

        private static ARTrackedImageManager trackedImageManager;

        public static void Open()
        {
            Control.IsOpened = true;
        }

        public static void Close()
        {
            if (Control.IsOpened)
            {
                Control.IsOpened = false;
            }

            Control.callback = null;

            Control.isInit = false;
        }

        public static void ARImageInit(ARTrackedImageManager trackedImageManager, InitResult callback)
        {
            Ext.DebugX.Log("ARTrackedImageInfoRuntimeControl ARImageInit...");
            if (!Control.IsOpened) return;

            Control.callback = callback;
            Control.trackedImageManager = trackedImageManager;

            CoroutineTaskManager.AddTask(Control.ARImageInitialize());
        }

        private static IEnumerator ARImageInitialize()
        {
            Ext.DebugX.Log(
                string.Format("ARSession Init State : {0}", ARSession.state.ToString())
            );

#if UNITY_EDITOR
            Control.callback?.Invoke(false, null);
            yield break;
#endif

            if ((ARSession.state == ARSessionState.None) ||
                (ARSession.state == ARSessionState.CheckingAvailability))
            {
                yield return ARSession.CheckAvailability();
            }
            else if (ARSession.state == ARSessionState.Unsupported)
            {
                Control.callback?.Invoke(false, null);
                Control.callback = null;
            }
            else if (ARSession.state == ARSessionState.SessionInitializing)
            {
                MutableRuntimeReferenceImageLibrary runtimeLib = Control
                    .trackedImageManager
                    .CreateRuntimeLibrary() as MutableRuntimeReferenceImageLibrary;

                CoroutineTaskManager.AddTask(
                    Control.AddImages(runtimeLib)
                );
            }
        }

        private static IEnumerator AddImages(MutableRuntimeReferenceImageLibrary lib)
        {
            List<ARControl.ResponseData> imageDatas = ARControl.GetAllImageData();

            if (imageDatas == null || imageDatas.Count == 0)
            {
                Ext.DebugX.Log("<color=red>AR Image Load Failed</color>");
            }
            else
            {
                Ext.DebugX.Log("ARTrackedImageInfoRuntimeControl AddImages...");
                Texture2D[] textures = new Texture2D[imageDatas.Count];
                string[] names = new string[imageDatas.Count];

                int count = 0;
                foreach (ARControl.ResponseData respData in imageDatas)
                {
                    WWWManaged.GetCdnTexture(respData.downloadPath, null, (isSucess, _texture) =>
                    {
                        if (_texture != null)
                        {
                            textures[count] = _texture;
                            names[count] = respData.code.ToString();

                            Ext.DebugX.Log(
                                string.Format("Texture Format : {0}", textures[count].format)
                            );
                        }
                        else
                        {
                            Ext.DebugX.Log(
                                string.Format("AR Target Image Load Failed {0}", respData.downloadPath)
                            );
                        }

                        Ext.DebugX.Log(
                            string.Format("Add Image {0} finished!", count)
                        );

                        count++;
                    });
                }
                yield return new WaitUntil(() => count == imageDatas.Count);

                Ext.DebugX.Log(
                    string.Format("Add Images To Runtime Library Finished! {0}", count)
                );

                yield return AddImageJob(textures, names, lib);
            }

            Control.callback?.Invoke(true, lib);
            Control.callback = null;
        }

        private static IEnumerator AddImageJob(Texture2D[] textures, string[] imageNames, MutableRuntimeReferenceImageLibrary dynamicLibrary)
        {
            yield return null;

            if (!isInit)
            {
                Ext.DebugX.Log(
                    string.Format("Is Texture Readable : {0}", textures[0].isReadable)
                );
                var jobHandle = dynamicLibrary.ScheduleAddImageWithValidationJob(textures[0], imageNames[0], 1f);

                ARControl.Reset();

                //note. 첫 실행시(카메라 권한 설정 팝업 등장 하는 경우) 첫번째 이미지가 정상적으로 등록되지 않는 문제가 있어
                //첫 등록은 더미로 처리 (첫번째 등록만 그러함)
                //https://github.com/Unity-Technologies/arfoundation-samples/issues/932
                Ext.DebugX.Log("Dummy Begin Job");
                yield return new WaitUntil(() => jobHandle.jobHandle.IsCompleted);
                Ext.DebugX.Log(
                    string.Format("Dummy End Job - {0}", jobHandle.status)
                );
                bool dummySuccess = (jobHandle.status != AddReferenceImageJobStatus.ErrorUnknown) && (jobHandle.status != AddReferenceImageJobStatus.ErrorInvalidImage);

                for (int i = 0; i < textures.Length; i++)
                {
                    if ((i == 0) && dummySuccess)
                    {
                        ARControl.AddTrackedImage(textures[0].GetHashCode(), short.Parse(imageNames[0]));
                        Ext.DebugX.Log("Test 999 dummy success job add textures[i].GetHashCode() : " + textures[0].GetHashCode());
                        continue;
                    }

                    jobHandle = dynamicLibrary.ScheduleAddImageWithValidationJob(textures[i], imageNames[i], 1f);
                    ARControl.AddTrackedImage(textures[i].GetHashCode(), short.Parse(imageNames[i]));
                    Ext.DebugX.Log("Test 999 job add textures[i].GetHashCode() : " + textures[i].GetHashCode());

                    Ext.DebugX.Log(
                        string.Format("Begin Job {0}", i)
                    );

                    yield return new WaitUntil(() => jobHandle.jobHandle.IsCompleted);

                    Ext.DebugX.Log(
                        string.Format("End Job {0}, status : {1}", i, jobHandle.status)
                    );

                    Ext.DebugX.Log(
                        string.Format("End Job {0}, image Name : {1}", i, imageNames[i])
                    );
                }
            }

            Ext.DebugX.Log(
                string.Format("ARSession State : {0}", ARSession.state)
            );

            Ext.DebugX.Log(
                string.Format("ARSession.notTrackingReason : {0}", ARSession.notTrackingReason)
            );

            isInit = true;
        }
#endif  // ENABLE_AR_CONTENT
    }
}
