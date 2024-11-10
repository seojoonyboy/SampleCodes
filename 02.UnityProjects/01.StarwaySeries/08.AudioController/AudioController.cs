using System;
using Snowballs.Sheets;
using Snowballs.Sheets.Data;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AYellowpaper.SerializedCollections;
using Cysharp.Threading.Tasks;
using Snowballs.Client.Model;
using DG.Tweening;
using Newtonsoft.Json.Linq;
using Oboe.Clip;
using UnityEngine;
using UnityEngine.Networking;
using Random = UnityEngine.Random;

namespace Snowballs.Client.Controller
{
    public class AudioController : MonoBehaviour
    {
        [SerializeField] private AudioSource audioBGM;
        [SerializeField] private AudioSource previewAudioBGM;
        
        [SerializeField] private AudioClip[] audioBGMClips;

        [SerializeField] private AudioSource audioVoice;
        [SerializeField] private AudioClip[] audioVoiceClips;

        [SerializeField] private AudioClip[] audioCardVoiceClips;

        List<VoiceResource> voiceList;
        const string TitleDefaultBGM = "Sounds/download_ikon_master";

        [SerializedDictionary("Sheet Name", "Sheet List")]
        public SerializedDictionary<string, List<AudioSheetData>> audioDict;

        private Dictionary<Channel, List<AudioSource>> audioSourcePool;
        private AndroidAudioController androidAudioController;

        private Dictionary<string, List<TitleVoiceData>> titleVoiceMap;
        
        private bool _isPreviewOpened = false;
        public bool IsPreviewOpened
        {
            get
            {
                return this._isPreviewOpened;
            }
            set
            {
                this._isPreviewOpened = value;
            }
        }
        
        public enum Channel
        {
            Intro = 0,
            Lobby = 1,
            BGM = 2,
            Card = 3,
            
            Common = 4,
            
            Ingame = 5,
            
            NormalBlock_Explode = 10,
            Puzzle_Rocket = 11,
            Puzzle_Meteor = 12,
            Puzzle_Bomb = 13,
            Puzzle_BlackHole = 14,
            Puzzle_ShootingStar = 15,
            Puzzle_Mission = 16
        }

        public void Awake()
        {
            StartCoroutine(Initialize());
        }

        public enum LogoVoiceType
        {
            SNOWBALLS = 0,
            ENTERTAINMENT = 1,
            LOGIN = 2
        }
        
        public void PlayTitleLogoVoice(LogoVoiceType voiceType, float volume = 1.0f)
        {
            if(!SoundEffectAvailable) return;
            
            int equipProfileArtistCode = GameStorage.EquipProfileArtistCode;
            
            TitleVoiceData titleVoiceData = null;
            switch (voiceType)
            {
                case LogoVoiceType.SNOWBALLS:
                    {
                        var targetList = this.titleVoiceMap["snowballs"];
                        if (equipProfileArtistCode == -1)
                        {
                            int rndIndex = Random.Range(0, targetList.Count - 1);
                            titleVoiceData = targetList[rndIndex];
                        }
                        else
                        {
                            titleVoiceData = targetList
                                .Find(x => x.artistCode == equipProfileArtistCode);
                        }
                    }
                    break;
                case LogoVoiceType.ENTERTAINMENT:
                    {
                        var targetList = this.titleVoiceMap["entertainment"];
                        if (equipProfileArtistCode == -1)
                        {
                            int rndIndex = Random.Range(0, targetList.Count - 1);
                            titleVoiceData = targetList[rndIndex];
                        }
                        else
                        {
                            titleVoiceData = targetList
                                .Find(x => x.artistCode == equipProfileArtistCode);
                        }
                    }
                    break;

                case LogoVoiceType.LOGIN:
                    {
                        var targetList = this.titleVoiceMap["login"];
                        int rndIndex = Random.Range(0, 2);
                        titleVoiceData = targetList[rndIndex];
                    }
                    break;
            }

            if (titleVoiceData != null)
            {
                AudioClip clip = Resources.Load<AudioClip>(titleVoiceData.path);
                if (clip != null)
                {
                    var targetChannel = this.GetTargetChannel("Intro", 0);
                    AudioSource audioSource = this.GetAvailableAudioSource(targetChannel);
                    if (audioSource == null) return;
                            
                    //Note. Android Oboe Asset 연동 예정
                    audioSource.clip = clip;
                    audioSource.volume = volume;
                    audioSource.Play();
                    
                    CoroutineTaskManager.AddTask(_InvokeAfter(
                        audioSource.clip.length, 
                        audioSource
                    ));
                
                    IEnumerator _InvokeAfter(float sec, AudioSource audioSource)
                    {
                        yield return new WaitForSeconds(sec);
                        audioSource.Stop();
                        audioSource.volume = 1.0f;
                        audioSource.clip = null;
                    }
                }
            }
        }

        private void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            this.androidAudioController.DisposeOboeAudio();
#endif
        }

        private void InitAudioSourcePool(Channel channel, int poolSize)
        {
            if (this.audioSourcePool == null) this.audioSourcePool = new Dictionary<Channel, List<AudioSource>>();

            if (!this.audioSourcePool.ContainsKey(channel))
            {
                this.audioSourcePool.Add(channel, new List<AudioSource>());
            }
            else
            {
                this.audioSourcePool[channel].Clear();
                this.audioSourcePool.Add(channel, new List<AudioSource>());
            }

            for (int i = 0; i < poolSize; i++)
            {
                string objName = channel + "_" + i;
                GameObject obj = new GameObject(objName);
                obj.transform.SetParent(transform.Find("SoundEffect"));
                var audioSource = obj.AddComponent<AudioSource>();
                this.audioSourcePool[channel].Add(audioSource);
            }
        }

        private void ClearAudioSourcePool()
        {
            if(this.audioSourcePool != null) this.audioSourcePool.Clear();
        }

        public static bool SoundBGMAvailable
        {
            get { return (PlayerPrefs.GetInt("SoundBGM", 1) == 1); }
            set { PlayerPrefs.SetInt("SoundBGM", value ? 1 : 0); }
        }

        public static bool SoundEffectAvailable
        {
            get { return (PlayerPrefs.GetInt("SoundEffect", 1) == 1); }
            set { PlayerPrefs.SetInt("SoundEffect", value ? 1 : 0); }
        }

        public enum BGM
        {
            Title,
            Game,           //퍼즐 인게임, 리듬 로비 미리듣기, 리듬 인게임...
        }

        public enum VoiceBundle
        {
            FirstLobby = 1,                         //로비 첫 진입 시
            EntryPuzzleMode = 2,                    //퍼즐 모드 화면 진입
            EntryRhythmMode = 3,                    //리듬 모드 화면 진입
            ProfileEquip = 4,                       // 프로필 장착 버튼 터치 시
            GetMagazine = 5,                        // 로비에서 매거진 획득 연출 시 출력.
            EntryMagazine = 6,                      // 매거진 씬 진입 시 
            SetLobbyBackground = 7,                 //매거진 배경 장착
            EntryRecommendShop = 8,                 //추천 상점 입장
            EntryDiaShop = 9,                          //9. 다이아 상점 입장 시
            EntryMileage = 10,                      //마일리지 상점 입장
            StageFail = 11,                         //레벨 실패 팝업 등장 시
            Exit = 12,                              //12. 앱 종료시
            
