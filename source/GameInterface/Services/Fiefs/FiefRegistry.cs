using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Fiefs;

class FiefRegistry : IAutoRegistry<Fief>
{
    ILogger Logger { get; }
    public FiefRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Town))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<Fief> registry)
    {
        foreach (var fief in Town.AllTowns)
        {
            var networkId = $"{nameof(Fief)}_{fief.StringId}";
            registry.RegisterExistingObject(networkId, fief);
        }
    }

    public void OnClientCreated(Fief obj, string id)
    {
    }

    public void OnClientDestroyed(Fief obj, string id)
    {
    }

    public void OnServerCreated(Fief obj, string id)
    {
    }

    public void OnServerDestroyed(Fief obj, string id)
    {
    }
}
