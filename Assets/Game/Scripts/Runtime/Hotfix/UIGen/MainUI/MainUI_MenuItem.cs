using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Runtime.Hotfix
{
    public partial class MainUI_MenuItem : ResBase
    {
        [SerializeField] private RectTransform_Image_Button_Container m_button;
        public RectTransform_Image_Button_Container button => m_button;
        [SerializeField] private RectTransform_Text_Container m_text;
        public RectTransform_Text_Container text => m_text;
        [SerializeField] private RectTransform_Container m_Bg;
        public RectTransform_Container Bg => m_Bg;
    }
}
