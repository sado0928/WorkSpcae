using System;
using System.Collections.Generic;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 安全字典：支持在遍历过程中进行 Add 和 Remove 操作。
    /// 原理：基于 Dictionary 封装，提供线程安全且遍历安全的缓存机制。
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TVal">值类型</typeparam>
    public class SafeMap<TKey, TVal> : IUpdate where TVal : IUpdate
    {
        // 主数据源
        protected Dictionary<TKey, TVal> m_Datas = new Dictionary<TKey, TVal>();
        
        // 缓冲池
        protected Dictionary<TKey, TVal> m_AddCache = new Dictionary<TKey, TVal>();
        protected HashSet<TKey> m_RemoveCache = new HashSet<TKey>();

        // 状态标记
        protected int m_TraversalCount = 0;
        protected bool m_IsDirty = false;
        protected bool m_NeedsClear = false;

        #region 属性获取

        /// <summary>
        /// 获取当前生效的数据量（不含缓冲）
        /// </summary>
        public int Count => m_Datas.Count - m_RemoveCache.Count;

        /// <summary>
        /// 获取底层 Dictionary 引用
        /// </summary>
        public Dictionary<TKey, TVal> RawMap => m_Datas;

        #endregion

        #region 核心操作接口

        public void Add(TKey key, TVal val)
        {
            if (key == null || val == null) return;

            if (m_TraversalCount > 0)
            {
                m_AddCache[key] = val;
                m_RemoveCache.Remove(key); // 从删除缓冲中移除
                m_IsDirty = true;
            }
            else
            {
                m_Datas[key] = val;
            }
        }

        public void Remove(TKey key)
        {
            if (key == null) return;

            if (m_TraversalCount > 0)
            {
                m_RemoveCache.Add(key);
                m_AddCache.Remove(key); // 如果在新增缓冲中，移出
                m_IsDirty = true;
            }
            else
            {
                m_Datas.Remove(key);
            }
        }

        public void Clear()
        {
            if (m_TraversalCount > 0)
            {
                m_NeedsClear = true;
                m_IsDirty = true;
            }
            else
            {
                InternalClear();
            }
        }

        private void InternalClear()
        {
            m_Datas.Clear();
            m_AddCache.Clear();
            m_RemoveCache.Clear();
            m_IsDirty = false;
            m_NeedsClear = false;
        }

        public bool TryGetValue(TKey key, out TVal value)
        {
            // 优先查找主库
            if (m_Datas.TryGetValue(key, out value))
            {
                // 如果主库有但标记为删除了，则视为没有
                if (m_RemoveCache.Contains(key)) return false;
                return true;
            }
            
            // 查找新增缓存
            return m_AddCache.TryGetValue(key, out value);
        }

        public bool ContainsKey(TKey key)
        {
            if (m_Datas.ContainsKey(key))
            {
                return !m_RemoveCache.Contains(key);
            }
            return m_AddCache.ContainsKey(key);
        }

        #endregion

        #region 遍历与逻辑驱动

        public void Foreach(Action<TVal> action)
        {
            if (action == null) return;

            BeginTraversal();

            foreach (var kv in m_Datas)
            {
                // 跳过标记删除的项
                if (m_RemoveCache.Contains(kv.Key)) continue;
                action(kv.Value);
            }

            EndTraversal();
        }

        public void OnIUpdate(float dt)
        {
            BeginTraversal();

            foreach (var kv in m_Datas)
            {
                if (m_RemoveCache.Contains(kv.Key)) continue;
                kv.Value?.OnIUpdate(dt);
            }

            EndTraversal();
        }

        #endregion

        #region 内部缓冲管理

        protected void BeginTraversal()
        {
            m_TraversalCount++;
        }

        protected void EndTraversal()
        {
            m_TraversalCount--;
            if (m_TraversalCount <= 0)
            {
                m_TraversalCount = 0;
                if (m_IsDirty)
                {
                    ApplyCacheChanges();
                }
            }
        }

        protected void ApplyCacheChanges()
        {
            if (m_NeedsClear)
            {
                InternalClear();
                return;
            }

            // 处理删除
            if (m_RemoveCache.Count > 0)
            {
                foreach (var key in m_RemoveCache)
                {
                    m_Datas.Remove(key);
                }
                m_RemoveCache.Clear();
            }

            // 处理新增
            if (m_AddCache.Count > 0)
            {
                foreach (var kv in m_AddCache)
                {
                    m_Datas[kv.Key] = kv.Value;
                }
                m_AddCache.Clear();
            }

            m_IsDirty = false;
        }

        #endregion

        public void OnDestroy()
        {
            Clear();
        }

        // 索引器
        public TVal this[TKey key]
        {
            get
            {
                TryGetValue(key, out var val);
                return val;
            }
            set => Add(key, value);
        }
    }
}
