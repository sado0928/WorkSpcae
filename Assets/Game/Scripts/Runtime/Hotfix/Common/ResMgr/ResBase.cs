using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 资源管理基类
    /// 职责：为 UI 或其他逻辑对象提供极简的动态资源加载封装。
    /// 特色：内部自动处理组件赋值与异步回调，降低业务层心智负担。
    /// </summary>
    public abstract class ResBase : MonoBehaviour
    {
        // 语言标识，预留给未来多语言系统
        protected string m_Language = "zh_CN";

        // 动态资源追踪表：记录本对象生命周期内通过该脚本加载的所有资源路径
        private Dictionary<string, ResType> m_LoadedAssets = new Dictionary<string, ResType>();
        
        // 托管特效列表：UI 销毁时自动回收关联特效 key => instanceID
        private Dictionary<int,EffectHandle> m_EffectDic = new Dictionary<int,EffectHandle>();
        private List<EffectHandle> m_EffectList = new List<EffectHandle>();
        
        // 动态特效的order管理 key => instanceID
        private Dictionary<int, int> m_EffectOrderDic = new Dictionary<int, int>();
        private int m_EffectOrderStep = 5;
        
        #region 资源加载接口 (Simplified API)

        /// <summary>
        /// 播放 UI 特效并托管生命周期
        /// </summary>
        /// <param name="path">特效资源路径</param>
        /// <param name="parent">挂点，默认为当前 UI</param>
        /// <returns></returns>
        public EffectHandle PlayEffect(string path, Transform parent,bool isLoop = false,float duration = -1f )
        {
            if (string.IsNullOrEmpty(path))
            {
                Global.LogError("error by ResBase function for PlayEffect , path is empty");
                return null;
            }
            if (parent == null)
            {
                Global.LogError("error by ResBase function for PlayEffect , parent is empty");
                return null;
            }
            
            // 如果是同一个父节点需要清理一下老特效
            int parentId = parent.GetInstanceID();
            int parentLayer = parent.gameObject.layer;
            if (m_EffectDic.TryGetValue(parentId,out EffectHandle effectHandle))
            {
                m_EffectDic.Remove(parentId);
                m_EffectList.Remove(effectHandle);
                Global.gApp.gEffectMgr.Dispose(effectHandle);
            }
            
            // 向上查找第一个 Canvas 
            Canvas canvas = parent.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Global.LogError("error by ResBase function for PlayEffect , parent canvas is not found");
                return null;
            }

            int sortingOrder = 0;
            int canvasId = canvas.GetInstanceID();
            if (!m_EffectOrderDic.ContainsKey(canvasId))
            {
                m_EffectOrderDic.Add(canvasId,canvas.sortingOrder);
            }

            if (m_EffectOrderDic.TryGetValue(canvasId,out int order))
            {
                int nextOrder = order + m_EffectOrderStep;
                sortingOrder = nextOrder;
                m_EffectOrderDic[canvasId] = nextOrder;
            }
            
            EffectHandle handle = Global.gApp.gEffectMgr.PlayEffect(path, parent,isLoop,duration);
            handle.SetCallback((effectHandle) =>
            {
                if (effectHandle != null && canvas != null)
                {
                    effectHandle.m_Base.SetGameObjectLayer(parentLayer);
                    effectHandle.m_Base.SetSortingOrder(sortingOrder);
                }
            });

            m_EffectDic.Add(parentId,handle);
            m_EffectList.Add(handle);
            return handle;
        }

        /// <summary>
        /// 自动加载并设置图片 (Image)
        /// </summary>
        /// <param name="image">UI Image 组件</param>
        /// <param name="path">资源路径</param>
        /// <param name="forceGlobal">是否强制全局域 (默认 false)</param>
        public void LoadSprite(Image image, string path, bool forceGlobal = false)
        {
            if (image == null || string.IsNullOrEmpty(path)) return;

            RecordAsset(path, ResType.Sprite);
            Global.gApp.gResMgr.LoadSpriteAsync(path, (sprite) =>
            {
                // 异步回调安全检查：确保 Image 对象在加载回来时还没被销毁
                if (image != null && sprite != null)
                {
                    image.sprite = sprite;
                }
            }, forceGlobal);
        }

        /// <summary>
        /// 自动加载并设置 RawImage
        /// </summary>
        public void LoadTexture(RawImage rawImage, string path, bool forceGlobal = false)
        {
            if (rawImage == null || string.IsNullOrEmpty(path)) return;

            RecordAsset(path, ResType.Asset); // Texture 通常作为普通 Asset 处理或扩展 ResType
            Global.gApp.gResMgr.LoadAssetAsync<Texture2D>(path, ResType.Asset, (tex) =>
            {
                if (rawImage != null && tex != null)
                {
                    rawImage.texture = tex;
                }
            }, forceGlobal);
        }

        /// <summary>
        /// 自动加载并设置材质 (Renderer)
        /// </summary>
        public void LoadMaterial(Renderer renderer, string path, bool forceGlobal = false)
        {
            if (renderer == null || string.IsNullOrEmpty(path)) return;

            RecordAsset(path, ResType.Material);
            Global.gApp.gResMgr.LoadMaterialAsync(path, (mat) =>
            {
                if (renderer != null && mat != null)
                {
                    renderer.material = mat;
                }
            }, forceGlobal);
        }

        #endregion

        #region 音频播放封装 (Wrapper for AudioMgr)

        /// <summary>
        /// 播放 UI 音效 (2D)
        /// </summary>
        public void PlaySound(string audioKey)
        {
            Global.gApp.gAudioMgr.PlaySFX(audioKey);
        }

        /// <summary>
        /// 播放语音
        /// </summary>
        public void PlayVoice(string audioKey)
        {
            Global.gApp.gAudioMgr.PlayVoice(audioKey);
        }

        #endregion

        #region 内部管理与自动释放

        private void RecordAsset(string path, ResType type)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!m_LoadedAssets.ContainsKey(path))
            {
                m_LoadedAssets.Add(path, type);
            }
        }

        private void ReleaseAssets()
        {
            if (m_LoadedAssets.Count == 0) return;

            foreach (var kv in m_LoadedAssets)
            {
                Global.gApp.gResMgr.UnloadAsset(kv.Key);
            }

            m_LoadedAssets.Clear();
        }
        
        private void DisposeEffect()
        {
            if (m_EffectList != null)
            {
                foreach (var handle in m_EffectList)
                {
                    if (handle != null)
                    {
                        Global.gApp.gEffectMgr.Dispose(handle);
                    }
                }
                m_EffectList.Clear();
            }
            m_EffectDic.Clear();
            m_EffectOrderDic.Clear();
        }
        protected virtual void OnDestroy()
        {
            ReleaseAssets();
            DisposeEffect();
        }
        
        #endregion
    }
}