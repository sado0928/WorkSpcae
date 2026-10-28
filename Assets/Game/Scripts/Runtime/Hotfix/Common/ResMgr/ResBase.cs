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
        private Dictionary<string, object> m_LoadedAssets = new Dictionary<string, object>();
        private readonly Dictionary<string, Action> m_ReleaseActions = new Dictionary<string, Action>();
        private readonly Dictionary<string, List<Action<UnityEngine.Object>>> m_LoadingAssets = new Dictionary<string, List<Action<UnityEngine.Object>>>();
        private bool m_ResourcesDestroyed;
        
        // 托管特效列表：UI 销毁时自动回收关联特效 key => instanceID
        private Dictionary<int,EffectBase> m_EffectDic = new Dictionary<int,EffectBase>();
        private List<EffectBase> m_EffectList = new List<EffectBase>();
        
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
        public void PlayEffect(string path, Transform parent,bool isLoop = false,float duration = -1f )
        {
            if (string.IsNullOrEmpty(path))
            {
                Global.LogError("error by ResBase function for PlayEffect , path is empty");
                return ;
            }
            if (parent == null)
            {
                Global.LogError("error by ResBase function for PlayEffect , parent is empty");
                return ;
            }
            
            // 如果是同一个父节点需要清理一下老特效
            int parentId = parent.GetInstanceID();
            int parentLayer = parent.gameObject.layer;
            if (m_EffectDic.TryGetValue(parentId,out EffectBase effectBase))
            {
                m_EffectDic.Remove(parentId);
                m_EffectList.Remove(effectBase);
                Global.gApp.gEffectMgr.Dispose(effectBase);
            }
            
            // 向上查找第一个 Canvas 
            Canvas canvas = parent.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Global.LogError("error by ResBase function for PlayEffect , parent canvas is not found");
                return ;
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
            
            Global.gApp.gEffectMgr.PlayEffect<EffectBase>(path, parent,isLoop,duration).SetCallback((effectBase) =>
            {
                if (effectBase != null && canvas != null)
                {
                    effectBase.SetGameObjectLayer(parentLayer);
                    effectBase.SetSortingOrder(sortingOrder);
                }
                m_EffectDic.Add(parentId,effectBase);
                m_EffectList.Add(effectBase);
            });
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

            LoadResourceAsync<Sprite>(path, ResType.Sprite, (sprite) =>
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

            // Texture 通常作为普通 Asset 处理或扩展 ResType
            LoadResourceAsync<Texture2D>(path, ResType.Asset, (tex) =>
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

            LoadResourceAsync<Material>(path, ResType.Material, (mat) =>
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

        private void RecordAsset(string path, object asset)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!m_LoadedAssets.ContainsKey(path))
            {
                m_LoadedAssets.Add(path, asset);
            }
        }

        private void LoadResourceAsync<T>(string path, ResType type, Action<T> callback, bool forceGlobal) where T : UnityEngine.Object
        {
            if (m_ResourcesDestroyed) return;
            if (m_LoadedAssets.TryGetValue(path, out var existing))
            {
                // 同一对象再次使用资源，直接返回已加载实例，不重复增加 ResMgr 引用计数。
                callback(existing as T);
                return;
            }
            if (m_LoadingAssets.TryGetValue(path, out var waiting))
            {
                // 首次请求还未完成时也合并请求，避免连续调用造成多次计数。
                waiting.Add(asset => callback(asset as T));
                return;
            }
            waiting = new List<Action<UnityEngine.Object>> { asset => callback(asset as T) };
            m_LoadingAssets.Add(path, waiting);
            Global.gApp.gResMgr.LoadAssetTrackedAsync<T>(path, type, (asset, release) =>
            {
                m_LoadingAssets.Remove(path);
                if (m_ResourcesDestroyed || this == null)
                {
                    // UI 已销毁才完成加载：立即归还本次成功加载的引用。
                    release?.Invoke();
                    return;
                }
                if (asset != null)
                {
                    RecordAsset(path, asset);
                    m_ReleaseActions[path] = release;
                }
                foreach (var done in waiting)
                {
                    if (m_ResourcesDestroyed) break;
                    try { done(asset); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }, forceGlobal);
        }

        private void ReleaseAssets()
        {
            if (m_LoadedAssets.Count == 0) return;

            foreach (var kv in m_LoadedAssets)
            {
                // 归还时使用加载时记录的域，Global 共享资源不会减错到当前场景。
                m_ReleaseActions[kv.Key]?.Invoke();
            }

            m_LoadedAssets.Clear();
            m_ReleaseActions.Clear();
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
            m_ResourcesDestroyed = true;
            m_LoadingAssets.Clear();
            ReleaseAssets();
            DisposeEffect();
        }
        
        #endregion
    }
}
