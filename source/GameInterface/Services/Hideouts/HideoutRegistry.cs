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

namespace GameInterface.Services.Hideouts;
internal class HideoutRegistry : AutoRegistryBase<Hideout>
{
    public HideoutRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Hideout))
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var hideout in Hideout.All)
        {
            RegisterExistingObject(hideout.StringId, hideout);
        }
    }

    public override void OnClientCreated(Hideout obj, string id)
    {
    }

    public override void OnClientDestroyed(Hideout obj, string id)
    {
    }

    public override void OnServerCreated(Hideout obj, string id)
    {
    }

    public override void OnServerDestroyed(Hideout obj, string id)
    {
    }
}
