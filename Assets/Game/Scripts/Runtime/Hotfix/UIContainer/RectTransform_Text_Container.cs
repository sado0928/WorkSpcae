using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Runtime.Hotfix
{
    [System.Serializable]
    public class RectTransform_Text_Container
    {
        [SerializeField] private GameObject m_gameObject;
        public GameObject gameObject => m_gameObject;
        [SerializeField] private RectTransform m_rectTransform;
        public RectTransform rectTransform => m_rectTransform;
        [SerializeField] private Text m_text;
        public Text text => m_text;
        public RectTransform_Text_Container() { }
    }
}
