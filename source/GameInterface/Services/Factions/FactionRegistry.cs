using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Factions;
internal class FactionRegistry : IAutoRegistry<IFaction>
{
    ILogger Logger { get; }
    public FactionRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        // TODO add interface functionality
        // autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Kingdom)),
        AccessTools.Constructor(typeof(Clan))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<IFaction> registry)
    {
        var factions = Campaign.Current.CampaignObjectManager.Factions;
        int counter = 0;


        foreach (var faction in factions)
        {
            var networkId = $"{nameof(IFaction)}_{faction.StringId}_{counter++}";
            if (registry.RegisterExistingObject(networkId, faction) == false)
                Logger.Error($"Unable to register {faction}");
        }
    }

    public void OnClientCreated(IFaction obj, string id)
    {
    }

    public void OnClientDestroyed(IFaction obj, string id)
    {
    }

    public void OnServerCreated(IFaction obj, string id)
    {
    }

    public void OnServerDestroyed(IFaction obj, string id)
    {
    }
}
