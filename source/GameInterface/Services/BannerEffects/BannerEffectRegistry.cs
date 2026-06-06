using Common;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Services.BannerEffects;
internal class BannerEffectRegistry : AutoRegistryBase<BannerEffect>
{
    public BannerEffectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(BannerEffect), new Type[] { typeof(string) })
    };

    // TODO find destructor for banner effects
    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
    }

    public override void OnClientCreated(BannerEffect obj, string id)
    {
    }

    public override void OnClientDestroyed(BannerEffect obj, string id)
    {
    }

    public override void OnServerCreated(BannerEffect obj, string id)
    {
    }

    public override void OnServerDestroyed(BannerEffect obj, string id)
    {
    }
}
