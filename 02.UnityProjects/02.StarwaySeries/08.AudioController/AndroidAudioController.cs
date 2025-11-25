using System.Collections.Generic;
using System.Linq;
using Oboe.Clip;
using UnityEngine;

namespace Snowballs.Client.Controller
{
    public class AndroidAudioController
    {
        //OboeAudioClip을 따로 캐싱한다.
        //1차 분류 : 채널 이름
        //2차 분류 : Sheet의 id
        private Dictionary<AudioController.Channel, Dictionary<string, OboeAudioClipPlayer>> audioPlayers;
        private OboeAudioSource audioSource;

        public OboeAudioSource AudioSource
        { 
            get
            {
                return audioSource;
            }
        }

        public void InitAudioSource()
        {
            this.audioSource = new OboeAudioSource(
                48000, 
                PerformanceMode.LowLatency, 
                SharingMode.Shared,
                AudioApi.AAudio, 
                384, 
                128
            );
        }
        
        public void InitAudioClipPlayers(AudioController.Channel channel, string id, AudioClip audioClip)
        {
            //SBDebug.Log("InitAudioClipPlayers channel : " + channel);
            //SBDebug.Log("InitAudioClipPlayers id : " + id);
            
            if (this.audioPlayers == null)
            {
                this.audioPlayers = new Dictionary<AudioController.Channel, Dictionary<string, OboeAudioClipPlayer>>();
            }
            
            if (!this.audioPlayers.ContainsKey(channel))
            {
                this.audioPlayers.Add(channel, new Dictionary<string, OboeAudioClipPlayer>());
            }

            OboeAudioClip oboeAudioClip = new OboeAudioClip(audioClip);
            OboeAudioClipPlayer audioClipPlayer = new OboeAudioClipPlayer(oboeAudioClip);
            audioClipPlayer.Stop();
            
            this.audioSource.AddAudioPlayer(audioClipPlayer);
            this.audioPlayers[channel].Add(id, audioClipPlayer);
        }
    
        public void DisposeOboeAudio()
        {
            if (this.audioPlayers != null)
            {
                foreach (KeyValuePair<AudioController.Channel, Dictionary<string, OboeAudioClipPlayer>> keyValuePair in this.audioPlayers)
                {
                    foreach (KeyValuePair<string, OboeAudioClipPlayer> innerKeyValuePair in keyValuePair.Value)
                    {
                        innerKeyValuePair.Value.OboeAudioClip.Dispose();
                    }
                }
            }
        }
    
        public void NormalizeSounds(AudioController.Channel channel, int rawId)
        { 
            if(!this.audioPlayers.ContainsKey(channel)) return;
            
            var targetAudioClipPlayerList = this.audioPlayers[channel][rawId.ToString()];
            if(targetAudioClipPlayerList == null) return;
            
            // int playingCount = targetAudioClipPlayerList.Count(x => x.Playing);
            // float targetVolume = 1.0f - ((playingCount - 1) * 0.15f);
            
            // foreach (OboeAudioClipPlayer audioClipPlayer in targetAudioClipPlayerList)
            // {
            //     audioClipPlayer.Volume = targetVolume;
            // }
        }

        public OboeAudioClipPlayer GetAvailableAudioPlayer(AudioController.Channel channel, int rawId)
        {
            //SBDebug.Log("GetAvailableAudioPlayer");
            if (this.audioPlayers == null) return null;
            
            //SBDebug.Log("GetAvailableAudioPlayer 01");
            var targetChannel = this.audioPlayers[channel];
            
            //SBDebug.Log("GetAvailableAudioPlayer 02 channel : " + channel);
            var targetAudioPlayer = targetChannel[rawId.ToString()];
            return targetAudioPlayer;
        }

        public OboeAudioClipPlayer GetTimingAudioPlayer(AudioController.Channel channel, int rawId)
        {
            if (this.audioPlayers == null) return null;
            var targetChannel = this.audioPlayers[channel];
            var targetAudioPlayer = targetChannel[rawId.ToString()];
            //targetAudioPlayer.Looping = true;
            //targetAudioPlayer.Time = sec;
            return targetAudioPlayer;
        }

        public void MuteSoundEffect(AudioController.Channel channel)
        {
            if(this.audioPlayers == null) return;
        }
    }
}

