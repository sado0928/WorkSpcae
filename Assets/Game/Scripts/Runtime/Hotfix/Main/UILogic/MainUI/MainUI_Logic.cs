using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Hotfix
{
    public partial class MainUI
    {
        protected override void OnInit()
        {
            Global.LogError("进入到主界面了");
        }

        protected override void OnClose()
        {
            
        }       
    }
}