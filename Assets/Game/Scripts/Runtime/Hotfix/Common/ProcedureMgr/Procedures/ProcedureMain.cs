using UnityEngine;

namespace Game.Runtime.Hotfix
{
    public class ProcedureMain : ProcedureBase
    {
        public override void OnEnter()
        {
            Global.gApp.gUIMgr.OpenUIAsync(UIDefine.MainUI);
        }

        public override void OnUpdate(float dt)
        {
            // 业务逻辑
        }

        public override void OnLeave()
        {
            Global.gApp.gUIMgr.Close(UIDefine.MainUI);
        }

        public override void OnDestroy()
        {
        }
    }
}