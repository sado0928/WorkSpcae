using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Runtime.Hotfix
{
    [System.Serializable]
    public class RectTransform_Image_Container
    {
        [SerializeField] private GameObject m_gameObject;
        public GameObject gameObject => m_gameObject;
        [SerializeField] private RectTransform m_rectTransform;
        public RectTransform rectTransform => m_rectTransform;
        [SerializeField] private Image m_image;
        public Image image => m_image;
        public RectTransform_Image_Container() { }
    }
}
