using Common;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Services.BannerEffects;
internal class BannerEffectRegistry : IAutoRegistry<BannerEffect>
{
    ILogger Logger { get; }
    public BannerEffectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(BannerEffect), new Type[] { typeof(string) })
    };

    // TODO find destructor for banner effects
    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IObjectManager objectManager)
    {
    }

    public void OnClientCreated(BannerEffect obj, string id)
    {
    }

    public void OnClientDestroyed(BannerEffect obj, string id)
    {
    }

    public void OnServerCreated(BannerEffect obj, string id)
    {
    }

    public void OnServerDestroyed(BannerEffect obj, string id)
    {
    }
}
