namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 资源类型枚举
    /// 作用：定义资源的逻辑分类，解耦具体目录结构
    /// </summary>
    public enum ResType
    {
        Prefab = 0, //预制
        SpriteAtlas, //图集
        Sprite, //图片
        Audio, //音效
        Font, //字体
        Asset, //asset (ScriptableObject, Config, etc.)
        Bytes, //bytes
        Material, //mat
        Scenes, //场景
    }
    
    public enum ResTypeByScene
    {
        Global, // 全局常驻资源
        Level1, // 示例：关卡1资源
        // Level2, 
        // Battle,
    }

    public static class ResDefine
    {
       
    }
    
}