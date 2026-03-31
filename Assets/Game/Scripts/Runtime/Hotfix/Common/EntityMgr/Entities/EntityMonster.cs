namespace Game.Runtime.Hotfix
{
    public class EntityMonster:EntityBase
    {
        public monster m_Cfg { get;private set; }
        public float Hp;
        public float Atk;
        public float Speed;
        
        public void SetData(monster data)
        {
            m_Cfg = data;
            Hp = m_Cfg.Hp;
            Atk = m_Cfg.Atk;
            Speed = m_Cfg.Speed;
        }
    }
}