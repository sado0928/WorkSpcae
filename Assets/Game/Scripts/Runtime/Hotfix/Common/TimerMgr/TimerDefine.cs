using System;
using System.Collections.Generic;

namespace Game.Runtime.Hotfix
{
    public class TimerMap<Tkey,TVal>:IUpdate where TVal:IUpdate
    {
        public int Count
        {
            get { return m_Datas.Count - m_RemoveCache.Count; }
        }
        public int RCount
        {
            get {return m_Datas.Count - m_RemoveCache.Count + m_AddCache.Count; }
        }

        protected Dictionary<Tkey, TVal> m_Datas;
        protected Dictionary<Tkey, TVal> m_AddCache;
        protected Dictionary<Tkey, bool> m_RemoveCache;
        protected int m_TraversalCount = 0;
        protected bool m_IsDirty = false;
        protected bool m_IsClearData = false;
        protected Action<Tkey, TVal, float> m_UpdateAct;
        protected Action<Tkey, TVal, float> m_DirtyUpdateAct;
        protected Action<Tkey, TVal, float> m_CleanUpdateAct;
        public TimerMap()
        {
            m_DirtyUpdateAct = DirtyUpdate;
            m_CleanUpdateAct = CleanUpdate;
            m_UpdateAct = CleanUpdate;
            Init();
        }
        private void Init()
        {
            m_Datas = new Dictionary<Tkey, TVal>();
            m_AddCache = new Dictionary<Tkey, TVal>();
            m_RemoveCache = new Dictionary<Tkey, bool>();
            m_TraversalCount = 0;
            m_IsDirty = false;
        }

        public Dictionary<Tkey, TVal> GetAll()
        {
            return m_Datas;
        }
        public Dictionary<Tkey, TVal> GetAllForDel()
        {
            if (m_AddCache.Count > 0)
            {
                foreach (KeyValuePair<Tkey,TVal> keyValuePair in m_AddCache)
                {
                    m_Datas.Add(keyValuePair.Key, keyValuePair.Value);
                }
            }
            return m_Datas;
        }
        public bool TryGetValue(Tkey key, out TVal value)
        {
            return m_Datas.TryGetValue(key, out value);
        }
        public bool ContainsKey(Tkey key)
        {
            return m_Datas.ContainsKey(key);
        }
        // 如果 add cache 里面有东西 可能会 少遍历 也就是遍历的过程中 增加或者删除了
        public void Foreach(Action<TVal> callBack)
        {
            // 防止在遍历的过程中 报错。导致 遍历失败
            //try
            //{
                if (m_IsClearData)
                {
                    Clear();
                    m_IsClearData = false;
                }
                //m_IsDirty = false;
                m_TraversalCount = m_TraversalCount + 1;
                foreach (KeyValuePair<Tkey, TVal> kv in m_Datas)
                {
                    if (!m_RemoveCache.ContainsKey(kv.Key))
                    {
                        callBack(kv.Value);
                    }
                }
                foreach (KeyValuePair<Tkey, TVal> kv in m_AddCache)
                {
                    if (!m_RemoveCache.ContainsKey(kv.Key))
                    {
                        callBack(kv.Value);
                    }
                }

            //}
            //catch (Exception e)
            //{
            //    UnityEngine.Debug.LogError("msg" + e.Message);
            //}
            //finally
            {
                ClearCacheData();
                if (m_IsClearData)
                {
                    Clear();
                    m_IsClearData = false;
                }
            }
        }
        public TVal Get(Tkey key)
        {
            TVal val;
            if (m_Datas.TryGetValue(key, out val))
            {
                return val;
            }
            else
            {
                return default(TVal);
            }
        }
        public void Add(Tkey key, TVal val)
        {
            if (m_TraversalCount > 0)
            {
                m_AddCache[key] = val;
                m_RemoveCache.Remove(key);
                m_IsDirty = true;
                m_UpdateAct = m_DirtyUpdateAct;
            }
            else
            {
                m_Datas[key] = val;
            }
        }

        public void Remove(Tkey key)
        {
            if (m_TraversalCount > 0)
            {
                // 防止多次调用 报错
                if (!m_RemoveCache.ContainsKey(key))
                {
                    m_RemoveCache.Add(key, true);
                    m_AddCache.Remove(key);
                    m_IsDirty = true;
                    m_UpdateAct = m_DirtyUpdateAct;
                }
            }
            else
            {
                m_Datas.Remove(key);
            }
        }

        public void OnIUpdate(float dt)
        {
//#if !UNITY_EDITOR
//            try
//#endif
            {
                if (m_IsClearData)
                {
                    Clear();
                    m_IsClearData = false;
                }
                m_IsDirty = false;
                m_UpdateAct = m_CleanUpdateAct;
                m_TraversalCount = m_TraversalCount + 1;
                foreach (KeyValuePair<Tkey, TVal> kv in m_Datas)
                {
                    //if (!m_RemoveCache.ContainsKey(kv.Key))
                    //{
                    //    kv.Value.DUpdate(dt);
                    //}
                    m_UpdateAct(kv.Key, kv.Value, dt);
                }

            }
//#if !UNITY_EDITOR

//            catch (Exception e)
//            {
//                UnityEngine.Debug.LogError("msg" + e.Message);
//            }
//            finally
//#endif
            {
                ClearCacheData();
                if (m_IsClearData)
                {
                    Clear();
                    m_IsClearData = false;
                }
            }
        }

