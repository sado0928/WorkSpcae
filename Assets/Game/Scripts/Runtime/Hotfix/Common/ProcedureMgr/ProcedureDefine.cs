using System.Collections.Generic;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 流程配置
    /// 遵循一个场景对应一个流程好管理
    /// </summary>
    public class ProcedureConfig
    {
        public string Path;                // 资源地址
        public ResTypeByScene ResTypeByScene;      // 归属场景
    }

    /// <summary>
    /// Procedure 定义文件
    /// </summary>
    public static class ProcedureDefine
    {
        public const string LoadingScene = "LoadingScene";
        public const string MainScene = "MainScene";
        public const string Level1Scene = "Level1Scene";

        public static Dictionary<string, ProcedureConfig> ProcedureInfo = new Dictionary<string, ProcedureConfig>()
        {
            {LoadingScene,new ProcedureConfig(){Path ="Scenes/LoadingScene",ResTypeByScene = ResTypeByScene.Global}},
            {MainScene,new ProcedureConfig(){Path ="Scenes/MainScene",ResTypeByScene = ResTypeByScene.Global}},
            {Level1Scene,new ProcedureConfig(){Path ="Scenes/Level1Scene",ResTypeByScene = ResTypeByScene.Level1}},
        };

        public static ProcedureConfig GetProcedureConfig(string key)
        {
            if (ProcedureInfo.TryGetValue(key,out ProcedureConfig procedureConfig))
            {
                return procedureConfig;
            }
            return null;
        }
    }
}