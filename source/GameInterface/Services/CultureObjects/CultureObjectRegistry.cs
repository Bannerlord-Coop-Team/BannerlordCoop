using Common;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CultureObjects;
internal class CultureObjectRegistry : AutoRegistryBase<CultureObject>
{
    public CultureObjectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(CultureObject))
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var culture in MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
        {
            RegisterExistingObject(culture.StringId, culture);
        }
    }

    public override void OnClientCreated(CultureObject obj, string id)
    {
    }

    public override void OnClientDestroyed(CultureObject obj, string id)
    {
    }

    public override void OnServerCreated(CultureObject obj, string id)
    {
    }

    public override void OnServerDestroyed(CultureObject obj, string id)
    {
    }
}
