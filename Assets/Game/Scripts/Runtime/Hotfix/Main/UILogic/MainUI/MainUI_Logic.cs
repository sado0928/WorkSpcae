using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Hotfix
{
    public partial class MainUI
    {
        protected override void OnInit()
        {
            Global.LogError("进入到主界面了");
            item tbitem1 = Global.gApp.gCfgMgr.Tables.Tbitem.Get(1001);
            Global.LogError($"测试表数据 ： name{tbitem1.Name}"); 
            item tbitem3 = Global.gApp.gCfgMgr.Tables.Tbitem.Get(1003);
            Global.LogError($"测试表数据 ： name{tbitem3.Name}"); 
        }

        protected override void OnClose()
        {
            
        }       
    }
}