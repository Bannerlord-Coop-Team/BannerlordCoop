using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyBases;
internal class PartyBaseRegistry : AutoRegistryBase<PartyBase>
{
    public PartyBaseRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    // Intentionally empty: PartyBase creation replicates through PartyBaseLifetimeHandler's
    // custom flow, which lets clients ADOPT the PartyBase they already construct for a new
    // MobileParty instead of receiving a skip-constructed duplicate shell (the shell re-pointed
    // MobileParty.Party and orphaned everything keyed by the original reference — party visuals
    // in particular). The registry still owns join-time registration and the destroy flow.
    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

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
