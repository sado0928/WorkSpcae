using Luban;
using UnityEngine;

namespace Game.Runtime.Hotfix
{
    public class CfgMgr
    {
        public Tables Tables { get;private set; }
        
        public CfgMgr()
        {
            Tables = new Tables(LoadByteBuf);
        }

        private ByteBuf LoadByteBuf(string file)
        {
            string path = $"Config/{file}";
            var asset = Global.gApp.gResMgr.LoadTextAsset(path,true);
        
            if (asset == null)
            {
                Global.LogError($"[ConfigMgr] Failed to load config file: {path}");
                return null;
            }
        
            return new ByteBuf(asset.bytes);
        }
    }
}