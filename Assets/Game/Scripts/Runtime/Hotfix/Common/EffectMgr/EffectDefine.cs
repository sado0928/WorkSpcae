using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 特效 异步加载句柄
    /// </summary>
    /// <typeparam name="T">EntityBase 的具体类型</typeparam>
    public class EffectHandle<T> where T : EffectBase
    {
        private Action<T> m_OnComplete;
        private T m_Result;
        private bool m_IsDone;
        
        public Vector3 Position { get;private set; }
        public Vector3 Rotation { get;private set; }
        public Vector3 Scale { get;private set; }
        
        public EffectHandle()
        {
            m_IsDone = false;
        }

        /// <summary>
        /// 设置加载完成后的回调
        /// 如果已经加载完成，会立即执行回调
        /// </summary>
        public void SetCallback(Action<T> callback)
        {
            m_OnComplete = callback;
            if (m_IsDone && m_Result != null)
            {
                m_OnComplete?.Invoke(m_Result);
                m_OnComplete = null; // 触发一次后清空，避免重复
            }
        }

        /// <summary>
        /// 内部调用：标记加载完成
        /// </summary>
        public void Complete(T result)
        {
            m_Result = result;
            m_IsDone = true;
            if (Position !=default) SetPosition(Position);
            if (Rotation !=default) SetRotation(Rotation);
            if (Scale !=default) SetScale(Scale);
            m_OnComplete?.Invoke(m_Result);
            m_OnComplete = null;
        }
        
        public void SetPosition(Vector3 pos)
        {
            Position = pos;
            if (m_IsDone) m_Result.OnSetPosition(pos); 
        }

        public void SetRotation(Vector3 rot)
        {
            Rotation = rot;
            if (m_IsDone) m_Result.OnSetRotation(rot);
        }
        
        public void SetScale(Vector3 scale)
        {
            Scale = scale;
            if (m_IsDone) m_Result.OnSetScale(scale);
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
