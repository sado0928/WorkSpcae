namespace Game.Runtime.Hotfix
{
    public class EntityHero:EntityBase
    {
        public hero m_Cfg { get;private set; }
        public float Hp;
        public float Atk;
        public float Speed;
        
        public void SetData(hero data)
        {
            m_Cfg = data;
            Hp = m_Cfg.Hp;
            Atk = m_Cfg.Atk;
            Speed = m_Cfg.Speed;
        }
    }
}