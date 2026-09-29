using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
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
        foreach (var ship in Campaign.Current.CampaignObjectManager.MobileParties.SelectMany(party => party.Ships))
        {
            RegisterExistingObject(ship.Owner.MobileParty?.StringId, ship);
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
