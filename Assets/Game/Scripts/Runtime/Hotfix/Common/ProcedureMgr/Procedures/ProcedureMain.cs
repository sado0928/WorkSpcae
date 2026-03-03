using UnityEngine;

namespace Game.Runtime.Hotfix
{
    public class ProcedureMain : ProcedureBase
    {
        public override void OnEnter()
        {
            Global.gApp.gUIMgr.OpenUIAsync(UIDefine.MainUI);
            Global.gApp.gAudioMgr.PlayBGM(AudioDefine.MainBgm,false);
        }

        public override void OnUpdate(float dt)
        {
            // 业务逻辑
        }

        public override void OnLeave()
        {
            Global.gApp.gUIMgr.CloseUI(UIDefine.MainUI);
            Global.gApp.gAudioMgr.StopBGM();
        }

        public override void OnDestroy()
        {
        }
    }
}