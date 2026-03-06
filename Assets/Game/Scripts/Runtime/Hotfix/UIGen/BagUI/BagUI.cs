using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Runtime.Hotfix
{
    public partial class BagUI : UIBase
    {
        [SerializeField] private RectTransform_Image_Container m_Bg;
        public RectTransform_Image_Container Bg => m_Bg;
        [SerializeField] private RectTransform_Container m_Eff;
        public RectTransform_Container Eff => m_Eff;
    }
}
