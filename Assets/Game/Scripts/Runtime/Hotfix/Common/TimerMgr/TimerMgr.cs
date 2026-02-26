using System;

namespace Game.Runtime.Hotfix
{
    public class TimerMgr:IUpdate
    {
        private int m_Guid = 0;
        private TimerMap<int,Timer> m_TimerMap;
        public TimerMgr()
        {
            m_TimerMap = new TimerMap<int,Timer>();
        }
        public void OnIUpdate(float dt)
        {
            if (dt > 0)
            {
                m_TimerMap.OnIUpdate(dt);
            }
        }
        public void OnTimerMapIUpdate(float dt)
        {
            if (dt > 0)
            {
                m_TimerMap.OnTimerMapIUpdate(dt);
            }
        }
        public int AddTimer(float dtTime, int callTimes, Action<float, bool> callBack)
        {
            int guid = GetNewGuid();
            Timer timer = new Timer(this, guid, dtTime, callTimes, callBack);
            m_TimerMap.Add(guid, timer);
            return guid;
        }

        public void RemoveTimer(int guid)
        {
            m_TimerMap.Remove(guid);
        }
        public void SetTimerScale(int guid, float scale)
        {
            IUpdate timer = m_TimerMap.Get(guid);
            if (timer != null)
            {
                ((Timer)timer).SetTimeScale(scale);
            }
        }
        public Timer GetTimer(int guid)
        {
            return (Timer)m_TimerMap.Get(guid);
        }
        private int GetNewGuid()
        {
            m_Guid++;
            return m_Guid;
        }
        public void ClearTimer()
        {
            m_TimerMap.OnDestroy();
        }
    }
}