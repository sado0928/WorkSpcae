using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Hotfix
{
    public partial class HeadUI:UIBase
    {
        
        protected override void OnInit()
        {
            Global.LogError("HeadUI界面!");
        }
         
        protected override void OnClose()
        {
        }

        protected override void OnRefresh()
        {
        }
    }
}