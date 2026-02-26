using System;

namespace Game.Runtime.Hotfix
{
    public class FrameTimerMgr : IUpdate
    {
        private int m_Guid = 0;
        private TimerMap<int, FrameTimer> m_TimerMap;
        public FrameTimerMgr()
        {
            m_TimerMap = new TimerMap<int, FrameTimer>();
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
        public int AddFrameTimer(int dtFrame, int callTimes, Action<float, bool> callBack)
        {
            int guid = GetNewGuid();
            FrameTimer timer = new FrameTimer(this, guid, dtFrame, callTimes, callBack);
            m_TimerMap.Add(guid, timer);
            return guid;
        }

        public void RemoveTimer(int guid)
        {
            m_TimerMap.Remove(guid);
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