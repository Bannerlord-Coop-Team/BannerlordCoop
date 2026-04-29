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
internal class PartyComponentRegistry : IAutoRegistry<PartyComponent>
{
    ILogger Logger { get; }
    public PartyComponentRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }
    public IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IObjectManager objectManager)
    {
        foreach (var party in MobileParty.All)
        {
            var networkId = $"{party.PartyComponent.GetType().Name}_{party.StringId}";
            objectManager.AddExisting(networkId, party.PartyComponent);
        }
    }

    public void OnClientCreated(PartyComponent obj, string id)
    {
    }

    public void OnClientDestroyed(PartyComponent obj, string id)
    {
    }

    public void OnServerCreated(PartyComponent obj, string id)
    {
    }

    public void OnServerDestroyed(PartyComponent obj, string id)
    {
    }
}

