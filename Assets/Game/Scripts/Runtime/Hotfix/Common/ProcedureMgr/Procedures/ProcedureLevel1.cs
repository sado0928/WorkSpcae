namespace Game.Runtime.Hotfix
{
    public class ProcedureLevel1:ProcedureBase
    {
        public override void OnEnter()
        {
            Global.gApp.gUIMgr.OpenUIAsync<MainUI>(UIDefine.MainUI).SetCallback(ui =>
            {
                Global.gApp.gDispatcherMgr.Dispatch(EventDefine.LoadingFinish,true);
                Global.gApp.gAudioMgr.PlayBGM(AudioDefine.MainBgm,false);
            });
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