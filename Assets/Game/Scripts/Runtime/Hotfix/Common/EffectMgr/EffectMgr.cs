using UnityEngine;
using System.Collections.Generic;

namespace Game.Runtime.Hotfix
{
    public class EffectMgr:IUpdate
    {
        public Transform m_EffectRoot { get; private set; }
        public EffectConfig CurrentConfig { get; private set; }
        
        private List<EffectBase> m_Effects = new List<EffectBase>();

        public bool IsEffectLimit { get;private set; }
        
        public EffectMgr()
        {
            m_EffectRoot = new GameObject("EffectRoot").transform;
            Object.DontDestroyOnLoad(m_EffectRoot.gameObject);
            
            // 默认初始化为高画质配置
            SetEffectConfig(EffectDefine.High);
        }

        /// <summary>
        /// 切换画质，并应用到所有现有特效
        /// </summary>
        public void SetEffectConfig(string key)
        {
            CurrentConfig = EffectDefine.GetUIConfig(key);
            if (CurrentConfig == null) return;

            foreach (var effectBase in m_Effects)
            {
                effectBase.ApplyConfig(CurrentConfig);
            }
            
            Debug.Log($"[EffectMgr] Quality Switched to: {key}");
        }

        /// <summary>
        /// 设置特效限制，启用后会对特效限制
        /// </summary>
        public void SetEffectIsLimit(bool isLimit)
        {
            IsEffectLimit = isLimit;
        }

        /// <summary>
        /// 播放特效方法
        /// </summary>
        /// <param name="assetPath">地址</param>
        /// <param name="parent">挂点</param>
        /// <param name="isLoop">是否循环,循环的情况下持续时间不生效</param>
        /// <param name="duration">持续时间</param>
        /// <returns></returns>
        public EffectHandle<T> PlayEffect<T>(string assetPath, Transform parent = null, bool isLoop = false,float duration = -1f) where T : EffectBase
        {
            EffectHandle<T> handle = new EffectHandle<T>();

            Global.gApp.gPoolMgr.Spawn<T>(assetPath).SetCallback((effectBase) =>
            {
                effectBase.transform.SetParent(parent ?? m_EffectRoot, false);
                if (isLoop) effectBase.SetLoop(true);
                
                handle.Complete(effectBase);
                m_Effects.Add(effectBase);

                // 优先使用传入的 duration，如果没有则使用 EffectBase 自动计算的时长
                if (!isLoop)
                {
                    float finalDuration = duration > 0 ? duration : effectBase.m_MaxDuration;
                    effectBase.SetFinalDuration(finalDuration);
                }
            });

            return handle;
        }
        
        public void PlayEffect(string assetPath, Transform parent = null, bool isLoop = false,float duration = -1f)
        {
            EffectHandle<EffectBase> handle = new EffectHandle<EffectBase>();

            Global.gApp.gPoolMgr.Spawn<EffectBase>(assetPath).SetCallback((effectBase) =>
            {
                effectBase.transform.SetParent(parent ?? m_EffectRoot, false);
                if (isLoop) effectBase.SetLoop(true);
                
                handle.Complete(effectBase);
                m_Effects.Add(effectBase);

                // 优先使用传入的 duration，如果没有则使用 EffectBase 自动计算的时长
                if (!isLoop)
                {
                    float finalDuration = duration > 0 ? duration : effectBase.m_MaxDuration;
                    effectBase.SetFinalDuration(finalDuration);
                }
            });
        }
        // 唯一接口
        public void Dispose(EffectBase handle)
        {
            if (handle == null) return;
            if (m_Effects.Contains(handle))
            {
                Global.gApp.gPoolMgr.Despawn(handle.gameObject);
            }
        }
        
        public void OnDespawn(EffectBase handle)
        {
            if (handle == null) return;
            if (m_Effects.Contains(handle))
            {
                m_Effects.Remove(handle);
            }
        }
        public void OnDestroy()
        {
            var list = new List<EffectBase>(m_Effects);
            foreach (var h in list) Dispose(h);
            m_Effects.Clear();
            if (m_EffectRoot != null) Global.gApp.gResMgr.Destroy(m_EffectRoot.gameObject);
        }

        public void OnIUpdate(float dt)
        {
            if (IsEffectLimit && m_Effects.Count > CurrentConfig.EffectLimit)
            {
                // 从后往前清理超出限制的特效
                for (int i = m_Effects.Count - 1; i >= CurrentConfig.EffectLimit; i--)
                {
                    Dispose(m_Effects[i]);
                }
            }
        }
    }
}
