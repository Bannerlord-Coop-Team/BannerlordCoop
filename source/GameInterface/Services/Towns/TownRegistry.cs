using Common;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns;
internal class TownRegistry : AutoRegistryBase<Town>
{
    public TownRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Town))
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var town in Town.AllFiefs)
        {
            RegisterExistingObject(town.StringId, town);
        }
    }

    public override void OnClientCreated(Town obj, string id)
    {
    }

    public override void OnClientDestroyed(Town obj, string id)
    {
    }

    public override void OnServerCreated(Town obj, string id)
    {
    }

    public override void OnServerDestroyed(Town obj, string id)
    {
    }
}