            EntrySelectCard = 14,                   //뽑기 씬 진입 시
            EntryMyCard = 15,                       //내 카드 진입 시
            EntryCardDeco = 16,                     //카드 꾸미기 입장 시
            CardRankUp = 17,                        //카드 랭크업
            StageStart = 18,                        // 게임 시작 버튼 터치 시 [일반, 스코어 모드]
            SkillGaugeMax = 19,                     //스킬 충전 완료
            RhythmStart = 20,                       //리듬 모드 시작
            EntryRhythmScoreMode = 21,              //리듬 스코어 모드 영역 터치 시 [공사중]
            PuzzleStageClear = 22,                        //스테이지 클리어
            UseCardSkill = 23,                          //카드 스킬 발동
            RhythmFeverMode = 24,                   //리듬 피버 모드
            RhythmInGame = 99,                      //리듬 인게임 BGM
        }
        
        

        private List<AudioClipInfo> ingameAudioClipInfos = new List<AudioClipInfo>();
        
        public class AudioClipInfo
        {
            public AudioSource audioSource;
            public AudioClip clip;
            
            public float remainTime;

            public AudioClipInfo(AudioSource audioSource, AudioClip clip)
            {
                this.clip = clip;
                this.audioSource = audioSource;
                
                remainTime = 0.0f;
            }
        }

        private void Update()
        {
            foreach (AudioClipInfo clipInfo in this.ingameAudioClipInfos)
            {
                clipInfo.remainTime = clipInfo.clip.length - clipInfo.audioSource.time;
            }
        }

        public void SceneChangePlay(SceneController.Scene scene)
        {
            audioBGM.volume = 1.0f;
            
            if (SoundBGMAvailable)
            {
                switch (scene)
                {
                    case SceneController.Scene.Title:
                        if (audioBGM.clip != audioBGMClips[(int)BGM.Title] || !audioBGM.isPlaying)
                        {
                            audioBGM.clip = audioBGMClips[(int)BGM.Title];
                            audioBGM.Play();
                        }
                        break;
                    case SceneController.Scene.Orientation:
                    case SceneController.Scene.RhythmLobby:                                                
                    case SceneController.Scene.RhythmInGameLoading:
                    case SceneController.Scene.RhythmInGame:
                        if (audioBGM.clip != audioBGMClips[(int)BGM.Game] || !audioBGM.isPlaying)
                        {
                            audioBGM.clip = audioBGMClips[(int)BGM.Game];
                            audioBGM.Stop();
                        }
                        audioBGM.volume = 0f;
                        if(IsPreviewOpened)
                            ClosePreviewBGM();
                        break;
                    case SceneController.Scene.MyCard:
                    case SceneController.Scene.ArtBook:
                    case SceneController.Scene.SelectLobby:
                        audioBGM.volume = 0.8f;
                        
                        ClosePreviewBGM();
                        
                        if (audioBGM.clip != audioBGMClips[(int)BGM.Game] || !audioBGM.isPlaying)
                        {
                            audioBGM.clip = audioBGMClips[(int)BGM.Game];
                            audioBGM.Play();
                        }
                        break;
                    case SceneController.Scene.PuzzleLobby:                
                    case SceneController.Scene.SelectCard:
                    case SceneController.Scene.Shop:
                    case SceneController.Scene.RhythmResult:
                        if (audioBGM.clip != audioBGMClips[(int)BGM.Game] || !audioBGM.isPlaying)
                        {
                            audioBGM.clip = audioBGMClips[(int)BGM.Game];
                            audioBGM.Play();
                        }
                        audioBGM.volume = 0.5f;
                        if (IsPreviewOpened)
                            ClosePreviewBGM();
                        break;

                    case SceneController.Scene.InGame:
                        StartCoroutine(LoadIngameBGM());
                        if (IsPreviewOpened)
                            ClosePreviewBGM();
                        audioBGM.volume = 0.7f;
                        break;
                }
            }
        }

        public IEnumerator Initialize()
        {
            yield return InitTitleBGM();
            this.InitializeAudioSourcePools();
            yield return InitTitleVoiceMap();
        }

        private void InitializeAudioSourcePools()
        {
#if !UNITY_EDITOR && UNITY_ANDROID
            this.InitAudioSourcePool(Channel.Intro, 2);
#else
            //로비(+기타) 관련 오디오 풀 초기화
            {
                this.InitAudioSourcePool(Channel.Intro, 2);
                this.InitAudioSourcePool(Channel.Card, 5);
                this.InitAudioSourcePool(Channel.Lobby, 5);
                this.InitAudioSourcePool(Channel.BGM, 1);
                
                this.InitAudioSourcePool(Channel.Common, 10);
                
            }

            //퍼즐, 리듬 인게임 관련 오디오 풀 초기화
            {
                this.InitAudioSourcePool(Channel.NormalBlock_Explode, 5);
                this.InitAudioSourcePool(Channel.Puzzle_Rocket, 10);
                this.InitAudioSourcePool(Channel.Puzzle_Meteor, 10);
                this.InitAudioSourcePool(Channel.Puzzle_Bomb, 10);
                this.InitAudioSourcePool(Channel.Puzzle_BlackHole, 10);
                this.InitAudioSourcePool(Channel.Puzzle_ShootingStar, 10);
                this.InitAudioSourcePool(Channel.Puzzle_Mission, 10);
                this.InitAudioSourcePool(Channel.Ingame, 10);
            }
#endif
        }

