using System;

namespace Game.Runtime.Hotfix
{
    public class AnimHandle
    {
        public Action<string> m_Aaction { get;private set; }
        
        public void SetAction(Action<string> action)
        {
            m_Aaction = action;
        }

        public void OnAction(string val)
        {
            m_Aaction?.Invoke(val);
        }
    }
}