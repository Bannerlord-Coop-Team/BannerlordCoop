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
using System.Reflection;

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
            MobileParty mainParty = MobileParty.MainParty;

            mainParty.RemoveParty();
            foreach(Hero hero in Hero.AllAliveHeroes)
            {
                CoopObjectManager.AddObject(hero.CharacterObject);
                CoopObjectManager.AddObject(hero);
            }

            foreach(Hero hero in Hero.DeadOrDisabledHeroes)
            {
                CoopObjectManager.AddObject(hero.CharacterObject);
                CoopObjectManager.AddObject(hero);
            }

            foreach(Settlement settlement in Settlement.All)
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

            foreach (Kingdom kingdom in Campaign.Current.Kingdoms)
            {
                CoopObjectManager.AddObject(kingdom);
            }

            foreach (MobileParty party in MobileParty.All)
            {
                CoopObjectManager.AddObject(party);
            }

            foreach (CharacterObject characterObject in CharacterObject.All)
            {
                CoopObjectManager.AddObject(characterObject);
            }
        }
    }
}
