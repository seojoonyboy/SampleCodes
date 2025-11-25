using System.Collections.Generic;

using UnityEngine;
#if ENABLE_AR_CONTENT
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif

using com.dalcomsoft.project.app.view.sub;

namespace com.dalcomsoft.project.app.control.contents
{
    public class ARContentsControl : MonoBehaviour
    {
#if ENABLE_AR_CONTENT
        [SerializeField]
        ARTrackedImageManager arTrackedImageManager;

        [SerializeField]
        AR2DVideoView fullScreenVideoView;              // 전체화면 재생용

        [SerializeField]
        GameObject arSceneCloseBtn;
#else
        [SerializeField]
        GameObject arTrackedImageManager;

        [SerializeField]
        GameObject fullScreenVideoView;

        [SerializeField]
        GameObject arSceneCloseBtn;
#endif

#if ENABLE_AR_CONTENT
        private void OnEnable()
        {
            Ext.DebugX.Log("<color=yellow>Start ARVideoControl...</color>");
            this.arTrackedImageManager.trackedImagesChanged += this.OnTrackedImagesChanged;
        }

        private void OnDisable()
        {
            Ext.DebugX.Log("ArVideControl OnDisable");
            this.arTrackedImageManager.trackedImagesChanged -= this.OnTrackedImagesChanged;
        }

        private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
        {
            foreach (ARTrackedImage trackedImage in eventArgs.updated)
            {
                if (trackedImage.trackingState == TrackingState.Tracking)
                {
                    if (!trackedImage.gameObject.activeSelf)
                        trackedImage.gameObject.SetActive(true);

                    ARTrackedControl arTrackedControl = trackedImage.GetComponent<ARTrackedControl>();
                    //Ext.DebugX.Log(string.Format("trackedImage code : {0}, status : {1}", arTrackedView.Code, trackedImage.trackingState));

                    if (!arTrackedControl.IsPlay)
                    {
                        short code = ARControl.GetTrackedImageCode(trackedImage.referenceImage.texture.GetHashCode());
                        if (code == -1)
                        {
                            Ext.DebugX.Log("Code not found!");
                            return;
                        }

                        ARControl.Data data = ARControl.GetARData(code);

                        //test code.
                        //data = ARControl.GetARData(3);
                        //end test code.

                        if (data == null)
                        {
                            Ext.DebugX.Log("ARData not found!");
                            return;
                        }

                        if (data.ContentType == 0)
                        {
                            ARTrackedControl.Parm p = new ARTrackedControl.Parm();
                            p.arData = data;
                            p.camera = GetComponent<ARSessionOrigin>().camera;
                            p.contentType = (ARTrackedControl.ContentType)data.ContentType;
                            p.code = data.Code;
                            p.arSceneCloseBtn = this.arSceneCloseBtn;

                            arTrackedControl.Init(p);
                        }
                        else if (data.ContentType == 1)
                        {
                            if (data.GroupCode.HasValue)
                            {
                                List<ARQuizControl.Data> quizDatas = ARQuizControl.GetARQuizData(data.GroupCode.Value);

                                ARTrackedControl.Parm p = new ARTrackedControl.Parm();
                                p.arData = data;
                                p.arQuizDatas = quizDatas;
                                p.camera = GetComponent<ARSessionOrigin>().camera;
                                p.contentType = (ARTrackedControl.ContentType)data.ContentType;
                                p.code = data.Code;
                                p.fullScreenVideoView = this.fullScreenVideoView;
                                p.arSceneCloseBtn = this.arSceneCloseBtn;

                                //test code.
                                //p.contentType = ARTrackedControl.ContentType.MINI_GAME;
                                //end test code.

                                arTrackedControl.Init(p);
                            }
                            else
                            {
                                Ext.DebugX.Log("No Group Code!!");
                            }
                        }
                    }
                }

                if (trackedImage.trackingState == TrackingState.Limited)
                {
                    ARTrackedControl arTrackedControl = trackedImage.GetComponent<ARTrackedControl>();

                    Ext.DebugX.Log("Deactivate Contents");
                    short code = ARControl.GetTrackedImageCode(trackedImage.referenceImage.texture.GetHashCode());
                    if (code == -1)
                    {
                        Ext.DebugX.Log("Code not found!");
                        return;
                    }

                    ARControl.Data data = ARControl.GetARData(code);
                    //test code.
                    //data = ARControl.GetARData(3);
                    Ext.DebugX.Log("Deactivate Contents Type : " + data.ContentType);
                    //end test code.

                    if (data == null)
                    {
                        Ext.DebugX.Log("ARData not found!");
                        return;
                    }

                    arTrackedControl.Stop(data);
                    if (trackedImage.gameObject.activeSelf)
                    {
                        trackedImage.gameObject.SetActive(false);
                    }
                }
            }
        }
#endif  // ENABLE_AR_CONTENT
    }
}
