using UnityEngine;
using UnityEngine.Video;

namespace com.dalcomsoft.project.app.view.sub
{
    public class AR3DVideoView : MonoBehaviour
    {
        [SerializeField]
        VideoPlayer videoPlayer;

#if ENABLE_AR_CONTENT
        public bool IsPlay { get; private set; }

        public delegate void OnVideoPlayComplete(AR3DVideoView videoView);
        private OnVideoPlayComplete videoPlayCompleteCallback;

        private void Start()
        {
            this.IsPlay = false;
        }

        private void OnDestroy()
        {
            this.IsPlay = false;

            this.videoPlayCompleteCallback = null;

            this.videoPlayer.loopPointReached -= this.OnLoopPointReached;
        }

        public void Play(string filePath, OnVideoPlayComplete callback)
        {
            this.IsPlay = true;

            this.videoPlayCompleteCallback = callback;

            this.videoPlayer.loopPointReached += this.OnLoopPointReached;
            this.videoPlayer.url = filePath;
            this.videoPlayer.isLooping = true;

            this.videoPlayer.Play();
        }

        public void Pause()
        {
            this.IsPlay = false;

            this.videoPlayer.Pause();
        }

        private void OnLoopPointReached(VideoPlayer source)
        {
            this.IsPlay = false;

            this.videoPlayCompleteCallback?.Invoke(this);
            this.videoPlayCompleteCallback = null;

            this.videoPlayer.loopPointReached -= this.OnLoopPointReached;
        }
#endif  // ENABLE_AR_CONTENT
    }
}
