using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Debug = UnityEngine.Debug;

namespace Game.Runtime.Hotfix
{
    public class Global : MonoBehaviour
    {
        public static App gApp { get;private set; }
        private void Awake()
        {
            gApp = new App();
            gApp.OnAwake(this);
            Application.lowMemory -= OnLowMemory;
            Application.lowMemory += OnLowMemory;
        }
    
        private void Start()
        {
            gApp.OnStart();
        }
    
        // 心跳预留
        private void Update()
        {
            float dtTime = Time.deltaTime;
            if (gApp != null)
            {
                gApp.OnIUpdate(dtTime);
            }        
            
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                gApp.gUIMgr.CloseAll();
            }
            if (Input.GetKeyDown(KeyCode.F1))
            {
                gApp.gUIMgr.LoadUIAsync<HeadUI>(UIDefine.HeadUI);
            }
            if (Input.GetKeyDown(KeyCode.F2))
            {
                gApp.gUIMgr.LoadUIAsync<BagUI>(UIDefine.BagUI);
            }
            if (Input.GetKeyDown(KeyCode.F3))
            {
                gApp.gUIMgr.LoadUIAsync<HeroUI>(UIDefine.HeroUI);
            }
            if (Input.GetKeyDown(KeyCode.F4))
            {
                gApp.gUIMgr.LoadUIAsync<LevelUI>(UIDefine.LevelUI);
            }
            if (Input.GetKeyDown(KeyCode.F5))
            {
                gApp.gUIMgr.LoadUIAsync<HelpUI>(UIDefine.HelpUI);
            }
            if (Input.GetKeyDown(KeyCode.F6))
            {
                gApp.gUIMgr.LoadUIAsync<SettingUI>(UIDefine.SettingUI);
            } 
            if (Input.GetKeyDown(KeyCode.F7))
            {
                gApp.gUIMgr.LoadUIAsync<ShopUI>(UIDefine.ShopUI);
            }
        }
    
        private void OnDestroy()
        {
            gApp.OnDestroy();
        }
        
        // 后台预留
        private void OnApplicationFocus(bool focus)
        {
            
        }
        
        /// <summary>
        /// 内存警告
        /// 当前帧的逻辑可能还在申请新内存（例如加载新资源、实例化对象），这可能成为压死骆驼的最后一根稻草。
        /// 如果先切断引用，但延迟调用 GC，内存实际上并没有归还给操作系统，依然面临被杀风险。
        /// </summary>
        public static void OnLowMemory()
        {
            Debug.LogWarning("[Global] OnLowMemory: Releasing memory...");
            
            if (gApp != null)
            {
                // 1. 强制清理未使用的 UI
                if (gApp.gUIMgr != null)
                {
                    gApp.gUIMgr.ReleaseUnUseUI(true);
                }
                
                // 2. 卸载未使用资源并 GC
                if (gApp.gResMgr != null)
                {
                    gApp.gResMgr.UnLoadAssets();
                }
                
                // 3. 强制清理特效
                if (gApp.gEffectMgr != null)
                {
                    gApp.gEffectMgr.SetEffectIsLimit(true);
                }
            }
        }
        
        [Conditional("DEBUG")]
        public static void LogEditor(object logStr)
        {
            Debug.Log(logStr);
        }
    
        [Conditional("DEBUG")]
        public static void LogErrorEditor(object logStr)
        {
            Debug.LogError(logStr);
        }
    
        public static void LogError(object logStr)
        {
            Debug.LogError(logStr);
        }
        
        public static void QuitGame()
        {
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
    #else
                Application.Quit();
    #endif
        }
    }

}
