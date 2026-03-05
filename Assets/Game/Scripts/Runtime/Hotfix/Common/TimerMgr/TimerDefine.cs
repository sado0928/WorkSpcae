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
            m_CurTime += dt * m_scale;
            while (m_CurTime >= m_DtTime)
            {
                m_CurTime -= m_DtTime;
                m_CurCallTimes++;
                CheckEnd();
                m_CallBack(dt, m_IsEnd);
                
                // 如果已经结束，立即停止追帧
                if (m_IsEnd) break;
            }
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