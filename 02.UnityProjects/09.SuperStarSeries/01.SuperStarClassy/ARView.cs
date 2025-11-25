using UnityEngine;
#if ENABLE_AR_CONTENT
using UnityEngine.XR.ARFoundation;
#endif

using com.dalcomsoft.project.app.control.contents;
using com.dalcomsoft.project.app.view.sub;

namespace com.dalcomsoft.project.app.view.scene
{
    public class ARView : SceneView
    {
        public Camera arCamera;
        public GameObject arSessionObj;
        public GameObject closeBtn;

        public GameObject trackedImagePrefab;
        public ARContentsControl arContentsControl;

        public GameObject dummyObj; //AR Camera가 없는 Editor 상에서 사용되는 테스트용 더미 Gameobject

        public AR2DVideoView videoView;

#if ENABLE_AR_CONTENT
        public ARTrackedImageManager arTrackedImageManager;
        public ARSession arSession;
#else
        public GameObject arTrackedImageManager;
        public GameObject arSession;
#endif

#if ENABLE_AR_CONTENT
        public enum EVENT
        {
            Back,
            UnSupported
        }

        public override void Show()
        {
            base.Show();
        }

        public override void Hide()
        {
            base.Hide();
        }

        public override void Refresh() { }

        public void OnBack(GameObject go)
        {
            this.DoEvent(EVENT.Back, go);
        }

        public void OnUnsupportedEvent()
        {
            this.DoEvent(EVENT.UnSupported);
        }
#endif  // ENABLE_AR_CONTENT
    }
}
