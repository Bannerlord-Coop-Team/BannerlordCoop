using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.PartyBases;
internal class PartyBaseRegistry : AutoRegistryBase<PartyBase>
{
    public PartyBaseRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(PartyBase), new Type[] { typeof(MobileParty), typeof(Settlement) })
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();
    public override void RegisterAllObjects()
    {
        foreach (var party in MobileParty.All)
        {
            if (party?.Party == null) continue;
            RegisterExistingObject(party.StringId, party.Party);
        }

        foreach (var settlement in Settlement.All)
        {
            if (settlement?.Party == null) continue;
            RegisterExistingObject(settlement.StringId, settlement.Party);
        }
    }

    public override void OnClientCreated(PartyBase obj, string id)
    {
        // Field initializer skipped by the constructor-less create; Ship.Owner adds to it directly.
        if (obj._ships == null)
            obj._ships = new MBList<Ship>();
    }

    public override void OnClientDestroyed(PartyBase obj, string id)
    {
        obj.SetVisualAsDirty();
    }

    public override void OnServerCreated(PartyBase obj, string id)
    {
    }

    public override void OnServerDestroyed(PartyBase obj, string id)
    {
        obj.SetVisualAsDirty();
    }
}
