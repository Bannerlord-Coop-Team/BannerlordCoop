using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyBases;
internal class PartyBaseRegistry : IAutoRegistry<PartyBase>
{
    ILogger Logger { get; }
    public PartyBaseRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(PartyBase), new Type[] { typeof(MobileParty), typeof(Settlement) })
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<PartyBase> registry)
    {
        foreach (var party in MobileParty.All)
        {
            var networkId = party.StringId;

            if (registry.RegisterExistingObject(networkId, party.Party) == false)
                Logger.Error("Unable to register PartyBase from Party with the object manager");
        }

        foreach (var settlement in Settlement.All)
        {
            var networkId = settlement.StringId;

            if (registry.RegisterExistingObject(networkId, settlement.Party) == false)
                Logger.Error("Unable to register PartyBase from Party with the object manager");
        }
    }

    public void OnClientCreated(PartyBase obj, string id)
    {
    }

    public void OnClientDestroyed(PartyBase obj, string id)
    {
    }

    public void OnServerCreated(PartyBase obj, string id)
    {
    }

    public void OnServerDestroyed(PartyBase obj, string id)
    {
    }
}
