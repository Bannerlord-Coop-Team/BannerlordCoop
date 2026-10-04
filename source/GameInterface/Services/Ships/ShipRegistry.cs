using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;

namespace GameInterface.Services.Ships;

internal class ShipRegistry : AutoRegistryBase<Ship>
{
    public ShipRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(Ship));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var party in Campaign.Current.CampaignObjectManager.MobileParties)
        {
            if (party.Ships == null) continue;

            var index = 0;
            foreach (var ship in party.Ships)
            {
                RegisterExistingObject($"MobileParty_{party.StringId}_{index++}", ship);
            }
        }

        foreach (var settlement in Campaign.Current.CampaignObjectManager.Settlements)
        {
            if (settlement.Party == null || settlement.Party.Ships == null) continue;

            var index = 0;
            foreach (var ship in settlement.Party.Ships)
            {
                RegisterExistingObject($"Settlement_{settlement.StringId}_{index++}", ship);
            }
        }
    }

    public override void OnClientCreated(Ship obj, string id)
    {
    }

    public override void OnClientDestroyed(Ship obj, string id)
    {
    }

    public override void OnServerCreated(Ship obj, string id)
    {
    }

    public override void OnServerDestroyed(Ship obj, string id)
    {
    }
}
