using Common;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns;
internal class TownRegistry : IAutoRegistry<Town>
{
    private readonly IObjectManager objectManager;

    ILogger Logger { get; }

    public TownRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
    {
        Logger = logger;
        this.objectManager = objectManager;
        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Town))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IObjectManager objectManager)
    {
        foreach (var town in Town.AllFiefs)
        {
            objectManager.AddExisting(town.StringId, town);
        }
    }

    public void OnClientCreated(Town obj, string id)
    {
    }

    public void OnClientDestroyed(Town obj, string id)
    {
    }

    public void OnServerCreated(Town obj, string id)
    {
    }

    public void OnServerDestroyed(Town obj, string id)
    {
    }
}

