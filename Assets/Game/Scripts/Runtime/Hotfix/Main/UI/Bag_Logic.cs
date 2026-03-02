using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Hotfix
{
    public partial class BagUI:UIBase
    {
        
        protected override void OnInit()
        {
            Global.LogError("BagUI界面!");
        }
         
        protected override void OnClose()
        {
        }

        protected override void OnRefresh()
        {
        }
    }
}