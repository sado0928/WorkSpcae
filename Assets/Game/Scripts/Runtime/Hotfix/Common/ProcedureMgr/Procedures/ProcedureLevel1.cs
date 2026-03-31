using UnityEngine;

namespace Game.Runtime.Hotfix
{
    public class ProcedureLevel1 : ProcedureBase
    {
        public override void OnEnter()
        {
            Global.gApp.gBattleMgr.m_WorldCamera.gameObject.SetActive(true);
            Global.gApp.gBattleMgr.OnStartBattle();
            Global.gApp.gDispatcherMgr.Dispatch(EventDefine.LoadingFinish, true);
        }

        public override void OnUpdate(float dt)
        {
            // 这里可以处理一些关卡特有的逻辑，如波次控制
        }

        public override void OnLeave()
        {
            Global.gApp.gBattleMgr.OnStopBattle();
            Global.gApp.gBattleMgr.m_WorldCamera.gameObject.SetActive(false);
        }

        public override void OnDestroy()
        {
        }
    }
}
