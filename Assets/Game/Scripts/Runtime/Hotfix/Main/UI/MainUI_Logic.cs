using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Hotfix
{
    public partial class MainUI:UIBase
    {
        
        protected override void OnInit()
        {
            Global.LogError("进入到MainUI界面!");
        }
         
        protected override void OnClose()
        {
        }
    }
}