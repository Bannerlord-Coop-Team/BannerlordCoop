using Common;
using GameInterface.Registry;
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
internal class VillageRegistry : AutoRegistryBase<Village>
{
    public VillageRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Village))
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var village in Village.All)
        {
            RegisterExistingObject(village.StringId, village);
        }
    }

    public override void OnClientCreated(Village obj, string id)
    {
    }

    public override void OnClientDestroyed(Village obj, string id)
    {
    }

    public override void OnServerCreated(Village obj, string id)
    {
    }

    public override void OnServerDestroyed(Village obj, string id)
    {
    }
}
