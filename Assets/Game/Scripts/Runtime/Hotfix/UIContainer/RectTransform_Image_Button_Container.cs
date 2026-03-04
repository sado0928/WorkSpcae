using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Runtime.Hotfix
{
    [System.Serializable]
    public class RectTransform_Image_Button_Container
    {
        [SerializeField] private GameObject m_gameObject;
        public GameObject gameObject => m_gameObject;
        [SerializeField] private RectTransform m_rectTransform;
        public RectTransform rectTransform => m_rectTransform;
        [SerializeField] private Image m_image;
        public Image image => m_image;
        [SerializeField] private Button m_button;
        public Button button => m_button;
        public RectTransform_Image_Button_Container() { }
    }
}
