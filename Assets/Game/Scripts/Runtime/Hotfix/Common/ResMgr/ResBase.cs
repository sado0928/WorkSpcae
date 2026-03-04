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

        #region 极简资源加载接口 (Simplified API)

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

        private void ReleaseTrackedAssets()
        {
            if (m_LoadedAssets.Count == 0) return;

            foreach (var kv in m_LoadedAssets)
            {
                Global.gApp.gResMgr.UnloadAsset(kv.Key);
            }

            m_LoadedAssets.Clear();
        }

        protected virtual void OnDestroy()
        {
            ReleaseTrackedAssets();
        }

        #endregion
    }
}