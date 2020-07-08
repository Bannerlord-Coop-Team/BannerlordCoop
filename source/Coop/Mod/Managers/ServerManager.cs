using System.Collections.Generic;
using Coop.Mod.DebugUtil;
using Coop.Mod.Serializers;
using SandBox;
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
            CLICommands.StartServer(new List<string>());
            PlayerHeroSerializer phs = new PlayerHeroSerializer(Hero.MainHero);
            CoopClient.Instance.RemoteStoreCreated += (remoteStore) => {
                remoteStore.OnObjectReceived += (objId, obj) =>
                {
                    if (obj is PlayerHeroSerializer serializedPlayerHero)
                    {
                        Hero hero = (Hero)serializedPlayerHero.Deserialize();
                    }
                };
            };
            
            //PlayerHeroSerializer heroSerializer = new PlayerHeroSerializer(Hero.MainHero);
            //Hero hero = (Hero)heroSerializer.Deserialize();
        }
    }
}
