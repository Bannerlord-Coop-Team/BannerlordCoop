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
            if (registry.RegisterNewObject(party.Party, out var _) == false) Logger.Error("Unable to register PartyBase from Party with the object manager");
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
