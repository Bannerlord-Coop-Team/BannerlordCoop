using Coop.Mod.Patch.World;
using Coop.Mod.Serializers;
using Network.Infrastructure;
using Network.Protocol;
using SandBox;
using Sync.Store;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem.Load;
using System.Linq;

namespace Coop.Mod.Managers
{
    /// <summary>
    /// Dedicated server game manager.
    /// </summary>
    public class ServerGameManager : SandBoxGameManager
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

            foreach(Hero hero in Campaign.Current.AliveHeroes)
            {
                CoopObjectManager.AddObject(hero.CharacterObject);
                CoopObjectManager.AddObject(hero);
            }

            foreach(Hero hero in Campaign.Current.DeadOrDisabledHeroes)
            {
                CoopObjectManager.AddObject(hero.CharacterObject);
                CoopObjectManager.AddObject(hero);
            }

            foreach(Settlement settlement in Campaign.Current.Settlements)
            {
                CoopObjectManager.AddObject(settlement);
            }

            foreach (Town town in Town.AllFiefs)
            {
                CoopObjectManager.AddObject(town);
                town.Buildings.ForEach(building => CoopObjectManager.AddObject(building));
                foreach(Building building in town.BuildingsInProgress)
                {
                    CoopObjectManager.AddObject(building);
                }
            }

            foreach (Village village in Village.All)
            {
                CoopObjectManager.AddObject(village);
            }

            foreach (IFaction faction in Campaign.Current.Factions)
            {
                CoopObjectManager.AddObject(faction);
            }

            foreach (MobileParty party in Campaign.Current.MobileParties)
            {
                CoopObjectManager.AddObject(party);
                CoopObjectManager.AddObject(party.Party);
            }

            //CoopClient.Instance.RemoteStoreCreated += (remoteStore) => {
            //    remoteStore.OnObjectReceived += (objId, obj) =>
            //    {
            //        if (obj is PlayerHeroSerializer serializedPlayerHero)
            //        {
            //            // Hero received from client after character creation
            //            Hero hero = (Hero)serializedPlayerHero.Deserialize();
            //            CoopSaveManager.PlayerParties.Add(serializedPlayerHero.PlayerId, hero.Id);

            //            Settlement settlement = Settlement.Find("tutorial_training_field");
            //            EnterSettlementAction.ApplyForParty(hero.PartyBelongedTo, settlement);
            //        }
            //    };
            //};
        }
    }
}
