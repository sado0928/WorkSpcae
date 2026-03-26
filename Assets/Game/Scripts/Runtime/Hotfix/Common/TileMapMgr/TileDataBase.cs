namespace Game.Runtime.Hotfix
{
    // 数据层 所有地图静态数据对象基类，todo一下 后面这个要跟世界实体公用相同得接口
    public class TileDataBase
    {
        // 类型 静态数据与动态数据要公用 比如 空地0 建筑1 建筑包含动态与静态，还有地编中无法编辑的如军队其实是动态得但是要通过一个枚举值管理
        public MapObjectType m_Type { get;private set; }
        
    }
}