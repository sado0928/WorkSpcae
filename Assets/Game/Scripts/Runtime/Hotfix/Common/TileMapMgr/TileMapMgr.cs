using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.Runtime.Hotfix
{
    public class TileMapMgr
    {
        public MapObjectType[,] m_Grid;
        public int m_Width { get;private set; }
        public int m_Height { get;private set; }

        public Tilemap m_TileMap { get; set; }

        private TileBase[] m_BlobTiles = new TileBase[47];

        public TileMapMgr()
        {
            
        }
        
        /// <summary>
        /// 初始化 Blob 47 瓦片素材 (从 ResMgr 加载)
        /// </summary>
        public void InitBlobTiles(string startAssetPath)
        {
            if (m_TileMap == null)
            {
                var go = GameObject.Find("Tilemap");
                if (go != null)
                {
                    m_TileMap = go.GetComponent<Tilemap>();
                }
            }
            
            for (int i = 0; i < 47; i++)
            {
                string path = $"{startAssetPath}_{i}";
                Tile tile = Global.gApp.gResMgr.LoadAsset<Tile>(path, ResType.Asset);

                if (tile != null)
                {
                    m_BlobTiles[i] = tile;
                }
                else
                {
                    Debug.LogError($"[TileMapMgr] 瓦片资源加载失败: {path}");
                }
            }
        }

        /// <summary>
        /// 关联渲染：根据 RandomMapMgr 的数据渲染指定区域
        /// </summary>
        public void RenderRandomMap(int startX, int startY, int width, int height)
        {
            var randomMgr = Global.gApp.gRandomMapMgr;
            if (randomMgr == null) 
            {
                Debug.LogError("[TileMapMgr] RandomMapMgr 为空，无法渲染");
                return;
            }
            if (m_TileMap == null)
            {
                Debug.LogError("[TileMapMgr] m_TileMap 尚未绑定，渲染中止。请先调用 BindFromScene 或手动赋值。");
                return;
            }

            Debug.Log($"[TileMapMgr] 开始渲染随机地图区域: ({startX},{startY}) Size: {width}x{height}");

            for (int y = startY; y < startY + height; y++)
            {
                for (int x = startX; x < startX + width; x++)
                {
                    int blobIdx = randomMgr.GetTileIndex(x, y);
                    if (blobIdx == -1)
                    {
                        m_TileMap.SetTile(new Vector3Int(x, y, 0), m_BlobTiles[8]);
                    }
                    else
                    {
                        m_TileMap.SetTile(new Vector3Int(x, y, 0), m_BlobTiles[blobIdx]);
                    }
                }
            }
        }

       
        public void RenderMap()
        {
            if (m_TileMap == null) return;
            for (int y = 0; y < m_Height; y++)
            for (int x = 0; x < m_Width; x++)
            {
                var type = m_Grid[x, y];
                TileBase tile = GetTileByType(type);
                m_TileMap.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }

        #region 暂时无用

        public void LoadFromJson(string json)
        {
            var tiledData = JsonUtility.FromJson<TiledMapData>(json);
            m_Width = tiledData.width;
            m_Height = tiledData.height;
            m_Grid = new MapObjectType[m_Width, m_Height];
            
            TiledLayerData layer = tiledData.layers.First(x => x.name == "ObjectLayer");
            
            for (int y = 0; y < m_Height; y++)
            for (int x = 0; x < m_Width; x++)
            {
                int index = layer.data[y * m_Width + x];
                m_Grid[x, y] = (MapObjectType)index;
            }
        }
        
        private TileBase GetTileByType(MapObjectType type)
        {
            return type switch
            {
                _ => null
            };
        }

        public Vector3 GetWorldPos(int x, int y)
        {
            if (m_TileMap == null) return Vector3.zero;
            Vector3Int cellPos = new Vector3Int(x, y, 0);
            return m_TileMap.CellToWorld(cellPos);
        }

        public Vector2Int GetCellPos(Vector3 worldPos)
        {
            if (m_TileMap == null) return Vector2Int.zero;
            Vector3Int cellPos = m_TileMap.WorldToCell(worldPos);
            return new Vector2Int(cellPos.x, cellPos.y);
        }

        #endregion
       
    }
}