        public void OnTimerMapIUpdate(float dt)
        {
#if !UNITY_EDITOR
                        try
#endif
            {
                if (m_IsClearData)
                {
                    Clear();
                    m_IsClearData = false;
                }
                m_IsDirty = false;
                m_UpdateAct = m_CleanUpdateAct;
                m_TraversalCount = m_TraversalCount + 1;
                foreach (KeyValuePair<Tkey, TVal> kv in m_Datas)
                {
                    //if (!m_RemoveCache.ContainsKey(kv.Key))
                    //{
                    //    kv.Value.DUpdate(dt);
                    //}
                    m_UpdateAct(kv.Key, kv.Value, dt);
                }

            }
#if !UNITY_EDITOR

                        catch (Exception e)
                        {
                            UnityEngine.Debug.LogError("msg" + e.Message);
                        }
                        finally
#endif
            {
                ClearCacheData();
                if (m_IsClearData)
                {
                    Clear();
                    m_IsClearData = false;
                }
            }
        }

        private void DirtyUpdate(Tkey tkey,TVal val, float dt)
        {
            if (!m_RemoveCache.ContainsKey(tkey))
            {
                val?.OnIUpdate(dt);
            }
        }
        private void CleanUpdate(Tkey tkey,TVal val, float dt)
        {
            val?.OnIUpdate(dt);
        }
        private void ClearCacheData()
        {
            m_TraversalCount = m_TraversalCount - 1;
            if (m_IsDirty && m_TraversalCount == 0)
            {
                m_IsDirty = false;
                foreach (KeyValuePair<Tkey, TVal> kv in m_AddCache)
                {
                    if (!m_Datas.ContainsKey(kv.Key))
                    {
                        m_Datas.Add(kv.Key, m_AddCache[kv.Key]);
                    }
                }
                m_AddCache.Clear();

                foreach (Tkey key in m_RemoveCache.Keys)
                {
                    m_Datas.Remove(key);
                }
                m_RemoveCache.Clear();
            }
        }
        public TVal this[Tkey index]
        {
            get
            {
                return Get(index);
            }
        }
        public void OnDestroy()
        {
            if (m_TraversalCount == 0)
            {
                Clear();
                m_IsClearData = false;
            }
            else
            {
                m_IsClearData = true;
            }
        }

        public void Clear()
        {
            if (m_TraversalCount == 0)
            {
                m_Datas.Clear();
                m_AddCache.Clear();
                m_RemoveCache.Clear();
                m_TraversalCount = 0;
                m_IsDirty = false;
                m_IsClearData = false;
            }
            else
            {
                m_IsClearData = true;
            }
        }
        public int GetCount()
        {
            return m_Datas.Count;
        }

    }

    public class Timer:IUpdate
    {
        private int m_RepeatForeverTimes = 0;

        private TimerMgr m_TimerMgr;
        private Action<float, bool> m_CallBack;
        private float m_DtTime;
        private float m_CurTime;
        private float m_scale;

        private int m_CallTimes;
        private int m_CurCallTimes;
        private int m_Guid;

        private bool m_IsEnd;

        public Timer(TimerMgr timerMgr, int guid, float dtTime, int callTimes, Action<float, bool> callBack)
        {
            m_TimerMgr = timerMgr;
            m_Guid = guid;
            m_DtTime = dtTime;
            m_CallTimes = callTimes;
            m_CurCallTimes = 0;
            m_CallBack = callBack;
            m_CurTime = 0;
            m_scale = 1;

            m_IsEnd = false;

        }
        public void OnIUpdate(float dt)
        {
            float curTimer = m_CurTime + dt * m_scale;
            if ((curTimer - m_DtTime) >= 0)
            {
                // 修正：减去周期时间，保留误差，防止时间越跑越慢
                curTimer -= m_DtTime;
                
                m_CurCallTimes++;
                CheckEnd();
                
                // 注意：如果 CheckEnd 导致 RemoveSelf，m_IsEnd 会变 true
                // 这里把 dt 传回去可能不准(因为只是触发了一次)，但通常够用
                m_CallBack(dt, m_IsEnd);
            }
            m_CurTime = curTimer;
        }
        public void CheckEnd()
        {
            if (m_CallTimes > m_RepeatForeverTimes && m_CurCallTimes >= m_CallTimes)
            {
                RemoveSelf();
            }
        }
        public void SetTimeScale(float scale = 1)
        {
            m_scale = scale;
        }
        private void RemoveSelf()
        {
            m_TimerMgr.RemoveTimer(m_Guid);
            m_IsEnd = true;
        }
    }
    
    public class FrameTimer : IUpdate
    {
        private int m_RepeatForeverTimes = 0;

        private FrameTimerMgr m_TimerMgr;
        private Action<float, bool> m_CallBack;
        private int m_DtFrame;
        private int m_CurFrame;

        private int m_CallTimes;
        private int m_CurCallTimes;
        private int m_Guid;

        private bool m_IsEnd;

        public FrameTimer(FrameTimerMgr timerMgr, int guid, int dtFrame, int callTimes, Action<float, bool> callBack)
        {
            m_TimerMgr = timerMgr;
            m_Guid = guid;
            m_DtFrame = dtFrame;
            m_CallTimes = callTimes;
            m_CurCallTimes = 0;
            m_CallBack = callBack;
            m_CurFrame = 0;

            m_IsEnd = false;

        }
        public void OnIUpdate(float dt)
        {
            int curTimer = m_CurFrame + 1;
            if (curTimer >= m_DtFrame)
            {
                curTimer = curTimer - m_DtFrame;
                m_CurCallTimes++;
                CheckEnd();
                m_CallBack(dt, m_IsEnd);
            }
            m_CurFrame = curTimer;
        }
        public void CheckEnd()
        {
            if (m_CallTimes > m_RepeatForeverTimes && m_CurCallTimes >= m_CallTimes)
            {
                RemoveSelf();
            }
        }
        private void RemoveSelf()
        {
            m_TimerMgr.RemoveTimer(m_Guid);
            m_IsEnd = true;
        }
    }
}