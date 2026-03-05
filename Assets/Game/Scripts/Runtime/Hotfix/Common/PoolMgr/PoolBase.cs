using UnityEngine;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 池对象基类
    /// 职责：所有需要池化的 Prefab 根节点必须挂载该组件。
    /// </summary>
    public abstract class PoolBase : MonoBehaviour
    {
        // 基础的设置
        private Vector3 m_InitPosition;
        private Quaternion m_InitRotation;
        private Vector3 m_InitScale;
        
        // 资源归属场景域
        public ResTypeByScene ResTypeByScene;
        // 资源地址
        public string Path { get; private set; }

        // 是否处于使用中
        public bool IsSpawned { get; private set; }

        /// <summary>
        /// 初始化
        /// </summary>
        public virtual void OnInit(string key, ResTypeByScene resTypeByScene)
        {
            Path = key;
            ResTypeByScene = resTypeByScene;
        }

        /// <summary>
        /// 从池中取出时调用
        /// </summary>
        public void InternalOnSpawn()
        {
            IsSpawned = true;
            
            m_InitPosition = transform.localPosition;
            m_InitScale = transform.localScale;
            m_InitRotation = transform.localRotation;
            
            OnSpawn();
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 归还到池中时调用
        /// </summary>
        public void InternalOnDespawn()
        {
            IsSpawned = false;
            
            OnDespawn();
            gameObject.SetActive(false);
            
            transform.localPosition = m_InitPosition;
            transform.localScale = m_InitScale;
            transform.localRotation = m_InitRotation;
        }

        // 业务钩子由具体子类实现
        protected abstract void OnSpawn();
        protected abstract void OnDespawn();
    }
}
