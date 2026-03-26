using System.Collections.Generic;

namespace Game.Runtime.Hotfix
{
    
    public enum MapObjectType {
        Space = 0,
        Build = 1,
    }
    
    [System.Serializable]
    public class TiledMapData
    {
        public int width;
        public int height;
        public List<TiledLayerData> layers;
    }
    
    
    [System.Serializable]
    public class TiledLayerData
    {
        public string name;
        public int[] data; // 关键：格子索引数组
    }
    
    public class TileMapDefine
    {
        
    }
}