using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 随机地图生成管理器 (Blob 47 算法 + 细胞自动机)
    /// 职责：负责地图逻辑数据的生成、平滑处理，以及邻居掩码(Bitmask)到素材索引的映射。
    /// </summary>
    public class RandomMapMgr
    {
        // ---------------------------------------------------------
        // 核心映射表：由人工对齐的 47 个位掩码(Bitmask)
        // 每一个数值代表一种邻居组合，对应 level_0 到 level_46 瓦片资源
        // 权重定义: NW(1), N(2), NE(4), E(8), SE(16), S(32), SW(64), W(128)
        // ---------------------------------------------------------
        private static readonly int[] m_YourSpriteMasks = {
            56,   // [0]  右 + 右下 + 下                     = 8+16+32
            248,  // [1]  右 + 右下 + 下 + 左下 + 左         = 8+16+32+64+128
            224,  // [2]  下 + 左下 + 左                     = 32+64+128
            175,  // [3]  左上 + 上 + 右上 + 右 + 下 + 左     = 1+2+4+8+32+128
            32,   // [4]  下                                 = 32
            46,   // [5]  上 + 右上 + 右 + 下                 = 2+4+8+32
            163,  // [6]  上 + 左上 + 左 + 下                 = 2+1+128+32
            62,   // [7]  上 + 右上 + 右 + 右下 + 下           = 2+4+8+16+32
            255,  // [8]  全部                               = 1+2+4+8+16+32+64+128
            227,  // [9]  左上 + 上 + 下 + 左下 + 左           = 1+2+32+64+128
            235,  // [10] 左上 + 上 + 右 + 下 + 左下 + 左      = 1+2+8+32+64+128
            190,  // [11] 上 + 右上 + 右 + 右下 + 下 + 左      = 2+4+8+16+32+128
            8,    // [12] 右                                 = 8
            170,  // [13] 上 + 右 + 下 + 左                   = 2+8+32+128
            128,  // [14] 左                                 = 128
            232,  // [15] 右 + 下 + 左下 + 左                 = 8+32+64+128
            184,  // [16] 右 + 右下 + 下 + 左                 = 8+16+32+128
            14,   // [17] 上 + 右上 + 右                     = 2+4+8
            143,  // [18] 左上 + 上 + 右上 + 右 + 左           = 1+2+4+8+128
            131,  // [19] 左上 + 上 + 左                     = 1+2+128
            250,  // [20] 上 + 右 + 右下 + 下 + 左下 + 左      = 2+8+16+32+64+128
            2,    // [21] 上                                 = 2
            139,  // [22] 左上 + 上 + 右 + 左                 = 1+2+8+128
            142,  // [23] 上 + 右上 + 右 + 左                 = 2+4+8+128
            40,   // [24] 右 + 下                             = 8+32
            136,  // [25] 右 + 左                             = 8+128
            160,  // [26] 下 + 左                             = 32+128
            239,  // [27] 左上 + 上 + 右上 + 右 + 下 + 左下 + 左 = 1+2+4+8+32+64+128
            191,  // [28] 左上 + 上 + 右上 + 右 + 右下 + 下 + 左 = 1+2+4+8+16+32+128
            168,  // [29] 右 + 下 + 左                       = 8+32+128
            186,  // [30] 上 + 右 + 右下 + 下 + 左             = 2+8+16+32+128
            234,  // [31] 上 + 右 + 下 + 左下 + 左             = 2+8+32+64+128
            34,   // [32] 上 + 下                             = 2+32
            0,    // [33] 孤立                               = 0
            251,  // [34] 左上 + 上 + 右 + 右下 + 下 + 左下 + 左 = 1+2+8+16+32+64+128
            254,  // [35] 上 + 右上 + 右 + 右下 + 下 + 左下 + 左 = 2+4+8+16+32+64+128
            42,   // [36] 上 + 右 + 下                         = 2+8+32
            162,  // [37] 上 + 下 + 左                         = 2+32+128
            174,  // [38] 上 + 右上 + 右 + 下 + 左             = 2+4+8+32+128
            171,  // [39] 左上 + 上 + 右 + 下 + 左             = 1+2+8+32+128
            10,   // [40] 上 + 右                             = 2+8
            130,  // [41] 上 + 左                             = 2+128
            58,   // [42] 上 + 右 + 右下 + 下                   = 2+8+16+32
            226,  // [43] 上 + 下 + 左下 + 左                   = 2+32+64+128
            138,  // [44] 上 + 右 + 左                         = 2+8+128
            238,  // [45] 上 + 右上 + 右 + 下 + 左下 + 左        = 2+4+8+32+64+128
            187   // [46] 左上 + 上 + 右 + 右下 + 下 + 左        = 1+2+8+16+32+128
        };

        private byte[] m_MapData; 
        private byte[] m_QuickLookup = new byte[256];
        private int m_Width;
        private int m_Height;

        public RandomMapMgr()
        {
            Init();
        }

        private void Init()
        {
            // 预先建立 256 种全状态邻居掩码到 47 核心瓦片的降维映射
            for (int i = 0; i < 256; i++)
            {
                m_QuickLookup[i] = (byte)CalculateStandardIndex(i);
            }
        }

        /// <summary>
        /// 生成平滑的洞穴状随机地图
        /// </summary>
        /// <param name="width">地图宽度</param>
        /// 
        /// <param name="height">地图高度</param>
        /// <param name="fillPercent">墙体填充率 (建议 45-50)</param>
        /// <param name="smoothCycles">平滑迭代次数 (建议 5)</param>
        public void Generate(int width, int height, int fillPercent, int smoothCycles = 5)
        {
            m_Width = width;
            m_Height = height;
            m_MapData = new byte[width * height];

            // 1. 初始离散随机
            for (int i = 0; i < m_MapData.Length; i++)
            {
                m_MapData[i] = (Random.Range(0, 100) < fillPercent) ? (byte)1 : (byte)0;
            }

            // 2. 细胞自动机迭代，收敛出连贯形状
            for (int i = 0; i < smoothCycles; i++)
            {
                SmoothMap();
            }
        }

        private void SmoothMap()
        {
            byte[] oldData = (byte[])m_MapData.Clone();
            for (int x = 0; x < m_Width; x++)
            {
                for (int y = 0; y < m_Height; y++)
                {
                    // ==========================================
                    // 【核心规则】
                    // 1. 边界格子：强制变成墙
                    // 2. 内部格子：正常平滑
                    // ==========================================
                    bool isBorder = 
                        x == 0 || 
                        x == m_Width - 1 || 
                        y == 0 || 
                        y == m_Height - 1;

                    if (isBorder)
                    {
                        // 边界 → 强制墙
                        m_MapData[y * m_Width + x] = 1;
                    }
                    else
                    {
                        // 内部 → 正常细胞自动机平滑
                        int neighborWalls = GetSurroundingWallCount(x, y, oldData);
                        if (neighborWalls > 4)
                            m_MapData[y * m_Width + x] = 1;
                        else if (neighborWalls < 4)
                            m_MapData[y * m_Width + x] = 0;
                    }
                }
            }
        }

        private int GetSurroundingWallCount(int x, int y, byte[] data)
        {
            int count = 0;
            for (int nx = x - 1; nx <= x + 1; nx++) {
                for (int ny = y - 1; ny <= y + 1; ny++) {
                    if (nx == x && ny == y) continue;
                    // 地图边界之外跳过
                    if (nx < 0 || nx >= m_Width || ny < 0 || ny >= m_Height) continue;
                    else if (data[ny * m_Width + nx] == 1) count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 获取坐标对应的 47 种核心瓦片索引之一 (0-46)
        /// </summary>
        public int GetTileIndex(int x, int y)
        {
            if (x < 0 || x >= m_Width || y < 0 || y >= m_Height) return -1;
            if (m_MapData[y * m_Width + x] == 0) return -1;

            int rawMask = Get8NeighborMask(x, y);
            return m_QuickLookup[rawMask];
        }

        private int Get8NeighborMask(int x, int y)
        {
            int mask = 0;
            // 按照标准权重累加：NW(1), N(2), NE(4), E(8), SE(16), S(32), SW(64), W(128)
            if (IsWall(x - 1, y + 1)) mask += 1;
            if (IsWall(x,     y + 1)) mask += 2;
            if (IsWall(x + 1, y + 1)) mask += 4;
            if (IsWall(x + 1, y))     mask += 8;
            if (IsWall(x + 1, y - 1)) mask += 16;
            if (IsWall(x,     y - 1)) mask += 32;
            if (IsWall(x - 1, y - 1)) mask += 64;
            if (IsWall(x - 1, y))     mask += 128;
            return mask;
        }

        private bool IsWall(int x, int y)
        {
            if (x < 0 || x >= m_Width || y < 0 || y >= m_Height) return false; 
            return m_MapData[y * m_Width + x] == 1;
        }

        private int CalculateStandardIndex(int rawMask)
        {
            // 处理转角(Corner)与邻边(Edge)的依赖逻辑：
         
            
            int refined = rawMask;
            if ((rawMask & 2) == 0 || (rawMask & 128) == 0) refined &= ~1;  // 无N或无W -> 无NW
            if ((rawMask & 2) == 0 || (rawMask & 8) == 0)   refined &= ~4;  // 无N或无E -> 无NE
            if ((rawMask & 32) == 0 || (rawMask & 8) == 0)  refined &= ~16; // 无S或无E -> 无SE
            if ((rawMask & 32) == 0 || (rawMask & 128) == 0) refined &= ~64; // 无S或无W -> 无SW

            // 搜索映射表，找出对应的 level_X 编号
            for (int i = 0; i < m_YourSpriteMasks.Length; i++) {
                if (m_YourSpriteMasks[i] == refined) return i;
            }
            return 8; // 兜底返回 255 (全填充)
        }
    }
}
