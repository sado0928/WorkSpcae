using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Hotfix
{
    public partial class MainUI:UIBase
    {
        
        protected override void OnInit()
        {
            Global.LogError("哈哈哈哈哈哈我要成功了吗");
        }
        
        protected override void OnClose()
        {
        }

        protected override void OnRefresh()
        {
        }
    }
}