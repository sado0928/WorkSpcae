using UnityEngine;

namespace Game.Runtime.Hotfix
{
    public class ProcedureLevel1 : ProcedureBase
    {
        public override void OnEnter()
        {
           
            // Global.gApp.gBattleMgr.m_WorldCamera.gameObject.SetActive(true);
            // Global.gApp.gAudioMgr.PlayBGM(AudioDefine.MainBgm, false);
            // Global.gApp.gDispatcherMgr.Dispatch(EventDefine.LoadingFinish, true);
            //
            // // 2. 加载英雄
            // GameObject heroPrefab = Global.gApp.gResMgr.LoadAsset<GameObject>("Prefabs/Hero/1001/Hero1001", ResType.Prefab);
            // if (heroPrefab != null)
            // {
            //     GameObject heroObj = GameObject.Instantiate(heroPrefab);
            //         
            //     // 3. 初始化实体数据
            //     Global.gApp.gEntityMgr.CreateHero(heroObj, Vector2.zero);
            //
            //     // 4. 启动战斗逻辑驱动
            //     Global.gApp.gBattleMgr.OnStartBattle();
            //
            //     // 5. 测试：生成一批怪物 (暂时使用英雄模型作为占位)
            //     for (int i = 0; i < 1000; i++)
            //     {
            //         Vector2 randomPos = new Vector2(Random.Range(-50f, 50f), Random.Range(-50f, 50f));
            //         GameObject monsterObj = GameObject.Instantiate(heroPrefab);
            //         Global.gApp.gEntityMgr.SpawnMonster(monsterObj, randomPos);
            //     }
            // }
            // else
            // {
            //     Debug.LogError("[ProcedureLevel1] Failed to load Hero1001 prefab!");
            // }
        }

        public override void OnUpdate(float dt)
        {
            // 这里可以处理一些关卡特有的逻辑，如波次控制
        }

        public override void OnLeave()
        {
            Global.gApp.gBattleMgr.m_WorldCamera.gameObject.SetActive(false);
            Global.gApp.gAudioMgr.StopBGM();
        }

        public override void OnDestroy()
        {
        }
    }
}
