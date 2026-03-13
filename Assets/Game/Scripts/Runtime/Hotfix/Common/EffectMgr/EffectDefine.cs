using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Hotfix
{
    public class EffectHandle
    {
        public string Path { get; private set; }
        public EffectBase m_Base { get; private set; }
        public GameObject m_GameObject
        {
            get
            {
                return  m_Base != null ? m_Base.gameObject : null;;
            }
            private set { }
        }

        public bool IsLoaded => m_Base != null;

        private Action<EffectHandle> m_Callback;

        public EffectHandle(string path)
        {
            Path = path;
        }

        public EffectHandle SetCallback(Action<EffectHandle> callback)
        {
            if (IsLoaded)
            {
                callback?.Invoke(this);
            }
            else
            {
                m_Callback = callback;
            }
            return this;
        }

        public void Complete(EffectBase baseComp)
        {
            m_Base = baseComp;
            m_Callback?.Invoke(this);
            m_Callback = null;
        }

        public void Dispose()
        {
            Global.gApp.gEffectMgr.Dispose(this);
        }

        public void SetPosition(Vector3 pos)
        {
            if (IsLoaded) m_GameObject.transform.position = pos;
        }

        public void SetRotation(Quaternion rot)
        {
            if (IsLoaded) m_GameObject.transform.rotation = rot;
        }
        
        public void SetParent(Transform parent, bool worldPositionStays = false)
        {
            if (IsLoaded) m_GameObject.transform.SetParent(parent, worldPositionStays);
        }
    }

    /// <summary>
    /// 特效运行配置
    /// </summary>
    public class EffectConfig
    {
        public float ParticleRateMultiplier; // 粒子发射速率倍率
        public int MaxParticlesLimit;        // 最大粒子数限制
        public int WaveQuality;                 // 波纹/折射精细度 (0-2)
        public int EffectLimit;                 // 粒子最低数量限制，用于内存低时处理特效
    }

    public static class EffectDefine
    {
        public const string Low = "Low";
        public const string Medium = "Medium";
        public const string High = "High";

        #region 配置字典

        public static Dictionary<string, EffectConfig> EffectInfo = new Dictionary<string, EffectConfig>()
        {
            {
                Low, new EffectConfig 
                { 
                    ParticleRateMultiplier = 0.4f, 
                    MaxParticlesLimit = 50, 
                    WaveQuality = 0,
                    EffectLimit = 15,
                }
            },
            {
                Medium, new EffectConfig 
                { 
                    ParticleRateMultiplier = 0.7f, 
                    MaxParticlesLimit = 200, 
                    WaveQuality = 1,
                    EffectLimit = 30,
                }
            },
            {
                High, new EffectConfig 
                { 
                    ParticleRateMultiplier = 1.0f, 
                    MaxParticlesLimit = 1000, 
                    WaveQuality = 2,
                    EffectLimit = 50,
                }
            },
        };

        #endregion

        public static EffectConfig GetUIConfig(string key)
        {
            if (EffectInfo.TryGetValue(key, out EffectConfig config))
            {
                return config;
            }
            return EffectInfo[High]; // 默认返回高画质
        }
    }
}
