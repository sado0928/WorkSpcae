using UnityEngine;

namespace Game.Runtime.Hotfix
{
    public class App : IUpdate
    {
        public Global gGlobal { get; private set; }
        public Transform m_KeepNode { get; private set; }
        public Canvas gCanvas { get; private set; }
        public Camera gUICamera { get; private set; }
        public DispatcherMgr gDispatcherMgr { get; set; }
        public ResMgr gResMgr { get; private set; }
        public TimerMgr gTimerMgr { get; private set; }
        public FrameTimerMgr gFrameTimerMgr { get; private set; }
        public AudioMgr gAudioMgr { get; private set; }
        public UIMgr gUIMgr { get; private set; }
        public PoolMgr gPoolMgr { get; private set; }
        public EffectMgr gEffectMgr { get; private set; }
        public ProcedureMgr gProcedureMgr { get; private set; }

        public void OnAwake(Global global)
        {
            gGlobal = global;
            m_KeepNode = gGlobal.transform.parent.transform;
            gCanvas = m_KeepNode.transform.Find("Canvas").GetComponent<Canvas>();
            gUICamera = m_KeepNode.transform.Find("CameraNode/UICamera").GetComponent<Camera>();
        }

        public void OnStart()
        {
            // 初始化管理类
            gDispatcherMgr = new DispatcherMgr();
            gResMgr = new ResMgr();
            gTimerMgr = new TimerMgr();
            gFrameTimerMgr = new FrameTimerMgr();
            gPoolMgr = new PoolMgr();
            gEffectMgr = new EffectMgr();
            gAudioMgr = new AudioMgr();
            gUIMgr = new UIMgr();

            // 初始化流程管理器
            gProcedureMgr = new ProcedureMgr();

            Debug.Log("=== Hotfix: App Started, Managers Initialized ===");

            // 启动主流程
            gProcedureMgr.ChangeState(ProcedureDefine.MainScene);
        }

        public void OnIUpdate(float dt)
        {
            if (gTimerMgr != null) gTimerMgr.OnIUpdate(dt);
            if (gFrameTimerMgr != null) gFrameTimerMgr.OnIUpdate(dt);
            if (gAudioMgr != null) gAudioMgr.OnIUpdate(dt);
            if (gUIMgr != null) gUIMgr.OnIUpdate(dt);
            if (gEffectMgr != null) gEffectMgr.OnIUpdate(dt);
            if (gProcedureMgr != null) gProcedureMgr.OnIUpdate(dt);
        }

        public void OnDestroy()
        {
            if (gAudioMgr != null) gAudioMgr.OnDestroy();
            if (gProcedureMgr != null) gProcedureMgr.OnDestroy();
            if (gResMgr != null) gResMgr.OnDestroy();
            if (gPoolMgr != null) gPoolMgr.OnDestroy();
            if (gEffectMgr != null) gEffectMgr.OnDestroy();
        }
    }
}
