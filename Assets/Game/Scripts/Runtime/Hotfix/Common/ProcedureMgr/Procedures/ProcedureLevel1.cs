using UnityEngine;

namespace Game.Runtime.Hotfix
{
    public class ProcedureLevel1 : ProcedureBase
    {
        public override void OnEnter()
        {
            Global.gApp.gBattleMgr.m_WorldCamera.gameObject.SetActive(true);
            //生成大地图
            Global.gApp.gRandomMapMgr.Generate(100, 100, 25,1);
            //初始化 47 个连续命名的瓦片
            Global.gApp.gTileMapMgr.InitBlobTiles("TileMap/level");
            //渲染屏幕范围
            Global.gApp.gTileMapMgr.RenderRandomMap(0, 0, 100, 100);

            // Global.gApp.gBattleMgr.OnStartBattle();
            Global.gApp.gDispatcherMgr.Dispatch(EventDefine.LoadingFinish, true);
        }

        public override void OnUpdate(float dt)
        {
            // 这里可以处理一些关卡特有的逻辑，如波次控制
        }

        public override void OnLeave()
        {
            Global.gApp.gBattleMgr.OnStopBattle();
            Global.gApp.gBattleMgr.m_WorldCamera.gameObject.SetActive(false);
        }

        public override void OnDestroy()
        {
        }
    }
}
