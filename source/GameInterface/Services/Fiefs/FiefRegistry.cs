using Common;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Fiefs;
internal class FiefRegistry : IAutoRegistry<Fief>
{
    ILogger Logger { get; }

    public FiefRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;
        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Fief))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();
    
    public void RegisterAllObjects(IRegistry<Fief> registry)
    {
        foreach (Fief fief in Town.AllFiefs)
        {
            var networkId = fief.StringId;
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
