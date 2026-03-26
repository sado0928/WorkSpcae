using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using Vector2 = System.Numerics.Vector2;

namespace Game.Runtime.Hotfix
{
    public class TileMapMgr
    {
        public MapObjectType[,] m_Grid;
        public int m_Width { get;private set; }
        public int m_Height { get;private set; }

        public Tilemap m_TileMap { get; set; }
        public TileBase m_SpaceTile { get; set; }
        public TileBase m_BuildTile { get; set; }

        public void LoadFromJson(string json)
        {
            var tiledData = JsonUtility.FromJson<TiledMapData>(json);
            m_Width = tiledData.width;
            m_Height = tiledData.height;
            m_Grid = new MapObjectType[m_Width, m_Height];
            
            // 对象层
            TiledLayerData layer = tiledData.layers.First(x => x.name == "ObjectLayer");
            
            // 把平铺数组转成二维格子
            for (int y = 0; y < m_Height; y++)
            for (int x = 0; x < m_Width; x++)
            {
                int index = layer.data[y * m_Width + x];
                m_Grid[x, y] = (MapObjectType)index;
            }
        }

        // 根据数据层渲染地图
        public void RenderMap()
        {
            for (int y = 0; y < m_Height; y++)
            for (int x = 0; x < m_Width; x++)
            {
                var type = m_Grid[x, y];
                TileBase tile = GetTileByType(type);
                m_TileMap.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }

        private TileBase GetTileByType(MapObjectType type)
        {
            return type switch
            {
                MapObjectType.Space => m_SpaceTile,
                MapObjectType.Build => m_BuildTile,
                _ => null
            };
        }

        #region 外部方法

        // ======================
        // 格子坐标 → 世界坐标
        // ======================
        public Vector3 GetWorldPos(int x, int y)
        {
            Vector3Int cellPos = new Vector3Int(x, y, 0);
            return m_TileMap.CellToWorld(cellPos);
        }

        // ======================
        // 世界坐标 → 格子坐标
        // ======================
        public Vector2Int GetCellPos(Vector3 worldPos)
        {
            Vector3Int cellPos = m_TileMap.WorldToCell(worldPos);
            return new Vector2Int(cellPos.x, cellPos.y);
        }
        
        #endregion
        
    }
}