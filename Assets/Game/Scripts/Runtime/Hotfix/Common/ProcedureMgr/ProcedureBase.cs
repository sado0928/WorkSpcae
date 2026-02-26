
namespace Game.Runtime.Hotfix
{
    public abstract class ProcedureBase
    {
        // 当前 流程 的配置信息
        public ProcedureConfig Config { get; private set; }

        // 当前 流程 的 Key (ProcedureDefine 中的常量)
        public string Key { get; private set; }
        
        public virtual void OnInit(string key, ProcedureConfig config)
        {
            Key = key;
            Config = config;
        }

        public abstract void OnEnter();
        public abstract void OnUpdate(float dt);
        public abstract void OnLeave();
        public abstract void OnDestroy();
    }
}
