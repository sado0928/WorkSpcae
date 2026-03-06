using UnityEngine;
using System.Collections.Generic;

namespace Game.Runtime.Hotfix
{
    public class EffectBase : PoolBase
    {
        public EffectHandle m_EffectHandle { get;private set; }
        private ParticleSystem[] m_Particles;
        public float m_MaxDuration { get; private set; } 
        private int m_LoopTimerId = -1;
        private int m_FinalDurationTimerId = -1;

        private void Awake()
        {
            CalculateMaxDuration();
        }

        /// <summary>
        /// 综合计算粒子与动画的最大时长
        /// </summary>
        private void CalculateMaxDuration()
        {
            m_MaxDuration = 0f;

            // 1. 扫描粒子系统
            m_Particles = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in m_Particles)
            {
                var main = ps.main;
                float totalTime = main.duration + main.startDelay.constantMax;
                if (totalTime > m_MaxDuration) m_MaxDuration = totalTime;
            }

            // 2. 扫描 Animator (取默认状态的动画时长)
            var animators = GetComponentsInChildren<Animator>(true);
            foreach (var animator in animators)
            {
                if (animator.runtimeAnimatorController != null)
                {
                    var clips = animator.runtimeAnimatorController.animationClips;
                    foreach (var clip in clips)
                    {
                        if (clip.length > m_MaxDuration) m_MaxDuration = clip.length;
                    }
                }
            }

            // 3. 扫描 Animation
            var animations = GetComponentsInChildren<Animation>(true);
            foreach (var anim in animations)
            {
                if (anim.clip != null && anim.clip.length > m_MaxDuration)
                    m_MaxDuration = anim.clip.length;
            }

            // 增加时长保底
            if (m_MaxDuration <= 0) m_MaxDuration = 0.1f;
        }

        #region  设置渲染相关

        public void SetGameObjectLayer(int layer)
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            foreach (var tr in transforms)
            {
                tr.gameObject.layer = layer;
            }
        }

        public void SetSortingLayer(int layerId)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var ren in renderers)
            {
                ren.sortingLayerID = layerId;
            }
        } 
       
        public void SetSortingOrder(int order)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var ren in renderers)
            {
                ren.sortingOrder = order;
            }
        }

        #endregion
       

        protected override void OnSpawn()
        {
            ApplyConfig(Global.gApp.gEffectMgr.CurrentConfig);
        }

        protected override void OnDespawn()
        {
            m_EffectHandle.Dispose();
            StopLoopTimer();
            StopFinalDurationTimer();
            StopAllParticles();
        }

        /// <summary>
        /// 应用画质配置
        /// </summary>
        public void ApplyConfig(EffectConfig config)
        {
            if (config == null) return;
            
            if (m_Particles == null) m_Particles = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in m_Particles)
            {
                var emission = ps.emission;
                emission.rateOverTimeMultiplier = config.ParticleRateMultiplier;

                var main = ps.main;
                main.maxParticles = Mathf.Min(main.maxParticles, config.MaxParticlesLimit);
            }
        }

        public void SetHandle(EffectHandle handle)
        {
            m_EffectHandle = handle;
        }

        public void SetLoop(bool isLoop)
        {
            StopLoopTimer();
            if (!isLoop) return;

            m_LoopTimerId = Global.gApp.gTimerMgr.AddTimer(m_MaxDuration, -1, (t, isEnd) =>
            {
                Restart();
            });
        }

        public void SetFinalDuration(float finalDuration)
        {
            m_FinalDurationTimerId = Global.gApp.gTimerMgr.AddTimer(finalDuration, 1, (t, isEnd) =>
            {
                m_EffectHandle.Dispose();
            });
        }
        
        public void Restart()
        {
            StopAllParticles();
            
            // 重启粒子
            foreach (var ps in m_Particles) ps.Play();
            
            // 重启 Animator
            var animators = GetComponentsInChildren<Animator>(true);
            foreach (var anim in animators) anim.Play(0, -1, 0f);

            // 重启 Animation
            var animations = GetComponentsInChildren<Animation>(true);
            foreach (var anim in animations) anim.Play();
        }
        
        private void StopAllParticles()
        {
            if (m_Particles == null) return;
            foreach (var ps in m_Particles)
            {
                ps.Clear();
                ps.Stop();
            }
        }

        private void StopLoopTimer()
        {
            if (m_LoopTimerId != -1)
            {
                Global.gApp.gTimerMgr.RemoveTimer(m_LoopTimerId);
                m_LoopTimerId = -1;
            }
        }
        private void StopFinalDurationTimer()
        {
            if (m_FinalDurationTimerId != -1)
            {
                Global.gApp.gTimerMgr.RemoveTimer(m_FinalDurationTimerId);
                m_FinalDurationTimerId = -1;
            }
        }
        
    }
}
