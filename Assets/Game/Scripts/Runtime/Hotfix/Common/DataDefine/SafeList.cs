using System;
using System.Collections.Generic;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 安全列表：支持在遍历过程中进行 Add 和 Remove 操作。
    /// 原理：通过双缓冲机制，将遍历期间的修改暂存在 Cache 中，遍历结束后统一合并。
    /// </summary>
    /// <typeparam name="TVal">元素类型</typeparam>
    public class SafeList<TVal> : IUpdate where TVal : IUpdate
    {
        // 主数据源
        protected List<TVal> m_Datas = new List<TVal>();
        
        // 缓冲池：用于暂存遍历期间的新增和删除请求
        protected List<TVal> m_AddCache = new List<TVal>();
        protected HashSet<TVal> m_RemoveCache = new HashSet<TVal>();

        // 状态标记
        protected int m_TraversalCount = 0; // 当前遍历嵌套层数
        protected bool m_IsDirty = false;    // 是否有待处理的缓冲数据
        protected bool m_NeedsClear = false; // 是否有待处理的清空请求

        #region 属性获取
        
        /// <summary>
        /// 获取当前生效的数据量（不含缓冲）
        /// </summary>
        public int Count => m_Datas.Count - m_RemoveCache.Count;

        /// <summary>
        /// 获取底层 List 引用（非必要请勿直接操作）
        /// </summary>
        public List<TVal> RawList => m_Datas;

        #endregion

        #region 核心操作接口

        public void Add(TVal val)
        {
            if (val == null) return;

            // 如果正在遍历，存入缓冲
            if (m_TraversalCount > 0)
            {
                m_AddCache.Add(val);
                m_RemoveCache.Remove(val); // 如果之前在删除缓冲里，将其移出
                m_IsDirty = true;
            }
            else
            {
                // 非遍历状态，直接操作
                if (!m_Datas.Contains(val))
                {
                    m_Datas.Add(val);
                }
            }
        }

        public void Remove(TVal val)
        {
            if (val == null) return;

            if (m_TraversalCount > 0)
            {
                m_RemoveCache.Add(val);
                m_AddCache.Remove(val); // 如果之前在新增缓冲里，将其移出
                m_IsDirty = true;
            }
            else
            {
                m_Datas.Remove(val);
            }
        }

        public void Clear()
        {
            // 如果正在遍历，标记为待清空
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

        #endregion

        #region 遍历与逻辑驱动

        /// <summary>
        /// 通用遍历接口
        /// </summary>
        public void Foreach(Action<TVal> action)
        {
            if (action == null) return;

            BeginTraversal();
            
            foreach (var item in m_Datas)
            {
                // 跳过已标记删除的元素
                if (m_RemoveCache.Contains(item)) continue;
                action(item);
            }

            EndTraversal();
        }

        /// <summary>
        /// 每帧更新驱动
        /// </summary>
        public void OnIUpdate(float dt)
        {
            BeginTraversal();

            // 只有未被标记删除的元素才执行 Update
            foreach (var item in m_Datas)
            {
                if (m_RemoveCache.Contains(item)) continue;
                item?.OnIUpdate(dt);
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
            
            // 只有当最外层遍历结束时，才应用所有变更
            if (m_TraversalCount <= 0)
            {
                m_TraversalCount = 0; // 安全修正
                if (m_IsDirty)
                {
                    ApplyCacheChanges();
                }
            }
        }

        /// <summary>
        /// 应用缓冲区中的修改
        /// </summary>
        protected void ApplyCacheChanges()
        {
            // 优先处理清空请求
            if (m_NeedsClear)
            {
                InternalClear();
                return;
            }

            // 处理删除
            if (m_RemoveCache.Count > 0)
            {
                foreach (var item in m_RemoveCache)
                {
                    m_Datas.Remove(item);
                }
                m_RemoveCache.Clear();
            }

            // 处理新增
            if (m_AddCache.Count > 0)
            {
                foreach (var item in m_AddCache)
                {
                    if (!m_Datas.Contains(item)) m_Datas.Add(item);
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
    }
}
