namespace Game.Runtime.Hotfix
{
    public class EntityMonster1001:EntityBase
    {
        public monster m_Cfg { get;private set; }
        public int Hp { get; set; }
        public int Atk { get; set; }
        public float Speed { get; set; }
        
        public EntityMonster1001(monster data)
        {
            m_Cfg = data;
            Hp = m_Cfg.Hp;
            Atk = m_Cfg.Atk;
            Speed = m_Cfg.Speed;
        }
    }
}