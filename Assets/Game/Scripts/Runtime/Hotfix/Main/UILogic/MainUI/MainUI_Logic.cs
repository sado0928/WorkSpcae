using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Hotfix
{
    public partial class MainUI
    {
        protected override void OnInit()
        {
            Global.LogError("进入到主界面了");
            hero tbhero1 = Tbhero.Data.Get(1001);
            Global.LogError($"测试表数据 ： name{tbhero1.Name}"); 
        }

        protected override void OnClose()
        {
            
        }       
    }
}