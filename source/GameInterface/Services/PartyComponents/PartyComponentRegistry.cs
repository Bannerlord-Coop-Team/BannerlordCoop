using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents;

/// <summary>
/// Registry for <see cref="PartyComponent"/> objects
/// </summary>
internal class PartyComponentRegistry : AutoRegistryBase<PartyComponent>
{
    public PartyComponentRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var party in MobileParty.All)
        {
            if (party?.PartyComponent == null) continue;

            RegisterExistingObject(party.StringId, party.PartyComponent);
        }
    }

    public override void OnClientCreated(PartyComponent obj, string id)
    {
    }

    public override void OnClientDestroyed(PartyComponent obj, string id)
    {
    }

    public override void OnServerCreated(PartyComponent obj, string id)
    {
    }

    public override void OnServerDestroyed(PartyComponent obj, string id)
    {
    }
}

