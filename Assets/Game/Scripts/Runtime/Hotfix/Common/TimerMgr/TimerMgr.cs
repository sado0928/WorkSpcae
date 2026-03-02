using System;

namespace Game.Runtime.Hotfix
{
    public class TimerMgr:IUpdate
    {
        private int m_Guid = 0;
        private SafeMap<int,Timer> m_SafeMap;
        public TimerMgr()
        {
            m_SafeMap = new SafeMap<int,Timer>();
        }
        public void OnIUpdate(float dt)
        {
            if (dt > 0)
            {
                m_SafeMap.OnIUpdate(dt);
            }
        }
        
        public int AddTimer(float dtTime, int callTimes, Action<float, bool> callBack)
        {
            int guid = GetNewGuid();
            Timer timer = new Timer(this, guid, dtTime, callTimes, callBack);
            m_SafeMap.Add(guid, timer);
            return guid;
        }

        public void RemoveTimer(int guid)
        {
            m_SafeMap.Remove(guid);
        }
        public void SetTimerScale(int guid, float scale)
        {
            Timer timer = m_SafeMap[guid];
            if (timer != null)
            {
                timer.SetTimeScale(scale);
            }
        }
        public Timer GetTimer(int guid)
        {
            return m_SafeMap[guid];
        }
        private int GetNewGuid()
        {
            m_Guid++;
            return m_Guid;
        }
        public void ClearTimer()
        {
            m_SafeMap.OnDestroy();
        }
    }
}