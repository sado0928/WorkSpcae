using System.Collections.Generic;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 音频类型枚举
    /// </summary>
    public enum AudioType
    {
        BGM,    // 背景音乐
        SFX,    // 音效 (2D)
        Voice,  // 语音
        SFX3D,  // 3D音效
    }

    /// <summary>
    /// Audio 配置结构
    /// </summary>
    public class AudioConfig
    {
        public string Path;                // 资源地址
        public AudioType Type;              // 类型
        public ResTypeByScene ResTypeByScene;      // 资源归属场景
        
        public AudioConfig()
        {
            
        }
    }
    
    /// <summary>
    /// 音频常量定义
    /// </summary>
    public static class AudioDefine
    {
        public const string MusicVolumeKey = "MusicVolume";
        public const string SoundVolumeKey = "SoundVolume";
        public const string VoiceVolumeKey = "VoiceVolume";
        public const string IsMuteKey = "IsMute";

        // 默认音量
        public const float DefaultVolume = 1.0f;

        public const string MainBgm = "MainBgm";
        
        #region 配置字典

        public static Dictionary<string, AudioConfig> AudioInfo = new Dictionary<string, AudioConfig>()
        {
            {MainBgm,new AudioConfig(){Path ="Audio/Sound/Bgm/bgm_main",Type =AudioType.BGM,ResTypeByScene = ResTypeByScene.Global}},
        };

        #endregion

        public static AudioConfig GetAudioConfig(string key)
        {
            if (AudioInfo.TryGetValue(key,out AudioConfig audioConfig))
            {
                return audioConfig;
            }
            return null;
        }
        
    }
}