        public IEnumerator InitTitleBGM()
        {
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(CommonProcessController.GetTitleBGMUri, AudioType.OGGVORBIS))
            {
                yield return www.SendWebRequest();

                // SBDebug.Log("InitTitleBGM res : " + www.result);
                if (www.result == UnityWebRequest.Result.Success)
                {
                    AudioClip myClip = DownloadHandlerAudioClip.GetContent(www);
                    
                    audioBGMClips[(int)BGM.Title] = myClip;
                    GameStorage.TitleBGMFilePath = CommonProcessController.GetTitleBGMUri;
                }
                //Asset Title 배경음이 존재하지 않는 경우 (최초) 
                else
                {
                    var defaultBGMClip = Resources.Load<AudioClip>(TitleDefaultBGM);
                    audioBGMClips[(int)BGM.Title] = defaultBGMClip;
                }
            }
        }
        
        private IEnumerator InitTitleVoiceMap()
        {
            bool isFinished = false;
            this.ReadTitleVoiceJsonFile((isSuccess) =>
            {
                isFinished = true;

                if (!isSuccess)
                {
                    SBDebug.LogWarning("TitleVoice 초기화 실패");
                }
            });

            yield return new WaitUntil(() => isFinished);
        }

        public IEnumerator InitLobbyBGM()
        {
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(CommonProcessController.GetLobbyBGMUri, AudioType.OGGVORBIS))
            {
                yield return www.SendWebRequest();

                // SBDebug.Log("InitLobbyBGM res : " + www.result);
                if (www.result == UnityWebRequest.Result.Success)
                {
                    AudioClip myClip = DownloadHandlerAudioClip.GetContent(www);
                    audioBGMClips[(int)BGM.Game] = myClip;
                }
            }
        }

        private IEnumerator LoadIngameBGM()
        {    
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(CommonProcessController.GetCurrentStageBGMUri, AudioType.OGGVORBIS))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError)
                {
                    var defaultBGMClip = Resources.Load<AudioClip>(TitleDefaultBGM);
                    audioBGM.clip = defaultBGMClip;
                    audioBGM.Play();
                }
                else
                {
                    AudioClip myClip = DownloadHandlerAudioClip.GetContent(www);
                    audioBGM.clip = myClip;
                    audioBGM.Play();
                }
            }
        }

        private List<PreviewAudioData> previewAudioClips;

        /// <summary>
        /// 미리듣기 음원들을 불러온다 [초기화]
        /// </summary>
        public void OpenPreviewBGM(Action onFinished = null)
        {
            this.previewAudioClips = new List<PreviewAudioData>();
            StartCoroutine(_Task(onFinished));

            IEnumerator _Task(Action onFinished = null)
            {
                foreach (RhythmPlayPreviewMusic previewMusic in SBDataSheet.Instance.RhythmPlayPreviewMusic.Values)
                {
                    int musicCode = previewMusic.Code;
                
                    var path = LocaleController.GetRhythmPlayPreviewMusicLocale(musicCode);
                    if(string.IsNullOrEmpty(path)) continue;
                    path = (string.Format("file://{0}", path));

                    yield return _InitPreView(path, musicCode);
                }
                
                onFinished?.Invoke();
                this.IsPreviewOpened = true;
            }
            
            IEnumerator _InitPreView(string path, int musicCode)
            {
                using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(path, AudioType.OGGVORBIS))
                {
                    yield return www.SendWebRequest();                
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        AudioClip myClip = DownloadHandlerAudioClip.GetContent(www);

                        PreviewAudioData previewAudioData = new PreviewAudioData(musicCode, myClip);
                        previewAudioClips.Add(previewAudioData);
                    }
                    else
                    {
                        SBDebug.Log(string.Format("<color=red>Preview 음원 설정 실패 {0} </color>", path));
                    }
                }
            }
        }

        /// <summary>
        /// 미리듣기 음원들을 해제한다.
        /// </summary>
        public void ClosePreviewBGM()
        {
            if (this.previewAudioBGM.clip != null)
            {
                this.previewAudioBGM.Stop();
                this.previewAudioBGM.clip = null;   
            }

            if(this.previewAudioClips != null) 
                this.previewAudioClips.Clear();
        }

        /// <summary>
        /// 미리듣기 음원 파일을 가져온다.
        /// </summary>
        /// <param name="musicCode">해당 Music Code 값</param>
        public PreviewAudioData GetRhythmPreviewAudioClip(int musicCode)
        {
            if(this.previewAudioClips == null || this.previewAudioClips.Count == 0) return null;
            return this.previewAudioClips.Find(x => x.code == musicCode);
        }
        
        
        /// <summary>
        /// 미리듣기를 재생한다.
        /// </summary>
        /// <param name="musicCode">해당 Music Code 값</param>
        /// <param name="volume">볼륨</param>
        /// <param name="milSec">재생 지연 시간</param>
        public void PlayRhythmPreviewBGM(
            int musicCode,
            float volume = 1, 
            float intervalMillSec = 0)
        {
            if(!this.IsPreviewOpened) return;
            
            if(this.previewAudioClips == null || this.previewAudioClips.Count == 0) return;

            var targetAudioClip = this.previewAudioClips.Find(x => x.code == musicCode);
            if(targetAudioClip == null) return;

            float sec = (float)intervalMillSec / 1000.0f;
            
            previewAudioBGM.clip = targetAudioClip.audioClip;
            previewAudioBGM.volume = volume;
            previewAudioBGM.PlayDelayed(sec);
            previewAudioBGM.loop = true;
        }

        public void ChangeRhythmPreviewVolume(float volume)
        {
            if(!this.IsPreviewOpened) return;
            if(this.previewAudioClips == null || this.previewAudioClips.Count == 0) return;

            if (previewAudioBGM.clip != null)
            {
                previewAudioBGM.volume = volume;
                audioBGM.volume = 0;
            }
        }

        /// <summary>
        /// 현재 재생중인 미리듣기 음원 볼륨을 줄인다
        /// </summary>
        
        public void PlayRhythmPreviewVolume(float volum = 1)
        {
            previewAudioBGM.volume = volum;
        }


        /// <summary>
        /// 현재 재생중인 미리듣기 음원 정지
        /// </summary>
        public void StopPreviewMusic()
        {
            if(!this.IsPreviewOpened) return;
            
            if (this.previewAudioBGM.isPlaying)
            {
                this.previewAudioBGM.Stop();
                this.previewAudioBGM.clip = null;
            }
        }

        public void FadeOutPreviewMusic()
        {
            StartCoroutine(PreviewAudioFadeOut(this.previewAudioBGM, 1.7f));
        }

        public IEnumerator PreviewAudioFadeOut(AudioSource audioSource, float FadeTime)
        {
            float startVolume = audioSource.volume;

            while (audioSource.volume > 0.01f)
            {
                audioSource.volume -= startVolume * Time.deltaTime / FadeTime;

                yield return null;
            }
            audioSource.volume = 0f;
        }

        // 배경 음악 STOP.
        public void StopBGM()
        {
            SoundBGMAvailable = false;
            this.audioBGM.Stop();
        }

        // 배경 음악 PLAY.
        public void PlayBGM()
        {
            SoundBGMAvailable = true;

            if(SceneController.currentSceneSceneId == SceneController.Scene.InGame)
            {
                this.SceneChangePlay(SceneController.Scene.InGame);
            }
            else 
            {
                this.SceneChangePlay(SceneController.Scene.SelectLobby);
            }
        }

        // 효과음 STOP
        public void StopSoundEffect()
        {
            SoundEffectAvailable = false;

            foreach (KeyValuePair<Channel, List<AudioSource>> pair in this.audioSourcePool)
            {
                foreach (AudioSource audioSource in pair.Value)
                {
                    audioSource.Stop();
                }
            }
        }

        // 효과음 PLAY.
        public void SetSoundEffectAvailable()
        {
            SoundEffectAvailable = true;
        }

        private List<AudioClip> muteAudioClip = new List<AudioClip>();

        public void MuteSoundEffect(string sheetName, int id, float duration = 0.3f)
        {
            var targetClip = this.audioDict[sheetName].Find(x => x.ID == id).audioClip;
            if (targetClip == null) return;

            var SE_Type = this.GetTargetChannel(sheetName, id);
            if (!this.audioSourcePool.ContainsKey(SE_Type)) return;

            var targetAudioSources = this.audioSourcePool[SE_Type]
                .FindAll(x => x.clip != null && x.isPlaying);
            foreach (AudioSource audioSource in targetAudioSources)
            {
                if (audioSource.clip == targetClip)
                {
                    audioSource.volume = 1.0f;
                    audioSource.Stop();
                    audioSource.clip = null;
                }
            }
        }

        public void MuteSoundEffectNoneNull(string sheetName, int id, float duration = 0.3f)
        {
            var targetClip = this.audioDict[sheetName].Find(x => x.ID == id).audioClip;
            if (targetClip == null) return;

            var SE_Type = this.GetTargetChannel(sheetName, id);
            if (!this.audioSourcePool.ContainsKey(SE_Type)) return;

            var targetAudioSources = this.audioSourcePool[SE_Type]
                .FindAll(x => x.clip != null && x.isPlaying);
            foreach (AudioSource audioSource in targetAudioSources)
            {
                if (audioSource.clip == targetClip)
                {
                    audioSource.Stop();
                }
            }
        }

        public void MuteSoundEffectOboeAudio(string sheetName, int id, float duration = 0.3f)
        {
            var targetClip = this.audioDict[sheetName].Find(x => x.ID == id).audioClip;
            if (targetClip == null) return;

            var SE_Type = this.GetTargetChannel(sheetName, id);


            OboeAudioClipPlayer oboeAudioClipPlayer = androidAudioController.GetAvailableAudioPlayer(SE_Type, id);

            if (oboeAudioClipPlayer != null)
                oboeAudioClipPlayer.Stop();
        }

        public void RhythmTimingOboeAudio(string sheetName, int id, Action onFinished, float volume = 1.0f)
        {
            if (SoundEffectAvailable)
            {
                try
                {
                    AudioSheetData targetAudioSheetData = this.audioDict[sheetName].Find(x => x.ID == id);
                    AudioClip targetClip = targetAudioSheetData.audioClip;
                    if (muteAudioClip.Contains(targetClip)) { return; }

                    var SE_Type = this.GetTargetChannel(sheetName, id);
#if !UNITY_EDITOR && UNITY_ANDROID
                    OboeAudioClipPlayer oboeAudioClipPlayer = androidAudioController
                        .GetTimingAudioPlayer(SE_Type, id);
                    oboeAudioClipPlayer.Volume = volume;
                    oboeAudioClipPlayer.Looping = true;
                    //SBDebug.Log("PlaySoundEffect clip name : " + targetClip.name);
                    //SBDebug.Log("PlaySoundEffect clip length : " + targetClip.length);

                    if (oboeAudioClipPlayer == null)
                    {
                        SBDebug.Log("OboeAudioClipPlayer is null");
                    }

                    oboeAudioClipPlayer.OboeAudioClip = targetAudioSheetData.oboeAudioClip;

                   /*SBDebug.Log(
                        "PlaySoundEffect OboeAudioClip name : " + 
                        oboeAudioClipPlayer.OboeAudioClip.AudioClip.name
                    );
                    SBDebug.Log(
                        "PlaySoundEffect OboeAudioClip length : " + 
                        oboeAudioClipPlayer.OboeAudioClip.Length
                    );*/
                    
                    oboeAudioClipPlayer.Play();
#endif                   
                    //Sound 중첩에 따른 증폭음 밸런싱...
#if !UNITY_EDITOR && UNITY_ANDROID
                    androidAudioController.NormalizeSounds(SE_Type, id);                    
#endif

#if !UNITY_EDITOR && UNITY_ANDROID
                    CoroutineTaskManager.AddTask(_InvokeAndroidAfter(
                        onFinished,
                        oboeAudioClipPlayer.OboeAudioClip.Length + 1.0f,
                        oboeAudioClipPlayer,
                        sheetName,
                        id)
                    );
#endif

                }
                catch (Exception ex)
                {
                    SBDebug.Log(
                        string.Format("음원을 찾을 수 없습니다! sheet명 : {0}, id값 : {1}", sheetName, id)
                    );

                    SBDebug.Log("PlaySoundEffect Exception : " + ex.Message);

                    onFinished?.Invoke();
                }

                IEnumerator _InvokeAndroidAfter(
                    Action cb,
                    float sec,
                    OboeAudioClipPlayer audioClipPlayer,
                    string sheetName,
                    int id)
                {
                    yield return new WaitForSeconds(sec);
                    audioClipPlayer.Stop();                    
                    cb?.Invoke();
                }

                IEnumerator _InvokeAfter(Action cb, float sec, AudioSource audioSource, string sheetName, int id)
                {
                    yield return new WaitForSeconds(sec);
                    audioSource.Stop();
                    audioSource.volume = 1.0f;
                    audioSource.clip = null;

                    if (this.IsSpecialBlockSE(sheetName, id))
                    {
                        var targetItem = this.ingameAudioClipInfos.Find(
                            x => x.clip == audioSource.clip
                        );

                        if (targetItem != null) this.ingameAudioClipInfos.Remove(targetItem);
                    }
                    cb?.Invoke();
                }
            }
            else
            {
                onFinished?.Invoke();
            }                                                
        }

        public void ResetSoundEffect()
        {
            // foreach (KeyValuePair<Channel, List<AudioSource>> pair in this.audioSourcePool)
            // {
            //     foreach (AudioSource audioSource in pair.Value)
            //     {
            //         audioSource.Stop();
            //         audioSource.volume = 1.0f;
            //         audioSource.clip = null;
            //     }
            // }
        }


        private Coroutine fadeInOutCor = null;
        public void FadeInOutBGM(float duration, float startVolume, float minVolume)
        {
            if(fadeInOutCor != null) StopCoroutine(fadeInOutCor);
            fadeInOutCor = StartCoroutine(_Task(duration));
            
            IEnumerator _Task(float duration)
            {
                if (audioBGM != null) { audioBGM.volume = minVolume; }
                yield return new WaitForSeconds(duration);
                if (audioBGM != null) { audioBGM.volume = startVolume; }
                
                // float startVal = startVolume;
                // float currVal = startVal;
                // while (currVal > minVolume)
                // {
                //     currVal -= startVal * 0.1f * Time.deltaTime / (duration * 0.5f);
                //     if (audioBGM != null) { audioBGM.volume = currVal; }
                //     yield return null;
                // }
                //
                // while (currVal < startVal)
                // {
                //     currVal += startVal * 0.1f * Time.deltaTime / (duration * 0.5f);
                //     if (audioBGM != null) { audioBGM.volume = currVal; }
                //     yield return null;
                // }
                //
                fadeInOutCor = null;
            }
        }

        public void PlaySoundEffectWithDelay(
            string sheetName, 
            int id, 
            Action onFinished, 
            float volume = 1.0f,
            bool isZeroIndex = false,
            float delay = 1.0f)
        {
            if(!SoundEffectAvailable) return;

            _PlayDelay();
            
            async void _PlayDelay()
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay));
                PlaySoundEffect(sheetName, id, onFinished, volume, isZeroIndex);
            }
        }

        public void PlaySoundEffect(string sheetName, int id, Action onFinished, float volume = 1.0f, bool isZeroIndex= false)
        {
            if (SoundEffectAvailable)
            {
                try
                {
                    AudioSheetData targetAudioSheetData = this.audioDict[sheetName].Find(x => x.ID == id);
                    AudioClip targetClip = targetAudioSheetData.audioClip;
                    if (muteAudioClip.Contains(targetClip)) { return; }

                    var SE_Type = this.GetTargetChannel(sheetName, id);
#if !UNITY_EDITOR && UNITY_ANDROID
                    OboeAudioClipPlayer oboeAudioClipPlayer = androidAudioController
                        .GetAvailableAudioPlayer(SE_Type, id);
                    oboeAudioClipPlayer.Volume = volume;                    
                    //SBDebug.Log("PlaySoundEffect clip name : " + targetClip.name);
                    //SBDebug.Log("PlaySoundEffect clip length : " + targetClip.length);

                    if (oboeAudioClipPlayer == null)
                    {
                        SBDebug.Log("OboeAudioClipPlayer is null");
                    }

                    oboeAudioClipPlayer.OboeAudioClip = targetAudioSheetData.oboeAudioClip;

                   /*SBDebug.Log(
                        "PlaySoundEffect OboeAudioClip name : " + 
                        oboeAudioClipPlayer.OboeAudioClip.AudioClip.name
                    );
                    SBDebug.Log(
                        "PlaySoundEffect OboeAudioClip length : " + 
                        oboeAudioClipPlayer.OboeAudioClip.Length
                    );*/
                    
                    oboeAudioClipPlayer.Play();
#else
                    AudioSource audioSource;
                    if (isZeroIndex)
                    {
                        audioSource = this.GetAudioSourceZeroIndex(SE_Type, 1);
                    }
                    else
                    {
                        audioSource = this.GetAvailableAudioSource(SE_Type);
                        if (audioSource == null) return;
                    }        
                    
                    //Note. Android Oboe Asset 연동 예정
                    audioSource.clip = targetClip;
                    audioSource.volume = volume;
                    audioSource.Play();
#endif
                    if (isZeroIndex)
                        return;

                    //Sound 중첩에 따른 증폭음 밸런싱...
#if !UNITY_EDITOR && UNITY_ANDROID
                    androidAudioController.NormalizeSounds(SE_Type, id);
#else
                    this.NormalizeSounds(SE_Type);
                    
                    if (IsSpecialBlockSE(sheetName, id))
                    {
                        this.ingameAudioClipInfos.Add(new AudioClipInfo(audioSource, audioSource.clip));
                    }
                    else if (IsNormalBlockExplodeSE(sheetName, id))
                    {
                        this.ingameAudioClipInfos.Add(new AudioClipInfo(audioSource, audioSource.clip));
                    }
#endif

#if !UNITY_EDITOR && UNITY_ANDROID
                    CoroutineTaskManager.AddTask(_InvokeAndroidAfter(
                        onFinished,
                        oboeAudioClipPlayer.OboeAudioClip.Length + 1.0f,
                        oboeAudioClipPlayer,
                        sheetName,
                        id)
                    );
#else
                    CoroutineTaskManager.AddTask(_InvokeAfter(
                        onFinished, 
                        audioSource.clip.length + 1.0f, 
                        audioSource, 
                        sheetName, 
                        id)
                    );
#endif
                    
                }
                catch (Exception ex)
                {
                    SBDebug.Log(
                        string.Format("음원을 찾을 수 없습니다! sheet명 : {0}, id값 : {1}", sheetName, id)
                    );
                    
                    SBDebug.Log("PlaySoundEffect Exception : " + ex.Message);
                    
                    onFinished?.Invoke();
                }

                IEnumerator _InvokeAndroidAfter(
                    Action cb, 
                    float sec, 
                    OboeAudioClipPlayer audioClipPlayer,
                    string sheetName, 
                    int id)
                {
                    yield return new WaitForSeconds(sec);
                    audioClipPlayer.Stop();
                    
                    // if (this.IsSpecialBlockSE(sheetName, id))
                    // {
                    //     var targetItem = this.ingameAudioClipInfos.Find(
                    //         x => x.clip == audioSource.clip
                    //     );
                    //     
                    //     if(targetItem != null) this.ingameAudioClipInfos.Remove(targetItem);
                    // }
                    cb?.Invoke();
                }
                
                IEnumerator _InvokeAfter(Action cb, float sec, AudioSource audioSource, string sheetName, int id)
                {
                    yield return new WaitForSeconds(sec);
                    audioSource.Stop();
                    audioSource.volume = 1.0f;
                    audioSource.clip = null;

                    if (this.IsSpecialBlockSE(sheetName, id))
                    {
                        var targetItem = this.ingameAudioClipInfos.Find(
                            x => x.clip == audioSource.clip
                        );
                        
                        if(targetItem != null) this.ingameAudioClipInfos.Remove(targetItem);
                    }
                    cb?.Invoke();
                }
            }
            else
            {
                onFinished?.Invoke();
            }
        }


        public void PlayOneShotSoundEffect(string sheetName, int id, Action onFinished, int index, float volume = 1.0f)
        {
            if (SoundEffectAvailable)
            {
                try
                {
                    AudioSheetData targetAudioSheetData = this.audioDict[sheetName].Find(x => x.ID == id);
                    AudioClip targetClip = targetAudioSheetData.audioClip;
                    if (muteAudioClip.Contains(targetClip)) { return; }

                    var SE_Type = this.GetTargetChannel(sheetName, id);
#if !UNITY_EDITOR && UNITY_ANDROID
                    OboeAudioClipPlayer oboeAudioClipPlayer = androidAudioController
                        .GetAvailableAudioPlayer(SE_Type, id);
                    
                    //SBDebug.Log("PlaySoundEffect clip name : " + targetClip.name);
                    //SBDebug.Log("PlaySoundEffect clip length : " + targetClip.length);

                    if (oboeAudioClipPlayer == null)
                    {
                        SBDebug.Log("OboeAudioClipPlayer is null");
                    }

                    oboeAudioClipPlayer.OboeAudioClip = targetAudioSheetData.oboeAudioClip;
                    
                    androidAudioController.AudioSource.PlayOneShot(oboeAudioClipPlayer.OboeAudioClip);
#else
                    AudioSource audioSource = this.GetAudioSourceZeroIndex(SE_Type, index);
                //     if (audioSource == null) return;

                    //Note. Android Oboe Asset 연동 예정
                    audioSource.clip = targetClip;
                    if (audioSource.volume != volume)
                    {
                        audioSource.volume = volume;
                    }
                    audioSource.PlayOneShot(targetClip);
#endif
                }
                catch (Exception ex)
                {
                    SBDebug.Log(
                        string.Format("음원을 찾을 수 없습니다! sheet명 : {0}, id값 : {1}", sheetName, id)
                    );

                    SBDebug.Log("PlaySoundEffect Exception : " + ex.Message);

                    onFinished?.Invoke();
                }
            }
            else
            {
                onFinished?.Invoke();
            }
        }

        private void NormalizeSounds(Channel type)
        {
            var targetList = this.audioSourcePool[type];
            int playingCount = targetList.FindAll(x => x.isPlaying).Count;
            float targetVolume = 1.0f - ((playingCount - 1) * 0.15f);
            
            foreach (AudioSource audioSource in targetList)
            {
                audioSource.volume = targetVolume;
            }
        }

        private bool IsSpecialBlockSE(string sheetName, int id)
        {
            if (sheetName != "Ingame") return false;
            return id is >= 3 and <= 17;
        }

        private bool IsNormalBlockExplodeSE(string sheetName, int id)
        {
            if (sheetName != "Ingame") return false;
            return id == 0;
        }

        private Channel GetTargetChannel(string sheetName, string id)
        {
            int parsedInt = -1; 
            int.TryParse(id, out parsedInt);

            return GetTargetChannel(sheetName, parsedInt);
        }

        private Channel GetTargetChannel(string sheetName, int id)
        {
            if (sheetName != "Ingame")
            {
                Channel targetChannel = (Channel)Enum.Parse(typeof(Channel), sheetName);
                //SBDebug.Log("GetTargetChannel sheetName : " + sheetName);
                //SBDebug.Log("targetChannel : " + targetChannel);
                return targetChannel;
            }
            else
            {
                if (id is >= 0 and <= 2)
                {
                    return Channel.NormalBlock_Explode;
                }
                if (id is >= 3 and <= 5)
                {
                    return Channel.Puzzle_Bomb;
                }
                if (id is >= 6 and <= 7)
                {
                    return Channel.Puzzle_Rocket;
                }

                if (id is >= 8 and <= 11)
                {
                    return Channel.Puzzle_Meteor;
                }

                if (id is >= 12 and <= 13)
                {
                    return Channel.Puzzle_BlackHole;
                }
                
                if (id is >= 14 and <= 17)
                {
                    return Channel.Puzzle_ShootingStar;
                }
                
                if (id is >= 18 and <= 20)
                {
                    return Channel.Puzzle_Mission;
                }
                
                return Channel.Ingame;
            }
        }

        private AudioSource GetAvailableAudioSource(Channel channel)
        {
            return this.audioSourcePool[channel].Find(x => x.clip == null);
        }

        private AudioSource GetAudioSourceZeroIndex(Channel channel, int index)
        {
            return this.audioSourcePool[channel][index];
        }

        //음원이 중첩되었을 때 처리를 위한 기존 음원 FadeOut 처리 
        public void FadeOutBlockExplodeSounds(AudioSource[] targetChannel)
        {
            foreach (AudioSource source in targetChannel)
            {
                if (source.clip != null && source.isPlaying)
                {
                    var targetClip = this.ingameAudioClipInfos.Find(x => x.clip == source.clip);
                    if (targetClip != null)
                    {
                        source
                            .DOFade(0, targetClip.remainTime)
                            .SetEase(Ease.OutQuint);
                    }
                }
            }
        }

        public Dictionary<int, List<VoiceInfo>> VoiceMap { get; private set; }
        public List<CardVoiceBundle> cardVoiceList { get; private set; }

        public class VoiceInfo
        {
            public VoiceResource voiceResource;
            public ArtistVoice artistVoice;

            public VoiceInfo(VoiceResource voiceResource, ArtistVoice artistVoice)
            {
                this.voiceResource = voiceResource;
                this.artistVoice = artistVoice;
            }
        }

        public void SetVoiceAudio()
        {
            int count = 0;

            this.VoiceMap = new Dictionary<int, List<VoiceInfo>>();

            foreach (ArtistVoice data in SBDataSheet.Instance.ArtistVoice.Values)
            {
                string path = AssetPathController.PATH_FOLDER_ASSETS + SBDataSheet.Instance.VoiceResource[data.VoiceRes].Address;
                
                if (VoiceMap.TryGetValue(data.Bundle, out var value))
                {
                    value.Add(new VoiceInfo(SBDataSheet.Instance.VoiceResource[data.VoiceRes], data));
                }
                else
                {
                    List<VoiceInfo> pathList = new List<VoiceInfo>();
                    pathList.Add(new VoiceInfo(SBDataSheet.Instance.VoiceResource[data.VoiceRes], data));
                
                    VoiceMap.Add(data.Bundle, pathList);
                }
                
                count++;
            }

            voiceList = new List<VoiceResource>();
            foreach (VoiceResource data in SBDataSheet.Instance.VoiceResource.Values)
            {
                voiceList.Add(data);
            }
            
            cardVoiceList = new List<CardVoiceBundle>();
            foreach (CardVoiceBundle data in SBDataSheet.Instance.CardVoiceBundle.Values)
            {
                cardVoiceList.Add(data);
            }

            audioCardVoiceClips = new AudioClip[SBDataSheet.Instance.CardVoiceBundle.Values.Count];

            audioVoiceClips = new AudioClip[SBDataSheet.Instance.VoiceResource.Values.Count];

        }

        public void PlayTutorialVoice(int artistVoiceBundleCode)
        {
            if(!SoundEffectAvailable){ return; }

            var infoList = VoiceMap[artistVoiceBundleCode];
            if(infoList == null) return;

            var selectedTutorialVoiceCode = GameStorage.SelectedTutorialVoiceCode;
            if(selectedTutorialVoiceCode == -1) return;

            var data = infoList.Find(x => x.artistVoice.ArtistCode == selectedTutorialVoiceCode);
            if(data == null) return;
            
            string path = AssetPathController.PATH_FOLDER_ASSETS + data.voiceResource.Address;
            
#if (UNITY_ANDROID || UNITY_IOS)
            path = string.Format("file://{0}", path);
#endif

            int voiceIndex = voiceList.FindIndex(x => x.Code == data.voiceResource.Code);

            CoroutineTaskManager.AddTask(GetAudioClip(path, voiceIndex));
        }

        public void PlayVoice(VoiceBundle bundle, int artistCode, bool isPlayBGM = true)
        {
            if (!SoundEffectAvailable)
            {
                return;
            }

            var Infolist = VoiceMap[(int)bundle];

            Infolist = Infolist.FindAll(x => x.artistVoice.ArtistCode == artistCode);

            int index = Random.Range(0, Infolist.Count);
            var data = Infolist[index];

            string path = AssetPathController.PATH_FOLDER_ASSETS + data.voiceResource.Address;

#if (UNITY_ANDROID || UNITY_IOS)
            path = string.Format("file://{0}", path);
#endif

            int voiceIndex = voiceList.FindIndex(x => x.Code == data.voiceResource.Code);

            CoroutineTaskManager.AddTask(GetAudioClip(path, voiceIndex, isPlayBGM));
        }
        
        public void PlayVoice(int cardBundleCode)
        {
            if (!SoundEffectAvailable)
            {
                return;
            }

            var Infolist = cardVoiceList;

            Infolist = Infolist.FindAll(x => x.Bundle == cardBundleCode);
            
            int index = Random.Range(0, Infolist.Count);
            var data = Infolist[index];

            string path = AssetPathController.PATH_FOLDER_ASSETS + data.Address;

#if (UNITY_ANDROID || UNITY_IOS)
            path = string.Format("file://{0}", path);
#endif

            int voiceIndex = cardVoiceList.FindIndex(x => x.Code == data.Code);

            CoroutineTaskManager.AddTask(GetCardVoiceAudioClip(path, voiceIndex));
        }
        
        public float GetRhythmBGM(VoiceBundle voiceBundle)
        {
            float volume = 0;
            if (voiceBundle == VoiceBundle.RhythmInGame)
            {
                volume =  GameStorage.RhythmBGM;
            }
            return volume;
        }
        

        Coroutine AutofadeInCoroutine;
        Coroutine fadeOutCoroutine;
        Coroutine fadeInCoroutine;
        Coroutine rhythmBGM;

        IEnumerator GetAudioClip(string uri, int index, bool isPlayBGM = true)
        {
            float bgm = 0;
            bgm = GameStorage.RhythmBGM;
            if (audioVoiceClips[index] != null)
            {
                audioVoice.clip = audioVoiceClips[index];

                if (fadeInCoroutine != null)
                {
                    StopCoroutine(fadeInCoroutine);
                }
                if (AutofadeInCoroutine != null)
                {
                    StopCoroutine(AutofadeInCoroutine);
                }
                if(rhythmBGM != null)
                {
                    StopCoroutine(rhythmBGM);
                }
                if (fadeOutCoroutine != null)
                {
                    StopCoroutine(fadeOutCoroutine);
                }
              
                if (isPlayBGM)
                {
                    //fadeOutCoroutine = StartCoroutine(BGMFadeOut(audioBGM, 0.35f));
                    //AutofadeInCoroutine = StartCoroutine(ControllVoiceVolume());                                        
                }

                audioVoice.Play();

                if (isPlayBGM)
                {
                    if (audioVoice.isPlaying)
                    {
                        previewAudioBGM.volume = 0.3f;
                        StartCoroutine(ControllRhythmBGM(bgm));
                    }
                }
            }
            else
            {
                using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.OGGVORBIS))
                {
                    yield return www.SendWebRequest();

                    if (www.result == UnityWebRequest.Result.ConnectionError)
                    {
                        Debug.Log(www.error);
                    }
                    else
                    {
                        if(www.result != UnityWebRequest.Result.Success) yield break;
                        
                        AudioClip myClip = DownloadHandlerAudioClip.GetContent(www);

                        audioVoice.clip = audioVoiceClips[index] = myClip;

                        if (fadeInCoroutine != null)
                        {
                            StopCoroutine(fadeInCoroutine);
                        }
                        if (AutofadeInCoroutine != null)
                        {
                            StopCoroutine(AutofadeInCoroutine);
                        }
                        if (fadeOutCoroutine != null)
                        {
                            StopCoroutine(fadeOutCoroutine);
                        }
                      
                        if (isPlayBGM)
                        {
                            //fadeOutCoroutine = StartCoroutine(BGMFadeOut(audioBGM, 0.35f));
                            //AutofadeInCoroutine = StartCoroutine(ControllVoiceVolume());
                        }

                        audioVoice.Play();
                    }
                }
            }            
            if(!audioVoice.isPlaying)
            GameStorage.RhythmBGM = bgm;
        }



        IEnumerator GetCardVoiceAudioClip(string uri, int index, bool isPlayBGM = true)
        {
            float bgm = 0;
            bgm = GameStorage.RhythmBGM;
            if (audioCardVoiceClips[index] != null)
            {
                audioVoice.clip = audioCardVoiceClips[index];

                if (fadeInCoroutine != null)
                {
                    StopCoroutine(fadeInCoroutine);
                }
                if (AutofadeInCoroutine != null)
                {
                    StopCoroutine(AutofadeInCoroutine);
                }
                if (rhythmBGM != null)
                {
                    StopCoroutine(rhythmBGM);
                }
                if (fadeOutCoroutine != null)
                {
                    StopCoroutine(fadeOutCoroutine);
                }

                if (isPlayBGM)
                {
                    //fadeOutCoroutine = StartCoroutine(BGMFadeOut(audioBGM, 0.35f));
                    //AutofadeInCoroutine = StartCoroutine(ControllVoiceVolume());                                        
                }

                audioVoice.Play();

                if (isPlayBGM)
                {
                    if (audioVoice.isPlaying)
                    {
                        previewAudioBGM.volume = 0.3f;
                        StartCoroutine(ControllRhythmBGM(bgm));
                    }
                }
            }
            else
            {
                using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.OGGVORBIS))
                {
                    yield return www.SendWebRequest();

                    if (www.result == UnityWebRequest.Result.ConnectionError)
                    {
                        Debug.Log(www.error);
                    }
                    else
                    {
                        if (www.result != UnityWebRequest.Result.Success) yield break;

                        AudioClip myClip = DownloadHandlerAudioClip.GetContent(www);

                        audioVoice.clip = audioCardVoiceClips[index] = myClip;

                        if (fadeInCoroutine != null)
                        {
                            StopCoroutine(fadeInCoroutine);
                        }
                        if (AutofadeInCoroutine != null)
                        {
                            StopCoroutine(AutofadeInCoroutine);
                        }
                        if (fadeOutCoroutine != null)
                        {
                            StopCoroutine(fadeOutCoroutine);
                        }

                        if (isPlayBGM)
                        {
                            //fadeOutCoroutine = StartCoroutine(BGMFadeOut(audioBGM, 0.35f));
                            //AutofadeInCoroutine = StartCoroutine(ControllVoiceVolume());
                        }

                        audioVoice.Play();
                    }
                }
            }
            if (!audioVoice.isPlaying)
                GameStorage.RhythmBGM = bgm;
        }



        IEnumerator ControllVoiceVolume()
        {
            do
            {
                yield return null;
            }
            while (audioVoice.isPlaying);

            if (fadeInCoroutine != null)
            {
                StopCoroutine(fadeInCoroutine);
            }
            fadeInCoroutine = StartCoroutine(BGMFadeIn(audioBGM, 0.4f));
        }

        IEnumerator ControllRhythmBGM(float value)
        {
            yield return new WaitForSeconds(1.5f);
            previewAudioBGM.volume = value;
        }


        public IEnumerator BGMFadeOut(AudioSource audioSource, float FadeTime)
        {
            float startVolume = audioSource.volume;

            while (audioSource.volume > 0.15f)
            {
                audioSource.volume -= startVolume * Time.deltaTime / FadeTime;

                yield return null;
            }
            audioSource.volume = 0.15f;
        }

        public IEnumerator BGMFadeIn(AudioSource audioSource, float FadeTime)
        {
            float startVolume = 0.15f;

            while (audioSource.volume < 0.4f)
            {
                audioSource.volume += startVolume * Time.deltaTime / FadeTime;

                yield return null;
            }

            audioSource.volume = 0.4f;
        }

        public void AllStopCourutine()
        {
            if (fadeInCoroutine != null)
            {
                StopCoroutine(fadeInCoroutine);
            }
            if (AutofadeInCoroutine != null)
            {
                StopCoroutine(AutofadeInCoroutine);
            }
            if (fadeOutCoroutine != null)
            {
                StopCoroutine(fadeOutCoroutine);
            }
        }

        public void SetBGMVolume(float value)
        {
            audioBGM.volume = value;
        }

        private void ReadTitleVoiceJsonFile(Action<bool> cb)
        {
            this.titleVoiceMap = new Dictionary<string, List<TitleVoiceData>>();
            
            var data = Resources.Load("Sounds/Sheets/TitleVoiceMap") as TextAsset;
            
            SBDebug.Log("ReadTitleVoiceJsonFile");
            
            if (data != null)
            {
                try
                {
                    SBDebug.Log("ReadTitleVoiceJsonFile 01");
                    
                    var jArray = JArray.Parse(data.text);

                    foreach (JObject item in jArray)
                    {
                        List<TitleVoiceData> titleVoiceDataList = new List<TitleVoiceData>();

                        var snowballsVoiceSet = item["snowballs"];
                        foreach (JObject voiceData in snowballsVoiceSet)
                        {
                            JValue _artistCode = voiceData["artistCode"] as JValue;
                            string _str_artistCode = _artistCode.Value.ToString();
                            int.TryParse(_str_artistCode, out var artistCode);
                            JValue _path = voiceData["path"] as JValue;
                            string path = _path.Value.ToString();
                            
                            TitleVoiceData titleVoiceData = new TitleVoiceData(artistCode, path);
                            titleVoiceDataList.Add(titleVoiceData);
                        }

                        List<TitleVoiceData> cp = titleVoiceDataList.ToList();
                        this.titleVoiceMap.Add("snowballs", cp);
                        titleVoiceDataList.Clear();
                        
                        var entertainmentVoiceSet = item["entertainment"];
                        foreach (JObject voiceData in entertainmentVoiceSet)
                        {
                            JValue _artistCode = voiceData["artistCode"] as JValue;
                            string _str_artistCode = _artistCode.Value.ToString();
                            int.TryParse(_str_artistCode, out var artistCode);
                            JValue _path = voiceData["path"] as JValue;
                            string path = _path.Value.ToString();
                            
                            TitleVoiceData titleVoiceData = new TitleVoiceData(artistCode, path);
                            titleVoiceDataList.Add(titleVoiceData);
                        }
                        
                        List<TitleVoiceData> cp2 = titleVoiceDataList.ToList();
                        this.titleVoiceMap.Add("entertainment", cp2);
                        titleVoiceDataList.Clear();

                        var loginVoiceSet = item["login"];
                        foreach (JObject voiceData in loginVoiceSet)
                        {
                            JValue _artistCode = voiceData["artistCode"] as JValue;
                            string _str_artistCode = _artistCode.Value.ToString();
                            int.TryParse(_str_artistCode, out var artistCode);
                            JValue _path = voiceData["path"] as JValue;
                            string path = _path.Value.ToString();
                            
                            TitleVoiceData titleVoiceData = new TitleVoiceData(artistCode, path);
                            titleVoiceDataList.Add(titleVoiceData);
                        }
                        
                        List<TitleVoiceData> cp3 = titleVoiceDataList.ToList();
                        this.titleVoiceMap.Add("login", cp3);
                        titleVoiceDataList.Clear();
                    }
                    
                    cb?.Invoke(true);
                }
                catch (Exception ex)
                {
                    SBDebug.LogWarning(ex.Message);
                    cb?.Invoke(false);
                }
            }
            else
            {
                cb?.Invoke(false);
            }
        }

        public bool ReadAudioExcelSheetData()
        {
            try
            {
                string[] filteredSheetNames = new string[] { "BGM", "Card", "Common", "Ingame", "Lobby" };
                
#if !UNITY_EDITOR && UNITY_ANDROID
                this.androidAudioController = new AndroidAudioController();
                this.androidAudioController.InitAudioSource();
#else
#endif
                
                audioDict = new SerializedDictionary<string, List<AudioSheetData>>();
                foreach (var sheetName in filteredSheetNames)
                {
                    SBDebug.Log("Audio Controller SheetName : " + sheetName);
                    
                    List<AudioSheetData> dataList = new List<AudioSheetData>();
                    var data = Resources.Load("Sounds/Sheets/" + sheetName) as TextAsset;
                    // SBDebug.Log("Audio Controller 03");
                    if (data != null)
                    {
                        var jArray = JArray.Parse(data.text);
                        
                        foreach (JObject item in jArray)
                        {
                            string id = item.GetValue("id").ToString();
                            string keyName = item.GetValue("keyName").ToString();
                            string path = item.GetValue("path").ToString().Split('.')[0];

                            SBDebug.Log("Audio Controller id : " + id);
                            SBDebug.Log("Audio Controller keyName : " + keyName);
                            SBDebug.Log("Audio Controller path : " + path);
                        
                            AudioSheetData audioSheetData = new AudioSheetData(id, keyName, path);
                            dataList.Add(audioSheetData);
                            
#if !UNITY_EDITOR && UNITY_ANDROID
                            if (audioSheetData.audioClip != null)
                            {
                                androidAudioController.InitAudioClipPlayers(
                                    this.GetTargetChannel(sheetName, id), 
                                    id, 
                                    audioSheetData.audioClip
                                );
                            }
#else
#endif
                        }
                        this.audioDict.Add(sheetName, dataList);
                    }
                    else
                    {
                        SBDebug.LogWarning("Audio Controller Data is null " + sheetName);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                SBDebug.LogWarning("Audio Controller Init Error : " + ex.Message);
                return false;
            }
        }
    }

    [System.Serializable]
    public class AudioSheetData
    {
        public Int32 ID;
        public string keyName;
        public string path;

        public AudioClip audioClip;
        public OboeAudioClip oboeAudioClip;

        public AudioSheetData(string code, string keyName, string path)
        {
            Int32.TryParse(code, out this.ID);
            this.keyName = keyName;
            this.path = path;
                
            this.audioClip = Resources.Load(this.path) as AudioClip;
            this.oboeAudioClip = new OboeAudioClip(this.audioClip);
        }
    }

    public class PreviewAudioData
    {
        public Int32 code;
        public AudioClip audioClip;

        public PreviewAudioData(Int32 code, AudioClip audioClip)
        {
            this.code = code;
            this.audioClip = audioClip;
        }
    }

    public class TitleVoiceData
    {
        public int artistCode;
        public string path;

        public TitleVoiceData(int artistCode, string path)
        {
            this.artistCode = artistCode;
            this.path = path;
        }
    }
}
