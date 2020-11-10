using System.Reflection;
using Coop.Mod.DebugUtil;
using Coop.Mod.Serializers;
using Network.Infrastructure;
using SandBox;
using SandBox.View.Map;
using Sync.Store;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem.Load;
using Module = TaleWorlds.MountAndBlade.Module;

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

        public override void OnAfterCampaignStart(Game game)
        {
            NetworkMain.InitializeAsDedicatedServer();
            base.OnAfterCampaignStart(game);
        }

        public override void OnLoadFinished()
        {
            NetworkMain.InitializeAsDedicatedServer();
            base.OnLoadFinished();
            if (CoopServer.Instance.StartServer() == null)
            {
                ServerConfiguration config = CoopServer.Instance.Current.ActiveConfig;
                CoopClient.Instance.Connect(config.NetworkConfiguration.LanAddress, config.NetworkConfiguration.LanPort);
            }

            Settlement settlement = Settlement.Find("tutorial_training_field");
            Campaign.Current.HandleSettlementEncounter(MobileParty.MainParty, settlement);
            //BattleMission.StartBattle();

            CoopClient.Instance.RemoteStoreCreated += (remoteStore) => {
                remoteStore.OnObjectReceived += (objId, obj) =>
                {
                    if (obj is PlayerHeroSerializer serializedPlayerHero)
                    {
                        Hero hero = (Hero)serializedPlayerHero.Deserialize();
                    }
                };
            };
        }
    }
}
