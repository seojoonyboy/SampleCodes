using com.dalcomsoft.project.app.control;
using com.dalcomsoft.project.app.control.based;
using com.dalcomsoft.project.app.control.contents;
using com.dalcomsoft.project.app.view;
using com.dalcomsoft.project.app.view.scene;
using com.dalcomsoft.project.app.view.popup;

namespace com.dalcomsoft.project.app.scene
{
    public class ARScene : Scene
    {
#if ENABLE_AR_CONTENT
        public enum STATE
        {
            LoadingData,
            Wait,
            ARInitializing, //AR 초기화 단계
            UnSupported,    //AR 지원하지 않는 경우 (Exception)
            Done            //초기화 완료 단계
        }

        ARView sceneView;

        private static ARScene arSceneInstance = null;
        public static ARScene Instance
        {
            get
            {
                return arSceneInstance;
            }
            set
            {
                arSceneInstance = value;
            }
        }

        protected override void OnOpen()
        {
            ARScene.Instance = this;

            this.sceneView = (ARView)this.SceneViewComponent;
            this.sceneView.BindEvent(ARView.EVENT.Back, (evt, arg) => { this.OnBack(); });
            this.sceneView.BindEvent(ARView.EVENT.UnSupported, (evt, arg) => { this.SetStateUnSupported(); });

            this.sceneView.Hide();

            BgmPlayer.StopAll();

            LoadingIndicator.Complete();
            Fade.Close();

            SceneControl.IsEnableBackButton = true;

            this.SetStateLoadingData();
        }

        void SetStateLoadingData()
        {
            this.SetState(STATE.LoadingData);

            this.sceneView.arCamera.enabled = false;
            this.sceneView.closeBtn.SetActive(false);

            ARAssist.Open();
            ARAssist.Instance.OpenPopup(() =>
            {
                this.sceneView.arCamera.enabled = true;
                this.sceneView.closeBtn.SetActive(true);

                this.sceneView.arSession.Reset();

                this.SetStateARInitializing();
            });
        }

        void SetStateARInitializing()
        {
            Ext.DebugX.Log("ARScene SetStateARInitializing...");
            this.SetState(STATE.ARInitializing);

            this.sceneView.Show();
            this.SettingAR();
        }

        /// <summary>
        /// AR 관련 세팅
        /// 이미지 라이브러리 세팅
        /// </summary>
        private void SettingAR()
        {
            Ext.DebugX.Log("ARScene SettingAR...");
            ARTrackedImageInfoRuntimeControl.Open();

            ARView arView = this.sceneView;
            UnityEngine.XR.ARFoundation.ARTrackedImageManager trackedImageManager = arView.arTrackedImageManager;

            trackedImageManager.enabled = false;
            arView.arContentsControl.enabled = false;

            ARTrackedImageInfoRuntimeControl.ARImageInit(trackedImageManager, (isSucess, lib) =>
            {
                if (!isSucess)
                {
#if UNITY_EDITOR
                    Ext.DebugX.Log("Unity Editor detected.");
                    this.sceneView.dummyObj.SetActive(true);
                    ARTrackedControl trackedView = this.sceneView
                        .dummyObj
                        .transform.GetChild(0)
                        .GetComponent<ARTrackedControl>();

                    ARTrackedControl.Parm p = new ARTrackedControl.Parm();

                    p.camera = this.sceneView.arCamera;
                    short dummyCode = 0;
                    var arData = control.contents.ARControl.GetARData(dummyCode);
                    p.arData = arData;
                    p.contentType = ARTrackedControl.ContentType.VIDEO;
                    p.arQuizDatas = control.contents.ARQuizControl.GetARQuizData(100);
                    p.code = arData.Code;
                    p.fullScreenVideoView = this.sceneView.videoView;
                    p.arSceneCloseBtn = this.sceneView.closeBtn;

                    trackedView.Init(p);
#else
                    Ext.DebugX.Log("ARImageInit Failed! maybe unsupported device");
                    this.sceneView.OnUnsupportedEvent();
#endif

                }
                //Image Setting Success
                else
                {
                    trackedImageManager.referenceLibrary = lib;
                    trackedImageManager.requestedMaxNumberOfMovingImages = 1;
                    trackedImageManager.trackedImagePrefab = arView.trackedImagePrefab;

                    trackedImageManager.enabled = true;
                    arView.arContentsControl.enabled = true;

                    Ext.DebugX.Log("ARAscene ARTrackedImageInfoRuntimeControl.ARImageInit callback success");
                    this.SetStateDone();
                }
            });
        }

        public void StopARForcely()
        {
            if (ARScene.Instance == null) return;

            Ext.DebugX.Log("StopARForcely");

            ARView arView = this.sceneView;
            arView.arContentsControl.enabled = false;
            arView.arTrackedImageManager.enabled = false;
            arView.arSession.enabled = false;

            this.sceneView.arCamera.enabled = false;
        }

        public void ProcceedARForcely()
        {
            if (ARScene.Instance == null) return;

            Ext.DebugX.Log("StopARForcely");

            ARView arView = this.sceneView;
            arView.arContentsControl.enabled = true;
            arView.arTrackedImageManager.enabled = true;
            arView.arSession.enabled = true;

            this.sceneView.arCamera.enabled = true;
        }

        private void SetStateUnSupported()
        {
            this.SetState(STATE.UnSupported);

            this.sceneView.arCamera.enabled = false;
#if UNITY_EDITOR
#else
            PopupRoot.Instance.ClosePopupForce();
            ARAssist.Instance.OpenUnsupportedPopup(() =>
            {
                SceneControl.OpenScene(
                    SceneControl.SCENE_TYPE.Lobby
                );
            });
#endif

        }

        private void SetStateDone()
        {
            this.SetState(STATE.Done);
        }

        protected override void OnClose()
        {
            this.sceneView.RemoveEvent(ARView.EVENT.Back);
            this.sceneView.RemoveEvent(ARView.EVENT.UnSupported);

            ARTrackedImageInfoRuntimeControl.Close();

            this.sceneView = null;
        }

        protected override void OnBack()
        {
            SceneControl.OpenScene(SceneControl.SCENE_TYPE.Lobby);
        }
#endif  // ENABLE_AR_CONTENT
    }
}
