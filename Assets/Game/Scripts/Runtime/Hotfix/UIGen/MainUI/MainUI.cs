using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Runtime.Hotfix
{
    public partial class MainUI : UIBase
    {
        [SerializeField] private RectTransform_Image_Button_Container m_Head;
        public RectTransform_Image_Button_Container Head => m_Head;
        [SerializeField] private RectTransform_Container m_HLayout;
        public RectTransform_Container HLayout => m_HLayout;
        [SerializeField] private RectTransform_MainUI_MenuItem_Container m_MenuItem;
        public RectTransform_MainUI_MenuItem_Container MenuItem => m_MenuItem;

        [System.Serializable]
        public class RectTransform_MainUI_MenuItem_Container {
            [SerializeField] private GameObject m_gameObject;
            public GameObject gameObject => m_gameObject;
            [SerializeField] private RectTransform m_rectTransform;
            public RectTransform rectTransform => m_rectTransform;
            [SerializeField] private MainUI_MenuItem m_MenuItem;
            public MainUI_MenuItem MenuItem => m_MenuItem;

            [System.NonSerialized] public List<MainUI_MenuItem> mCachedList = new List<MainUI_MenuItem>();
            private Queue<MainUI_MenuItem> mCachedInstances;
            public MainUI_MenuItem GetInstance(bool ignoreSibling = false) {
                MainUI_MenuItem instance = null;
                if (mCachedInstances != null) while ((instance == null || instance.Equals(null)) && mCachedInstances.Count > 0) instance = mCachedInstances.Dequeue();
                if (instance == null || instance.Equals(null)) instance = Instantiate<MainUI_MenuItem>(m_MenuItem);
                Transform t0 = m_MenuItem.transform; Transform t1 = instance.transform;
                t1.SetParent(t0.parent, false); t1.localPosition = t0.localPosition; t1.localRotation = t0.localRotation; t1.localScale = t0.localScale;
                if (!ignoreSibling) t1.SetSiblingIndex(t0.GetSiblingIndex() + 1); else t1.SetAsLastSibling();
                instance.gameObject.SetActive(true); mCachedList.Add(instance); return instance;
            }
            public void CacheInstance(MainUI_MenuItem instance) {
                if (instance == null) return;
                if (mCachedInstances == null) mCachedInstances = new Queue<MainUI_MenuItem>();
                if (!mCachedInstances.Contains(instance)) { instance.gameObject.SetActive(false); mCachedInstances.Enqueue(instance); }
            }
            public void CacheInstanceList() { foreach (var instance in mCachedList) CacheInstance(instance); mCachedList.Clear(); }
        }
    }
}
