using Coop.Mod.Serializers;
using Network.Infrastructure;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem.Load;

namespace Coop.Mod.Managers
{
    /// <summary>
    /// Dedicated server game manager.
    /// </summary>
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

            // Removes main party on server.
            MobileParty.MainParty.RemoveParty();

            CoopClient.Instance.RemoteStoreCreated += (remoteStore) => {
                remoteStore.OnObjectReceived += (objId, obj) =>
                {
                    if (obj is PlayerHeroSerializer serializedPlayerHero)
                    {
                        // Hero received from client after character creation
                        Hero hero = (Hero)serializedPlayerHero.Deserialize();
                    }
                };
            };
        }
    }
}
