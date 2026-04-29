using Common;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Villages;
internal class VillageRegistry : IAutoRegistry<Village>
{
    ILogger Logger { get; }
    public VillageRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Village))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IObjectManager objectManager)
    {
        foreach (var village in Village.All)
        {
            objectManager.AddExisting(village.StringId, village);
        }
    }

    public void OnClientCreated(Village obj, string id)
    {
    }

    public void OnClientDestroyed(Village obj, string id)
    {
    }

    public void OnServerCreated(Village obj, string id)
    {
    }

    public void OnServerDestroyed(Village obj, string id)
    {
    }
}
