using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Hotfix
{
    public class ProcedureMgr:IUpdate
    {
        // 当前流程
        private ProcedureBase m_CurrentProcedure;
        // 流程注册表
        private Dictionary<string, ProcedureBase> m_Procedures = new Dictionary<string, ProcedureBase>();
        
        // 目标流程 Key (在 Loading 期间暂存)
        public string TargetProcedureKey { get; private set; }

        public ProcedureMgr()
        {
            // 注册流程
            AddProcedure(ProcedureDefine.LoadingScene, new ProcedureLoading());
            AddProcedure(ProcedureDefine.MainScene, new ProcedureMain());
        }
        
        public void AddProcedure(string key, ProcedureBase procedure)
        {
            // 获取配置并初始化
            ProcedureConfig config = ProcedureDefine.GetProcedureConfig(key);
            procedure.OnInit(key, config);

            if (!m_Procedures.ContainsKey(key))
            {
                m_Procedures.Add(key, procedure);
            }
        }

        public void ChangeState(string key)
        {
            // 如果目标是 Loading 流程本身，直接切
            if (key == ProcedureDefine.LoadingScene)
            {
                SwitchInternal(key);
                return;
            }

            // 如果当前已经在 Loading 流程中，不要重复切 Loading，直接更新目标
            if (m_CurrentProcedure != null && m_CurrentProcedure.Key == ProcedureDefine.LoadingScene)
            {
                TargetProcedureKey = key;
                return;
            }

            // 正常业务切换：拦截！先切到 Loading
            TargetProcedureKey = key;
            SwitchInternal(ProcedureDefine.LoadingScene);
        }

        /// <summary>
        /// Loading 流程结束后的回调，由 ProcedureLoading 调用
        /// </summary>
        public void OnLoadingComplete()
        {
            if (!string.IsNullOrEmpty(TargetProcedureKey))
            {
                SwitchInternal(TargetProcedureKey);
                TargetProcedureKey = null;
            }
            else
            {
                Debug.LogError("[ProcedureMgr] Loading completed but no target procedure set!");
            }
        }

        private void SwitchInternal(string key)
        {
            if (m_Procedures.TryGetValue(key, out ProcedureBase targetProcedure))
            {
                if (m_CurrentProcedure != null)
                {
                    m_CurrentProcedure.OnLeave();
                }
                
                m_CurrentProcedure = targetProcedure;
                Debug.Log($"[ProcedureMgr] Switch to: {key}");
                m_CurrentProcedure.OnEnter();
            }
            else
            {
                Debug.LogError($"[ProcedureMgr] Procedure not found: {key}");
            }
        }

        public void OnIUpdate(float dt)
        {
            if (m_CurrentProcedure != null)
            {
                m_CurrentProcedure.OnUpdate(dt);
            }
        }

        public void OnDestroy()
        {
            foreach (var kv in m_Procedures)
            {
                kv.Value.OnDestroy();
            }
            m_Procedures.Clear();
        }
    }
}
