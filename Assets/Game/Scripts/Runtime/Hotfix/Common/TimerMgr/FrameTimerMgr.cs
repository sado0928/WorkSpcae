using System;

namespace Game.Runtime.Hotfix
{
    public class FrameTimerMgr : IUpdate
    {
        private int m_Guid = 0;
        private SafeMap<int, FrameTimer> m_SafeMap;
        public FrameTimerMgr()
        {
            m_SafeMap = new SafeMap<int, FrameTimer>();
        }
        public void OnIUpdate(float dt)
        {
            if (dt > 0)
            {
                m_SafeMap.OnIUpdate(dt);
            }
        }
       
        public int AddFrameTimer(int dtFrame, int callTimes, Action<float, bool> callBack)
        {
            int guid = GetNewGuid();
            FrameTimer timer = new FrameTimer(this, guid, dtFrame, callTimes, callBack);
            m_SafeMap.Add(guid, timer);
            return guid;
        }

        public void RemoveTimer(int guid)
        {
            m_SafeMap.Remove(guid);
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