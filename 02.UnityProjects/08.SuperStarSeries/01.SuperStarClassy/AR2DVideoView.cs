using UnityEngine;

using RenderHeads.Media.AVProVideo;

namespace com.dalcomsoft.project.app.view.sub
{
    public class AR2DVideoView : MonoBehaviour
    {
#if ENABLE_AR_CONTENT
        public bool IsPlay { get; private set; }

        public delegate void OnVideoPlayComplete(AR2DVideoView videoView);
        private OnVideoPlayComplete videoPlayCompleteCallback;

        private RectTransform rectTransform;

        private MediaPlayer mediaPlayer;

        private void Awake()
        {
            this.IsPlay = false;

            if (this.rectTransform == null)
                this.rectTransform = this.GetComponent<RectTransform>();

            if (this.mediaPlayer == null)
                this.mediaPlayer = this.GetComponent<MediaPlayer>();
            this.mediaPlayer.Events.AddListener(this.OnVideoEvent);

            RectTransform rootCanvas = this.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>();
            this.rectTransform.sizeDelta = new Vector2(rootCanvas.rect.height, rootCanvas.rect.width);

            if (Screen.orientation == ScreenOrientation.LandscapeRight)
                this.transform.localEulerAngles = new Vector3(0f, 0f, -90f);
            else
                this.transform.localEulerAngles = new Vector3(0f, 0f, 90f);
        }

        private void OnDestroy()
        {
            this.Stop();
        }

        public void Play(string filePath, OnVideoPlayComplete callback)
        {
            this.videoPlayCompleteCallback = callback;

            this.mediaPlayer.OpenVideoFromFile(MediaPlayer.FileLocation.RelativeToPeristentDataFolder, filePath, false);
        }

        public void Stop()
        {
            this.IsPlay = false;

            this.videoPlayCompleteCallback = null;

            this.mediaPlayer.Events.RemoveListener(this.OnVideoEvent);
            this.mediaPlayer.Stop();
        }

        private void OnVideoEvent(MediaPlayer mp, MediaPlayerEvent.EventType et, ErrorCode errorCode)
        {
            switch (et)
            {
                case MediaPlayerEvent.EventType.ReadyToPlay:
                    {
                        this.IsPlay = true;

                        this.mediaPlayer.Rewind(true);
                        this.mediaPlayer.Play();
                    }
                    break;

                case MediaPlayerEvent.EventType.FinishedPlaying:
                    {
                        // 영상 재생 완료 후 처음으로 돌아가기
                        this.videoPlayCompleteCallback?.Invoke(this);
                        this.Stop();
                    }
                    break;
            }

            Ext.DebugX.Log("Event: " + et.ToString());
        }
#endif  // ENABLE_AR_CONTENT
    }
}
