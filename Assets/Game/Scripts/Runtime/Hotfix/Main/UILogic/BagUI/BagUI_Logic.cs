using UnityEngine;
namespace Game.Runtime.Hotfix
{
    public partial class BagUI
    {
        protected override void OnInit()
        {
            PlayEffect("Prefabs/Effect/eff_test", m_Eff.rectTransform,true);
        }
        protected override void OnClose()
        {
        }
    }
}