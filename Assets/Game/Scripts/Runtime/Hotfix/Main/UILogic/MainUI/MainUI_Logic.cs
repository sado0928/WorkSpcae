using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Hotfix
{
    public partial class MainUI
    {
        private EntityHandle m_Test;

        protected override void OnInit()
        {
            Global.LogError("进入到主界面了");
            hero tbhero1 = Tbhero.Data.Get(1001);
            Global.LogError($"测试表数据 ： name{tbhero1.Name}");
            // m_Test = Global.gApp.gEntityMgr.CreateEntity("Prefabs/Hero/1001/Hero1001",EntityType.Hero);
        }

        protected override void OnClose()
        {
            
        }

        private void Update()
        {
            // if (Input.GetKeyDown(KeyCode.D))
            // {
            //     if (m_Test != null)
            //     {
            //         Global.gApp.gEntityMgr.Dispose(m_Test);
            //     }
            // }
        }
    }
}