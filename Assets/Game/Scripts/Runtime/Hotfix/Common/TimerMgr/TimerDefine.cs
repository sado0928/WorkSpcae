using System;
using System.Collections.Generic;

namespace Game.Runtime.Hotfix
{
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