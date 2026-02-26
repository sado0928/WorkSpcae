using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Hotfix
{
    public partial class LoadingUI:UIBase
    {
        private Slider m_Slider;

        protected override void OnInit()
        {
            // 在子节点中寻找 Slider 组组件
            m_Slider = GetComponentInChildren<Slider>(true);
            if (m_Slider == null)
            {
                Debug.LogError("[LoadingUI] No Slider component found in children!");
            }
        }

        public void SetProgress(float progress)
        {
            if (m_Slider != null)
            {
                m_Slider.value = progress;
            }
        }

        protected override void OnClose()
        {
        }

        protected override void OnRefresh()
        {
        }
    }
}