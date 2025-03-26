using Common;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.ItemRosters
{
    internal class ItemRosterRegistry : IAutoRegistry<ItemRoster>
    {
        ILogger Logger { get; }
        public ItemRosterRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
        {
            Logger = logger;

            autoRegistryFactory.RegisterType(this);
        }

        public IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(ItemRoster));

        public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

        public void RegisterAllObjects(IRegistry<ItemRoster> registry)
        {
            var objectManager = Campaign.Current?.CampaignObjectManager;

            if (objectManager == null)
            {
                Logger.Error("Unable to register objects when CampaignObjectManager is null");
                return;
            }

            foreach (MobileParty party in objectManager.MobileParties)
            {
                if (party.ItemRoster == null) continue;

                var networkId = $"{nameof(ItemRoster)}_{party.StringId}";

                registry.RegisterExistingObject(networkId, party.ItemRoster);
            }

            foreach (Settlement settlement in objectManager.Settlements)
            {
                if (settlement.ItemRoster == null) continue;

                var networkId = $"{nameof(ItemRoster)}_{settlement.StringId}";

                registry.RegisterExistingObject(networkId, settlement.ItemRoster);
            }
        }

        public void OnClientCreated(ItemRoster obj, string id)
        {
        }

        public void OnClientDestroyed(ItemRoster obj, string id)
        {
        }

        public void OnServerCreated(ItemRoster obj, string id)
        {
        }

        public void OnServerDestroyed(ItemRoster obj, string id)
        {
        }
    }
}
