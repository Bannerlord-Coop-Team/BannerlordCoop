using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.ItemRosters
{
    internal class ItemRosterRegistry : AutoRegistryBase<ItemRoster>
    {
        public override bool Debug => true;

        public ItemRosterRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager) : base(logger, autoRegistryFactory, objectManager)
        {
        }

        public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

        public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

        public override void RegisterAllObjects()
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

                RegisterExistingObject(party.StringId, party.ItemRoster);
            }

            foreach (Settlement settlement in objectManager.Settlements)
            {
                if (settlement.ItemRoster != null)
                {
                    RegisterExistingObject(settlement.StringId, settlement.ItemRoster);
                }

                if (settlement.Stash != null)
                {
                    RegisterExistingObject($"{settlement.StringId}_stash", settlement.Stash);
                }
            }
        }

        public override void OnClientCreated(ItemRoster obj, string id)
        {
        }

        public override void OnClientDestroyed(ItemRoster obj, string id)
        {
        }

        public override void OnServerCreated(ItemRoster obj, string id)
        {
        }

        public override void OnServerDestroyed(ItemRoster obj, string id)
        {
        }
    }
}
