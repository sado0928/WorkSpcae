using UnityEngine;

namespace Game.Runtime.Hotfix
{
    public class ProcedureLoading : ProcedureBase
    {
        private float m_Progress;
        private bool m_IsLoadingScene;
        private bool m_IsSceneLoaded;
        
        // 假进度参数
        private float m_FakeProgressSpeed = 1.0f; // 1秒走完假进度
        private float m_FakeProgressTarget = 0.98f;
        
        public override void OnEnter()
        {
            Debug.Log("[ProcedureLoading] Enter Loading...");
            m_Progress = 0f;
            m_IsLoadingScene = false;
            m_IsSceneLoaded = false;

            // 1. 打开 Loading UI，并等待其加载完成
            Global.gApp.gUIMgr.OpenUIAsync<LoadingUI>(UIDefine.LoadingUI).SetCallback((ui) =>
            {
                // UI 加载完毕，进度设为 0
                if (ui != null) ui.SetProgress(0f);

                // 2. 只有当遮罩(LoadingUI)盖住屏幕后，才开始加载过渡场景
                Global.gApp.gResMgr.LoadSceneAsync(Config.Path, ResTypeByScene.Global, () =>
                {
                    // Loading 场景加载完毕，开始异步加载目标场景
                    LoadTargetScene();
                });
            });
        }

        private void LoadTargetScene()
        {
            string targetKey = Global.gApp.gProcedureMgr.TargetProcedureKey;
            ProcedureConfig targetConfig = ProcedureDefine.GetProcedureConfig(targetKey);

            if (targetConfig == null)
            {
                Debug.LogError($"[ProcedureLoading] Target config not found for: {targetKey}");
                // 强制结束防卡死
                m_IsSceneLoaded = true;
                return;
            }

            m_IsLoadingScene = true;
            // 开始加载目标场景
            Global.gApp.gResMgr.LoadSceneAsync(targetConfig.Path, targetConfig.ResTypeByScene, () =>
            {
                m_IsSceneLoaded = true;
            });
        }

        public override void OnUpdate(float dt)
        {
            // 1. 模拟假进度 (0 -> 0.9)
            if (m_Progress < m_FakeProgressTarget)
            {
                m_Progress += dt / m_FakeProgressSpeed;
                if (m_Progress > m_FakeProgressTarget) m_Progress = m_FakeProgressTarget;
            }

            // 2. 如果目标场景已加载完，且假进度也跑完了，冲刺到 1.0
            if (m_IsSceneLoaded && m_Progress >= m_FakeProgressTarget)
            {
                m_Progress = 1.0f;
                FinishLoading();
            }

            // 更新 LoadingUI 进度条
            LoadingUI loadingUI = Global.gApp.gUIMgr.GetUI<LoadingUI>(UIDefine.LoadingUI);
            if (loadingUI != null)
            {
                loadingUI.SetProgress(m_Progress);
            }
        }

        private void FinishLoading()
        {
            // 防止重复调用
            if (!m_IsLoadingScene) return;
            m_IsLoadingScene = false;

            // 通知管理器加载完成，切换到目标状态
            Global.gApp.gProcedureMgr.OnLoadingComplete();
        }

        public override void OnLeave()
        {
            // 关闭 Loading UI
            Global.gApp.gUIMgr.Close(UIDefine.LoadingUI);
        }

        public override void OnDestroy()
        {
        }
    }
}