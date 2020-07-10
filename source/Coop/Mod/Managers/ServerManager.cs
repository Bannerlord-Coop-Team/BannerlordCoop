using System.Collections.Generic;
using Coop.Mod.DebugUtil;
using Coop.Mod.Serializers;
using Network.Infrastructure;
using SandBox;
using Sync.Store;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem.Load;

namespace Coop.Mod.Managers
{
    public class ServerGameManager : CampaignGameManager
    {
        public ServerGameManager() : base() { }
        public ServerGameManager(LoadResult saveGameData) : base(saveGameData) { }

        ~ServerGameManager()
        {
            // TODO save all heros
        }

        public override void OnLoadFinished()
        {
            base.OnLoadFinished();
            if (CoopServer.Instance.StartServer() == null)
            {
                ServerConfiguration config = CoopServer.Instance.Current.ActiveConfig;
                CoopClient.Instance.Connect(config.LanAddress, config.LanPort);
            }

            CoopClient.Instance.RemoteStoreCreated += (remoteStore) => {
                remoteStore.OnObjectReceived += (objId, obj) =>
                {
                    if (obj is PlayerHeroSerializer serializedPlayerHero)
                    {
                        Hero hero = (Hero)serializedPlayerHero.Deserialize();
                        remoteStore.Remove(objId);
                    }
                };
            };
            
            //PlayerHeroSerializer heroSerializer = new PlayerHeroSerializer(Hero.MainHero);
            //Hero hero = (Hero)heroSerializer.Deserialize();
        }
    }
}
