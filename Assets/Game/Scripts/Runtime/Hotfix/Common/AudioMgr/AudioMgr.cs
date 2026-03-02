using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 功能全面、通用的声源管理器
    /// 职责：
    /// 1. BGM/SFX/Voice 分类管理。
    /// 2. BGM 淡入淡出切换。
    /// 3. SFX 对象池管理。
    /// 4. 2D/3D 音效支持。
    /// 5. 音量持久化与全局开关。
    /// </summary>
    public class AudioMgr
    {
        private App m_App;
        private Transform m_AudioRoot;

        // 音源组件
        private AudioSource m_BGMSource;
        private AudioSource m_VoiceSource;
        private List<AudioSource> m_SFXPool;
        private int m_MaxSFXCount = 16; // 最大同时播放音效数

        // 设置
        private float m_GlobalVolume = 1.0f;
        private float m_MusicVolume = 1.0f;
        private float m_SoundVolume = 1.0f;
        private float m_VoiceVolume = 1.0f;
        private bool m_IsMute = false;

        // 状态
        private string m_CurrentBGMPath;
        private float m_BGMTargetVolume = 1.0f;
        private float m_FadeSpeed = 1.5f;
        private bool m_IsFading = false;

        // 监听器
        private AudioListener m_MainListener;

        public void Init(Transform keepNode)
        {
            m_App = Global.gApp;
            
            // 创建音频根节点
            GameObject go = new GameObject("AudioRoot");
            m_AudioRoot = go.transform;
            m_AudioRoot.SetParent(keepNode);

            // 1. 先清理场景中可能存在的冗余监听器（比如 LogoScene 自带的）
            AudioListener[] listeners = Object.FindObjectsOfType<AudioListener>();
            foreach (var l in listeners)
            {
                Object.Destroy(l);
                Debug.Log($"[AudioMgr] Removed initial AudioListener on {l.gameObject.name} to ensure framework's listener is unique.");
            }

            // 2. 建立常驻监听器 (属于框架，不随场景销毁)
            m_MainListener = m_AudioRoot.gameObject.AddComponent<AudioListener>();

            // 初始化音源
            m_BGMSource = CreateAudioSource("BGMSource", true);
            m_VoiceSource = CreateAudioSource("VoiceSource", false);
            m_SFXPool = new List<AudioSource>();

            LoadSettings();
        }

        /// <summary>
        /// 每帧驱动：处理监听器同步与冗余清理
        /// </summary>
        public void OnIUpdate(float dt)
        {
            // 监听器位置同步 (关键：支持 3D 音效随相机移动)
            // 即使场景切换，只要新相机标记为 MainCamera，听觉中心就会自动跟过去
            if (Camera.main != null)
            {
                m_AudioRoot.position = Camera.main.transform.position;
                m_AudioRoot.rotation = Camera.main.transform.rotation;
            }

            // 二次保险：防止新场景异步加载后又带进来新的监听器
            if (Time.frameCount % 60 == 0) 
            {
                AudioListener[] listeners = Object.FindObjectsOfType<AudioListener>();
                if (listeners.Length > 1)
                {
                    foreach (var l in listeners)
                    {
                        if (l != m_MainListener) Object.Destroy(l);
                    }
                }
            }
        }

        private AudioSource CreateAudioSource(string name, bool loop, Transform parent = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent ?? m_AudioRoot);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            return source;
        }

        #region 设置与音量控制

        private void LoadSettings()
        {
            m_MusicVolume = PlayerPrefs.GetFloat(AudioDefine.MusicVolumeKey, AudioDefine.DefaultVolume);
            m_SoundVolume = PlayerPrefs.GetFloat(AudioDefine.SoundVolumeKey, AudioDefine.DefaultVolume);
            m_VoiceVolume = PlayerPrefs.GetFloat(AudioDefine.VoiceVolumeKey, AudioDefine.DefaultVolume);
            m_IsMute = PlayerPrefs.GetInt(AudioDefine.IsMuteKey, 0) == 1;

            ApplyBGMVolume();
            ApplyVoiceVolume();
        }

        public void SetMute(bool isMute)
        {
            m_IsMute = isMute;
            PlayerPrefs.SetInt(AudioDefine.IsMuteKey, isMute ? 1 : 0);
            ApplyBGMVolume();
            ApplyVoiceVolume();
            // 已播放的音效通常不动态改静音，下次播放生效
        }

        public void SetMusicVolume(float volume)
        {
            m_MusicVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(AudioDefine.MusicVolumeKey, m_MusicVolume);
            ApplyBGMVolume();
        }

        public void SetSoundVolume(float volume)
        {
            m_SoundVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(AudioDefine.SoundVolumeKey, m_SoundVolume);
        }

        private void ApplyBGMVolume()
        {
            if (m_BGMSource != null)
            {
                m_BGMSource.mute = m_IsMute;
                m_BGMSource.volume = m_MusicVolume * m_GlobalVolume;
            }
        }

        private void ApplyVoiceVolume()
        {
            if (m_VoiceSource != null)
            {
                m_VoiceSource.mute = m_IsMute;
                m_VoiceSource.volume = m_VoiceVolume * m_GlobalVolume;
            }
        }

        #endregion

        #region BGM 播放

        /// <summary>
        /// 根据配置 Key 播放 BGM
        /// </summary>
        public void PlayBGM(string key, bool fade = true)
        {
            AudioConfig config = AudioDefine.GetAudioConfig(key);
            if (config == null)
            {
                Debug.LogError($"[AudioMgr] PlayBGM failed, key not found: {key}");
                return;
            }

            if (config.Path == m_CurrentBGMPath) return;

            m_CurrentBGMPath = config.Path;
            bool forceGlobal = config.ResTypeByScene == ResTypeByScene.Global;

            m_App.gResMgr.LoadAudioAsync(config.Path, (clip) =>
            {
                if (clip == null) return;
                
                // 确保异步加载回来后，还是我们要播的那首（防止连续切换导致的错乱）
                if (m_CurrentBGMPath != config.Path) return;

                m_BGMSource.clip = clip;
                m_BGMSource.Play();
                ApplyBGMVolume();
            }, forceGlobal);
        }

        public void StopBGM()
        {
            m_BGMSource.Stop();
            m_CurrentBGMPath = null;
        }

        public void PauseBGM() => m_BGMSource.Pause();
        public void UnPauseBGM() => m_BGMSource.UnPause();

        #endregion

        #region SFX 播放 (2D)

        /// <summary>
        /// 根据配置 Key 播放 2D 音效
        /// </summary>
        public void PlaySFX(string key)
        {
            if (m_IsMute) return;

            AudioConfig config = AudioDefine.GetAudioConfig(key);
            if (config == null)
            {
                Debug.LogError($"[AudioMgr] PlaySFX failed, key not found: {key}");
                return;
            }

            bool forceGlobal = config.ResTypeByScene == ResTypeByScene.Global;

            m_App.gResMgr.LoadAudioAsync(config.Path, (clip) =>
            {
                if (clip == null) return;
                AudioSource source = GetAvailableSFXSource();
                source.transform.position = Vector3.zero;
                source.spatialBlend = 0; // 2D
                source.clip = clip;
                source.volume = m_SoundVolume * m_GlobalVolume;
                source.Play();
            }, forceGlobal);
        }

        private AudioSource GetAvailableSFXSource()
        {
            // 找空闲的
            for (int i = 0; i < m_SFXPool.Count; i++)
            {
                if (!m_SFXPool[i].isPlaying) return m_SFXPool[i];
            }

            // 没找到则创建
            if (m_SFXPool.Count < m_MaxSFXCount)
            {
                AudioSource source = CreateAudioSource($"SFX_{m_SFXPool.Count}", false);
                m_SFXPool.Add(source);
                return source;
            }

            // 还是没找到，抢占最旧的一个或第一个
            return m_SFXPool[0];
        }

        #endregion

        #region 3D 音效播放

        /// <summary>
        /// 根据配置 Key 在指定位置播放音效 (一次性)
        /// </summary>
        public void PlaySFXAtPos(string key, Vector3 position)
        {
            if (m_IsMute) return;

            AudioConfig config = AudioDefine.GetAudioConfig(key);
            if (config == null) return;

            bool forceGlobal = config.ResTypeByScene == ResTypeByScene.Global;

            m_App.gResMgr.LoadAudioAsync(config.Path, (clip) =>
            {
                if (clip == null) return;
                AudioSource source = GetAvailableSFXSource();
                source.transform.position = position;
                source.spatialBlend = 1.0f; // 3D
                source.clip = clip;
                source.volume = m_SoundVolume * m_GlobalVolume;
                source.minDistance = 1.0f;
                source.maxDistance = 20.0f;
                source.Play();
            }, forceGlobal);
        }

        /// <summary>
        /// 根据配置 Key 挂载到指定物体的 3D 音效
        /// </summary>
        public void PlaySFXOnTransform(string key, Transform target)
        {
            if (m_IsMute || target == null) return;

            AudioConfig config = AudioDefine.GetAudioConfig(key);
            if (config == null) return;

            bool forceGlobal = config.ResTypeByScene == ResTypeByScene.Global;

            m_App.gResMgr.LoadAudioAsync(config.Path, (clip) =>
            {
                if (clip == null) return;
                
                GameObject go = new GameObject("Temp3DAudio");
                go.transform.SetParent(target);
                go.transform.localPosition = Vector3.zero;
                
                AudioSource source = go.AddComponent<AudioSource>();
                source.clip = clip;
                source.spatialBlend = 1.0f;
                source.volume = m_SoundVolume * m_GlobalVolume;
                source.Play();
                
                Object.Destroy(go, clip.length + 0.1f);
            }, forceGlobal);
        }

        #endregion

        #region 语音播放

        /// <summary>
        /// 根据配置 Key 播放语音
        /// </summary>
        public void PlayVoice(string key)
        {
            AudioConfig config = AudioDefine.GetAudioConfig(key);
            if (config == null) return;

            bool forceGlobal = config.ResTypeByScene == ResTypeByScene.Global;

            m_App.gResMgr.LoadAudioAsync(config.Path, (clip) =>
            {
                if (clip == null) return;
                m_VoiceSource.clip = clip;
                m_VoiceSource.Play();
                ApplyVoiceVolume();
            }, forceGlobal);
        }

        #endregion

        #region 统一播放接口

        /// <summary>
        /// 智能播放：自动根据配置类型选择播放方式
        /// </summary>
        public void Play(string key)
        {
            AudioConfig config = AudioDefine.GetAudioConfig(key);
            if (config == null) return;

            switch (config.Type)
            {
                case AudioType.BGM:
                    PlayBGM(key);
                    break;
                case AudioType.SFX:
                    PlaySFX(key);
                    break;
                case AudioType.Voice:
                    PlayVoice(key);
                    break;
                case AudioType.SFX3D:
                    PlaySFXAtPos(key, Vector3.zero);
                    break;
            }
        }

        #endregion
        
        public void OnDestroy()
        {
            if (m_AudioRoot != null)
            {
                Object.Destroy(m_AudioRoot.gameObject);
            }
        }
    }
}
