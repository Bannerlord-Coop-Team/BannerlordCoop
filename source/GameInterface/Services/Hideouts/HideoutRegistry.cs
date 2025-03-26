using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Hideouts;
internal class HideoutRegistry : IAutoRegistry<Hideout>
{
    ILogger Logger { get; }
    public HideoutRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Hideout))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<Hideout> registry)
    {
        foreach (var hideout in Hideout.All)
        {
            registry.RegisterExistingObject(hideout.StringId, hideout);
        }
    }

    public void OnClientCreated(Hideout obj, string id)
    {
    }

    public void OnClientDestroyed(Hideout obj, string id)
    {
    }

    public void OnServerCreated(Hideout obj, string id)
    {
    }

    public void OnServerDestroyed(Hideout obj, string id)
    {
    }
}
